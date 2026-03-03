using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class PathViewer : MonoBehaviour
{
    public bool isShowPath = true; // 開關
    public LineRenderer lineRenderer;

    void Awake()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();

        // 確保使用世界座標
        lineRenderer.useWorldSpace = true;
    }

    // 在原本 Update 呼叫這個函式
    public void UpdatePath(List<Vector3> currentPath, int targetIndex)
    {
        Debug.Log($"    {currentPath.Count}");
        // 1. 對應原本的: if (!isShowpath) return;
        //    以及防呆檢查
        if (!isShowPath || currentPath == null || currentPath.Count <= 1)
        {
            lineRenderer.positionCount = 0; // 隱藏線條
            return;
        }

        // 2. 對應原本的: if(_targetIndex < _currentPath.Count && _targetIndex >= 0)
        //    如果已經走到終點了，就沒線可畫了
        if (targetIndex >= currentPath.Count - 1 || targetIndex < 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // --- 核心邏輯修改 ---

        // 計算需要畫幾個點
        // 原本邏輯是從 targetIndex 連到 targetIndex+1
        // 這代表我們需要從 targetIndex 開始直到 List 結束的所有點
        int pointsNeeded = currentPath.Count - targetIndex;

        // 設定 LineRenderer 的點數量
        lineRenderer.positionCount = pointsNeeded;

        // 3. 把點填進去
        // 這裡不需要像 Gizmos 一樣跑迴圈畫線 (DrawLine)，LineRenderer 會自動把點連起來
        for (int i = 0; i < pointsNeeded; i++)
        {
            // 把 currentPath 裡的點，搬移到 LineRenderer 裡
            lineRenderer.SetPosition(i, currentPath[targetIndex + i]);
        }
    }
}