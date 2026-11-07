using UnityEngine;

public class RemoveBlockBooster : MonoBehaviour
{
    [Header("Settings (GameScene)")]
    [Tooltip("Container chứa các Block trong GameScene (nếu để trống script sẽ tự tìm từ BoosterController hoặc MapLoader)")]
    public Transform blockContainer;

    private void Awake()
    {
        EnsureBlockContainer();
    }

    private void EnsureBlockContainer()
    {
        if (blockContainer != null) return;

        if (BoosterController.Instance != null && BoosterController.Instance.blockContainer != null)
        {
            blockContainer = BoosterController.Instance.blockContainer;
            return;
        }

        GameObject containerObj = GameObject.Find("BlocksContainer");
        if (containerObj == null) containerObj = GameObject.Find("BlockContainer");
        if (containerObj != null)
        {
            blockContainer = containerObj.transform;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        MapLoader loader = FindFirstObjectByType<MapLoader>();
#else
        MapLoader loader = FindObjectOfType<MapLoader>();
#endif
        if (loader != null && loader.blocksContainer != null)
        {
            blockContainer = loader.blocksContainer;
        }
    }

    /// <summary>
    /// Gắn hàm này vào sự kiện OnClick() của nút UI Booster Xóa Block
    /// </summary>
    public void OnClickActivateButton()
    {
        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.SetActiveBooster(this);
            Debug.Log("<color=yellow>[RemoveBlockBooster]</color> Đã kích hoạt Remove Block Booster! Hãy click vào block muốn xóa.");
        }
    }

    /// <summary>
    /// Logic thực thi khi người chơi bấm vào một block cụ thể
    /// </summary>
    public void Execute(BlockItem targetBlock)
    {
        if (targetBlock == null) return;

        // Kiểm tra xem block có đang bị đóng băng hay không
        FreezableBlock freezable = targetBlock.GetComponent<FreezableBlock>();
        if (freezable == null && targetBlock is ArrowBlock arrow)
        {
            freezable = arrow.freezableBlock;
        }

        // Nếu là khối băng đang đóng băng: CHỈ TRỪ ĐI 1 SỐ chứ KHÔNG bị remove hẳn!
        if (freezable != null && freezable.isFrozen)
        {
            Debug.Log($"<color=cyan>[RemoveBlockBooster]</color> Click chọn khối băng '{targetBlock.name}'! Trừ 1 số trên khối băng ({freezable.freezeCount} -> {freezable.freezeCount - 1}) thay vì xóa block.");
            freezable.DecreaseFreezeCount(1);

            // Tắt trạng thái booster sau khi dùng xong 1 lần
            if (BoosterController.Instance != null)
            {
                BoosterController.Instance.ClearActiveBooster();
            }
            return;
        }

        // Hủy block bình thường
        Debug.Log($"<color=cyan>[RemoveBlockBooster]</color> Đã xóa thành công block: {targetBlock.gameObject.name}");
        Destroy(targetBlock.gameObject);

        // Tắt trạng thái booster sau khi dùng xong 1 lần
        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }
}
