using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ArrowDirection
{
    Right = 0,  // Xoay 0°   -> Di chuyển sang Phải (+X)
    Up = 1,     // Xoay 90°  -> Di chuyển Lên Trên (+Y)
    Left = 2,   // Xoay 180° -> Di chuyển sang Trái (-X)
    Down = 3    // Xoay 270° -> Di chuyển Xuống Dưới (-Y)
}

[SelectionBase]
[RequireComponent(typeof(Collider2D))]
public class ArrowBlock : MovableBlockBase
{
    [Header("1. Cấu hình Hướng di chuyển")]
    [Tooltip("Hướng di chuyển của Block theo mũi tên")]
    public ArrowDirection direction = ArrowDirection.Right;

    [Tooltip("Tự động đồng bộ góc xoay Transform và Hướng di chuyển (Xoay trên Scene -> Đổi hướng, Đổi hướng -> Tự xoay)")]
    public bool autoSyncRotation = true;

    [Tooltip("Góc bù trừ nếu Sprite gốc của mũi tên vẽ theo hướng khác (Mặc định 0° nếu Sprite vẽ mũi tên sang Phải)")]
    public float rotationOffset = 0f;

    [Header("2. Tốc độ & Chuyển động")]
    [Tooltip("Tốc độ trượt của block (Unity Units / giây)")]
    public float moveSpeed = 14f;

    [Tooltip("Hiệu ứng nảy nhẹ (Bounce) khi đâm vào block chặn")]
    public bool playImpactBounce = true;

    [Tooltip("Hiệu ứng rung nhẹ (Shake) khi bị kẹt không đi được")]
    public bool playBlockedShake = true;

    [Header("3. Bàn cờ & Grid Settings")]
    // (Các thông số bàn cờ được kế thừa từ MovableBlockBase)

    [Header("4. Di chuyển dọc Sideline ra Cổng thoát")]
    [Tooltip("Khi ra khỏi bàn cờ, khối block sẽ đi dọc theo đường viền Sideline để tìm đến Cổng thoát")]
    public bool navigateSidelineToExit = true;

    [Header("5. Sự kiện (Unity Events)")]
    public UnityEvent onMoveStart;
    public UnityEvent onMoveFinished;
    public UnityEvent onBlocked;

    [Header("6. Đóng băng (Freeze)")]
    [Tooltip("Tham chiếu tới FreezableBlock (nếu để trống sẽ tự động tìm trên cùng GameObject)")]
    public FreezableBlock freezableBlock;

    /// <summary>
    /// Kiểm tra xem Block có đang bị đóng băng bởi FreezableBlock hay không
    /// </summary>
    public bool IsFrozen
    {
        get
        {
            if (freezableBlock == null)
            {
                freezableBlock = GetComponent<FreezableBlock>();
            }
            return freezableBlock != null && freezableBlock.isFrozen;
        }
    }

    // Trạng thái runtime
    private bool isMoving = false;

    [SerializeField, HideInInspector] private ArrowDirection lastDirection = ArrowDirection.Right;
    [SerializeField, HideInInspector] private float lastRotationZ = 0f;

    public bool IsMoving => isMoving;

