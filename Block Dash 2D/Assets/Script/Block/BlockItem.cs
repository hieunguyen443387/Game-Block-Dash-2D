using UnityEngine;

public class BlockItem : MonoBehaviour
{
    [Tooltip("Tên định danh loại block...")]
    public string blockType = "";

    [HideInInspector] public int gridX;
    [HideInInspector] public int gridY;

    public string GetBlockType()
    {
        if (!string.IsNullOrEmpty(blockType))
        {
            return blockType.Trim();
        }
        return gameObject.name.Replace("(Clone)", "").Trim();
    }

    private void OnMouseDown()
    {
        // Kiểm tra xem hệ thống có đang bật booster nào yêu cầu tương tác với block không
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
        }
    }
}