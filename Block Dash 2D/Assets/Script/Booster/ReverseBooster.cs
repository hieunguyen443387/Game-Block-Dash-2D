using System.Collections;
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
            Debug.Log("<color=yellow>[ReverseBooster]</color> Đã kích hoạt Reverse Booster! Hãy chọn block cần đổi hướng.");
        }
    }

    /// <summary>
    /// Logic thực thi khi người chơi bấm vào một block cụ thể
    /// </summary>
    public void Execute(BlockItem targetBlock)
    {
        if (targetBlock == null) return;

        // 1. Kiểm tra nếu block đang bị đóng băng: KHÔNG CHO PHÉP ĐẢO HƯỚNG
        FreezableBlock freezable = targetBlock.GetComponent<FreezableBlock>();
        if (freezable == null && targetBlock is ArrowBlock arrow)
        {
            freezable = arrow.freezableBlock;
        }

        if (freezable != null && freezable.isFrozen)
        {
            Debug.LogWarning($"<color=yellow>[ReverseBooster]</color> Khối '{targetBlock.name}' đang bị đóng băng! Không thể đảo hướng. Booster vẫn được giữ, hãy chọn block khác.");
            StartCoroutine(ShakeFeedback(targetBlock.transform));
            // QUAN TRỌNG: KHÔNG gọi ClearActiveBooster() để không lãng phí booster của người chơi!
            return;
        }

        // 2. Loại trừ FreeMoveBlock (không có mũi tên cố định để đảo)
        if (targetBlock is FreeMoveBlock)
        {
            Debug.LogWarning($"<color=yellow>[ReverseBooster]</color> Không thể đảo hướng FreeMoveBlock! Hãy chọn block khác.");
            StartCoroutine(ShakeFeedback(targetBlock.transform));
            return; // Dừng lại, không tắt booster
        }

        bool success = false;

        // 3. Xử lý cho ArrowBlock (đảo ngược 180 độ: +2 mod 4)
        if (targetBlock is ArrowBlock arrowBlock)
        {
            ArrowDirection newDir = (ArrowDirection)(((int)arrowBlock.direction + 2) % 4);
            arrowBlock.SetDirection(newDir);
            Debug.Log($"<color=cyan>[ReverseBooster]</color> Đã đảo hướng ArrowBlock '{arrowBlock.name}' thành {newDir}");
            success = true;
        }
        // 4. Xử lý cho RetreatArrowBlock (mũi tên thối lui)
        else if (targetBlock is RetreatArrowBlock retreatBlock)
        {
            ArrowDirection newDir = (ArrowDirection)(((int)retreatBlock.direction + 2) % 4);
            retreatBlock.SetDirection(newDir);
            Debug.Log($"<color=cyan>[ReverseBooster]</color> Đã đảo hướng RetreatArrowBlock '{retreatBlock.name}' thành {newDir}");
            success = true;
        }
        // 5. Xử lý cho TwoWayBlock
        else if (targetBlock is TwoWayBlock twoWayBlock)
        {
            twoWayBlock.ToggleTwoWayDirection();
            Debug.Log($"<color=cyan>[ReverseBooster]</color> Đã đảo hướng TwoWayBlock '{twoWayBlock.name}'");
            success = true;
        }

        // 6. Nếu thao tác thành công trên block hợp lệ, tắt chế độ booster
        if (success && BoosterController.Instance != null)
        {
            BoosterController.Instance.ClearActiveBooster();
        }
    }

    /// <summary>
    /// Rung lắc nhẹ khi người chơi click vào block bị khóa (băng hoặc không hợp lệ)
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
