using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

public enum SidelineLanePosition
{
    [Tooltip("Căn CHÍNH GIỮA dải viền Sideline (Mặc định: cách đều mép bàn cờ và mép ngoài = borderThickness * stepSize / 2)")]
    CenterOfSideline = 0,

    [Tooltip("Nằm ở HÀNG NGOÀI CÙNG của Sideline (Lớp ngoài cùng = (borderThickness - 0.5) * stepSize)")]
    OuterRowOfSideline = 1,

    [Tooltip("Khoảng cách tùy chỉnh nhập tay (theo giá trị customLaneDistance)")]
    CustomDistance = 2
}

[SelectionBase]
[RequireComponent(typeof(Collider2D))]
public class ArrowBlock : BlockItem, IPointerDownHandler
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
    [Tooltip("Tự động tìm kiếm thông số Bàn cờ từ BoardGenerator hoặc MapSaver trong Scene")]
    public bool autoDetectBoard = true;

    [Tooltip("Kích thước mỗi ô Cell theo pixel (đồng bộ với Board: 36px)")]
    public float cellSizePixels = 36f;

    [Tooltip("Khoảng cách giữa các ô theo pixel (0px)")]
    public float spacingPixels = 0f;

    [Tooltip("Pixels Per Unit (100)")]
    public float pixelsPerUnit = 100f;

    [Tooltip("Chặn block dừng lại khi chạm tới mép ngoài cùng của Bàn cờ")]
    public bool clampToBoardBounds = true;
    public int boardColumns = 12;
    public int boardRows = 12;

    [Header("4. Phát hiện Vật cản (Collision)")]
    [Tooltip("LayerMask các vật thể được coi là vật cản chặn đường (Default, Block, Border...)")]
    public LayerMask obstacleLayers = ~0; // Mặc định quét mọi Layer

    [Tooltip("Bỏ qua các Collider dạng Trigger (chỉ bị chặn bởi Collider cứng)")]
    public bool ignoreTriggers = true;

    [Header("5. Di chuyển dọc Sideline ra Cổng thoát")]
    [Tooltip("Khi ra khỏi bàn cờ, khối block sẽ đi dọc theo đường viền Sideline để tìm đến Cổng thoát")]
    public bool navigateSidelineToExit = true;

    [Tooltip("Vị trí làn di chuyển trên Sideline:\n- CenterOfSideline: Căn chính giữa viền Sideline (cách đều mép bàn cờ và mép ngoài)\n- OuterRowOfSideline: Căn ở hàng ngoài cùng của Sideline\n- CustomDistance: Tự chỉnh khoảng cách")]
    public SidelineLanePosition sidelineLanePosition = SidelineLanePosition.CenterOfSideline;

    [Tooltip("Độ dày đường viền Sideline (số lớp ô bao quanh, mặc định: 2)")]
    public int borderThickness = 2;

    [Tooltip("Khoảng cách tùy chỉnh từ mép bàn cờ ra tâm làn Sideline (Unity Units, chỉ áp dụng khi chọn CustomDistance)")]
    public float customLaneDistance = 0.38f;

    [Tooltip("Tốc độ di chuyển dọc theo viền Sideline")]
    public float sidelineMoveSpeed = 16f;

    [Tooltip("Thời gian xoay góc khi đổi hướng ở các góc viền (giây)")]
    public float cornerTurnDuration = 0.08f;

    [Tooltip("Tọa độ tâm bàn cờ")]
    [HideInInspector] public Vector2 boardCenter = Vector2.zero;

    [Header("6. Sự kiện (Unity Events)")]
    public UnityEvent onMoveStart;
    public UnityEvent onMoveFinished;
    public UnityEvent onBlocked;

    [Header("7. Đóng băng (Freeze)")]
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
    private Collider2D myCollider;
    private int lastClickFrame = -1;

    [SerializeField, HideInInspector] private ArrowDirection lastDirection = ArrowDirection.Right;
    [SerializeField, HideInInspector] private float lastRotationZ = 0f;

    public bool IsMoving => isMoving;

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();

        if (freezableBlock == null)
        {
            freezableBlock = GetComponent<FreezableBlock>();
        }

        // Mặc định định danh loại block nếu chưa đặt
        if (string.IsNullOrEmpty(blockType))
        {
            blockType = "ArrowBlock";
        }
    }

    private void Start()
    {
        if (autoDetectBoard)
        {
            DetectBoardSettings();
        }

        // Cập nhật hướng di chuyển theo góc xoay thực tế khi load vào Scene
        SyncDirectionFromRotation();
    }

    // Hỗ trợ Unity EventSystem (UI / 2D Physics Raycaster)
    public void OnPointerDown(PointerEventData eventData)
    {
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }
        if (Time.frameCount == lastClickFrame) return;
        lastClickFrame = Time.frameCount;
        TriggerMove();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // Hỗ trợ New Input System Package
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClickAtScreenPosition(Mouse.current.position.ReadValue());
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            CheckClickAtScreenPosition(Touchscreen.current.primaryTouch.position.ReadValue());
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        // Hỗ trợ Legacy Input Manager
        if (Input.GetMouseButtonDown(0))
        {
            CheckClickAtScreenPosition(Input.mousePosition);
        }
