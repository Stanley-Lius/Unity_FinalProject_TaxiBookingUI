using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;


public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{

    //public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    //public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    // 當場景載入完成，且 Fusion 準備好時，會呼叫這裡
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        // 只有 Server (Host) 需要負責生成 AI 車流
        if (runner.IsServer)
        {
            SpawnTrafficSystem(runner);
        }
    }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    private NetworkRunner _runner;

    //遊戲物件
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Vector3 spawnStartPos = new Vector3(-40, 5, 0); // 起始點
    [SerializeField] private float spawnInterval = 5f; // 每台車的間隔距離 (例如 5公尺)
    [SerializeField] private Vector3 spacingDirection = Vector3.forward; // 排列方向 (往 Z 軸排)
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();


    [Header("AI 交通設定")]
    [SerializeField] private SplineContainer _roadSplineContainer;
    [SerializeField] private NetworkPrefabRef[] _npcCarPrefabs;
    [SerializeField] private int _npcCount = 7; // 要生成幾台 AI 車

    //建立遊戲階段
    async void StartGame(GameMode mode)
    {
        // Create the Fusion runner and let it know that we will be providing user input
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // Create the NetworkSceneInfo from the current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (scene.IsValid)
        {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        // Start or join (depends on gamemode) a session with a specific name
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TaxiSystem",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }


//判斷是host還是client
    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
            {
                StartGame(GameMode.Host);
            }
            if (GUI.Button(new Rect(0, 40, 200, 40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
    }

    //加入玩家(連線)
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            // Create a unique position for the player
            //Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);
            //NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            // Keep track of the player avatars for easy access
            // 1. 計算生成位置
            // 邏輯：起始點 + (方向 * 間隔 * 目前人數)
            // 這樣第 1 個人在 0，第 2 個人在 5，第 3 個人在 10...
            int index = _spawnedCharacters.Count;
            Vector3 spawnPos = spawnStartPos + (spacingDirection * spawnInterval * index);

            // 2. 生成計程車
            // 參數：Prefab, 位置, 旋轉, 玩家參考(Input Authority)
            NetworkObject networkObj = runner.Spawn(_playerPrefab, spawnPos, Quaternion.Euler(0, 90, 0), player);

            // 3. 記錄起來
            _spawnedCharacters.Add(player, networkObj);

            Debug.Log($"[Server] 已為玩家 {player.PlayerId} 生成計程車，位置：{spawnPos}");
            
        }
    }

    //玩家離開(斷線)
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    //生成AI車流系統
    private void SpawnTrafficSystem(NetworkRunner runner)
    {
        // 防呆：如果沒有設定任何車輛 Prefab，就不要執行，避免報錯
        if (_npcCarPrefabs == null || _npcCarPrefabs.Length == 0)
        {
            Debug.LogWarning("請在 Inspector 中設定 AI 車輛 Prefabs！");
            return;
        }

        var allSplines = _roadSplineContainer.Splines;

        if (allSplines.Count == 0) return;

        Debug.Log($"開始生成 AI 車流，目標數量: {_npcCount}");

        for (int i = 0; i < _npcCount; i++)
        {
            Spline randomSpline = allSplines[UnityEngine.Random.Range(0, 15)];

            int knotCount = randomSpline.Count;
            // 2. 隨機選位置
            float t = UnityEngine.Random.Range(0f, 1f);
            float splineTime = t * (knotCount - 1);
            while (splineTime < 1 || splineTime > 14)
            {
                t = UnityEngine.Random.Range(0f, 1f);
                splineTime = t * (knotCount - 1);
            }

            // 注意：這裡是呼叫 randomSpline.Evaluate，而不是 container.Evaluate
            randomSpline.Evaluate(t, out float3 localPos, out float3 localTan, out float3 localUp);

            // 因為 Spline 的資料是相對於 _roadContainer 的，所以要用 Container 的 Transform 來轉
            Vector3 spawnPos = _roadSplineContainer.transform.TransformPoint(localPos);
            Vector3 forwardDir = _roadSplineContainer.transform.TransformDirection(localTan);
            Vector3 upDir = _roadSplineContainer.transform.TransformDirection(localUp);

            Quaternion spawnRot = Quaternion.LookRotation(forwardDir, upDir);

            // --- 1. 隨機挑選一種車款 ---
            int randomPrefabIndex = UnityEngine.Random.Range(0, _npcCarPrefabs.Length);
            NetworkPrefabRef selectedPrefab = _npcCarPrefabs[randomPrefabIndex];

            // --- 3. 生成 AI 車 ---
            // 注意：這裡的第一個參數變成了 selectedPrefab
            runner.Spawn(selectedPrefab, spawnPos, spawnRot, null, (runner, obj) =>
            {
                // 初始化邏輯 (例如設定導航路徑)
                // var controller = obj.GetComponent<TaxiMovementController>();
                // if (controller != null) controller.InitializeRoute(...);
            });
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