    protected override void Awake()
    {
        base.Awake(); // khởi tạo myCollider
        if (freezableBlock == null)
            freezableBlock = GetComponent<FreezableBlock>();
        if (string.IsNullOrEmpty(blockType))
            blockType = "ArrowBlock";
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

    /// <summary>
    /// Trả về vector hướng di chuyển chuẩn hóa (Vector2)
    /// </summary>
    public Vector2 GetDirectionVector()
    {
        switch (direction)
        {
            case ArrowDirection.Right:
                return Vector2.right;
            case ArrowDirection.Up:
                return Vector2.up;
            case ArrowDirection.Left:
                return Vector2.left;
            case ArrowDirection.Down:
                return Vector2.down;
            default:
                return Vector2.right;
        }
    }

    /// <summary>
    /// Trả về góc xoay Z tương ứng với enum hướng
    /// </summary>
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

    /// <summary>
    /// Chuyển đổi từ góc xoay Z sang Enum hướng gần nhất (làm tròn bội số 90°)
    /// </summary>
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

    /// <summary>
    /// Đồng bộ hướng từ góc xoay Transform của GameObject
    /// </summary>
    public void SyncDirectionFromRotation()
    {
        float currentZ = transform.eulerAngles.z - rotationOffset;
        direction = AngleToDirection(currentZ);
        lastDirection = direction;
        lastRotationZ = transform.eulerAngles.z;
    }

    /// <summary>
    /// Xoay Transform của GameObject theo enum hướng đã chọn
    /// </summary>
    public void ApplyRotationFromDirection()
    {
        float targetAngle = DirectionToAngle(direction) + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
        lastRotationZ = targetAngle;
        lastDirection = direction;
    }

    /// <summary>
    /// Đặt hướng di chuyển mới và tự động xoay GameObject
    /// </summary>
    public void SetDirection(ArrowDirection newDir)
    {
        direction = newDir;
        ApplyRotationFromDirection();
    }

    /// <summary>
    /// Kích hoạt di chuyển khối block theo hướng mũi tên
    /// </summary>
    public void TriggerMove()
    {
        if (isMoving) return;

        // Nếu block đang bị đóng băng (isFrozen = true) thì khóa di chuyển
        if (IsFrozen)
        {
            Debug.Log($"[ArrowBlock] Khối '{name}' đang bị đóng băng (FreezableBlock.isFrozen = true). Không thể di chuyển!", this);
            onBlocked?.Invoke();
            if (playBlockedShake)
            {
                StartCoroutine(ShakeRoutine(GetDirectionVector(), GetStepSize() * 0.15f));
            }
            return;
        }

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

        // Quét từng ô về phía trước cho đến khi gặp vật cản hoặc mép bàn cờ
        int maxSafetySteps = 100;
        int stepsCount = 0;

        while (stepsCount < maxSafetySteps)
        {
            Debug.Log($"[START MOVE] Block={name} | Direction={direction} | " +$"Transform={transform.position} | " +$"Grid=({gridX},{gridY}) | " +$"BoardSize=({boardColumns},{boardRows})",this);
            Vector2 nextWorldPos = currentCheckPos + dirVector * stepSize;
            int nextGridX = currentCheckGridX + gridStep.x;
            int nextGridY = currentCheckGridY + gridStep.y;
            

            // Kiểm tra xem đã vượt ra ngoài biên bàn cờ chưa
            bool isOutsideBoard = nextGridX < 0 || nextGridX >= boardColumns || nextGridY < 0 || nextGridY >= boardRows;

            Debug.Log($"[GRID CHECK] Block={name} | Direction={direction} | " + $"CurrentGrid=({currentCheckGridX},{currentCheckGridY}) | " + $"NextGrid=({nextGridX},{nextGridY}) | " + $"BoardSize=({boardColumns},{boardRows}) | " + $"OutsideBoard={isOutsideBoard}",this);

            if (isOutsideBoard)
            {
                if (navigateSidelineToExit)
                {
                    // Lối ra viền Sideline thông suốt!
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
                    // Chạm biên bàn cờ, dừng tại ô hiện tại
                    break;
                }
            }

            // Kiểm tra Vật cản bằng OverlapBox (Block khác trên bàn cờ)
            Vector2 boxSize = new Vector2(stepSize * 0.75f, stepSize * 0.75f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(nextWorldPos, boxSize, 0f, obstacleLayers);
            Debug.Log($"Direction: {direction} | " + $"NextPos: {nextWorldPos} | " + $"Hits: {hits.Length}");      
            bool isBlocked = false;

            foreach (var hit in hits)
            {
                if (ignoreTriggers && hit.isTrigger) continue;
                if (hit != myCollider && hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    isBlocked = true;

                    // Báo va chạm cho CẢ HAI phía: block đang đâm và block bị đâm.
                    // Nhờ vậy boom bị trừ bất kể block nào là bên chủ động.
                    HitCountdownBoomBlock.NotifyCollision(gameObject, hit.gameObject);

                    break;
                }
            }

            if (isBlocked)
            {
                // Bị cản bởi block khác trên bàn cờ
                break;
            }

            // Ô này trống và hợp lệ trong bàn cờ
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
            isMoving = false;
            yield break;
        }

        onMoveStart?.Invoke();

        // 1. Nếu có lối thoát ra Sideline và có Cổng thoát trong Scene
#if UNITY_2023_1_OR_NEWER
        ExitGate exitGate = FindFirstObjectByType<ExitGate>();
#else
        ExitGate exitGate = FindObjectOfType<ExitGate>();
#endif

        if (canExitToSideline && exitGate != null)
        {
            // Trượt ra Sideline và di chuyển dọc viền theo đúng hướng mũi tên tới Cổng thoát!
            yield return StartCoroutine(NavigateAlongSidelineToGate(exitGate, sidelineExitPos));
            yield break;
        }

        // 2. Di chuyển thông thường trên bàn cờ nếu chưa thể ra viền
        while (Vector2.Distance(transform.position, targetWorldPos) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                moveSpeed * Time.deltaTime
            );
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
        isMoving = false;
    }

    /// <summary>
    /// Điều hướng khối block ra Sideline và chạy dọc theo viền tới Cổng thoát (luôn xoay theo hướng mũi tên)
    /// </summary>
    private IEnumerator NavigateAlongSidelineToGate(ExitGate exitGate, Vector2 initialLaneExitPos)
    {
        isMoving = true;

        // Đảm bảo Sprite luôn hiển thị nổi bật trên nền Sideline
        EnsureTopSortingOrder();

        // Tạm tắt collider để không bị kẹt khi đi dọc viền
        if (myCollider != null)
        {
            myCollider.enabled = false;
        }

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

        // 1. Trượt ra làn Sideline ban đầu theo hướng mũi tên hiện tại
        yield return StartCoroutine(SmoothMoveTo(initialLaneExitPos, moveSpeed));

        // 2. Chạy dọc theo viền Sideline tới Cổng thoát (mỗi khúc cua đều xoay mũi tên theo hướng di chuyển)
        switch (direction)
        {
            case ArrowDirection.Right:
            {
                // Đang ở làn Phải, rẽ LÊN (90°)
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;

                // Đi thẳng lên góc Trên-Phải
                yield return StartCoroutine(SmoothMoveTo(new Vector2(laneRight, laneTop), sidelineMoveSpeed));

                // Rẽ sang TRÁI (180°) hướng về phía Cổng thoát
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Left) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Left;

                // Đi dọc viền trên đến thẳng hàng với Cổng thoát
                yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));

                // Rẽ LÊN (90°) đâm thẳng vào Cổng thoát
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;

                // Đi thẳng vào Cổng thoát
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            }

            case ArrowDirection.Left:
            {
                // Đang ở làn Trái, rẽ LÊN (90°)
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;

                // Đi thẳng lên góc Trên-Trái
                yield return StartCoroutine(SmoothMoveTo(new Vector2(laneLeft, laneTop), sidelineMoveSpeed));

                // Rẽ sang PHẢI (0°) hướng về phía Cổng thoát
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Right) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Right;

                // Đi dọc viền trên đến thẳng hàng với Cổng thoát
                yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));

                // Rẽ LÊN (90°) đâm thẳng vào Cổng thoát
                yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                direction = ArrowDirection.Up;

                // Đi thẳng vào Cổng thoát
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            }

            case ArrowDirection.Up:
            {
                // Đang ở làn viền Trên
                if (Mathf.Abs(transform.position.x - targetGateX) > 0.15f)
                {
                    if (transform.position.x < targetGateX)
                    {
                        // Nằm bên trái cổng -> Rẽ sang PHẢI (0°)
                        yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Right) + rotationOffset, cornerTurnDuration));
                        direction = ArrowDirection.Right;

                        yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    }
                    else
                    {
                        // Nằm bên phải cổng -> Rẽ sang TRÁI (180°)
                        yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Left) + rotationOffset, cornerTurnDuration));
                        direction = ArrowDirection.Left;

                        yield return StartCoroutine(SmoothMoveTo(new Vector2(targetGateX, laneTop), sidelineMoveSpeed));
                    }

                    // Rẽ LÊN (90°) đâm thẳng vào Cổng thoát
                    yield return StartCoroutine(SmoothRotateTo(DirectionToAngle(ArrowDirection.Up) + rotationOffset, cornerTurnDuration));
                    direction = ArrowDirection.Up;
                }

                // Đi thẳng vào Cổng thoát
                yield return StartCoroutine(SmoothMoveTo(gatePos, sidelineMoveSpeed));
                break;
            }

            case ArrowDirection.Down:
            {
                // Đang ở làn Dưới, chọn đi vòng qua bên Phải hoặc Trái
                if (transform.position.x >= boardCenter.x)
                {
                    // Vòng qua bên Phải
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
                    // Vòng qua bên Trái
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
        }

        // 3. Kích hoạt hiệu ứng biến mất qua Cổng thoát
        onMoveFinished?.Invoke();
        yield return StartCoroutine(exitGate.PlayExitAnimationRoutine(this));
        isMoving = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(blockType))
        {
            blockType = "ArrowBlock";
        }

        if (freezableBlock == null)
        {
            freezableBlock = GetComponent<FreezableBlock>();
        }

        if (!autoSyncRotation) return;

        float currentZ = transform.eulerAngles.z;

        // 1. Nếu người dùng đổi Enum trên Inspector (Dropdown)
        if (direction != lastDirection)
        {
            ApplyRotationFromDirection();
            lastDirection = direction;
            lastRotationZ = transform.eulerAngles.z;
        }
        // 2. Nếu người dùng xoay GameObject trong Scene View bằng phím E / Transform
        else if (Mathf.Abs(Mathf.DeltaAngle(currentZ, lastRotationZ)) > 1f)
        {
            SyncDirectionFromRotation();
            lastDirection = direction;
            lastRotationZ = currentZ;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ mũi tên biểu diễn hướng di chuyển trong Scene View
        Vector2 dir = GetDirectionVector();
        float stepSize = GetStepSize();
        Vector3 start = transform.position;
        Vector3 end = start + (Vector3)(dir * stepSize);

        Gizmos.color = new Color(1f, 0.6f, 0f, 1f); // Màu cam nổi bật giống mũi tên
        Gizmos.DrawLine(start, end);

        // Vẽ chóp mũi tên
        Vector3 right = Quaternion.Euler(0, 0, 150) * dir * (stepSize * 0.3f);
        Vector3 left = Quaternion.Euler(0, 0, -150) * dir * (stepSize * 0.3f);
        Gizmos.DrawLine(end, end + right);
        Gizmos.DrawLine(end, end + left);

        // Vẽ ô dự kiến kế tiếp
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube(end, Vector3.one * stepSize * 0.8f);

        // Vẽ đường viền làn chạy Sideline trong Scene View để dễ quan sát vị trí
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
[CustomEditor(typeof(ArrowBlock))]
[CanEditMultipleObjects]
public class ArrowBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ArrowBlock block = (ArrowBlock)target;

        // Vẽ thanh chọn nhanh hướng di chuyển với 4 nút trực quan
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("⚡ Chọn nhanh hướng di chuyển:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = (block.direction == ArrowDirection.Left) ? Color.cyan : Color.white;
        if (GUILayout.Button("◄ TRÁI\n(180°)", GUILayout.Height(38)))
        {
            Undo.RecordObject(block.transform, "Change Arrow Direction Left");
            Undo.RecordObject(block, "Change Arrow Direction Left");
            block.SetDirection(ArrowDirection.Left);
            EditorUtility.SetDirty(block);
        }

        GUI.backgroundColor = (block.direction == ArrowDirection.Up) ? Color.cyan : Color.white;
        if (GUILayout.Button("▲ LÊN\n(90°)", GUILayout.Height(38)))
        {
            Undo.RecordObject(block.transform, "Change Arrow Direction Up");
            Undo.RecordObject(block, "Change Arrow Direction Up");
            block.SetDirection(ArrowDirection.Up);
            EditorUtility.SetDirty(block);
        }

        GUI.backgroundColor = (block.direction == ArrowDirection.Down) ? Color.cyan : Color.white;
        if (GUILayout.Button("▼ XUỐNG\n(270°)", GUILayout.Height(38)))
        {
            Undo.RecordObject(block.transform, "Change Arrow Direction Down");
            Undo.RecordObject(block, "Change Arrow Direction Down");
            block.SetDirection(ArrowDirection.Down);
            EditorUtility.SetDirty(block);
        }

        GUI.backgroundColor = (block.direction == ArrowDirection.Right) ? Color.cyan : Color.white;
        if (GUILayout.Button("► PHẢI\n(0°)", GUILayout.Height(38)))
        {
            Undo.RecordObject(block.transform, "Change Arrow Direction Right");
            Undo.RecordObject(block, "Change Arrow Direction Right");
            block.SetDirection(ArrowDirection.Right);
            EditorUtility.SetDirty(block);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Hiển thị nút test di chuyển khi đang Play Mode
        if (Application.isPlaying)
        {
            bool isFrozen = block.IsFrozen;
            if (isFrozen)
            {
                EditorGUILayout.HelpBox("🧊 Khối này đang BỊ ĐÓNG BĂNG (FreezableBlock.isFrozen = true). Chức năng di chuyển đã bị khóa!", MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(isFrozen);
            GUI.backgroundColor = isFrozen ? Color.gray : new Color(0.2f, 1f, 0.4f);
            if (GUILayout.Button(isFrozen ? "🔒 ĐANG BỊ KHÓA BĂNG (KHÔNG THỂ DI CHUYỂN)" : "▶ TEST CLICK (THỬ DI CHUYỂN)", GUILayout.Height(32)))
            {
                block.TriggerMove();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();
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
