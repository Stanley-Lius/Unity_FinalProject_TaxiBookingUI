using UnityEngine.SceneManagement;
using UnityEngine;
using Fusion;

public class TaxiPathShower: MonoBehaviour
{
    private TaxiPlayer currentSelectedTaxi; // 紀錄目前選中的車
    private TaxiStatusDisplay currentSeletedShower;
    public LayerMask taxiLayer; // 記得設定 Layer，只偵測計程車

    void Update()
    {
        
        // 當按下滑鼠左鍵 (0)
        if (Input.GetMouseButtonDown(0))
        {
            SelectTaxi();
        }
    }

    void SelectTaxi()
    {
        // 從攝影機向滑鼠位置發射射線
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        float detectRadius = 3.0f;

        if (Physics.SphereCast(ray, detectRadius, out hit, 1000f, taxiLayer))
        {
            // 嘗試取得車上的視覺化腳本
            TaxiPlayer taxi = hit.transform.GetComponent<TaxiPlayer>();
            var stats = hit.transform.GetComponentInParent<TaxiStatusDisplay>();
            // 注意：如果腳本掛在父物件，可能需要用 GetComponentInParent<T>()
            if (taxi == null) taxi = hit.transform.GetComponentInParent<TaxiPlayer>();

            if (taxi != null)
            {
                // 1. 把舊的車關掉
                if (currentSelectedTaxi != null)
                {
                    currentSelectedTaxi.isShowPath = false;
                }

                // 2. 把新的車打開
                currentSelectedTaxi = taxi;
                currentSelectedTaxi.isShowPath = true;

                if (taxi.Runner != null && !taxi.Runner.IsServer)
                {
                    taxi.RPC_RequestPath(taxi.Runner.LocalPlayer);
                }
                

                //Debug.Log($"已選取計程車: {taxi.name}");
            }
            if(stats != null)
            {
                if(currentSeletedShower != null)
                {
                    currentSeletedShower.ShowInfo(false);
                }

                currentSeletedShower = stats;
                currentSeletedShower.ShowInfo(true);

            }
        }
        else
        {
            // 如果點到空地，取消選取
            if (currentSelectedTaxi != null)
            {
                currentSelectedTaxi.isShowPath = false;
                currentSelectedTaxi = null;             
            }
            if(currentSeletedShower != null)
            {
                currentSeletedShower.ShowInfo(false);
                currentSeletedShower = null;
            }
            if (GlobalPathRenderer.Instance != null)
            {
                GlobalPathRenderer.Instance.ClearPath();
            }
        }
    }
}