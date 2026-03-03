using Fusion;
using Fusion.Sockets;
using NUnit.Framework.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Windows;
using System.Linq; // 需要用來把 List 轉 Array


public class TaxiPlayer : NetworkBehaviour
{
    public TaxiCollisionAvoidance avoidanceSystem; // 引用上面的腳本
    // --- 新增：靜態清單，用來讓 Host 知道目前場上有幾台車 ---
    public static List<TaxiPlayer> ActiveTaxis = new List<TaxiPlayer>();
    //[SerializeField] private  GameObject Stations;

    //有用到NetworkCharacterController才需要控制
    //private NetworkCharacterController _cc;
   
    //public float speed = 100f;
    public float TurnSpeed = 100f;

    public TaxiStatusDisplay statsDisplay;
    public PathViewer pathViewer;

    [SerializeField] public GameObject stations;

    private List<Vector3> _currentPath = new List<Vector3>();
    // 這是 Client 端用來顯示的暫存路徑
    private List<Vector3> _clientDisplayPath = new List<Vector3>();
    private int _clientTargetIndex=0;

    private Queue<List<Vector3>> _pathQueue = new Queue<List<Vector3>>();

    private int targetStationIndex = -1;
    private Vector3 targetStationPos;
    private Vector3 taxiStationPos;
    private bool isStartDrive = false;
    private bool _isMoving = false;
    private bool _isWaitingForPath = false;
    private int _targetIndex = 0;
    private int _currentStationIndex = 0;
    private bool _freeToBusy = false;
    private Vector3 _currentPos;
    private Vector3 _moveDirection = Vector3.zero;
    private float restDistance = 0f;
    public bool isShowPath = false;
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
            ActiveTaxis.Add(this);
            taxiStationPos = transform.position;
            // 建議根據 Object.InputAuthority 來排序，確保順序固定 (Host -> Client 1 -> Client 2...)
            // 這裡暫時依生成順序排列
        }
    }

    // --- 修改 2：銷毀時將自己移除名單 ---
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (runner.IsServer)
        {
            ActiveTaxis.Remove(this);
        }
    }

    public bool IsAvailable
    {
        get
        {
            // 只有當車子沒在移動，且佇列裡也沒有待辦路徑時，才算有空
            return (!_isMoving && _pathQueue.Count == 0) || (isStartDrive == false);
        }
    }
    public bool IsCarried
    {
        get
        {
            // 只有當車子沒在移動，且佇列裡也沒有待辦路徑時，才算有空
            return (isStartDrive == false);
        }
    }

    // --- 移除原本的 Update 與 DetectHostInput ---
    // private void Update() { ... } 刪除
    // private void DetectHostInput() { ... } 刪除

    // --- 修改 3：將 SetDestination 改為 public，讓調度員呼叫 ---
    public void SetDestination(Vector3 departure_Pos, Vector3 destination_Pos, int startIndex, int endIndex, string mode = "dealing")
    {
        if (_isWaitingForPath) return;
        _isWaitingForPath = true;
        //Vector3 startPos = new Vector3(-27.67f, 5.0f, 16.65f);
        targetStationIndex = endIndex;
        targetStationPos = destination_Pos;
        if ((departure_Pos - transform.position).magnitude > 0.1f)
        {
            for(int  i=0;i<stations.transform.childCount;i++)
            {
                if ((transform.position - stations.transform.GetChild(i).position).magnitude < 9f)
                {
                    _currentStationIndex = i;
                    Debug.Log($"[Taxi] 目前位置在車站 {_currentStationIndex}");
                }
               
            }
            if(!isStartDrive)
            {
                _freeToBusy = true;
            }
            PathRequestManager.Instance.RequestPath(transform.position, departure_Pos, _currentStationIndex, startIndex, 0, OnPathFound);
            PathRequestManager.Instance.RequestPath(departure_Pos, destination_Pos, startIndex, endIndex, 0, OnPathFound);
        }
        else
        {
            if(!isStartDrive)
            {
                _freeToBusy = true;
            }
            PathRequestManager.Instance.RequestPath(departure_Pos, destination_Pos, startIndex, endIndex, 0, OnPathFound);
        }
        if (mode == "Goback")
        {

            isStartDrive = false;
        }
        else
            isStartDrive = true;

    }

    // 這是 Callback 函式，當經理算完後會自動呼叫這裡
    private void OnPathFound(List<Vector3> newPath)
    {
        _isWaitingForPath = false;
        if (newPath != null && newPath.Count > 0)
        {
            //Debug.Log($"[Taxi] 找到路徑，共 {newPath.Count} 個點。第一個點{newPath[0]}");

            
            if (_isMoving==false || _freeToBusy==true)
            {
                startNewPath(newPath);
                //isStartDrive = true;    
            }
            else
            {
                _pathQueue.Enqueue(newPath);
                Debug.Log($"[Taxi] 車子忙碌中，新路徑已加入排程。目前佇列數: {_pathQueue.Count}");
            }
            //myMover.SetPath(newPath); // 把路徑丟給移動腳本
        }
        else
        {
            Debug.LogWarning("找不到路徑！");
            //_isMoving = false;
        }
        _freeToBusy = false;
    }

    public void startNewPath( List<Vector3> newPath)
    {
        restDistance = calPathDistance(newPath);
        _currentPath = newPath;
        _targetIndex = 0; // 重設目標點索引
        _isMoving = true;
    }
    // 如果你需要手動把車子瞬移到某個位置，呼叫這個方法
    /*public void TeleportTaxi(Vector3 newPosition)
    {
        if (Object.HasStateAuthority)
        {
            _cc.Teleport(newPosition);

            // 如果瞬移後要停止目前的導航，可以把路徑清空
            // _currentPath.Clear();
            // _isMoving = false;
        }
    }*/


    public override void FixedUpdateNetwork()
    {
        List<Vector3> pointsToDraw = null;
        float speed = avoidanceSystem.currentTargetSpeed;
        // 只有 Server 負責控制這台計程車的移動
        // Client 會透過 NetworkCharacterController 自動同步位置
        //if (!Object.HasStateAuthority) return;

        if(Runner.IsServer)
        {
            if (!_isMoving || _currentPath == null || _currentPath.Count == 0 || _targetIndex >= _currentPath.Count)
            {
                CheckForNextPath();
                return;
            }

            if (isStartDrive == false && _freeToBusy == true)
            {
                Debug.Log($"[Taxi] 車子閒置中，準備接續下一條路徑...");
                CheckForNextPath();
                return;
            }


            // 1. 取得當前目標點
            // 注意：這裡假設路徑點的 Y 值可能跟車子不同，如果你是平面移動，建議忽略 Y 軸
            Vector3 targetPos = _currentPath[_targetIndex];
            //Debug.Log(targetPos + "   " + transform.position);

            // 讓目標點的高度跟車子一樣，避免車子往地下鑽或飛起來 (視需求調整)
            targetPos.y = transform.position.y;


            // 2. 計算方向與距離
            Vector3 direction = targetPos - transform.position;
            //Debug.Log($"前進方向:{direction}");
            float distance = direction.magnitude;
            direction.y = 0; // 忽略 Y 軸的移動

            // 3. 檢查是否到達該點 (由距離判定)
            if (distance < 0.5f) // 0.5f 是誤差範圍，可依需求調整
            {
                _targetIndex++; // 前往下一點

                // 檢查是否跑完整條路徑
                if (_targetIndex >= _currentPath.Count)
                {
                    CheckForNextPath();
                }
            }

            // 4. 執行移動
            if (_isMoving && speed > 0.01f)
            {
                direction.Normalize();

                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

                // 步驟 3: 平滑旋轉 (TurnSpeed 控制轉向快慢)
                // 使用 Runner.DeltaTime 確保連線同步一致
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Runner.DeltaTime * TurnSpeed
                );
                Vector3 moveDistance = direction * speed * Runner.DeltaTime;
                restDistance -= moveDistance.magnitude;
                statsDisplay.SetCurrentProgress(restDistance);
                //Debug.DrawRay(transform.position, transform.forward * 5f, Color.red); // 顯示車頭方向
                transform.position += direction * speed * Runner.DeltaTime;
                _currentPos = transform.position;
                //Debug.Log($"[Taxi] 正在前往目標點位置：{targetPos}, 現在位置:{transform.position}");

                // 讓車頭朝向移動方向
                /*if (direction.sqrMagnitude > 0.001f)
                {

                    transform.rotation = Quaternion.LookRotation(direction);
                }*/

            }
            if(isShowPath)
            {
                pointsToDraw = _currentPath; // Server 用真資料
            }
            else
            {
                // 如果沒被選中，確保線是關掉的
                // (你可以讓 UpdatePath 裡處理，或是手動關掉)
                pathViewer.lineRenderer.positionCount = 0;
            }
            if (pointsToDraw != null && pointsToDraw.Count > 0)
            {
                GlobalPathRenderer.Instance.DrawPath(transform.position, pointsToDraw, _targetIndex);
            }
        }
        else
        {
            if(isShowPath)
            {
                RPC_RequestPath(Runner.LocalPlayer);
                Debug.Log($"[Client] 畫路徑，目標索引: {_clientTargetIndex}");
                pointsToDraw = _clientDisplayPath; // Client 用 RPC 資料
            }
            else
            {
                // 如果沒被選中，確保線是關掉的
                // (你可以讓 UpdatePath 裡處理，或是手動關掉)
                pathViewer.lineRenderer.positionCount = 0;
            }
            if (pointsToDraw != null && pointsToDraw.Count > 0)
            {
                GlobalPathRenderer.Instance.DrawPath(transform.position, pointsToDraw, _clientTargetIndex);
            }

        }
        
        

    }
    private void CheckForNextPath()
    {
        if (_isWaitingForPath) return;
        if(_pathQueue.Count > 0)
        {
            // 從佇列取出下一條路徑並執行
            List<Vector3> nextPath = _pathQueue.Dequeue();
            startNewPath(nextPath);
            Debug.Log($"[Taxi] 無縫接軌下一條路徑！剩餘排程: {_pathQueue.Count}");
        }
        else if(_pathQueue.Count ==0 && isStartDrive)
        {
            for (int i = 0; i < stations.transform.childCount; i++)
            {
                if ((_currentPos - stations.transform.GetChild(i).position).magnitude < 9f)
                {
                    _currentStationIndex = i;
                    Debug.Log($"[Taxi] 目前閒置計程車位置在車站 {_currentStationIndex}");
                }

            }
            // 已經到達目的地，回到原本的車站位置待命
            Debug.Log($"[Taxi] 已到達目的地，準備返回車站{taxiStationPos}待命。");
            Debug.Log($"[Taxi] 從station{_currentPos}到 {taxiStationPos}");
            isStartDrive = false;
            SetDestination(_currentPos, taxiStationPos,_currentStationIndex, 0, "Goback");
            //Debug.Log("[Taxi] 所有行程結束，待命。");
        }
        else
        {
            // 真的沒事做了，完全停下
            _isMoving = false;
            _currentPath = null;
        }
    }
    private void OnDrawGizmos()
    {
        // 1. 畫節點 (如果開關有開)
        /*if (showGraphNodes && allNodes != null)
        {
            Gizmos.color = Color.blue;
            foreach (var node in allNodes)
            {
                Gizmos.DrawSphere(node.Position, 0.2f);
                foreach (var edge in node.OutgoingEdges)
                {
                    Vector3 dir = (edge.ToNode.Position - node.Position).normalized;
                    // 為了避免 Scene 畫面太亂，只畫線，箭頭可以視情況省略
                    Gizmos.DrawLine(node.Position, edge.ToNode.Position);
                }
            }
        }*/

        // 2. 畫路徑 (只畫 cachedPath 裡面的東西，絕不在此處呼叫 FindPath)
        if (!isShowPath) return;

        if (_currentPath != null && _currentPath.Count > 1)
        {
            // 如果 cachedPath 沒東西，就畫 pathPoints (這是為了相容舊版)
            Gizmos.color = Color.red;
            if(_targetIndex < _currentPath.Count && _targetIndex >=0)
            {
                for (int i = _targetIndex; i < _currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(_currentPath[i], _currentPath[i +1]);
                    Gizmos.DrawSphere(_currentPath[i], 0.15f); // 標記路徑點
                }
            }
            
        }
    }

    private float calPathDistance(List<Vector3> path)
    {
        float totalDistance = Vector3.Distance(transform.position, path[0]);
        for(int i=0;i<path.Count -1;i++)
        {
            totalDistance += Vector3.Distance(path[i], path[i + 1]);
        }
        
        Debug.Log($"[Taxi] 計算路徑距離: {totalDistance} 公尺");
        return totalDistance;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestPath(PlayerRef requestor)
    {
        Debug.Log($"[Server] 收到 {requestor} 的路徑請求");

        if (_currentPath == null || _currentPath.Count == 0)
        {
            Debug.LogWarning("[Server] 目前沒有路徑資料可以回傳！");
            return;
        }

        Vector3[] pathArray = _currentPath.ToArray();

        // 回傳給請求者
        RPC_SendPathToClient(requestor, pathArray, _targetIndex);

       
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SendPathToClient([RpcTarget] PlayerRef player, Vector3[] pathPoints, int targetindex)
    {
        Debug.Log($"[Client] 收到路徑資料，長度: {pathPoints.Length}");

        _clientDisplayPath.Clear();
        _clientDisplayPath.AddRange(pathPoints);
        _clientTargetIndex = targetindex;
    }
}