using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Block tự tìm đường (BFS) đến biên bàn cờ gần nhất rồi trượt ra Cổng thoát.
/// Kế thừa MovableBlockBase để dùng chung thông số board, hiệu ứng chuyển động và xử lý click.
/// </summary>
public class FreeMoveBlock : MovableBlockBase
{
    [Header("1. Movement")]
    public float moveSpeed = 14f;

    [Header("2. Debug")]
    public bool showDebugLogs = true;

    // =========================================================
    // PRIVATE STATE
    // =========================================================

    private bool isMoving;
    private bool hasExited;

    // Hướng BFS
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    protected override void Awake()
    {
        base.Awake(); // khởi tạo myCollider

        if (string.IsNullOrEmpty(blockType))
        {
            blockType = "FreeMoveBlock";
        }
    }

    protected override void Start()
    {
        base.Start(); // gọi DetectBoardSettings nếu autoDetectBoard

        Log(
            $"Khởi tạo thành công. Board: {boardColumns}x{boardRows}, " +
            $"StepSize: {GetStepSize():F3}, Center: {boardCenter}"
        );
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[FreeMoveBlock] {gameObject.name}: {message}", this);
        }
    }

    // =========================================================
    // CLICK - Triển khai abstract từ MovableBlockBase
    // =========================================================

    protected override void OnBlockClicked()
    {
        Log("Đã nhận click.");

        if (isMoving || hasExited)
        {
            Log("Block đang di chuyển hoặc đã thoát.");
            return;
        }

        MoveToExit();
    }

    // =========================================================
    // GRID UTILITIES (riêng cho FreeMoveBlock - BFS style)
    // =========================================================

    private Vector2 GetFirstCellCenter()
    {
        float step = GetStepSize();
        float halfWidth  = boardColumns * step / 2f;
        float halfHeight = boardRows    * step / 2f;

        return new Vector2(
            boardCenter.x - halfWidth  + step / 2f,
            boardCenter.y - halfHeight + step / 2f
        );
    }

    private Vector2 GridToWorld(Vector2Int cell)
    {
        Vector2 firstCell = GetFirstCellCenter();
        float step = GetStepSize();
        return new Vector2(
            firstCell.x + cell.x * step,
            firstCell.y + cell.y * step
        );
    }

    private Vector2Int WorldToGrid(Vector2 worldPosition)
    {
        Vector2 firstCell = GetFirstCellCenter();
        float step = GetStepSize();

        return new Vector2Int(
            Mathf.RoundToInt((worldPosition.x - firstCell.x) / step),
            Mathf.RoundToInt((worldPosition.y - firstCell.y) / step)
        );
    }

    private bool IsInsideBoard(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < boardColumns &&
               cell.y >= 0 && cell.y < boardRows;
    }

    private bool IsEdgeCell(Vector2Int cell)
    {
        return cell.x == 0 || cell.x == boardColumns - 1 ||
               cell.y == 0 || cell.y == boardRows - 1;
    }

    // =========================================================
    // OBSTACLE CHECK
    // =========================================================

    private bool IsBlocked(Vector2Int cell)
    {
        if (!IsInsideBoard(cell)) return true;

        float step = GetStepSize();
        Vector2 worldPosition = GridToWorld(cell);
        
        // Thu nhỏ vùng quét một chút (0.6 thay vì 0.75) để tránh quét phạm sang ô bên cạnh của chính nó
        Vector2 boxSize = Vector2.one * step * 0.6f;

        // Lấy tất cả các collider tại ô này thuộc obstacleLayers
        Collider2D[] hits = Physics2D.OverlapBoxAll(worldPosition, boxSize, 0f, obstacleLayers);

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (ignoreTriggers && hit.isTrigger) continue;

            // Bỏ qua chính bản thân block này (so sánh cả collider cá nhân và toàn bộ cụm transform gốc)
            if (hit == myCollider || hit.transform == transform || hit.transform.root == transform.root) continue;

            // Nếu chạm phải bất kỳ Collider hợp lệ nào khác -> Ô này bị chặn
            return	true;
        }

        return false;
    }

    // =========================================================
    // PATHFINDING: BFS TÌM ĐƯỜNG TỚI BIÊN
    // =========================================================

    private List<Vector2Int> FindPathToEdge(Vector2Int start)
    {
        if (IsBlocked(start)) return null;

        Queue<Vector2Int>                  queue    = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> previous = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int>                visited  = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int target = start;
        bool found = false;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            if (IsEdgeCell(current))
            {
                target = current;
                found  = true;
                break;
            }

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;

                if (!IsInsideBoard(next) || visited.Contains(next)) continue;
                if (IsBlocked(next)) continue;

                visited.Add(next);
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!found)
        {
            Log("Không tìm được đường trống tới biên bàn cờ.");
            return null;
        }

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int step = target;
        path.Add(step);

        while (step != start)
        {
            if (!previous.ContainsKey(step)) return null;
            step = previous[step];
            path.Add(step);
        }

        path.Reverse();
        return path;
    }

    // =========================================================
    // MAIN MOVEMENT ENTRY POINT
    // =========================================================

    public void MoveToExit()
    {
        if (isMoving || hasExited) return;

        if (autoDetectBoard) DetectBoardSettings();

        float step = GetStepSize();
        if (boardRows <= 0 || boardColumns <= 0 || step <= 0f || myCollider == null) return;

#if UNITY_2023_1_OR_NEWER
        ExitGate exitGate = FindFirstObjectByType<ExitGate>();
#else
        ExitGate exitGate = FindObjectOfType<ExitGate>();
#endif
        if (exitGate == null) return;

        Vector2Int startCell = WorldToGrid(transform.position);
        if (!IsInsideBoard(startCell)) return;

        List<Vector2Int> path = FindPathToEdge(startCell);

        if (path == null || path.Count == 0)
        {
            // Bị kẹt hoàn toàn → Rung nhẹ báo hiệu
            StartCoroutine(ShakeRoutine(Vector2.up, step * 0.15f));
            return;
        }

        StartCoroutine(MoveRoutine(path, exitGate));
    }

    // =========================================================
    // MOVE ROUTINE
    // =========================================================

    private IEnumerator MoveRoutine(List<Vector2Int> path, ExitGate exitGate)
    {
        isMoving = true;
        float step = GetStepSize();

        // Đi theo path trên bàn cờ
        foreach (Vector2Int cell in path)
        {
            Vector2 target = GridToWorld(cell);
            yield return StartCoroutine(SmoothMoveTo(target, moveSpeed));
            gridX = cell.x;
            gridY = cell.y;
        }

        // Tính các làn Sideline
        Vector2Int edgeCell = path[path.Count - 1];

        float halfWidth  = boardColumns * step / 2f;
        float halfHeight = boardRows    * step / 2f;
        float laneDistance = Mathf.Max(1, borderThickness) * step / 2f;

        float laneRight  = boardCenter.x + halfWidth  + laneDistance;
        float laneLeft   = boardCenter.x - halfWidth  - laneDistance;
        float laneTop    = boardCenter.y + halfHeight + laneDistance;
        float laneBottom = boardCenter.y - halfHeight - laneDistance;

        Vector2 edgeWorld = GridToWorld(edgeCell);

        // Tắt collider và nâng sorting order
        if (myCollider != null) myCollider.enabled = false;
        EnsureTopSortingOrder();

        // Xác định điểm đầu Sideline tùy thuộc vào cạnh nào đang đứng
        Vector2 laneStart;
        if      (edgeCell.x == 0)                laneStart = new Vector2(laneLeft,  edgeWorld.y);
        else if (edgeCell.x == boardColumns - 1)  laneStart = new Vector2(laneRight, edgeWorld.y);
        else if (edgeCell.y == boardRows - 1)     laneStart = new Vector2(edgeWorld.x, laneTop);
        else                                       laneStart = new Vector2(edgeWorld.x, laneBottom);

        yield return StartCoroutine(SmoothMoveTo(laneStart, moveSpeed));

        Vector3 gatePosition = exitGate.transform.position;

        // Di chuyển dọc viền tới góc trên
        if (edgeCell.x == 0)
        {
            yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed));
        }
        else if (edgeCell.x == boardColumns - 1)
        {
            yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed));
        }
        else if (edgeCell.y == 0)
        {
            bool useLeft = Mathf.Abs(laneStart.x - laneLeft) < Mathf.Abs(laneStart.x - laneRight);
            float sideX = useLeft ? laneLeft : laneRight;
            yield return StartCoroutine(SmoothMoveTo(new Vector2(sideX, laneBottom), sidelineMoveSpeed));
            yield return StartCoroutine(SmoothMoveTo(new Vector2(sideX, laneTop),    sidelineMoveSpeed));
        }

        // Căn ngang với gate rồi đâm thẳng vào
        yield return StartCoroutine(SmoothMoveTo(new Vector2(gatePosition.x, laneTop), sidelineMoveSpeed));
        yield return StartCoroutine(SmoothMoveTo(gatePosition, sidelineMoveSpeed));

        hasExited = true;
        isMoving  = false;

        gameObject.SetActive(false);
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        float step = GetStepSize();
        if (step <= 0f) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one * step * 0.8f);
    }
}
