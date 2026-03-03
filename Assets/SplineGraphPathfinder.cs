using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;

public class SplineGraphPathfinder : MonoBehaviour
{
    [Header("Settings")]
    public SplineContainer targetContainer;
    public float mergeDistance = 0.1f;

    [Header("Debug & Gizmos")]
    public bool showGraphNodes = false; // 開關：是否顯示節點球體
    public bool showPath = true;       // 開關：是否顯示路徑
    public Transform testStartObj;
    public Transform testEndObj;

    public List<Vector3> pathPoints = new List<Vector3>();

    // --- 圖論資料結構 (保持不變) ---
    public class GraphNode
    {
        public int Id;
        public Vector3 Position;
        public int InGoingEdgeCount = 0;
        public List<GraphEdge> OutgoingEdges = new List<GraphEdge>();
    }

    public class GraphEdge
    {
        public GraphNode ToNode;
        public float Weight;
        public Spline SplineRef;
        public int SplineIndex;
        public int StartKnotIndex;
    }

    private List<GraphNode> allNodes = new List<GraphNode>();

    // 【新增】用來儲存計算好的路徑，供 Gizmos 繪製使用
    private List<Vector3> cachedPath = new List<Vector3>();

    private void Start()
    {
        if (targetContainer != null)
        {
            BuildGraph();
        }
    }

    // ... (BuildGraph 與 GetOrCreateNode 程式碼保持不變，省略以節省篇幅) ...
    public void BuildGraph()
    {
        allNodes.Clear();
        int nodeIdCounter = 0;
        int splineCount = 0;
        foreach (var spline in targetContainer.Splines)
        {
            for (int i = 0; i < spline.Count - 1; i++)
            {
                Vector3 p1 = targetContainer.transform.TransformPoint(spline[i].Position);
                Vector3 p2 = targetContainer.transform.TransformPoint(spline[i + 1].Position);
                GraphNode nodeA = GetOrCreateNode(p1, ref nodeIdCounter);
                GraphNode nodeB = GetOrCreateNode(p2, ref nodeIdCounter);
                float length = SplineUtility.CalculateLength(spline, i);
                GraphEdge edge = new GraphEdge
                {
                    ToNode = nodeB,
                    Weight = length,
                    SplineRef = spline,
                    SplineIndex = splineCount,
                    StartKnotIndex = i
                    
                };
                nodeB.InGoingEdgeCount++;
                nodeA.OutgoingEdges.Add(edge);
                if (splineCount >= 16 && splineCount <= 47)
                {
                    GraphEdge edgeReverse = new GraphEdge
                    {
                        ToNode = nodeA,
                        Weight = length,
                        SplineRef = spline,
                        SplineIndex = splineCount,
                        StartKnotIndex = i+1
                    };
                    nodeA.InGoingEdgeCount++;
                    nodeB.OutgoingEdges.Add(edgeReverse);
                    Debug.Log($"Added bidirectional edge between Node {nodeA.Id} at {nodeA.Position} and Node {nodeB.Id} at {nodeB.Position}");
                }
            }
                
            if (spline.Closed)
            {
                int lastIdx = spline.Count - 1;
                Vector3 p1 = targetContainer.transform.TransformPoint(spline[lastIdx].Position);
                Vector3 p2 = targetContainer.transform.TransformPoint(spline[0].Position);
                GraphNode nodeA = GetOrCreateNode(p1, ref nodeIdCounter);
                GraphNode nodeB = GetOrCreateNode(p2, ref nodeIdCounter);
                float length = Vector3.Distance(p1, p2);
                nodeA.OutgoingEdges.Add(new GraphEdge { ToNode = nodeB, Weight = length, SplineRef = spline, StartKnotIndex = lastIdx });
            }

            splineCount++;
        }
        Debug.Log($"Graph Built! Nodes: {allNodes.Count}");
    }

    private GraphNode GetOrCreateNode(Vector3 position, ref int idCounter)
    {
        foreach (var node in allNodes)
        {
            if (Vector3.Distance(node.Position, position) <= mergeDistance)
                return node;
        }
        GraphNode newNode = new GraphNode { Id = idCounter++, Position = position };
        allNodes.Add(newNode);
        return newNode;
    }

    // ... (FindPath 與 GetClosestNode 保持不變，但我們可以稍作修改讓外部更容易呼叫) ...

