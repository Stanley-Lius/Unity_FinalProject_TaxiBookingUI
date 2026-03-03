using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathRequestManager : MonoBehaviour
{
    public static PathRequestManager Instance;
    public GameObject pathCalculator;
    private void Awake()
    {
        Instance = this;
    }

    struct PathRequest
    {
        public Vector3 Start;
        public Vector3 End;
        public int startIndex;
        public int endIndex;
        public int stationIndex;
        public Action<List<Vector3>> Callback;

        public PathRequest(Vector3 s, Vector3 e,int si, int ei, Action<List<Vector3>> cb, int sti = 0)
        {
            Start = s;
            End = e;
            startIndex = si;
            endIndex = ei;
            stationIndex = sti;
            Callback = cb;
        }
    }

    private Queue<PathRequest> _requestQueue = new Queue<PathRequest>();
    private PathRequest _currentRequest;
    private bool _isProcessing;

    // 1. 外部呼叫這個
    public void RequestPath(Vector3 start, Vector3 end,int startIndex, int endIndex, int stationIndex, Action<List<Vector3>> callback)
    {
        PathRequest newRequest = new PathRequest(start, end, startIndex, endIndex, callback, stationIndex=0);
        if(_requestQueue.Count > 100)
        {
            Debug.LogError("[Error] 路徑請求佇列過長，可能發生阻塞！");
            return;
        }
        _requestQueue.Enqueue(newRequest);
        TryProcessNext();
    }

    private void TryProcessNext()
    {
        if (!_isProcessing && _requestQueue.Count > 0)
        {
            _currentRequest = _requestQueue.Dequeue();
            _isProcessing = true;

            // 啟動協程來處理路徑計算
            StartCoroutine(ProcessPathCoroutine());
        }
    }

    // 2. 這是在主執行緒跑的，所以可以用 NavMesh/Physics，不會報錯！
    IEnumerator ProcessPathCoroutine()
    {
        // 呼叫你的演算法 (這裡假設你的演算法會回傳 List<Vector3>)
        // 如果你的演算法本身很慢，請往下看 "如何修改演算法"
        // 1. 確認是否到達這裡
        Debug.Log($"[Debug] 準備開始尋路... Start: {_currentRequest.Start}, End: {_currentRequest.End}");


        // 執行你的原始代碼
        List<Vector3> path = pathCalculator.GetComponent<SplineGraphPathfinder>().FindPath(_currentRequest.Start, _currentRequest.End, _currentRequest.startIndex, _currentRequest.endIndex, _currentRequest.stationIndex);

        // 2. 確認執行結果
        if (path == null)
        {
            Debug.LogError("[Error] FindPath 返回了 null！");
        }
        else
        {
            path.Add(_currentRequest.End);
            Debug.Log($"[Debug] 尋路結束，路徑點數量: {path.Count}");
        }
        //Debug.Log("路徑請求已送出，等待經理處理...");
        // 為了避免瞬間卡頓，如果你是一次處理大量請求，可以在這裡等一幀
        yield return null;

        // 執行回調，把結果傳回去
        _currentRequest.Callback(new List<Vector3>(path));

        _isProcessing = false;
        TryProcessNext();
    }

    // --- 這裡放你的原始演算法 ---
    private List<Vector3> YourPathFindingLogic(Vector3 start, Vector3 end)
    {
        // 因為是在 Coroutine 裡，這裡可以放心的用 Unity API
        // 例如: NavMeshPath navPath = new NavMeshPath();
        // NavMesh.CalculatePath(start, end, NavMesh.AllAreas, navPath);

        // 這裡回傳假資料做示範
        List<Vector3> dummyPath = new List<Vector3>();
        dummyPath.Add(start);
        dummyPath.Add(Vector3.MoveTowards(start, end, 5f)); // 中間點
        dummyPath.Add(end);
        return dummyPath;
    }
}