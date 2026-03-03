using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting; // ★ 1. 必須引用這行才能控制 Button

public class TaxiInputDispatcher : NetworkBehaviour
{
    [Header("UI 設定")]
    [SerializeField] private GameObject hostControlPanel;
    [SerializeField] private TMP_InputField inputField_departure;
    [SerializeField] private TMP_InputField inputField_destination;
    [SerializeField] private GameObject stations;
    private static Dictionary<int, Vector3> stationPositions = new Dictionary<int, Vector3>();


    // ★ 2. 新增按鈕變數
    [SerializeField] private Button submitButton;

    public struct TaxiOrder
    {
        public Vector3 PickupPos;
        public Vector3 DropoffPos;
        public int StartNodeIndex; // 如果你的路徑系統需要
        public int EndNodeIndex;
    }

    private Queue<TaxiOrder> _pendingOrders = new Queue<TaxiOrder>();
    // 用來記錄現在輪到哪一台車
    private int currentTaxiIndex = 0;
    private int targetIndex;

    public override void Spawned()
    {
        // 檢查：我是不是伺服器 (Host)?
        if (Runner.IsServer)
        {
            if (hostControlPanel != null)
            {
                hostControlPanel.SetActive(true);
            }
        }
        else
        {
            if (hostControlPanel != null) hostControlPanel.SetActive(false);
            this.enabled = false;
        }
    }

    private void Start()
    {
        // 初始化車站位置字典
        for(int i =0;i < stations.transform.childCount;i++)
        { 
            stationPositions.Add(i, stations.transform.GetChild(i).position); 
            //Debug.Log($"Station {i} Position: {stations.transform.GetChild(i).localPosition}");
        }
        
        
        if (inputField_departure != null) inputField_departure.ActivateInputField();

        // ★ 3. 在程式碼中綁定按鈕事件
        // 意思是：「當 submitButton 被點擊時，執行 SubmitDestination 函式」
        if (submitButton != null)
        {
            // 先移除所有監聽器以防重複綁定 (Optional)
            submitButton.onClick.RemoveAllListeners();
            // 加入監聽
            submitButton.onClick.AddListener(SubmitDestination);
        }
        else
        {
            Debug.LogWarning("注意：尚未在 Inspector 中拖曳指定 Submit Button！");
        }
    }

    // 記得：當物件被銷毀時，最好移除監聽 (雖非強制，但在某些動態生成的UI中是好習慣)
    private void OnDestroy()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(SubmitDestination);
        }
    }

    private void Update()
    {
        // 更新 Placeholder 提示文字
        if (TaxiPlayer.ActiveTaxis.Count > 0)
        {
            targetIndex = currentTaxiIndex % TaxiPlayer.ActiveTaxis.Count;

            if (inputField_departure.placeholder != null && inputField_destination.placeholder != null)
            {
                ((TMP_Text)inputField_departure.placeholder).fontSize = 7;
                ((TMP_Text)inputField_destination.placeholder).fontSize = 7;
                ((TMP_Text)inputField_departure.placeholder).text = $"Taxi No.{targetIndex}: Input Departure (0-9)";
                ((TMP_Text)inputField_destination.placeholder).text = $"Taxi No.{targetIndex}: Input Destination (0-9)";
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 只有 Server 有權力分配任務
        if (!Runner.IsServer) return;

        // 如果沒有訂單，就不用檢查車子
        if (_pendingOrders.Count == 0) return;

        TaxiOrder taxiOrder = _pendingOrders.Peek();

        // 變數準備：用來存「第幾號」車
        int targetIndex = -1;
        float min_distance = float.MaxValue;

        // 2. 使用 for 迴圈來遍歷，這樣我們才能拿到 index (i)
        for (int i = 0; i < TaxiPlayer.ActiveTaxis.Count; i++)
        {
            var taxi = TaxiPlayer.ActiveTaxis[i];

            // 安全檢查：如果這台車意外被刪除了(null)，就跳過
            if (taxi == null) continue;

            if (taxi.IsAvailable)
            {
                // 這裡要確保 taxiOrder 不是 null，否則請檢查傳入資料
                float distance = Vector3.Distance(taxi.transform.position, taxiOrder.PickupPos);

                if (distance < min_distance)
                {
                    min_distance = distance;

                    // 【關鍵修改】我們只記錄「編號」，不記錄物件本身
                    targetIndex = i;
                }
            }
        }

        // 3. 迴圈結束後，透過編號把車抓出來
        if (targetIndex != -1)
        {
            // 二度安全檢查：確保這個編號還在列表範圍內 (防止列表在計算過程中變短)
            if (targetIndex < TaxiPlayer.ActiveTaxis.Count)
            {
                // 透過編號取得真正的計程車物件
                var finalTaxi = TaxiPlayer.ActiveTaxis[targetIndex];

                if (finalTaxi != null)
                {
                    Debug.Log($"找到最近的計程車，編號: {targetIndex}, 名稱: {finalTaxi.name}");

                    // 在這裡呼叫你要的功能
                    AssignOrderToTaxi(finalTaxi); 
                }
            }
            else
            {
                Debug.LogError("錯誤：找到的編號已經超出列表範圍 (可能列表在計算途中被修改)");
            }
        }

    }

    // 這個函式現在會被 Start() 裡面的 AddListener 呼叫
    public void SubmitDestination()
    {
        // 0. 基本檢查
        if (TaxiPlayer.ActiveTaxis.Count == 0)
        {
            Debug.LogError("錯誤：場上沒有任何計程車！");
            return;
        }

        // 1. 檢查兩個輸入框是否都有值
        if (string.IsNullOrEmpty(inputField_departure.text) || string.IsNullOrEmpty(inputField_destination.text))
        {
            Debug.LogError("錯誤：出發地或目的地不能為空！請輸入數值。");
            return;
        }

        // 2. 轉型檢查
        bool isDepNumeric = int.TryParse(inputField_departure.text, out int departureID);
        bool isDestNumeric = int.TryParse(inputField_destination.text, out int destinationID);

        if (!isDepNumeric || !isDestNumeric || departureID >=10 || destinationID >=10 || departureID == destinationID)
        {
            Debug.LogError("錯誤：請輸入有效的整數數字！數字範圍0到9");
            return;
        }

        // 3. 執行邏輯
        Vector3 departurePos = stationPositions[departureID];
        Vector3 destinationPos = stationPositions[destinationID];
        TaxiOrder newOrder = new TaxiOrder
        {
            PickupPos = departurePos,
            DropoffPos = destinationPos,
            StartNodeIndex = departureID,
            EndNodeIndex = destinationID
        };
        _pendingOrders.Enqueue(newOrder);
        Debug.Log($"[Dispatcher] 收到新訂單，目前等待中訂單數: {_pendingOrders.Count}");
   
        


    }

    private void AssignOrderToTaxi(TaxiPlayer taxi)
    {
        if (_pendingOrders.Count > 0)
        {
            TaxiOrder order = _pendingOrders.Dequeue();

            //Debug.Log($"[Dispatcher] 指派任務給車輛 {taxi.Object.Id}");

            // 呼叫你原本寫好的 SetDestination
            Debug.Log($"[成功] 指派計程車 No.{taxi.Object.Id}從 {order.PickupPos} 前往 {order.DropoffPos}");
            taxi.SetDestination(
                order.PickupPos,
                order.DropoffPos,
                order.StartNodeIndex,
                order.EndNodeIndex
            );
           
        }
    }
    private void ClearInputs()
    {
        inputField_departure.text = "";
        inputField_destination.text = "";
        inputField_departure.ActivateInputField();
    }
}