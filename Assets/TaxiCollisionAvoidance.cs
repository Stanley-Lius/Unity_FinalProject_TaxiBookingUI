using UnityEngine;

public class TaxiCollisionAvoidance : MonoBehaviour
{
    [Header("Sensor Settings")]
    public float detectionDistance = 10f; // 偵測距離
    public float stopDistance = 7f;       //在這個距離內會完全停車
    public LayerMask taxiLayer;           // 只偵測 "Taxi" Layer
    public Transform sensorPos;           // 射線發射點 (建議設在車頭)

    [Header("Speed Control")]
    public float maxSpeed = 7f;          // 正常巡航速度
    public float currentTargetSpeed;      // 輸出給移動腳本的速度
    public float stopTimer = 0f;

    private void Start()
    {
        currentTargetSpeed = maxSpeed;
    }

    void Update()
    {
        DetectFrontCar();
    }

    void DetectFrontCar()
    {
        RaycastHit hit;
        // 從車頭向前發射射線
        Vector3 direction = transform.forward;
        bool isStoppedBySensor = false;

        // 檢查是否打到東西
        if (Physics.Raycast(sensorPos.position, direction, out hit, detectionDistance, taxiLayer))
        {

            float distanceToCar = hit.distance;

            // 邏輯：越近越慢，太近則停
            if (distanceToCar <= stopDistance)
            {
                isStoppedBySensor = true;
                // 太近了，緊急煞車
                currentTargetSpeed = 0f;
                Debug.DrawRay(sensorPos.position, direction * distanceToCar, Color.red);
            }
            else
            {
                // 在偵測範圍內，進行線性減速 (Lerp)
                // 距離越短，速度比例越低
                float ratio = (distanceToCar - stopDistance) / (detectionDistance - stopDistance);
                currentTargetSpeed = Mathf.Lerp(0f, maxSpeed, ratio);

                Debug.DrawRay(sensorPos.position, direction * distanceToCar, Color.yellow);
            }
        }
        else
        {
            // 前方沒車，恢復全速
            currentTargetSpeed = Mathf.Lerp(currentTargetSpeed, maxSpeed, Time.deltaTime * 2f); // 平滑加速
            Debug.DrawRay(sensorPos.position, direction * detectionDistance, Color.green);
        }

        if (isStoppedBySensor)
        {
            stopTimer += Time.deltaTime;

            // 如果已經卡住超過 2 秒
            if (stopTimer > 1.0f)
            {
                // 強制無視障礙物 0.5 秒 (利用這段時間錯開位置)
                currentTargetSpeed = maxSpeed * 0.5f;

                // 或者是隨機等待一下再啟動 (Random.Range)，避免兩車同時啟動又同時煞車
                return;
            }
        }
        else
        {
            stopTimer = 0f; // 只要有在動，計時器歸零
        }
    }
}