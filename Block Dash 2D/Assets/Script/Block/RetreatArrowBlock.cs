using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
[RequireComponent(typeof(Collider2D))]
public class RetreatArrowBlock : MovableBlockBase
{
    [Header("1. Cấu hình Hướng di chuyển")]
    [Tooltip("Hướng di chuyển của Block theo mũi tên")]
    public ArrowDirection direction = ArrowDirection.Right;

    [Tooltip("Tự động đồng bộ góc xoay Transform và Hướng di chuyển")]
    public bool autoSyncRotation = true;

    [Tooltip("Góc bù trừ nếu Sprite gốc của mũi tên vẽ theo hướng khác")]
    public float rotationOffset = 0f;

    [Header("2. Tốc độ & Chuyển động")]
    [Tooltip("Tốc độ trượt tới (Unity Units / giây)")]
    public float moveSpeed = 14f;

    [Tooltip("Tốc độ trượt lùi về vị trí cũ (Unity Units / giây) - Có thể chỉnh nhanh hơn tốc độ tiến")]
    public float retreatSpeed = 18f;

    [Tooltip("Hiệu ứng rung nhẹ khi bị kẹt ngay ô đầu tiên")]
    public bool playBlockedShake = true;

    [Header("5. Di chuyển dọc Sideline ra Cổng thoát")]
    [Tooltip("Nếu đường thông thoáng ra ngoài biên, block vẫn có thể đi ra cổng thoát bình thường")]
    public bool navigateSidelineToExit = true;

    [Header("6. Sự kiện (Unity Events)")]
    public UnityEvent onMoveStart;
    public UnityEvent onMoveFinished;
    public UnityEvent onRetreatFinished; // Sự kiện khi lùi về chỗ cũ xong
    public UnityEvent onBlocked;

    // Trạng thái runtime
    private bool isMoving = false;

    [SerializeField, HideInInspector] private ArrowDirection lastDirection = ArrowDirection.Right;
    [SerializeField, HideInInspector] private float lastRotationZ = 0f;

    public bool IsMoving => isMoving;

    protected override void Awake()
    {
        base.Awake();
        if (string.IsNullOrEmpty(blockType))
            blockType = "RetreatArrowBlock";
    }

    protected override void Start()
    {
        base.Start(); // gọi DetectBoardSettings nếu autoDetectBoard
        SyncDirectionFromRotation();
    }

    protected override void OnBlockClicked()
    {
        TriggerMove();
    }

    public Vector2 GetDirectionVector()
    {
        switch (direction)
        {
            case ArrowDirection.Right: return Vector2.right;
            case ArrowDirection.Up:    return Vector2.up;
            case ArrowDirection.Left:  return Vector2.left;
            case ArrowDirection.Down:  return Vector2.down;
            default: return Vector2.right;
        }
    }

    public static float DirectionToAngle(ArrowDirection dir)
    {
        switch (dir)
        {
            case ArrowDirection.Right: return 0f;
            case ArrowDirection.Up:    return 90f;
            case ArrowDirection.Left:  return 180f;
            case ArrowDirection.Down:  return 270f;
            default: return 0f;
        }
    }

    public static ArrowDirection AngleToDirection(float angleZ)
    {
        float normalized = Mathf.Repeat(angleZ, 360f);
        int snapAngle = Mathf.RoundToInt(normalized / 90f) * 90;
        snapAngle = (snapAngle % 360 + 360) % 360;

        switch (snapAngle)
        {
            case 90:  return ArrowDirection.Up;
            case 180: return ArrowDirection.Left;
            case 270: return ArrowDirection.Down;
            default:  return ArrowDirection.Right;
        }
    }

    public void SyncDirectionFromRotation()
    {
        float currentZ = transform.eulerAngles.z - rotationOffset;
        direction = AngleToDirection(currentZ);
        lastDirection = direction;
        lastRotationZ = transform.eulerAngles.z;
    }

    public void ApplyRotationFromDirection()
    {
        float targetAngle = DirectionToAngle(direction) + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        lastRotationZ = targetAngle;
        lastDirection = direction;
    }

    public void SetDirection(ArrowDirection newDir)
    {
        direction = newDir;
        ApplyRotationFromDirection();
    }

