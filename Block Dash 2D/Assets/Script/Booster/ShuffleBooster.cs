using UnityEngine;

public class ShuffleBooster : MonoBehaviour
{
    [Header("Cấu hình Shuffle")]
    [Tooltip("Tỷ lệ số lượng block sẽ bị đảo ngược hướng (Ví dụ: 1.0 = đảo tất cả, 0.5 = đảo ngẫu nhiên 50% số block trên bàn cờ)")]
    [Range(0.1f, 1f)] public float shuffleRatio = 1.0f;

    /// <summary>
    /// Gắn hàm này vào sự kiện OnClick() của nút UI Booster Shuffle
    /// </summary>
    public void OnClickActivateButton()
    {
        ExecuteShuffle();
    }

    /// <summary>
    /// Thực hiện đảo ngược hướng (Reverse 180°) các ArrowBlock trên bàn cờ và snap lại vị trí
    /// </summary>
    public void ExecuteShuffle()
    {
        // Tìm tất cả các ArrowBlock đang có trong Scene
        ArrowBlock[] allArrowBlocks = FindObjectsByType<ArrowBlock>(FindObjectsSortMode.None);

        if (allArrowBlocks == null || allArrowBlocks.Length == 0)
        {
            Debug.LogWarning("Không tìm thấy ArrowBlock nào trên bàn cờ để Shuffle!");
            return;
        }

        int count = 0;
        foreach (var arrowBlock in allArrowBlocks)
        {
            if (arrowBlock == null) continue;

            // Kiểm tra tỷ lệ random có chọn block này không (nếu shuffleRatio < 1)
            if (Random.value > shuffleRatio) continue;

            // 1. Đảo ngược hướng hiện tại (Đang Right -> Left, Up -> Down, v.v.)
            ArrowDirection currentDir = arrowBlock.direction;
            ArrowDirection reversedDir = GetReversedDirection(currentDir);
            
            arrowBlock.SetDirection(reversedDir);

            // 2. Gọi hàm SetPosition từ BlockSnapToGrid để khớp chuẩn vào ô cell
            BlockSnapToGrid snapScript = arrowBlock.GetComponent<BlockSnapToGrid>();
            if (snapScript != null)
            {
                snapScript.SetPosition();
            }

            count++;
        }

        Debug.Log($"Đã Shuffle (đảo ngược hướng) thành công cho {count} ArrowBlock!");

        // Xóa trạng thái booster đang active (nếu hệ thống có quản lý BoosterController)
        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }

    /// <summary>
    /// Hàm hỗ trợ tính toán hướng ngược lại 180 độ
    /// </summary>
    private ArrowDirection GetReversedDirection(ArrowDirection dir)
    {
        switch (dir)
        {
            case ArrowDirection.Right: return ArrowDirection.Left;
            case ArrowDirection.Left:  return ArrowDirection.Right;
            case ArrowDirection.Up:    return ArrowDirection.Down;
            case ArrowDirection.Down:  return ArrowDirection.Up;
            default: return ArrowDirection.Left;
        }
    }
}