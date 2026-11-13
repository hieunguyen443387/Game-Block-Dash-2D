using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TwoWayAxis
{
    [Tooltip("Di chuyển theo chiều Ngang (Horizontal): Bắt đầu chọn Left hoặc Right")]
    Horizontal = 0,

    [Tooltip("Di chuyển theo chiều Dọc (Vertical): Bắt đầu chọn Top (Up) hoặc Down")]
    Vertical = 1
}

public enum HorizontalStartDirection
{
    Right = 0,
    Left = 2
}

public enum VerticalStartDirection
{
    Top = 1,
    Down = 3
}

public enum TwoWayDirection
{
    Right = 0,
    Up = 1,
    Left = 2,
    Down = 3
}

[SelectionBase]
[RequireComponent(typeof(Collider2D))]
public class TwoWayBlock : MovableBlockBase
{
    [Header("1. Cấu hình Hướng di chuyển Hai chiều (Two-Way)")]
    [Tooltip("Chọn trục di chuyển: Horizontal (Trái/Phải) hoặc Vertical (Lên/Xuống)")]
    public TwoWayAxis moveAxis = TwoWayAxis.Horizontal;

    [Tooltip("Hướng bắt đầu khi trục là Horizontal")]
    public HorizontalStartDirection horizontalStartDir = HorizontalStartDirection.Right;

    [Tooltip("Hướng bắt đầu khi trục là Vertical")]
    public VerticalStartDirection verticalStartDir = VerticalStartDirection.Top;

    [Tooltip("Tự động đồng bộ góc xoay Transform")]
    public bool autoSyncRotation = true;

    [Tooltip("Góc bù trừ nếu Sprite gốc của mũi tên vẽ theo hướng khác")]
    public float rotationOffset = 0f;

    [Header("2. Tốc độ & Chuyển động")]
    public float moveSpeed = 14f;
    public bool playImpactBounce = true;
    public bool playBlockedShake = true;

    [Header("3. Di chuyển dọc Sideline ra Cổng thoát")]
    public bool navigateSidelineToExit = true;

    [Header("4. Sự kiện (Unity Events)")]
    public UnityEvent onMoveStart;
    public UnityEvent onMoveFinished;
    public UnityEvent onBlocked;

    // Trạng thái runtime
    private bool isMoving = false;

    private TwoWayDirection currentTwoWayDir;

    [SerializeField, HideInInspector] private float lastRotationZ = 0f;

    public bool IsMoving => isMoving;

    protected override void Awake()
    {
        base.Awake();
        if (string.IsNullOrEmpty(blockType))
            blockType = "TwoWayBlock";
        InitializeCurrentDirection();
    }

    protected override void Start()
    {
        base.Start(); // gọi DetectBoardSettings nếu autoDetectBoard
        ApplyRotationFromDirection();
    }

    protected override void OnBlockClicked()
    {
        TriggerMove();
    }

    public void SyncDirectionFromCurrentRotation()
    {
        // Trừ thêm rotationOffset để đọc chính xác góc hướng thực tế
        float currentZ = transform.eulerAngles.z - rotationOffset;
        float currentAngle = Mathf.Round(currentZ / 90f) * 90f;
        currentAngle = (currentAngle % 360f + 360f) % 360f;

        if (Mathf.Abs(currentAngle - 0f) < 5f) currentTwoWayDir = TwoWayDirection.Right;
        else if (Mathf.Abs(currentAngle - 90f) < 5f) currentTwoWayDir = TwoWayDirection.Up;
        else if (Mathf.Abs(currentAngle - 180f) < 5f) currentTwoWayDir = TwoWayDirection.Left;
        else if (Mathf.Abs(currentAngle - 270f) < 5f) currentTwoWayDir = TwoWayDirection.Down;
    }

    public void InitializeCurrentDirection()
    {
        if (moveAxis == TwoWayAxis.Horizontal)
        {
            currentTwoWayDir = (horizontalStartDir == HorizontalStartDirection.Right) ? TwoWayDirection.Right : TwoWayDirection.Left;
        }
        else
        {
            currentTwoWayDir = (verticalStartDir == VerticalStartDirection.Top) ? TwoWayDirection.Up : TwoWayDirection.Down;
        }
    }

