using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[SelectionBase]
[RequireComponent(typeof(Collider2D))]
public class ExitGate : MonoBehaviour
{
    [Header("1. Cấu hình Cổng Thoát")]
    [Tooltip("Hướng thoát của cổng (khối block phải di chuyển theo hướng này để thoát)")]
    public ArrowDirection gateDirection = ArrowDirection.Up;

    [Tooltip("Cột bắt đầu của cổng trên bàn cờ (0-indexed)")]
    public int startColumn = 3;

    [Tooltip("Độ rộng cổng theo số ô cột (mặc định 2 ô cho cổng 74px)")]
    public int columnSpan = 2;

    [Tooltip("Tọa độ hàng của cổng (mặc định bằng số hàng của bàn cờ, ví dụ row 12 nằm ngay trên đỉnh bàn cờ 12x8)")]
    public int exitRow = 12;

    [Tooltip("Chỉ cho phép khối block có cùng hướng mũi tên với cổng mới được thoát")]
    public bool requireMatchingDirection = true;

    [Header("2. Hiệu ứng Thoát (Animation)")]
    [Tooltip("Tự động chạy hiệu ứng lướt qua cổng thoát khi block chạm đích")]
    public bool autoAnimateExit = true;

    [Tooltip("Tốc độ trượt ra ngoài khi thoát")]
    public float exitSpeed = 16f;

    [Tooltip("Xóa GameObject của block sau khi thoát xong")]
    public bool destroyBlockOnExit = true;

    [Header("3. Sự kiện (Unity Events)")]
    public UnityEvent<MonoBehaviour> onBlockExit; // Hỗ trợ linh hoạt cả ArrowBlock và TwoWayBlock

    [Header("4. Đếm số Block vào cổng")]
    [Tooltip("Số lượng block đã đi qua cổng này")]
    public int exitedBlockCount = 0;

    // Tổng số block đã đi qua tất cả các cổng trong màn chơi
    public static int TotalBlocksExitedAcrossAllGates = 0;

    // Danh sách các cổng thoát đang hoạt động trong Scene
    public static List<ExitGate> activeGates = new List<ExitGate>();

    private Collider2D gateCollider;

    /// <summary>
    /// Ghi nhận khi có một block đã vào cổng thành công
    /// </summary>
    public void NotifyBlockExited(MonoBehaviour block)
    {
        exitedBlockCount++;
        TotalBlocksExitedAcrossAllGates++;

        Debug.Log($"<color=green>[ExitGate]</color> Khối block '{(block != null ? block.name : "null")}' đã vào cổng! (Cổng này: {exitedBlockCount}, Tổng cộng: {TotalBlocksExitedAcrossAllGates})");

        // Kích hoạt UnityEvent
        onBlockExit?.Invoke(block);

        // Thông báo giảm số đếm trên tất cả các khối băng đang có trong màn chơi
        FreezableBlock.OnBlockEnteredGate();
    }

