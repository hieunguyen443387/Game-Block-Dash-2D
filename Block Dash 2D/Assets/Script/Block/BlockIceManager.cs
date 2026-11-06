using UnityEngine;
using TMPro;

[ExecuteAlways]
public class BlockIceManager : MonoBehaviour
{
    [Header("Cấu hình Băng")]
    public bool isIced = false;                  
    [Range(0, 99)] public int iceCount = 0;      

    [Header("Prefab & Giao diện")]
    public GameObject icePrefab;                 // Kéo Prefab băng của bạn vào đây trong Inspector
    public Transform iceSpawnPoint;              // Vị trí để đặt cục băng (nếu để trống sẽ mặc định lấy tâm block)
    
    [Header("References (Tự động hoặc thủ công)")]
    private GameObject spawnedIceInstance;       // Instance của Prefab băng được sinh ra
    public TextMeshProUGUI iceCountText;         // Text hiển thị số lượng

    private void Update()
    {
        // Khi chạy trong Editor hoặc Gameplay, tự động quản lý việc tạo và cập nhật Prefab
        UpdateIceState();
    }

    public void UpdateIceState()
    {
        // 1. Xử lý sinh hoặc hủy Prefab băng dựa trên trạng thái isIced
        if (isIced)
        {
            if (spawnedIceInstance == null && icePrefab != null)
            {
                // Nếuchưa có băng và được bật, tiến hành Instantiate Prefab băng làm con của block
                Transform parentTransform = iceSpawnPoint != null ? iceSpawnPoint : transform;
                spawnedIceInstance = Instantiate(icePrefab, parentTransform);
                
                // Đặt lại local position về 0 để nó nằm khớp tâm block
                spawnedIceInstance.transform.localPosition = Vector3.zero;
                spawnedIceInstance.transform.localRotation = Quaternion.identity;
                spawnedIceInstance.transform.localScale = Vector3.one;

                // Tự động tìm Text hiển thị số bên trong Prefab (nếu Prefab băng đã gắn sẵn Text)
                if (iceCountText == null)
                {
                    iceCountText = spawnedIceInstance.GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            // Cập nhật giá trị số đếm lên text
            if (iceCountText != null)
            {
                if (!iceCountText.gameObject.activeSelf) iceCountText.gameObject.SetActive(true);
                iceCountText.text = iceCount.ToString();
            }
        }
        else
        {
            // Nếu tắt băng, xóa instance của Prefab đi
            if (spawnedIceInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(spawnedIceInstance);
                else
                    DestroyImmediate(spawnedIceInstance);
            }

            if (iceCountText != null)
            {
                iceCountText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Hàm dùng trong game để giảm số đếm khi block vào cổng thoát
    /// </summary>
    public void DecreaseIceCount()
    {
        if (!isIced) return;

        iceCount--;
        if (iceCount <= 0)
        {
            iceCount = 0;
            BreakIce();
        }
        UpdateIceState();
    }

    private void BreakIce()
    {
        isIced = false;
        Debug.Log("Băng đã tan vỡ hoàn toàn!");
        // (Tùy chọn) Gọi hiệu ứng vỡ, âm thanh tại đây trước khi object bị hủy
    }
}