    public void TriggerMove()
    {
        if (isMoving) return;
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        isMoving = true;
        float stepSize = GetStepSize();
        Vector2 dirVector = GetDirectionVector();

        Vector2Int gridStep = Vector2Int.zero;
        switch (direction)
        {
            case ArrowDirection.Right: gridStep = new Vector2Int(1, 0); break;
            case ArrowDirection.Left:  gridStep = new Vector2Int(-1, 0); break;
            case ArrowDirection.Up:    gridStep = new Vector2Int(0, 1); break;
            case ArrowDirection.Down:  gridStep = new Vector2Int(0, -1); break;
        }

        Vector2 startPos = transform.position;
        Vector2 currentCheckPos = startPos;
        int currentCheckGridX = gridX;
        int currentCheckGridY = gridY;

        Vector2 targetWorldPos = startPos;
        int targetGridX = gridX;
        int targetGridY = gridY;

        bool canExitToSideline = false;
        Vector2 sidelineExitPos = Vector2.zero;

        int maxSafetySteps = 100;
        int stepsCount = 0;
        bool hitObstacle = false; // Đánh dấu xem có bị chặn bởi block khác hay không

        while (stepsCount < maxSafetySteps)
        {
            Vector2 nextWorldPos = currentCheckPos + dirVector * stepSize;
            int nextGridX = currentCheckGridX + gridStep.x;
            int nextGridY = currentCheckGridY + gridStep.y;

            bool isOutsideBoard = (nextGridX < 0 || nextGridX >= boardColumns || nextGridY < 0 || nextGridY >= boardRows);

            if (isOutsideBoard)
            {
                if (navigateSidelineToExit)
                {
                    canExitToSideline = true;
                    float halfW = (boardColumns * stepSize) / 2f;
                    float halfH = (boardRows * stepSize) / 2f;
                    float laneDist = GetSidelineLaneDistance();

                    if (direction == ArrowDirection.Right)
                        sidelineExitPos = new Vector2(boardCenter.x + halfW + laneDist, startPos.y);
                    else if (direction == ArrowDirection.Left)
                        sidelineExitPos = new Vector2(boardCenter.x - halfW - laneDist, startPos.y);
                    else if (direction == ArrowDirection.Up)
                        sidelineExitPos = new Vector2(startPos.x, boardCenter.y + halfH + laneDist);
                    else if (direction == ArrowDirection.Down)
                        sidelineExitPos = new Vector2(startPos.x, boardCenter.y - halfH - laneDist);

                    stepsCount++;
                    break;
                }
                else
                {
                    hitObstacle = true; // Chạm biên bàn cờ mà không thoát ra ngoài -> xem như bị chặn
                    break;
                }
            }

            Vector2 boxSize = new Vector2(stepSize * 0.7f, stepSize * 0.7f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(nextWorldPos, boxSize, 0f, obstacleLayers);
            bool isBlocked = false;

            foreach (var hit in hits)
            {
                if (hit == null) continue;
                if (ignoreTriggers && hit.isTrigger) continue;
                if (hit == myCollider || hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform))
                {
                    continue;
                }

                isBlocked = true;
                break;
            }

            if (isBlocked)
            {
                hitObstacle = true; // Bị chặn bởi vật cản
                break;
            }

            currentCheckPos = nextWorldPos;
            currentCheckGridX = nextGridX;
            currentCheckGridY = nextGridY;
            targetWorldPos = nextWorldPos;
            targetGridX = nextGridX;
            targetGridY = nextGridY;

            stepsCount++;
        }

        // Nếu ngay ô trước mặt đã bị cản hoàn toàn (không đi được bước nào)
        if (stepsCount == 0)
        {
            onBlocked?.Invoke();
            if (playBlockedShake)
            {
                yield return StartCoroutine(ShakeRoutine(dirVector, stepSize * 0.15f));
            }
            isMoving = false;
            yield break;
        }

        onMoveStart?.Invoke();

        // Kiểm tra lối thoát ra Sideline
#if UNITY_2023_1_OR_NEWER
        ExitGate exitGate = FindFirstObjectByType<ExitGate>();
#else
        ExitGate exitGate = FindObjectOfType<ExitGate>();
#endif

        if (canExitToSideline && exitGate != null)
        {
            yield return StartCoroutine(NavigateAlongSidelineToGate(exitGate, sidelineExitPos));
            yield break;
        }

        // --- CƠ CHẾ CHÍNH: LƯỚT TỚI ĐIỂM DỪNG, NẾU BỊ CHẶN THÌ LÙI VỀ VỊ TRÍ CŨ ---
        
        // 1. Trượt tới vị trí đích (targetWorldPos)
        while (Vector2.Distance(transform.position, targetWorldPos) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = new Vector3(targetWorldPos.x, targetWorldPos.y, transform.position.z);

        // 2. Nếu bị chặn (hitObstacle = true), thực hiện lùi về vị trí ban đầu (startPos)
        if (hitObstacle)
        {
            // Có thể thêm một khoảng dừng ngắn tại điểm chạm (ví dụ 0.05 giây) cho tự nhiên
            yield return new WaitForSeconds(0.05f);

            while (Vector2.Distance(transform.position, startPos) > 0.005f)
            {
                transform.position = Vector3.MoveTowards(transform.position, startPos, retreatSpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = new Vector3(startPos.x, startPos.y, transform.position.z);

            // Giữ nguyên tọa độ lưới ban đầu vì block đã quay về chỗ cũ
            onRetreatFinished?.Invoke();
        }
        else
        {
            // Nếu không bị chặn (trường hợp hiếm nếu không ra cửa thoát, nhưng giữ nguyên grid mới)
            gridX = targetGridX;
            gridY = targetGridY;
            onMoveFinished?.Invoke();
        }

        isMoving = false;
    }

    private IEnumerator NavigateAlongSidelineToGate(ExitGate exitGate, Vector2 initialLaneExitPos)
    {
        isMoving = true;
        EnsureTopSortingOrder();
        if (myCollider != null) myCollider.enabled = false;

        float stepSize = GetStepSize();
        float halfW = (boardColumns * stepSize) / 2f;
        float halfH = (boardRows * stepSize) / 2f;
        float laneDist = GetSidelineLaneDistance();

        float laneRight = boardCenter.x + halfW + laneDist;
        float laneLeft = boardCenter.x - halfW - laneDist;
        float laneTop = boardCenter.y + halfH + laneDist;
        float laneBottom = boardCenter.y - halfH - laneDist;

        Vector3 gatePos = exitGate.transform.position;
        float targetGateX = gatePos.x;

        yield return StartCoroutine(SmoothMoveTo(initialLaneExitPos, moveSpeed));

        // Code di chuyển dọc viền (tương tự ArrowBlock chuẩn)
        switch (direction)
        {
            case ArrowDirection.Right:
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed));
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Left) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Left;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            case ArrowDirection.Left:
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed));
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Right) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Right;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            case ArrowDirection.Up:
                if (Mathf.Abs(transform.position.x - targetGateX) > 0.15f)
                {
                    if (transform.position.x < targetGateX)
                    {
                        yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Right) + rotationOffset, cornerTurnDuration));
                        direction = ArrowDirection.Right;
                        yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    }
                    else
                    {
                        yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Left) + rotationOffset, cornerTurnDuration));
                        direction = ArrowDirection.Left;
                        yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    }
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Up;
                }
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            case ArrowDirection.Down:
                if (transform.position.x >= boardCenter.x)
                {
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Right) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Right;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneBottom), sidelineMoveSpeed));
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed));
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Left) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Left;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                }
                else
                {
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Left) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Left;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneBottom), sidelineMoveSpeed));
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed));
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Right) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Right;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                }
                break;
        }

        onMoveFinished?.Invoke();
        yield return StartCoroutine(exitGate.PlayExitAnimationRoutine(this));
        isMoving = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(blockType)) blockType = "RetreatArrowBlock";
        if (!autoSyncRotation) return;

        float currentZ = transform.eulerAngles.z;
        if (direction != lastDirection)
        {
            ApplyRotationFromDirection();
            lastDirection = direction;
            lastRotationZ = transform.eulerAngles.z;
        }
        else if (Mathf.Abs(Mathf.DeltaAngle(currentZ, lastRotationZ)) > 1f)
        {
            SyncDirectionFromRotation();
            lastDirection = direction;
            lastRotationZ = currentZ;
        }
    }
