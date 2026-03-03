using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.Windows;


public class CarPlayer : NetworkBehaviour
{
    public TaxiCollisionAvoidance avoidanceSystem;

    // --- 新增：靜態清單，用來讓 Host 知道目前場上有幾台車 ---
    //public static List<CarPlayer> ActiveTaxis = new List<CarPlayer>();
    //[SerializeField] private  GameObject Stations;

    //有用到NetworkCharacterController才需要控制
    //private NetworkCharacterController _cc;

    //public float speed = 100f;
    public float TurnSpeed = 100f;
    public int maxPathCount = 3;

    [SerializeField] public GameObject stations;
    [SerializeField] public SplineContainer _roadSplineContainer;
    [SerializeField] private string _targetRoadName = "Spline";
    [SerializeField] private string _targetStationName = "stations";


    public struct pathRequest
    {
        public Vector3 departure_Pos;
        public Vector3 destination_Pos;
        public int startIndex;
        public int endIndex;

        public pathRequest(Vector3 departure_Pos, Vector3 destination_Pos, int startIndex, int endIndex)
        {
            this.departure_Pos = departure_Pos;
            this.destination_Pos = destination_Pos;
            this.startIndex = startIndex;
            this.endIndex = endIndex;
        }
    }
    private List<Vector3> _currentPath = new List<Vector3>();

    // 用來標記「是否正在等待路徑系統的回傳」，避免重複發送請求
    private bool _isWaitingForPath = false;

    private int targetStationIndex = -1;
    private Vector3 targetStationPos;
    private Vector3 carStationPos;
    private bool isStartDrive = false;
    private bool _isMoving = false;
    private int _targetIndex = 0;
    private int _currentStationIndex = 0;
    private Vector3 _moveDirection = Vector3.zero;
    //[Networked] private Vector3 currentNetworkPos { get; set; }

    // --- 修改 1：生成時將自己加入名單 ---
    /*private void Awake()
    {
        _cc = GetComponent<NetworkCharacterController>();
    }*/
    public override void Spawned()
    {
        // 只有 Host 需要知道所有車子的清單來發號施令
        if (Runner.IsServer)
        {
            carStationPos = transform.position;

            //ActiveTaxis.Add(this);
            // 建議根據 Object.InputAuthority 來排序，確保順序固定 (Host -> Client 1 -> Client 2...)
            // 這裡暫時依生成順序排列
        }
    }

