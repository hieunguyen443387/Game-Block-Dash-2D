using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class FreeMoveBlock : BlockItem, IPointerDownHandler
{
    [Header("1. Movement")]
    public float moveSpeed = 14f;
    public float sidelineMoveSpeed = 16f;

    [Header("2. Board Settings")]
    public bool autoDetectBoard = true;

    public float cellSizePixels = 36f;
    public float spacingPixels = 0f;
    public float pixelsPerUnit = 100f;

    public int boardColumns = 12;
    public int boardRows = 12;

    public Vector2 boardCenter = Vector2.zero;

    [Header("3. Obstacle Detection")]
    public LayerMask obstacleLayers = ~0;
    public bool ignoreTriggers = true;

    [Header("4. Sideline")]
    public int borderThickness = 2;

    public float exitDistance = 0.38f;
    public float cornerTurnDuration = 0.08f;

    [Header("5. Debug")]
    public bool showDebugLogs = true;

    private Collider2D myCollider;
    private bool isMoving;
    private bool hasExited;
    private int lastClickFrame = -1;

    private float StepSize =>
        (cellSizePixels + spacingPixels) /
        Mathf.Max(1f, pixelsPerUnit);

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();

        if (string.IsNullOrEmpty(blockType))
        {
            blockType = "FreeMoveBlock";
        }
    }

    private void Start()
    {
        if (autoDetectBoard)
        {
            DetectBoardSettings();
        }

        Log(
            $"Khởi tạo thành công. Board: {boardColumns}x{boardRows}, " +
            $"StepSize: {StepSize:F3}, Center: {boardCenter}"
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
    // CLICK
    // =========================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }
        TryClick();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClick(Mouse.current.position.ReadValue());
        }
        else if (Touchscreen.current != null &&
                 Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            CheckClick(
                Touchscreen.current.primaryTouch.position.ReadValue()
            );
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            CheckClick(Input.mousePosition);
        }
#endif
    }

    private void OnMouseDown()
    {
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }
        TryClick();
    }

    private void CheckClick(Vector2 screenPosition)
    {
        if (Time.frameCount == lastClickFrame) return;

        Camera cam = Camera.main;
        if (cam == null || myCollider == null) return;

        Vector2 worldPosition = cam.ScreenToWorldPoint(screenPosition);

        if (myCollider.OverlapPoint(worldPosition))
        {
            if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
            {
                BoosterController.Instance.OnBlockClicked(this);
                return;
            }
            TryClick();
        }
    }

    private void TryClick()
    {
        if (Time.frameCount == lastClickFrame)
        {
            return;
        }

        lastClickFrame = Time.frameCount;

        Log("Đã nhận click.");

        if (isMoving || hasExited)
        {
            Log("Block đang di chuyển hoặc đã thoát.");
            return;
        }

        MoveToExit();
    }

    // =========================================================
    // BOARD SETTINGS
    // =========================================================

    public void DetectBoardSettings()
    {
        SideLine sideline = FindFirstObjectByType<SideLine>();

        if (sideline != null)
        {
            boardCenter = sideline.startPosition;
            boardRows = sideline.rows;
            boardColumns = sideline.columns;
            cellSizePixels = sideline.cellSizePixels;
            spacingPixels = sideline.spacingPixels;
            pixelsPerUnit = sideline.pixelsPerUnit;

            if (sideline.borderThickness > 0)
            {
                borderThickness = sideline.borderThickness;
            }

            Log("Đã lấy thông số từ SideLine.");
            return;
        }

        ExitGateGenerator gateGenerator =
            FindFirstObjectByType<ExitGateGenerator>();

        if (gateGenerator != null)
        {
            boardCenter = gateGenerator.startPosition;
            boardRows = gateGenerator.rows;
            boardColumns = gateGenerator.columns;
            cellSizePixels = gateGenerator.cellSizePixels;
            spacingPixels = gateGenerator.spacingPixels;
            pixelsPerUnit = gateGenerator.pixelsPerUnit;

            if (gateGenerator.borderThickness > 0)
            {
                borderThickness = gateGenerator.borderThickness;
            }

            Log("Đã lấy thông số từ ExitGateGenerator.");
            return;
        }

        BoardGenerator boardGenerator =
            FindFirstObjectByType<BoardGenerator>();

        if (boardGenerator != null)
        {
            boardCenter = boardGenerator.startPosition;
            boardRows = boardGenerator.rows;
            boardColumns = boardGenerator.columns;
            cellSizePixels = boardGenerator.cellSizePixels;
            spacingPixels = boardGenerator.spacingPixels;
            pixelsPerUnit = boardGenerator.pixelsPerUnit;

            Log("Đã lấy thông số từ BoardGenerator.");
            return;
        }

        MapSaver mapSaver = FindFirstObjectByType<MapSaver>();

        if (mapSaver != null)
        {
            boardCenter = mapSaver.startPosition;
            boardRows = mapSaver.rows;
            boardColumns = mapSaver.columns;
            cellSizePixels = mapSaver.cellSizePixels;
            spacingPixels = mapSaver.spacingPixels;
            pixelsPerUnit = mapSaver.pixelsPerUnit;

            Log("Đã lấy thông số từ MapSaver.");
            return;
        }

        Log("CẢNH BÁO: Không tìm thấy nguồn thông số board.");
    }

    public float GetStepSize()
    {
        return StepSize;
    }

    // =========================================================
    // GRID
    // =========================================================

    private Vector2 GetFirstCellCenter()
    {
        float halfWidth = boardColumns * StepSize / 2f;
        float halfHeight = boardRows * StepSize / 2f;

        return new Vector2(
            boardCenter.x - halfWidth + StepSize / 2f,
            boardCenter.y - halfHeight + StepSize / 2f
        );
    }

    private Vector2 GridToWorld(Vector2Int cell)
    {
        Vector2 firstCell = GetFirstCellCenter();

        return new Vector2(
            firstCell.x + cell.x * StepSize,
            firstCell.y + cell.y * StepSize
        );
    }

    private Vector2Int WorldToGrid(Vector2 worldPosition)
    {
        Vector2 firstCell = GetFirstCellCenter();

        return new Vector2Int(
            Mathf.RoundToInt(
                (worldPosition.x - firstCell.x) / StepSize
            ),
            Mathf.RoundToInt(
                (worldPosition.y - firstCell.y) / StepSize
            )
        );
    }

    private bool IsInsideBoard(Vector2Int cell)
    {
        return cell.x >= 0 &&
               cell.x < boardColumns &&
               cell.y >= 0 &&
               cell.y < boardRows;
    }

    private bool IsEdgeCell(Vector2Int cell)
    {
        return cell.x == 0 ||
               cell.x == boardColumns - 1 ||
               cell.y == 0 ||
               cell.y == boardRows - 1;
    }

    // =========================================================
    // OBSTACLE CHECK (Đã fix chuẩn chống đi xuyên)
    // =========================================================

    private bool IsBlocked(Vector2Int cell)
    {
        if (!IsInsideBoard(cell)) return true;

        Vector2 worldPosition = GridToWorld(cell);
        Vector2 boxSize = Vector2.one * StepSize * 0.75f;

        Collider2D[] hits = Physics2D.OverlapBoxAll(
            worldPosition,
            boxSize,
            0f,
            obstacleLayers
        );

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (ignoreTriggers && hit.isTrigger) continue;

            if (hit == myCollider ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // =========================================================
    // PATHFINDING: TÌM ĐƯỜNG TỚI BIÊN
    // =========================================================

    private List<Vector2Int> FindPathToEdge(Vector2Int start)
    {
        if (IsBlocked(start)) return null;

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> previous = new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

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
                found = true;
                break;
            }

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = current + direction;

                if (!IsInsideBoard(next) || visited.Contains(next))
                {
                    continue;
                }

                if (IsBlocked(next))
                {
                    continue;
                }

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
    // START MOVEMENT
    // =========================================================

    public void MoveToExit()
    {
        if (isMoving || hasExited) return;

        if (autoDetectBoard) DetectBoardSettings();

        if (boardRows <= 0 || boardColumns <= 0 || StepSize <= 0f || myCollider == null) return;

        ExitGate exitGate = FindFirstObjectByType<ExitGate>();
        if (exitGate == null) return;

        Vector2Int startCell = WorldToGrid(transform.position);
        if (!IsInsideBoard(startCell)) return;

        List<Vector2Int> path = FindPathToEdge(startCell);

        if (path == null || path.Count == 0)
        {
            // Bị kẹt hoàn toàn -> Rung nhẹ báo hiệu giống ArrowBlock
            StartCoroutine(ShakeRoutine(Vector2.up, StepSize * 0.15f));
            return;
        }

        StartCoroutine(MoveRoutine(path, exitGate));
    }

    private IEnumerator ShakeRoutine(Vector2 dir, float shakeDistance)
    {
        Vector3 basePos = transform.position;
        Vector3 bumpPos = basePos + (Vector3)(dir * shakeDistance);

        float duration = 0.05f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(basePos, bumpPos, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(bumpPos, basePos, elapsed / duration);
            yield return null;
        }

        transform.position = basePos;
    }

    // =========================================================
    // MOVE ROUTINE
    // =========================================================

    private IEnumerator MoveRoutine(
        List<Vector2Int> path,
        ExitGate exitGate)
    {
        isMoving = true;

        foreach (Vector2Int cell in path)
        {
            Vector2 target = GridToWorld(cell);

            yield return StartCoroutine(
                SmoothMoveTo(target, moveSpeed)
            );

            gridX = cell.x;
            gridY = cell.y;
        }

        Vector2Int edgeCell = path[path.Count - 1];

        float halfWidth = boardColumns * StepSize / 2f;
        float halfHeight = boardRows * StepSize / 2f;
        float laneDistance = Mathf.Max(1, borderThickness) * StepSize / 2f;

        float laneRight = boardCenter.x + halfWidth + laneDistance;
        float laneLeft = boardCenter.x - halfWidth - laneDistance;
        float laneTop = boardCenter.y + halfHeight + laneDistance;
        float laneBottom = boardCenter.y - halfHeight - laneDistance;

        Vector2 edgeWorld = GridToWorld(edgeCell);

        if (myCollider != null)
        {
            myCollider.enabled = false;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sortingOrder < 5)
        {
            sr.sortingOrder = 5;
        }

        Vector2 laneStart;

        if (edgeCell.x == 0)
        {
            laneStart = new Vector2(laneLeft, edgeWorld.y);
        }
        else if (edgeCell.x == boardColumns - 1)
        {
            laneStart = new Vector2(laneRight, edgeWorld.y);
        }
        else if (edgeCell.y == boardRows - 1)
        {
            laneStart = new Vector2(edgeWorld.x, laneTop);
        }
        else
        {
            laneStart = new Vector2(edgeWorld.x, laneBottom);
        }

        yield return StartCoroutine(
            SmoothMoveTo(laneStart, moveSpeed)
        );

        Vector3 gatePosition = exitGate.transform.position;

        if (edgeCell.x == 0)
        {
            yield return StartCoroutine(
                SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed)
            );
        }
        else if (edgeCell.x == boardColumns - 1)
        {
            yield return StartCoroutine(
                SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed)
            );
        }
        else if (edgeCell.y == 0)
        {
            bool useLeft = Mathf.Abs(laneStart.x - laneLeft) < Mathf.Abs(laneStart.x - laneRight);
            float sideX = useLeft ? laneLeft : laneRight;

            yield return StartCoroutine(
                SmoothMoveTo(new Vector2(sideX, laneBottom), sidelineMoveSpeed)
            );

            yield return StartCoroutine(
                SmoothMoveTo(new Vector2(sideX, laneTop), sidelineMoveSpeed)
            );
        }

        yield return StartCoroutine(
            SmoothMoveTo(new Vector2(gatePosition.x, laneTop), sidelineMoveSpeed)
        );

        yield return StartCoroutine(
            SmoothMoveTo(gatePosition, sidelineMoveSpeed)
        );

        hasExited = true;
        isMoving = false;

        gameObject.SetActive(false);
    }

    private IEnumerator SmoothMoveTo(Vector2 target, float speed)
    {
        Vector3 targetPosition = new Vector3(
            target.x,
            target.y,
            transform.position.z
        );

        while (Vector3.Distance(transform.position, targetPosition) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                speed * Time.deltaTime
            );

            yield return null;
        }

        transform.position = targetPosition;
    }

    private void OnDrawGizmosSelected()
    {
        if (StepSize <= 0f) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position,
            Vector3.one * StepSize * 0.8f
        );
    }
}