#endif
    }

    private void CheckClickAtScreenPosition(Vector2 screenPosition)
    {
        if (Time.frameCount == lastClickFrame) return;

        Camera cam = Camera.main;
        if (cam != null && myCollider != null)
        {
            Vector2 mouseWorld = cam.ScreenToWorldPoint(screenPosition);
            if (myCollider.OverlapPoint(mouseWorld))
            {
                // Nếu đang dùng booster, gọi booster xử lý rồi dừng lại
                if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
                {
                    BoosterController.Instance.OnBlockClicked(this);
                    return;
                }

                lastClickFrame = Time.frameCount;
                TriggerMove();
            }
        }
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    // Sự kiện click chuẩn của Unity Legacy Input
    private void OnMouseDown()
    {
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }

        if (Time.frameCount == lastClickFrame) return;
        lastClickFrame = Time.frameCount;
        TriggerMove();
    }
#endif

    /// <summary>
    /// Tự động lấy thông số bàn cờ từ các script đã có trong Scene
    /// </summary>
    public void DetectBoardSettings()
    {
#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
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
            return;
        }

#if UNITY_2023_1_OR_NEWER
        ExitGateGenerator gateGen = FindFirstObjectByType<ExitGateGenerator>();
#else
        ExitGateGenerator gateGen = FindObjectOfType<ExitGateGenerator>();
#endif
        if (gateGen != null)
        {
            boardCenter = gateGen.startPosition;
            boardRows = gateGen.rows;
            boardColumns = gateGen.columns;
            cellSizePixels = gateGen.cellSizePixels;
            spacingPixels = gateGen.spacingPixels;
            pixelsPerUnit = gateGen.pixelsPerUnit;
            if (gateGen.borderThickness > 0)
            {
                borderThickness = gateGen.borderThickness;
            }
            return;
        }