    private void Awake()
    {
        gateCollider = GetComponent<Collider2D>();
        if (gateCollider != null)
        {
            gateCollider.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        if (!activeGates.Contains(this))
        {
            activeGates.Add(this);
        }
    }

    private void OnDisable()
    {
        activeGates.Remove(this);
    }

    /// <summary>
    /// Kiểm tra xem tại vị trí ô (gridX, gridY) với hướng di chuyển dir có cổng thoát hợp lệ hay không
    /// </summary>
    public static bool CanExit(int gridX, int gridY, ArrowDirection dir)
    {
        return GetGateAt(gridX, gridY, dir) != null;
    }

    /// <summary>
    /// Tìm cổng thoát tại vị trí ô (gridX, gridY)
    /// </summary>
    public static ExitGate GetGateAt(int gridX, int gridY, ArrowDirection dir)
    {
        foreach (var gate in activeGates)
        {
            if (gate != null && gate.IsCellInGate(gridX, gridY, dir))
            {
                return gate;
            }
        }
        return null;
    }

    /// <summary>
    /// Tìm cổng thoát gần một vị trí World Position
    /// </summary>
    public static ExitGate GetGateNearPosition(Vector3 pos, float maxDist = 1.5f)
    {
        foreach (var gate in activeGates)
        {
            if (gate != null && Vector2.Distance(pos, gate.transform.position) <= maxDist)
            {
                return gate;
            }
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra xem ô (gridX, gridY) có nằm trong phạm vi đón block của cổng này không
    /// </summary>
    public bool IsCellInGate(int gridX, int gridY, ArrowDirection dir)
    {
        if (requireMatchingDirection && dir != gateDirection)
        {
            return false;
        }

        // Cổng hướng LÊN (Up) đón block ở các cột [startColumn, startColumn + columnSpan - 1] tại mép trên (gridY >= exitRow)
        if (gateDirection == ArrowDirection.Up)
        {
            bool colMatch = gridX >= startColumn && gridX < (startColumn + columnSpan);
            bool rowMatch = (gridY >= exitRow || gridY >= (exitRow - 1));
            return colMatch && rowMatch;
        }
        // Cổng hướng XUỐNG (Down)
        else if (gateDirection == ArrowDirection.Down)
        {
            bool colMatch = gridX >= startColumn && gridX < (startColumn + columnSpan);
            bool rowMatch = (gridY <= exitRow);
            return colMatch && rowMatch;
        }
        // Cổng hướng PHẢI (Right)
        else if (gateDirection == ArrowDirection.Right)
        {
            bool rowMatch = gridY >= startColumn && gridY < (startColumn + columnSpan);
            bool colMatch = (gridX >= exitRow);
            return rowMatch && colMatch;
        }
        // Cổng hướng TRÁI (Left)
        else if (gateDirection == ArrowDirection.Left)
        {
            bool rowMatch = gridY >= startColumn && gridY < (startColumn + columnSpan);
            bool colMatch = (gridX <= exitRow);
            return rowMatch && colMatch;
        }

        return false;
    }

    /// <summary>
    /// Xử lý khi khối block ArrowBlock vào cổng thoát
    /// </summary>
    public void HandleBlockExit(ArrowBlock block)
    {
        if (block == null) return;
        StartCoroutine(PlayExitAnimationRoutine(block));
    }

    /// <summary>
    /// Xử lý khi khối block TwoWayBlock vào cổng thoát
    /// </summary>
    public void HandleBlockExit(TwoWayBlock block)
    {
        if (block == null) return;
        StartCoroutine(PlayExitAnimationRoutine(block));
    }

    /// <summary>
    /// Hiệu ứng trượt qua cổng dành cho ArrowBlock
    /// </summary>
    public IEnumerator PlayExitAnimationRoutine(ArrowBlock block)
    {
        if (block == null) yield break;

        Collider2D col = block.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector3 startPos = block.transform.position;
        Vector3 exitDirection = (Vector3)block.GetDirectionVector();
        float stepSize = block.GetStepSize();
        Vector3 targetExitPos = startPos + exitDirection * (stepSize * 1.5f);

        Vector3 originalScale = block.transform.localScale;
        SpriteRenderer sr = block.GetComponent<SpriteRenderer>();
        Color startColor = (sr != null) ? sr.color : Color.white;

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            block.transform.position = Vector3.Lerp(startPos, targetExitPos, t);
            block.transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.2f, t);

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        NotifyBlockExited(block);

        if (destroyBlockOnExit)
        {
            Destroy(block.gameObject);
        }
    }

    /// <summary>
    /// Hiệu ứng trượt qua cổng dành cho TwoWayBlock
    /// </summary>
    public IEnumerator PlayExitAnimationRoutine(TwoWayBlock block)
    {
        if (block == null) yield break;

        Collider2D col = block.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector3 startPos = block.transform.position;
        Vector3 exitDirection = (Vector3)block.GetDirectionVector();
        float stepSize = block.GetStepSize();
        Vector3 targetExitPos = startPos + exitDirection * (stepSize * 1.5f);

        Vector3 originalScale = block.transform.localScale;
        SpriteRenderer sr = block.GetComponent<SpriteRenderer>();
        Color startColor = (sr != null) ? sr.color : Color.white;

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            block.transform.position = Vector3.Lerp(startPos, targetExitPos, t);
            block.transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.2f, t);

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        NotifyBlockExited(block);

        if (destroyBlockOnExit)
        {
            Destroy(block.gameObject);
        }
    }

    /// <summary>
    /// Hiệu ứng trượt qua cổng dành cho RetreatArrowBlock
    /// </summary>
    public IEnumerator PlayExitAnimationRoutine(RetreatArrowBlock block)
    {
        if (block == null) yield break;

        Collider2D col = block.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Vector3 startPos = block.transform.position;
        Vector3 exitDirection = (Vector3)block.GetDirectionVector();
        float stepSize = block.GetStepSize();
        Vector3 targetExitPos = startPos + exitDirection * (stepSize * 1.5f);

        Vector3 originalScale = block.transform.localScale;
        SpriteRenderer sr = block.GetComponent<SpriteRenderer>();
        Color startColor = (sr != null) ? sr.color : Color.white;

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            block.transform.position = Vector3.Lerp(startPos, targetExitPos, t);
            block.transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.2f, t);

            if (sr != null)
            {
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
            }

            yield return null;
        }

        NotifyBlockExited(block);

        if (destroyBlockOnExit)
        {
            Destroy(block.gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ArrowBlock arrowBlock = other.GetComponent<ArrowBlock>();
        if (arrowBlock != null && !arrowBlock.IsMoving)
        {
            HandleBlockExit(arrowBlock);
            return;
        }

        TwoWayBlock twoWayBlock = other.GetComponent<TwoWayBlock>();
        if (twoWayBlock != null && !twoWayBlock.IsMoving)
        {
            HandleBlockExit(twoWayBlock);
        }

        RetreatArrowBlock retreatBlock = other.GetComponent<RetreatArrowBlock>();
        if (retreatBlock != null && !retreatBlock.IsMoving)
        {
            StartCoroutine(PlayExitAnimationRoutine(retreatBlock));
        }
    }
}