#endif
}

#if UNITY_EDITOR
    [CustomEditor(typeof(RetreatArrowBlock))]
    [CanEditMultipleObjects]
    public class RetreatArrowBlockEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RetreatArrowBlock block = (RetreatArrowBlock)target;

            // Vẽ thanh chọn nhanh hướng di chuyển với 4 nút trực quan
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("⚡ Chọn nhanh hướng di chuyển:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = (block.direction == ArrowDirection.Left) ? Color.cyan : Color.white;
            if (GUILayout.Button("◄ TRÁI\n(180°)", GUILayout.Height(38)))
            {
                Undo.RecordObject(block.transform, "Change Retreat Arrow Direction Left");
                Undo.RecordObject(block, "Change Retreat Arrow Direction Left");
                block.SetDirection(ArrowDirection.Left);
                EditorUtility.SetDirty(block);
            }

            GUI.backgroundColor = (block.direction == ArrowDirection.Up) ? Color.cyan : Color.white;
            if (GUILayout.Button("▲ LÊN\n(90°)", GUILayout.Height(38)))
            {
                Undo.RecordObject(block.transform, "Change Retreat Arrow Direction Up");
                Undo.RecordObject(block, "Change Retreat Arrow Direction Up");
                block.SetDirection(ArrowDirection.Up);
                EditorUtility.SetDirty(block);
            }

            GUI.backgroundColor = (block.direction == ArrowDirection.Down) ? Color.cyan : Color.white;
            if (GUILayout.Button("▼ XUỐNG\n(270°)", GUILayout.Height(38)))
            {
                Undo.RecordObject(block.transform, "Change Retreat Arrow Direction Down");
                Undo.RecordObject(block, "Change Retreat Arrow Direction Down");
                block.SetDirection(ArrowDirection.Down);
                EditorUtility.SetDirty(block);
            }

            GUI.backgroundColor = (block.direction == ArrowDirection.Right) ? Color.cyan : Color.white;
            if (GUILayout.Button("► PHẢI\n(0°)", GUILayout.Height(38)))
            {
                Undo.RecordObject(block.transform, "Change Retreat Arrow Direction Right");
                Undo.RecordObject(block, "Change Retreat Arrow Direction Right");
                block.SetDirection(ArrowDirection.Right);
                EditorUtility.SetDirty(block);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Hiển thị nút test di chuyển khi đang Play Mode
            if (Application.isPlaying)
            {
                GUI.backgroundColor = new Color(0.2f, 1f, 0.4f);
                if (GUILayout.Button("▶ TEST CLICK (THỬ DI CHUYỂN & LÙI)", GUILayout.Height(32)))
                {
                    block.TriggerMove();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("🛣️ Căn chỉnh làn di chuyển Sideline:", EditorStyles.boldLabel);

            float step = block.GetStepSize();
            float currentDist = block.GetSidelineLaneDistance();
            float distInCells = (step > 0) ? (currentDist / step) : 0f;
            EditorGUILayout.HelpBox($"Khoảng cách ra làn Sideline: {currentDist:F2} Units ({distInCells:F1} ô cờ). Chế độ: {block.sidelineLanePosition}", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = (block.sidelineLanePosition == SidelineLanePosition.CenterOfSideline) ? new Color(0.3f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button("🎯 CHÍNH GIỮA VIỀN\n(Cách đều 2 mép)", GUILayout.Height(36)))
            {
                Undo.RecordObject(block, "Set Sideline Lane Center");
                block.sidelineLanePosition = SidelineLanePosition.CenterOfSideline;
                EditorUtility.SetDirty(block);
            }

            GUI.backgroundColor = (block.sidelineLanePosition == SidelineLanePosition.OuterRowOfSideline) ? new Color(0.3f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button("⬆️ HÀNG NGOÀI CÙNG\n(Lớp ngoài)", GUILayout.Height(36)))
            {
                Undo.RecordObject(block, "Set Sideline Lane Outer");
                block.sidelineLanePosition = SidelineLanePosition.OuterRowOfSideline;
                EditorUtility.SetDirty(block);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("🔄 Đồng bộ lại Bàn cờ & Sideline từ Scene", GUILayout.Height(28)))
            {
                Undo.RecordObject(block, "Detect Board Settings");
                block.DetectBoardSettings();
                EditorUtility.SetDirty(block);
            }

            EditorGUILayout.Space(8);

            // Vẽ các thuộc tính mặc định của Inspector
            DrawDefaultInspector();
        }
    }
    #endif
