using UnityEngine;

public class BoosterController : MonoBehaviour
{
    public static BoosterController Instance;

    [Header("References")]
    public BlockSpawner blockSpawner; // Kéo BlockSpawner vào đây

    // Lưu trữ booster đang được kích hoạt hiện tại (nếu có)
    private MonoBehaviour currentActiveBooster;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Tự động tìm BlockSpawner trong Scene nếu chưa được kéo vào
        if (blockSpawner == null)
        {
            blockSpawner = FindFirstObjectByType<BlockSpawner>();
            if (blockSpawner == null)
            {
                Debug.LogWarning("[BoosterController] Không tìm thấy BlockSpawner nào trong Scene!");
            }
        }
    }

    /// <summary>
    /// Đăng ký booster đang được chọn
    /// </summary>
    public void SetActiveBooster(MonoBehaviour booster)
    {
        currentActiveBooster = booster;
        Debug.Log("Đã kích hoạt Booster: " + (booster != null ? booster.GetType().Name : "None"));
    }

    /// <summary>
    /// Hủy kích hoạt booster hiện tại
    /// </summary>
    public void ClearActiveBooster()
    {
        currentActiveBooster = null;
    }

    /// <summary>
    /// Kiểm tra xem có đang dùng booster nào yêu cầu click vào block hay không
    /// </summary>
    public bool IsAnyBoosterActive()
    {
        return currentActiveBooster != null;
    }

    /// <summary>
    /// Được gọi từ BlockItem khi người dùng click vào block trên màn hình
    /// </summary>
    public void OnBlockClicked(BlockItem clickedBlock)
    {
        if (currentActiveBooster == null) return;

        // Nếu booster hiện tại là RemoveBlockBooster
        if (currentActiveBooster is RemoveBlockBooster removeBooster)
        {
            removeBooster.Execute(clickedBlock);
        }
        // BỔ SUNG: Nếu booster hiện tại là ReverseBooster
        else if (currentActiveBooster is ReverseBooster reverseBooster)
        {
            reverseBooster.Execute(clickedBlock);
        }
    }
}