    public void ToggleTwoWayDirection()
    {
        if (moveAxis == TwoWayAxis.Horizontal)
        {
            currentTwoWayDir = (currentTwoWayDir == TwoWayDirection.Right) ? TwoWayDirection.Left : TwoWayDirection.Right;
        }
        else
        {
            currentTwoWayDir = (currentTwoWayDir == TwoWayDirection.Up) ? TwoWayDirection.Down : TwoWayDirection.Up;
        }
        ApplyRotationFromDirection();
    }

    public TwoWayDirection GetCurrentDirection()
    {
        return currentTwoWayDir;
    }

    public Vector2 GetDirectionVector()
    {
        switch (currentTwoWayDir)
        {
            case TwoWayDirection.Right: return Vector2.right;
            case TwoWayDirection.Up:    return Vector2.up;
            case TwoWayDirection.Left:  return Vector2.left;
            case TwoWayDirection.Down:  return Vector2.down;
            default: return Vector2.right;
        }
    }

    public static float DirectionToAngle(TwoWayDirection dir)
    {
        switch (dir)
        {
            case TwoWayDirection.Right: return 0f;
            case TwoWayDirection.Up:    return 90f;
            case TwoWayDirection.Left:  return 180f;
            case TwoWayDirection.Down:  return 270f;
            default: return 0f;
        }
    }

    public void ApplyRotationFromDirection()
    {
        float targetAngle = DirectionToAngle(currentTwoWayDir) + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        lastRotationZ = targetAngle;
    }

