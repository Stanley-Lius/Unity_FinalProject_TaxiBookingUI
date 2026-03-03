using UnityEngine;
using UnityEngine.Splines;
using TMPro; // 記得引用 TextMeshPro

public class TaxiStatusDisplay : MonoBehaviour
{
    [Header("UI References")]
    public GameObject infoCanvas;   // 拖入剛剛做的 InfoCanvas 物件
    public TextMeshProUGUI infoText; // 拖入 Canvas 裡的 Text 物件

    public float restDistance;

    private Vector3 lastPosition;
    private float currentSpeed; // m/s

    void Start()
    {
        lastPosition = transform.position;
        // 初始先隱藏
        if (infoCanvas != null) infoCanvas.SetActive(false);
    }

    void Update()
    {
        // 只有當 UI 開啟時才運算，節省效能
        if (infoCanvas != null && infoCanvas.activeSelf)
        {
            CalculateStats();
            UpdateUI();
            LookAtCamera();
        }
    }

    // --- 供外部 (Selector) 呼叫的開關 ---
    public void ShowInfo(bool show)
    {
        if (infoCanvas != null) infoCanvas.SetActive(show);
    }

    // --- 供外部 (Mover) 更新目前的 t 值 ---
    public void SetCurrentProgress(float t)
    {
        restDistance = t;
    }

    void CalculateStats()
    {
        // 1. 計算即時車速 (距離 / 時間)
        float distMoved = Vector3.Distance(transform.position, lastPosition);
        if (Time.deltaTime > 0)
        {
            // 使用 Lerp 讓數字跳動不要那麼劇烈
            float instantSpeed = distMoved / Time.deltaTime;
            currentSpeed = Mathf.Lerp(currentSpeed, instantSpeed, Time.deltaTime * 5f);
        }
        lastPosition = transform.position;
    }

    void UpdateUI()
    {        

        // 3. 計算 ETA (時間 = 距離 / 速度)
        string etaStr = "N/A";
        if (currentSpeed > 0.1f) // 避免除以 0
        {
            float timeSec = restDistance / currentSpeed;
            etaStr = $"{timeSec:F1} s"; // 顯示小數點後一位
        }
        else
        {
            etaStr = "Stopped";
        }

        // 4. 轉換速度單位 (m/s -> km/h 需 * 3.6)
        float kmh = currentSpeed * 3.6f;

        // 5. 更新文字
        infoText.text = $"Speed: {kmh:F0} km/h\nETA: {etaStr}";
    }

    //讓 UI 永遠面向攝影機 (Billboard 效果)
    void LookAtCamera()
    {
        if (Camera.main != null)
        {
            Vector3 direction = infoCanvas.transform.position - Camera.main.transform.position;
            direction.x = direction.z = 0; // 只在 Y 軸旋轉
            // 讓 Canvas 的正面朝向攝影機
            infoCanvas.transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}