#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
#endif
        if (boardGen != null)
        {
            boardCenter = boardGen.startPosition;
            boardRows = boardGen.rows;
            boardColumns = boardGen.columns;
            cellSizePixels = boardGen.cellSizePixels;
            spacingPixels = boardGen.spacingPixels;
            pixelsPerUnit = boardGen.pixelsPerUnit;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        MapSaver saver = FindFirstObjectByType<MapSaver>();
#else
        MapSaver saver = FindObjectOfType<MapSaver>();
#endif
        if (saver != null)
        {
            boardCenter = saver.startPosition;
            boardRows = saver.rows;
            boardColumns = saver.columns;
            cellSizePixels = saver.cellSizePixels;
            spacingPixels = saver.spacingPixels;
            pixelsPerUnit = saver.pixelsPerUnit;
        }
    }

    /// <summary>
    /// Lấy kích thước 1 ô cờ theo đơn vị Unity (World Units)
    /// </summary>
    public float GetStepSize()
    {
        float ppu = (pixelsPerUnit > 0) ? pixelsPerUnit : 100f;
        return (cellSizePixels + spacingPixels) / ppu;
    }

    /// <summary>
    /// Lấy khoảng cách từ mép bàn cờ ra tâm làn di chuyển trên Sideline (World Units)
    /// </summary>
    public float GetSidelineLaneDistance()
    {
        float stepSize = GetStepSize();
        int thickness = Mathf.Max(1, borderThickness);

        switch (sidelineLanePosition)
        {
            case SidelineLanePosition.CenterOfSideline:
                // Căn chính giữa dải viền Sideline
                // Ví dụ: thickness = 2 -> giữa viền = 2 * stepSize / 2f = 1.0 * stepSize
                // Khối block sẽ nằm lọt thỏm chính giữa dải viền, cách đều cả mép bàn cờ và mép ngoài
                return (thickness * stepSize) / 2f;

            case SidelineLanePosition.OuterRowOfSideline:
                // Căn ở hàng ngoài cùng của Sideline (Lớp ngoài cùng)
                // Ví dụ: thickness = 2 -> hàng 2 = (2 - 0.5f) * stepSize = 1.5 * stepSize
                return (thickness - 0.5f) * stepSize;

            case SidelineLanePosition.CustomDistance:
                return (customLaneDistance > 0f) ? customLaneDistance : (thickness * stepSize) / 2f;

            default:
                return (thickness * stepSize) / 2f;
        }
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

                    // Kiểm tra block bị húc trúng có đang bật Boom không
                    HitCountdownBoomBlock boomBlock = hit.GetComponentInParent<HitCountdownBoomBlock>();
                    if (boomBlock != null && boomBlock.hasBoom)
                    {
                        boomBlock.TakeHit(1);
                    }

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

    private Vector2Int GetGridPositionFromWorld(Vector2 worldPosition)
    {
        float stepSize = GetStepSize();

        int x = Mathf.RoundToInt(
            (worldPosition.x - boardCenter.x) / stepSize
            + (boardColumns - 1) / 2f
        );

        int y = Mathf.RoundToInt(
            (worldPosition.y - boardCenter.y) / stepSize
            + (boardRows - 1) / 2f
        );

        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Điều hướng khối block ra Sideline và chạy dọc theo viền tới Cổng thoát (luôn xoay theo hướng mũi tên)
    /// </summary>
    private IEnumerator NavigateAlongSidelineToGate(ExitGate exitGate, Vector2 initialLaneExitPos)
    {
        isMoving = true;

        // Đảm bảo Sprite luôn hiển thị nổi bật trên nền Sideline
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sortingOrder < 5)
        {
            sr.sortingOrder = 5;
        }

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

    /// <summary>
    /// Xoay mượt mà góc Z của Transform (đảm bảo mũi tên luôn hướng theo chiều di chuyển)
    /// </summary>
    private IEnumerator SmoothRotateTo(float targetAngleZ, float duration)
    {
        float startAngleZ = transform.eulerAngles.z;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentZ = Mathf.LerpAngle(startAngleZ, targetAngleZ, t);
            transform.rotation = Quaternion.Euler(0f, 0f, currentZ);
            yield return null;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, targetAngleZ);
    }

    /// <summary>
    /// Di chuyển mượt mà Transform đến vị trí đích
    /// </summary>
    private IEnumerator SmoothMoveTo(Vector3 targetPos, float speed)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                speed * Time.deltaTime
            );
            yield return null;
        }
        transform.position = targetPos;
    }

    /// <summary>
    /// Hiệu ứng nảy ngược lại một chút khi đâm trúng vật cản tạo cảm giác va chạm sống động
    /// </summary>
    private IEnumerator ImpactBounceRoutine(Vector2 dir, float bounceDistance)
    {
        Vector3 basePos = transform.position;
        Vector3 bounceBackPos = basePos - (Vector3)(dir * bounceDistance);

        float duration = 0.06f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(basePos, bounceBackPos, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(bounceBackPos, basePos, elapsed / duration);
            yield return null;
        }

        transform.position = basePos;
    }

    /// <summary>
    /// Hiệu ứng rung nhẹ khi bị kẹt không thể di chuyển
    /// </summary>
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