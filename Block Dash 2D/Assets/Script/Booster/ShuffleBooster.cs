using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShuffleBooster : MonoBehaviour
{
    [Header("Cấu hình Shuffle Toàn Bàn Cờ (Tùy chọn)")]
    [Tooltip("Tỷ lệ số lượng block sẽ bị xáo trộn nếu dùng chế độ xáo trộn toàn bàn cờ")]
    [Range(0.1f, 1f)] public float shuffleRatio = 1.0f;

    /// <summary>
    /// Gắn hàm này vào sự kiện OnClick() của nút UI Booster Shuffle
    /// </summary>
    public void OnClickActivateButton()
    {
        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.SetActiveBooster(this);
            Debug.Log("<color=yellow>[ShuffleBooster]</color> Đã kích hoạt Shuffle Booster! Hãy click vào block cần xáo trộn hướng.");
        }
    }

    /// <summary>
    /// Logic thực thi khi người chơi bấm vào một block cụ thể trên bàn cờ
    /// </summary>
    public void Execute(BlockItem targetBlock)
    {
        if (targetBlock == null) return;

        // 1. Kiểm tra nếu block đang bị đóng băng: KHÔNG CHO PHÉP SHUFFLE
        FreezableBlock freezable = targetBlock.GetComponent<FreezableBlock>();
        if (freezable == null && targetBlock is ArrowBlock arrow)
        {
            freezable = arrow.freezableBlock;
        }

        if (freezable != null && freezable.isFrozen)
        {
            Debug.LogWarning($"<color=yellow>[ShuffleBooster]</color> Khối '{targetBlock.name}' đang bị đóng băng! Không thể Shuffle. Booster vẫn được giữ, hãy chọn block khác.");
            StartCoroutine(ShakeFeedback(targetBlock.transform));
            // QUAN TRỌNG: KHÔNG gọi ClearActiveBooster() để không lãng phí booster của người chơi!
            return;
        }

        // 2. Loại trừ FreeMoveBlock
        if (targetBlock is FreeMoveBlock)
        {
            Debug.LogWarning($"<color=yellow>[ShuffleBooster]</color> Không thể Shuffle FreeMoveBlock! Hãy chọn block khác.");
            StartCoroutine(ShakeFeedback(targetBlock.transform));
            return; // Dừng lại, không tắt booster
        }

        bool success = false;

        // 3. Xử lý cho ArrowBlock: chọn ngẫu nhiên 1 trong 3 hướng còn lại (đảm bảo luôn đổi hướng khác)
        if (targetBlock is ArrowBlock arrowBlock)
        {
            int currentDir = (int)arrowBlock.direction;
            int offset = Random.Range(1, 4); // Nhận 1, 2 hoặc 3
            ArrowDirection newDir = (ArrowDirection)((currentDir + offset) % 4);
            arrowBlock.SetDirection(newDir);
            Debug.Log($"<color=cyan>[ShuffleBooster]</color> Đã Shuffle ArrowBlock '{arrowBlock.name}' từ {(ArrowDirection)currentDir} sang hướng mới: {newDir}");
            success = true;
        }
        // 4. Xử lý cho RetreatArrowBlock (mũi tên thối lui)
        else if (targetBlock is RetreatArrowBlock retreatBlock)
        {
            int currentDir = (int)retreatBlock.direction;
            int offset = Random.Range(1, 4);
            ArrowDirection newDir = (ArrowDirection)((currentDir + offset) % 4);
            retreatBlock.SetDirection(newDir);
            Debug.Log($"<color=cyan>[ShuffleBooster]</color> Đã Shuffle RetreatArrowBlock '{retreatBlock.name}' từ {(ArrowDirection)currentDir} sang hướng mới: {newDir}");
            success = true;
        }
        // 5. Xử lý cho TwoWayBlock
        else if (targetBlock is TwoWayBlock twoWayBlock)
        {
            twoWayBlock.ToggleTwoWayDirection();
            Debug.Log($"<color=cyan>[ShuffleBooster]</color> Đã Shuffle TwoWayBlock '{twoWayBlock.name}'");
            success = true;
        }

        // 6. Nếu thao tác thành công trên block hợp lệ, tắt chế độ booster
        if (success && BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }

    /// <summary>
    /// Xáo trộn ngẫu nhiên tất cả các block trên bàn cờ (bỏ qua các block đang bị đóng băng)
    /// </summary>
    public void ShuffleAllNonFrozenBlocks()
    {
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

            // Bỏ qua block đang đóng băng
            FreezableBlock fz = arrowBlock.freezableBlock != null ? arrowBlock.freezableBlock : arrowBlock.GetComponent<FreezableBlock>();
            if (fz != null && fz.isFrozen) continue;

            if (Random.value > shuffleRatio) continue;

            int currentDir = (int)arrowBlock.direction;
            int offset = Random.Range(1, 4);
            ArrowDirection newDir = (ArrowDirection)((currentDir + offset) % 4);
            arrowBlock.SetDirection(newDir);
            count++;
        }

        Debug.Log($"<color=cyan>[ShuffleBooster]</color> Đã Shuffle ngẫu nhiên {count} ArrowBlock (đã bỏ qua các khối băng).");

        if (BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }

    /// <summary>
    /// Rung lắc nhẹ khi người chơi click vào block bị khóa
    /// </summary>
    private IEnumerator ShakeFeedback(Transform target)
    {
        if (target == null) yield break;

        Vector3 originalPos = target.position;
        float duration = 0.15f;
        float elapsed = 0f;
        float magnitude = 0.08f;

        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float xOffset = Mathf.Sin(elapsed * 50f) * magnitude * (1f - (elapsed / duration));
            target.position = originalPos + new Vector3(xOffset, 0f, 0f);
            yield return null;
        }

        if (target != null)
        {
            target.position = originalPos;
        }
    }
}
