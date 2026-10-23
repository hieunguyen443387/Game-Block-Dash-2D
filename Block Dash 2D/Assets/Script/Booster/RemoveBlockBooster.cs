using UnityEngine;

public class RemoveBlockBooster : MonoBehaviour
{
    [Header("Settings")]
    public BlockSpawner blockSpawner; // Hoặc có thể tự tìm qua BoosterController.Instance.blockSpawner

    /// <summary>
    /// Gắn hàm này vào sự kiện OnClick() của nút UI Booster Xóa Block
    /// </summary>
    public void OnClickActivateButton()
    {
        // Thông báo cho BoosterController rằng booster này đang được kích hoạt
        BoosterController.Instance.SetActiveBooster(this);
        
        // Bạn có thể bật Panel hướng dẫn "Hãy chọn block muốn xóa" ở đây
        Debug.Log("Đã bật chế độ Remove Block Booster!");
    }

    /// <summary>
    /// Logic thực thi khi người chơi bấm vào một block cụ thể
    /// </summary>
    public void Execute(BlockItem targetBlock)
    {
        if (targetBlock == null) return;

        // Lấy BlockSpawner từ Controller chung để xóa block khỏi danh sách quản lý
        if (BoosterController.Instance != null && BoosterController.Instance.blockSpawner != null)
        {
            BoosterController.Instance.blockSpawner.RemoveBlockFromList(targetBlock);
        }

        // Tạo hiệu ứng (Particle, Sound,...) nếu muốn ở đây trước khi hủy
        Debug.Log("RemoveBlockBooster đã xóa thành công block: " + targetBlock.gameObject.name);

        // Hủy block
        Destroy(targetBlock.gameObject);

        // Tắt trạng thái booster sau khi dùng xong 1 lần
        BoosterController.Instance.ClearActiveBooster();
        
        // Tắt Panel hướng dẫn (nếu có)
    }
}