    // --- 修改 2：銷毀時將自己移除名單 ---
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (runner.IsServer)
        {
            //ActiveTaxis.Remove(this);
           
        }
    }

    // --- 移除原本的 Update 與 DetectHostInput ---
    // private void Update() { ... } 刪除
    // private void DetectHostInput() { ... } 刪除

    private void Start()

    {

        if (_roadSplineContainer == null)

        {

            // 使用 GameObject.Find 透過字串名稱搜尋

            GameObject foundObj = GameObject.Find(_targetRoadName);


            if (foundObj != null)

            {

                // 找到了物件，嘗試取得身上的 SplineContainer 元件

                _roadSplineContainer = foundObj.GetComponent<SplineContainer>();


                if (_roadSplineContainer == null)

                {

                    //Debug.LogError($"❌ 找到了名為 '{_targetRoadName}' 的物件，但它身上沒有 SplineContainer 元件！");

                    return; // 找到物件但沒元件，視為失敗

                }

                else

                {

                    //Debug.Log($"✅ 成功透過名稱自動綁定道路：{_targetRoadName}");

                }

            }

            else

            {

                //Debug.LogError($"❌ 場景中找不到名為 '{_targetRoadName}' 的物件。請檢查 Hierarchy 中的名稱是否正確 (注意大小寫)。");

                return;

            }

        }

        if (stations == null)

        {

            // 使用 GameObject.Find 透過字串名稱搜尋

            GameObject foundObj = GameObject.Find(_targetStationName);


            if (foundObj != null)

            {

                // 找到了物件，嘗試取得身上的 SplineContainer 元件

                stations = foundObj;


                if (stations == null)

                {

                    //Debug.LogError($"❌ 找到了名為 '{_targetStationName}' 的物件，但它身上沒有 SplineContainer 元件！");

                    return; // 找到物件但沒元件，視為失敗

                }

                else

                {

                    //Debug.Log($"✅ 成功透過名稱自動綁定道路：{_targetStationName}");

                }

            }

            else

            {

                //Debug.LogError($"❌ 場景中找不到名為 '{_targetStationName}' 的物件。請檢查 Hierarchy 中的名稱是否正確 (注意大小寫)。");

                return;

            }

        }

    }
    public override void FixedUpdateNetwork()
    {

        float speed = avoidanceSystem.currentTargetSpeed;
        // 1. 權限檢查：只有 Server 能決定 AI 怎麼走
        if (!Object.HasStateAuthority) return;

        // 2. 狀態機：如果沒在動，且沒在等路徑，就找新目標
        if (!_isMoving && !_isWaitingForPath)
        {
            //Debug.Log($"[Taxi {Id}] 準備找新目標...");
            FindNewRandomDestination();
            return;
        }

        // 3. 移動邏輯
        if (_isMoving && _currentPath != null && _currentPath.Count > 0)
        {
            Vector3 targetPos = _currentPath[_targetIndex];
            targetPos.y = transform.position.y; // 鎖定高度

            Vector3 direction = targetPos - transform.position;
            float distance = direction.magnitude;
            direction.y = 0;

            // 檢查是否到達當前節點
            if (distance < 1.0f) // 範圍稍微大一點比較好轉彎
            {
                _targetIndex++; // 下一點

                // 檢查是否到達終點
                if (_targetIndex >= _currentPath.Count)
                {
                    _isMoving = false; // 停下來，下一幀會自動觸發 FindNewRandomDestination
                    _currentPath.Clear();
                    return;
                }
            }

            // 執行移動與旋轉
            direction.Normalize();
            if (direction != Vector3.zero && speed > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Runner.DeltaTime * TurnSpeed);
                transform.position += transform.forward * speed * Runner.DeltaTime;
            }
        }
    }

    // --- 新增：隨機找目標的邏輯 ---
    private void FindNewRandomDestination()
    {
        if (_roadSplineContainer == null) return;
        var allSplines = _roadSplineContainer.Splines;
        if (allSplines.Count == 0) return;

        _isWaitingForPath = true; // 鎖定狀態，避免重複請求

        // 1. 隨機選一條 Spline
        Spline randomSpline = allSplines[UnityEngine.Random.Range(0, 15)];
        int knotCount = randomSpline.Count;
        // 2. 隨機選位置
        float t = UnityEngine.Random.Range(0f, 1f);
        float splineTime = t * (knotCount - 1);
        while(splineTime < 1 || splineTime > 14)
        {
            t = UnityEngine.Random.Range(0f, 1f);
            splineTime = t * (knotCount - 1);
        }
        randomSpline.Evaluate(t, out float3 localPos, out float3 localTan, out float3 localUp);
        Vector3 destinationWorldPos = _roadSplineContainer.transform.TransformPoint(localPos);

        // 3. 發送請求 (假設你的 PathRequestManager 支援這樣呼叫)
        // 起點：自己目前位置
        // 終點：隨機算出來的 destinationWorldPos
        //Debug.Log($"[Taxi {Id}] 思考中... 決定前往: {destinationWorldPos}");

        // 呼叫你的管理器 (參數依照你原本的 struct 填寫)
        // 注意：這裡直接傳 transform.position 當起點
        PathRequestManager.Instance.RequestPath(transform.position, destinationWorldPos, 0, 0, 0, OnPathFound);
    }

    // --- 修改後的 Callback ---
    private void OnPathFound(List<Vector3> newPath)
    {
        _isWaitingForPath = false; // 解除鎖定

        if (newPath != null && newPath.Count > 0)
        {
            _currentPath = newPath;
            _targetIndex = 0;
            _isMoving = true;
            ////Debug.Log($"[Taxi {Id}] 路徑取得成功，開始移動！");
        }
        else
        {
            //Debug.LogWarning($"[Taxi {Id}] 找不到路徑，稍後重試...");
            // _isMoving 保持 false，下一幀 FixedUpdateNetwork 會再次嘗試 FindNewRandomDestination
        }
    }
}