    public void TriggerMove()
    {
        if (isMoving) return;
        SyncDirectionFromCurrentRotation();

        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        isMoving = true;
        float stepSize = GetStepSize();
        Vector2 dirVector = GetDirectionVector();
        Vector2Int gridStep = new Vector2Int(Mathf.RoundToInt(dirVector.x), Mathf.RoundToInt(dirVector.y));

        Vector2 startPos = transform.position;
        Vector2 currentCheckPos = startPos;
        Vector2Int startGrid = GetGridPositionFromWorld(startPos);

        int currentCheckGridX = startGrid.x;
        int currentCheckGridY = startGrid.y;

        Vector2 targetWorldPos = startPos;
        int targetGridX = startGrid.x;
        int targetGridY = startGrid.y;

        Debug.Log(
            $"[GRID INIT] Block={name} | " +
            $"World={startPos} | " +
            $"Grid=({currentCheckGridX},{currentCheckGridY}) | " +
            $"BoardCenter={boardCenter} | StepSize={stepSize}",
            this
        );

        bool canExitToSideline = false;
        Vector2 sidelineExitPos = Vector2.zero;

        int maxSafetySteps = 100;
        int stepsCount = 0;

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

                    if (currentTwoWayDir == TwoWayDirection.Right)
                        sidelineExitPos = new Vector2(boardCenter.x + halfW + laneDist, startPos.y);
                    else if (currentTwoWayDir == TwoWayDirection.Left)
                        sidelineExitPos = new Vector2(boardCenter.x - halfW - laneDist, startPos.y);
                    else if (currentTwoWayDir == TwoWayDirection.Up)
                        sidelineExitPos = new Vector2(startPos.x, boardCenter.y + halfH + laneDist);
                    else if (currentTwoWayDir == TwoWayDirection.Down)
                        sidelineExitPos = new Vector2(startPos.x, boardCenter.y - halfH - laneDist);

                    stepsCount++;
                    break;
                }
                else
                {
                    break;
                }
            }

            // Kiểm tra vật cản dựa trên kích thước Collider thực tế của block
            Vector2 checkCenter;
            Vector2 checkSize;

            if (myCollider != null)
            {
                Bounds bounds = myCollider.bounds;

                // Tâm kiểm tra nằm phía trước block một ô
                checkCenter = (Vector2)bounds.center + dirVector * stepSize;

                // Lấy kích thước collider, giảm nhẹ để tránh tính nhầm chính block này
                checkSize = (Vector2)bounds.size * 0.95f;
            }
            else
            {
                checkCenter = nextWorldPos;
                checkSize = Vector2.one * stepSize * 0.95f;
            }

            Collider2D[] hits = Physics2D.OverlapBoxAll(
                checkCenter,
                checkSize,
                0f,
                obstacleLayers
            );

            bool isBlocked = false;

            foreach (Collider2D hit in hits)
            {
                if (hit == null) continue;

                if (ignoreTriggers && hit.isTrigger) continue;

                // Bỏ qua collider của chính block hiện tại và các object con
                if (hit == myCollider) continue;
                if (hit.transform == transform) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                isBlocked = true;
                break;
            }

            if (isBlocked)
            {
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

        // Nếu ngay ô trước mặt đã bị cản (không đi được bước nào)
        if (stepsCount == 0)
        {
            onBlocked?.Invoke();
            if (playBlockedShake)
            {
                yield return StartCoroutine(ShakeRoutine(dirVector, stepSize * 0.15f));
            }
            
            // Đảo chiều hướng
            ToggleTwoWayDirection();
            
            // Cập nhật lại vector hướng mới ngay lập tức sau khi đổi chiều để tránh lệch nhịp
            dirVector = GetDirectionVector();
            
            isMoving = false;
            yield break;
        }

        onMoveStart?.Invoke();

#if UNITY_2023_1_OR_NEWER
        ExitGate exitGate = FindFirstObjectByType<ExitGate>();
#else
        ExitGate exitGate = FindObjectOfType<ExitGate>();
#endif

        if (canExitToSideline && exitGate != null)
        {
            yield return StartCoroutine(NavigateAlongSidelineToGate(exitGate, sidelineExitPos));
            ToggleTwoWayDirection();
            yield break;
        }

        while (Vector2.Distance(transform.position, targetWorldPos) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = new Vector3(targetWorldPos.x, targetWorldPos.y, transform.position.z);
        gridX = targetGridX;
        gridY = targetGridY;

        if (playImpactBounce)
        {
            yield return StartCoroutine(ImpactBounceRoutine(dirVector, stepSize * 0.12f));
        }

        onMoveFinished?.Invoke();
        ToggleTwoWayDirection();
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

        switch (currentTwoWayDir)
        {
            case TwoWayDirection.Right:
            {
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                currentTwoWayDir = TwoWayDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed));

                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Left) + rotationOffset, cornerTurnDuration));
                currentTwoWayDir = TwoWayDirection.Left;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));

                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                currentTwoWayDir = TwoWayDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            }
            case TwoWayDirection.Left:
            {
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                currentTwoWayDir = TwoWayDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed));

                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Right) + rotationOffset, cornerTurnDuration));
                currentTwoWayDir = TwoWayDirection.Right;
                yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));

                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                currentTwoWayDir = TwoWayDirection.Up;
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            }
            case TwoWayDirection.Up:
            {
                if (Mathf.Abs(transform.position.x - targetGateX) > 0.15f)
                {
                    if (transform.position.x < targetGateX)
                    {
                        yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Right) + rotationOffset, cornerTurnDuration));
                        currentTwoWayDir = TwoWayDirection.Right;
                        yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    }
                    else
                    {
                        yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Left) + rotationOffset, cornerTurnDuration));
                        currentTwoWayDir = TwoWayDirection.Left;
                        yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    }
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Up;
                }
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            }
            case TwoWayDirection.Down:
            {
                if (transform.position.x >= boardCenter.x)
                {
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Right) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Right;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneBottom), sidelineMoveSpeed));

                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed));

                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Left) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Left;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));

                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                }
                else
                {
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Left) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Left;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneBottom), sidelineMoveSpeed));

                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed));

                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Right) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Right;
                    yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));

                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(TwoWayDirection.Up) + rotationOffset, cornerTurnDuration));
                    currentTwoWayDir = TwoWayDirection.Up;
                    yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                }
                break;
            }
        }

        onMoveFinished?.Invoke();
        
        // Sử dụng reflection hoặc coroutine chung của ExitGate để tương thích an toàn tuyệt đối mà không cần ép kiểu
        yield return StartCoroutine(exitGate.PlayExitAnimationRoutine(this));
        isMoving = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(blockType))
        {
            blockType = "TwoWayBlock";
        }
        if (autoSyncRotation && !Application.isPlaying)
        {
            InitializeCurrentDirection();
            ApplyRotationFromDirection();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 dir = GetDirectionVector();
        float stepSize = GetStepSize();
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(dir * stepSize);

        Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
        Gizmos.DrawLine(start, end);

        Vector3 right = Quaternion.Euler(0, 0, 150) * dir * (stepSize * 0.3f);
        Vector3 left = Quaternion.Euler(0, 0, -150) * dir * (stepSize * 0.3f);
        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube(end, Vector3.one * stepSize * 0.8f);

        if (navigateSidelineToExit && boardColumns > 0 && boardRows > 0)
        {
            float halfW = (boardColumns * stepSize) / 2f;
            float halfH = (boardRows * stepSize) / 2f;
            float laneDist = GetSidelineLaneDistance();

            float lRight = boardCenter.x + halfW + laneDist;
            float lLeft = boardCenter.x - halfW - laneDist;
            float lTop = boardCenter.y + halfH + laneDist;
            float lBottom = boardCenter.y - halfH - laneDist;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Vector3 p1 = new Vector3(lLeft, lBottom, 0);
            Vector3 p2 = new Vector3(lRight, lBottom, 0);
            Vector3 p3 = new Vector3(lRight, lTop, 0);
            Vector3 p4 = new Vector3(lLeft, lTop, 0);

            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4);
            Gizmos.DrawLine(p4, p1);
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(TwoWayBlock))]
[CanEditMultipleObjects]
public class TwoWayBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TwoWayBlock block = (TwoWayBlock)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🔄 Cấu hình Di chuyển Hai chiều (TwoWayBlock):", EditorStyles.boldLabel);

        TwoWayAxis newAxis = (TwoWayAxis)EditorGUILayout.EnumPopup("Trục Di Chuyển", block.moveAxis);
        if (newAxis != block.moveAxis)
        {
            Undo.RecordObject(block, "Change Move Axis");
            block.moveAxis = newAxis;
            block.InitializeCurrentDirection();
            block.ApplyRotationFromDirection();
            EditorUtility.SetDirty(block);
        }

        if (block.moveAxis == TwoWayAxis.Horizontal)
        {
            HorizontalStartDirection newHDir = (HorizontalStartDirection)EditorGUILayout.EnumPopup("Hướng Bắt Đầu (Ngang)", block.horizontalStartDir);
            if (newHDir != block.horizontalStartDir)
            {
                Undo.RecordObject(block, "Change Horizontal Start Dir");
                block.horizontalStartDir = newHDir;
                block.InitializeCurrentDirection();
                block.ApplyRotationFromDirection();
                EditorUtility.SetDirty(block);
            }
        }
        else
        {
            VerticalStartDirection newVDir = (VerticalStartDirection)EditorGUILayout.EnumPopup("Hướng Bắt Đầu (Dọc)", block.verticalStartDir);
            if (newVDir != block.verticalStartDir)
            {
                Undo.RecordObject(block, "Change Vertical Start Dir");
                block.verticalStartDir = newVDir;
                block.InitializeCurrentDirection();
                block.ApplyRotationFromDirection();
                EditorUtility.SetDirty(block);
            }
        }

        EditorGUILayout.Space(8);

        if (Application.isPlaying)
        {
            GUI.backgroundColor = new Color(0.2f, 1f, 0.4f);
            if (GUILayout.Button("▶ TEST CLICK (THỬ DI CHUYỂN)", GUILayout.Height(32)))
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
        EditorGUILayout.HelpBox($"Khoảng cách ra làn Sideline: {currentDist:F2} Units ({distInCells:F1} ô cờ). Hướng hiện tại: {block.GetCurrentDirection()}", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = (block.sidelineLanePosition == SidelineLanePosition.CenterOfSideline) ? new Color(0.3f, 1f, 0.5f) : Color.white;
        if (GUILayout.Button("🎯 CHÍNH GIỮA VIỀN", GUILayout.Height(36)))
        {
            Undo.RecordObject(block, "Set Sideline Lane Center");
            block.sidelineLanePosition = SidelineLanePosition.CenterOfSideline;
            EditorUtility.SetDirty(block);
        }

        GUI.backgroundColor = (block.sidelineLanePosition == SidelineLanePosition.OuterRowOfSideline) ? new Color(0.3f, 0.8f, 1f) : Color.white;
        if (GUILayout.Button("⬆️ HÀNG NGOÀI CÙNG", GUILayout.Height(36)))
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
        
        // ❌ ĐÃ XÓA BỎ DÒNG DrawDefaultInspector(); Ở ĐÂY ĐỂ TRÁNH BỊ HIỂN THỊ LẶP LẠI PHẦN CẤU HÌNH PHÍA DƯỚI!
    }
}
#endif
