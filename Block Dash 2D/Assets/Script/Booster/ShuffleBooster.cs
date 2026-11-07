using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShuffleBooster : MonoBehaviour
{
    [Header("Cấu hình Đảo Ngược Hướng Toàn Bàn Cờ")]
    [Tooltip("Tỷ lệ số lượng block sẽ bị đổi hướng (1.0 = 100% tất cả block hợp lệ)")]
    [Range(0.1f, 1f)] public float shuffleRatio = 1.0f;

    /// <summary>
    /// Gắn hàm này vào sự kiện OnClick() của nút UI Booster Shuffle trong Unity.
    /// Khi bấm vào, toàn bộ block trên bàn cờ sẽ đồng loạt đổi sang hướng ngược lại.
    /// </summary>
    public void OnClickActivateButton()
    {
        ReverseAllNonFrozenBlocks();
    }

    /// <summary>
    /// Hàm tương thích để tránh lỗi nếu BoosterController gọi nhầm Execute
    /// </summary>
    public void Execute(BlockItem targetBlock)
    {
        ReverseAllNonFrozenBlocks();
    }

    /// <summary>
    /// Đảo ngược hướng tất cả các block trên bàn cờ (bỏ qua các block đang bị đóng băng)
    /// </summary>
    public void ReverseAllNonFrozenBlocks()
    {
        int count = 0;

        // 1. Xử lý cho ArrowBlock
        ArrowBlock[] allArrowBlocks = FindObjectsByType<ArrowBlock>(FindObjectsSortMode.None);
        if (allArrowBlocks != null)
        {
            foreach (var arrowBlock in allArrowBlocks)
            {
                if (arrowBlock == null) continue;

                // Bỏ qua block đang đóng băng
                FreezableBlock fz = arrowBlock.freezableBlock != null ? arrowBlock.freezableBlock : arrowBlock.GetComponent<FreezableBlock>();
                if (fz != null && fz.isFrozen) continue;

                if (Random.value > shuffleRatio) continue;

                // Đổi hướng ngược lại: (currentDir + 2) % 4
                int currentDir = (int)arrowBlock.direction;
                int newDirInt = (currentDir + 2) % 4;
                ArrowDirection newDir = (ArrowDirection)newDirInt;
                
                arrowBlock.SetDirection(newDir);
                count++;
            }
        }

        // 2. Xử lý cho RetreatArrowBlock (nếu có trên bàn cờ)
        RetreatArrowBlock[] allRetreatBlocks = FindObjectsByType<RetreatArrowBlock>(FindObjectsSortMode.None);
        if (allRetreatBlocks != null)
        {
            foreach (var retreatBlock in allRetreatBlocks)
            {
                if (retreatBlock == null) continue;

                FreezableBlock fz = retreatBlock.GetComponent<FreezableBlock>();
                if (fz != null && fz.isFrozen) continue;

                if (Random.value > shuffleRatio) continue;

                int currentDir = (int)retreatBlock.direction;
                int newDirInt = (currentDir + 2) % 4;
                ArrowDirection newDir = (ArrowDirection)newDirInt;

                retreatBlock.SetDirection(newDir);
                count++;
            }
        }

        // 3. Xử lý cho TwoWayBlock (nếu có)
        TwoWayBlock[] allTwoWayBlocks = FindObjectsByType<TwoWayBlock>(FindObjectsSortMode.None);
        if (allTwoWayBlocks != null)
        {
            foreach (var twoWayBlock in allTwoWayBlocks)
            {
                if (twoWayBlock == null) continue;

                if (Random.value > shuffleRatio) continue;

                twoWayBlock.ToggleTwoWayDirection();
                count++;
            }
        }

        Debug.Log($"<color=cyan>[ShuffleBooster]</color> Đã đảo ngược hướng thành công cho {count} block trên bàn cờ!");

        // Báo cho BoosterController để kết thúc trạng thái active
        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }
}