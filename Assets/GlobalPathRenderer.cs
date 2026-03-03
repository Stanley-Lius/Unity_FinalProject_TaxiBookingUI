using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class GlobalPathRenderer : MonoBehaviour
{
    // 單例模式：讓外部可以直接用 GlobalPathRenderer.Instance 呼叫
    public static GlobalPathRenderer Instance;

    private LineRenderer lineRenderer;

    void Awake()
    {
        // 初始化單例
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true; // 確保是用世界座標
        lineRenderer.enabled = false;      // 預設隱藏
    }

    /// <summary>
    /// 繪製路徑的函式
    /// </summary>
    /// <param name="carPos">車子當前位置 (為了把線連到車上)</param>
    /// <param name="pathPoints">路徑點列表</param>
    /// <param name="targetIndex">目前走到第幾個點</param>
    public void DrawPath(Vector3 carPos, List<Vector3> pathPoints, int targetIndex)
    {
        // 1. 防呆檢查
        if (pathPoints == null || pathPoints.Count == 0 || targetIndex >= pathPoints.Count)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;

        // 2. 計算需要畫的點數
        // 點數 = (車子自己) + (剩下的路徑點)
        int remainingPoints = pathPoints.Count - targetIndex;
        lineRenderer.positionCount = remainingPoints + 1;

        // 3. 設定第 0 點：車子當前位置 (把線稍微抬高一點，避免被地板吃掉)
        lineRenderer.SetPosition(0, carPos + Vector3.up * 0.5f);

        // 4. 設定剩下的路徑點
        for (int i = 0; i < remainingPoints; i++)
        {
            Vector3 p = pathPoints[targetIndex + i];
            // 每個點都稍微抬高一點
            lineRenderer.SetPosition(i + 1, p + Vector3.up * 0.5f);
        }
    }

    // 提供一個函式讓車子取消選取時呼叫
    public void ClearPath()
    {
        lineRenderer.enabled = false;
        lineRenderer.positionCount = 0;
    }
}