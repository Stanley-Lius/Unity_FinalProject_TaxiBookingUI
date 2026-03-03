using Fusion;
using UnityEngine;

public class TaxiVisuals : NetworkBehaviour
{
    [Header("設定")]
    [SerializeField] private GameObject _targetObject;
    [SerializeField] private Material[] _skinMaterials;
    private MeshRenderer _targetMeshRenderer;

    // 1. 依然使用 [Networked]，但拿掉 OnChanged 參數
    // 我們只需要它幫我們同步數據，不需要它通知我們
    [Networked]
    public int SkinIndex { get; set; }

    // 2. 用一個私有變數來記住「目前車上顯示的是幾號造型」
    // 預設為 -1 代表還沒初始化過
    private int _currentVisibleSkinIndex = -1;

    // Server 生成時執行一次，設定初始顏色
    public override void Spawned()
    {
        _targetMeshRenderer = _targetObject.GetComponent<MeshRenderer>();
        if (HasStateAuthority)
        {
            if (_skinMaterials != null && _skinMaterials.Length > 0)
            {
                // 【核心修改】使用 Object.Id 對陣列長度取餘數 (Mod)
                // Object.Id.Raw 是一個 uint (不重複的整數 ID)
                SkinIndex = (int)((Object.Id.Raw/2) % _skinMaterials.Length);
            }

        }
        else
        {
            UpdateSkin(SkinIndex);
        }
    }

    // 3. 使用 Render() 替代 Update()
    // 這是 Fusion 專門用來處理畫面邏輯的地方，每一幀都會跑
    public override void Render()
    {
        // 檢查：如果「網路上同步過來的 SkinIndex」跟「我現在顯示的」不一樣
        if (SkinIndex != _currentVisibleSkinIndex)
        {
            // 執行換色
            UpdateSkin(SkinIndex);

            // 更新紀錄，這樣下一幀就不會重複執行這段
            _currentVisibleSkinIndex = SkinIndex;
        }
    }

    private void UpdateSkin(int index)
    {
        // 防呆保護
        if (_skinMaterials == null || _skinMaterials.Length == 0) return;

        // 確保 Index 不會超過陣列範圍
        int validIndex = Mathf.Clamp(index, 0, _skinMaterials.Length - 1);
        //Debug.Log($"{Object.Id.Raw} 更換車輛材質至索引 {validIndex}");

        // 更換材質
        _targetMeshRenderer.sharedMaterial = _skinMaterials[validIndex];
    }
}