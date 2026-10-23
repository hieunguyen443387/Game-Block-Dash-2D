using UnityEngine;

public class ReverseBooster : MonoBehaviour
{
    /// <summary>
    /// Gắn hàm này vào sự kiện OnClick() của nút UI Booster Reverse
    /// </summary>
    public void OnClickActivateButton()
    {
        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.SetActiveBooster(this);
            Debug.Log("Đã kích hoạt Reverse Booster! Hãy chọn block cần đổi hướng.");
        }
    }

    /// <summary>
    /// Logic thực thi khi người chơi bấm vào một block cụ thể
    /// </summary>
    public void Execute(BlockItem targetBlock)
    {
        if (targetBlock == null) return;

        // 1. Loại trừ FreeMoveBlock (không cho phép chọn)
        if (targetBlock is FreeMoveBlock)
        {
            Debug.LogWarning("Không thể đảo hướng FreeMoveBlock!");
            return; // Dừng lại, không tắt booster để người chơi có thể chọn block khác hợp lệ
        }

        bool success = false;

        // 2. Xử lý cho ArrowBlock (đảo ngược 180 độ: cộng 2 vào enum và mod 4)
        if (targetBlock is ArrowBlock arrowBlock)
        {
            ArrowDirection newDir = (ArrowDirection)(((int)arrowBlock.direction + 2) % 4);
            arrowBlock.SetDirection(newDir);
            Debug.Log($"Đã đảo hướng ArrowBlock {arrowBlock.gameObject.name} thành {newDir}");
            success = true;
        }
        // 3. Xử lý cho TwoWayBlock (gọi sẵn hàm đổi chiều có sẵn trong TwoWayBlock)
        else if (targetBlock is TwoWayBlock twoWayBlock)
        {
            twoWayBlock.ToggleTwoWayDirection();
            Debug.Log($"Đã đảo hướng TwoWayBlock {twoWayBlock.gameObject.name}");
            success = true;
        }

        // Nếu thao tác thành công trên block hợp lệ, tắt chế độ booster
        if (success && BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }
}