    public List<Vector3> FindPath(Vector3 startPos, Vector3 endPos, int startIndex, int endIndex, int stationIndex=0)
    {
        pathPoints.Clear();
        // (這裡是你原本的 FindPath 邏輯，完全不用動)
        // ... 為了節省篇幅省略中間邏輯 ...
        // 確保如果 Graph 為空要 BuildGraph()
        if (allNodes == null || allNodes.Count == 0) BuildGraph();

        GraphNode startNode = GetClosestNode(startPos, 0);
        Debug.Log($"Start Node: {(startNode != null ? startNode.Id.ToString() : "null")}");
        GraphNode endNode = GetClosestNode(endPos, 1);
        Debug.Log($"End Node: {(endNode != null ? endNode.Id.ToString() : "null")}");

        if (startNode == null || endNode == null) return null;
        if (startNode == endNode) return new List<Vector3> { startNode.Position };

        Dictionary<GraphNode, float> distances = new Dictionary<GraphNode, float>();
        Dictionary<GraphNode, GraphNode> previous = new Dictionary<GraphNode, GraphNode>();
        List<GraphNode> unvisited = new List<GraphNode>();

        foreach (var node in allNodes)
        {
            distances[node] = float.MaxValue;
            unvisited.Add(node);
        }
        distances[startNode] = 0;

        while (unvisited.Count > 0)
        {
            unvisited.Sort((a, b) => distances[a].CompareTo(distances[b]));
            GraphNode current = unvisited[0];
            unvisited.RemoveAt(0);

            if (current == endNode) break;
            if (distances[current] == float.MaxValue) break;

            int edgeBelong = 0;
                foreach (var edge in current.OutgoingEdges)
                {

                    edgeBelong = edge.SplineIndex < 48 ? 0 : ((edge.SplineIndex - 48) / 4) + 1;
                    if (edgeBelong == 0)
                    {
                        //Debug.Log("公用道路");
                    }
                    else 
                    {
                        if(stationIndex == 0)
                        {
                            if ((edgeBelong != startIndex && edgeBelong != endIndex))
                                continue;
                        }
                        else
                        {
                            if(edgeBelong != stationIndex && edgeBelong != endIndex)
                                continue;
                    }
                        
                    }
                    float alt = distances[current] + edge.Weight;
                    if (alt < distances[edge.ToNode])
                    {
                        distances[edge.ToNode] = alt;
                        previous[edge.ToNode] = current;
                    }
                }


        }

       
        GraphNode curr = endNode;
        if (previous.ContainsKey(curr) || curr == startNode)
        {
            while (curr != null)
            {
                pathPoints.Add(curr.Position);
                //Debug.Log($"新增{curr.Position}到路徑中");
                curr = previous.ContainsKey(curr) ? previous[curr] : null;
            }
            pathPoints.Reverse();
            return pathPoints;
        }
        return null;
    }

    private GraphNode GetClosestNode(Vector3 pos, int mode=0)
    {
        GraphNode best = null;
        float minDst = float.MaxValue;
        foreach (var node in allNodes)
        {
            if(mode==0)
            {
                if (node.OutgoingEdges.Count > 0)
                {
                    // 只考慮 XZ 平面距離
                    Vector3 nodePosFlat = new Vector3(node.Position.x, 0, node.Position.z);
                    Vector3 targetPosFlat = new Vector3(pos.x, 0, pos.z);

                    float dst = Vector3.Distance(nodePosFlat, targetPosFlat);
                    if (dst < minDst)
                    {
                        minDst = dst;
                        best = node;
                    }
                }
            }
            else if(mode==1)
            {
                if (node.InGoingEdgeCount == 0) continue;
                Vector3 nodePosFlat = new Vector3(node.Position.x, 0, node.Position.z);
                Vector3 targetPosFlat = new Vector3(pos.x, 0, pos.z);

                float dst = Vector3.Distance(nodePosFlat, targetPosFlat);
                if (dst < minDst)
                {
                    minDst = dst;
                    best = node;
                }
            }
        }
        return best;
    }

    // ==========================================
    //  【修改重點 1】新增一個公開方法來觸發計算
    // ==========================================

    // 這個屬性讓你可以直接在 Inspector 的組件上按右鍵選擇 "Test Find Path" 來執行
    [ContextMenu("Test Find Path")]
    public void CalculateTestPath()
    {
        if (testStartObj != null && testEndObj != null)
        {
            Debug.Log("Manually calculating path...");
            // 將計算結果存入 cachedPath
            cachedPath = FindPath(testStartObj.position, testEndObj.position, -1, -1);

            if (cachedPath == null) Debug.LogWarning("Path not found!");
        }
        else
        {
            Debug.LogWarning("請先指派 Test Start Obj 與 Test End Obj");
        }
    }

    // ==========================================
    //  【修改重點 2】OnDrawGizmos 只負責「畫」
    // ==========================================
    /*private void OnDrawGizmos()
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
        }

        // 2. 畫路徑 (只畫 cachedPath 裡面的東西，絕不在此處呼叫 FindPath)
        
        if(pathPoints != null && pathPoints.Count > 1)
        {
            // 如果 cachedPath 沒東西，就畫 pathPoints (這是為了相容舊版)
            Gizmos.color = Color.red;
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Gizmos.DrawLine(pathPoints[i], pathPoints[i + 1]);
                Gizmos.DrawSphere(pathPoints[i], 0.15f); // 標記路徑點
            }
        }
        else if (showPath && cachedPath != null && cachedPath.Count > 1)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < cachedPath.Count - 1; i++)
            {
                // 畫出較粗或明顯的線條
                Gizmos.DrawLine(cachedPath[i], cachedPath[i + 1]);
                Gizmos.DrawSphere(cachedPath[i], 0.15f); // 標記路徑點
            }
        }
    } */
}