using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Base class chứa toàn bộ logic dùng chung cho các Block di chuyển được:
/// - Thông số bàn cờ (board settings)
/// - Phát hiện bàn cờ từ Scene (DetectBoardSettings)
/// - Tính toán kích thước ô (GetStepSize, GetSidelineLaneDistance)
/// - Hiệu ứng chuyển động (SmoothMoveTo, SmoothRotateTo)
/// - Hiệu ứng vật lý (ShakeRoutine, ImpactBounceRoutine)
/// - Xử lý click (OnPointerDown, CheckClickAtScreenPosition, OnMouseDown) - Đã tích hợp chặn click xuyên UI
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class MovableBlockBase : BlockItem, IPointerDownHandler
{
    // =========================================================
    // BOARD SETTINGS - Thông số bàn cờ dùng chung
    // =========================================================

    [Header("Bàn cờ & Grid Settings")]
    [Tooltip("Tự động tìm kiếm thông số Bàn cờ từ BoardGenerator hoặc MapSaver trong Scene")]
    public bool autoDetectBoard = true;

    [Tooltip("Kích thước mỗi ô Cell theo pixel (đồng bộ với Board: 36px)")]
    public float cellSizePixels = 38f;

    [Tooltip("Khoảng cách giữa các ô theo pixel (0px)")]
    public float spacingPixels = 0f;

    [Tooltip("Pixels Per Unit (100)")]
    public float pixelsPerUnit = 100f;

    [Tooltip("Chặn block dừng lại khi chạm tới mép ngoài cùng của Bàn cờ")]
    public int boardColumns = 12;
    public int boardRows = 12;

    [HideInInspector] public Vector2 boardCenter = Vector2.zero;

    // =========================================================
    // SIDELINE SETTINGS - Thông số đường viền
    // =========================================================

    [Header("Sideline Settings")]
    [Tooltip("Vị trí làn di chuyển trên Sideline")]
    public SidelineLanePosition sidelineLanePosition = SidelineLanePosition.CenterOfSideline;

    [Tooltip("Độ dày đường viền Sideline (số lớp ô bao quanh, mặc định: 2)")]
    public int borderThickness = 2;

    [Tooltip("Khoảng cách tùy chỉnh từ mép bàn cờ ra tâm làn Sideline (Unity Units)")]
    public float customLaneDistance = 0.38f;

    [Tooltip("Tốc độ di chuyển dọc theo viền Sideline")]
    public float sidelineMoveSpeed = 16f;

    [Tooltip("Thời gian xoay góc khi đổi hướng ở các góc viền (giây)")]
    public float cornerTurnDuration = 0.08f;

    // =========================================================
    // OBSTACLE DETECTION
    // =========================================================

    [Header("Phát hiện Vật cản (Collision)")]
    [Tooltip("LayerMask các vật thể được coi là vật cản chặn đường")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Bỏ qua các Collider dạng Trigger")]
    public bool ignoreTriggers = true;

    // =========================================================
    // PROTECTED FIELDS - Truy cập được bởi class con
    // =========================================================

    protected Collider2D myCollider;
    protected int lastClickFrame = -1;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    protected virtual void Awake()
    {
        myCollider = GetComponent<Collider2D>();
    }

    protected virtual void Start()
    {
        if (autoDetectBoard)
        {
            DetectBoardSettings();
        }
    }

    protected virtual void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClickAtScreenPosition(Mouse.current.position.ReadValue());
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            CheckClickAtScreenPosition(Touchscreen.current.primaryTouch.position.ReadValue());
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            CheckClickAtScreenPosition(Input.mousePosition);
        }
#endif
    }

    // =========================================================
    // CLICK HANDLING - Xử lý click / chạm (Đã tích hợp chặn UI)
    // =========================================================

    /// <summary>
    /// Xử lý click từ Unity EventSystem (UI / 2D Physics Raycaster).
    /// </summary>
    
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        // Chặn xử lý click nhiều lần trong cùng một frame
        if (Time.frameCount == lastClickFrame)
            return;

        // Đánh dấu ngay khi nhận click, kể cả khi đang dùng Booster
        lastClickFrame = Time.frameCount;

        if (BoosterController.Instance != null &&
            BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }

        OnBlockClicked();
    }

    
    protected void CheckClickAtScreenPosition(Vector2 screenPosition)
    {
    #if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.isPressed)
                {
                    int touchId = touch.touchId.ReadValue();

                    if (EventSystem.current != null &&
                        EventSystem.current.IsPointerOverGameObject(touchId))
                        return;
                }
            }
        }
    #endif

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (Time.frameCount == lastClickFrame)
            return;

        Camera cam = Camera.main;

        if (cam == null || myCollider == null)
            return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(screenPosition);

        if (!myCollider.OverlapPoint(mouseWorld))
            return;

        // Đánh dấu click trước khi xử lý Booster
        lastClickFrame = Time.frameCount;

        if (BoosterController.Instance != null &&
            BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }

        OnBlockClicked();
    }


#if ENABLE_LEGACY_INPUT_MANAGER
    
#if ENABLE_LEGACY_INPUT_MANAGER
    #if ENABLE_LEGACY_INPUT_MANAGER
    #if ENABLE_LEGACY_INPUT_MANAGER
    protected virtual void OnMouseDown()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        if (Time.frameCount == lastClickFrame)
            return;

        lastClickFrame = Time.frameCount;

        if (BoosterController.Instance != null &&
            BoosterController.Instance.IsAnyBoosterActive())
        {
            BoosterController.Instance.OnBlockClicked(this);
            return;
        }

        OnBlockClicked();
    }
#endif
    #endif
#endif

#endif

    /// <summary>
    /// Được gọi khi block nhận click hợp lệ.
    /// Class con phải override để thực hiện logic di chuyển của mình.
    /// </summary>
    protected abstract void OnBlockClicked();

    // =========================================================
    // BOARD SETTINGS - Phát hiện thông số bàn cờ
    // =========================================================

    /// <summary>
    /// Tự động lấy thông số bàn cờ từ các script đã có trong Scene
    /// (ưu tiên: SideLine > ExitGateGenerator > BoardGenerator > MapSaver)
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
            boardCenter      = sideline.startPosition;
            boardRows        = sideline.rows;
            boardColumns     = sideline.columns;
            cellSizePixels   = sideline.cellSizePixels;
            spacingPixels    = sideline.spacingPixels;
            pixelsPerUnit    = sideline.pixelsPerUnit;
            if (sideline.borderThickness > 0) borderThickness = sideline.borderThickness;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        ExitGateGenerator gateGen = FindFirstObjectByType<ExitGateGenerator>();
#else
        ExitGateGenerator gateGen = FindObjectOfType<ExitGateGenerator>();
#endif
        if (gateGen != null)
        {
            boardCenter      = gateGen.startPosition;
            boardRows        = gateGen.rows;
            boardColumns     = gateGen.columns;
            cellSizePixels   = gateGen.cellSizePixels;
            spacingPixels    = gateGen.spacingPixels;
            pixelsPerUnit    = gateGen.pixelsPerUnit;
            if (gateGen.borderThickness > 0) borderThickness = gateGen.borderThickness;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
#endif
        if (boardGen != null)
        {
            boardCenter      = boardGen.startPosition;
            boardRows        = boardGen.rows;
            boardColumns     = boardGen.columns;
            cellSizePixels   = boardGen.cellSizePixels;
            spacingPixels    = boardGen.spacingPixels;
            pixelsPerUnit    = boardGen.pixelsPerUnit;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        MapSaver saver = FindFirstObjectByType<MapSaver>();
#else
        MapSaver saver = FindObjectOfType<MapSaver>();
#endif
        if (saver != null)
        {
            boardCenter      = saver.startPosition;
            boardRows        = saver.rows;
            boardColumns     = saver.columns;
            cellSizePixels   = saver.cellSizePixels;
            spacingPixels    = saver.spacingPixels;
            pixelsPerUnit    = saver.pixelsPerUnit;
        }
    }

    // =========================================================
    // GRID UTILITIES - Tính toán ô lưới
    // =========================================================

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
                return (thickness * stepSize) / 2f;
            case SidelineLanePosition.OuterRowOfSideline:
                return (thickness - 0.5f) * stepSize;
            case SidelineLanePosition.CustomDistance:
                return (customLaneDistance > 0f) ? customLaneDistance : (thickness * stepSize) / 2f;
            default:
                return (thickness * stepSize) / 2f;
        }
    }

    /// <summary>
    /// Chuyển tọa độ World sang tọa độ Grid (cột, hàng)
    /// </summary>
    protected Vector2Int GetGridPositionFromWorld(Vector2 worldPosition)
    {
        float stepSize = GetStepSize();
        int x = Mathf.RoundToInt((worldPosition.x - boardCenter.x) / stepSize + (boardColumns - 1) / 2f);
        int y = Mathf.RoundToInt((worldPosition.y - boardCenter.y) / stepSize + (boardRows - 1) / 2f);
        return new Vector2Int(x, y);
    }

    // =========================================================
    // MOVEMENT COROUTINES - Hiệu ứng chuyển động dùng chung
    // =========================================================

    protected IEnumerator SmoothMoveTo(Vector3 targetPos, float speed)
    {
        while (Vector3.Distance(transform.position, targetPos) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
    }

    protected IEnumerator SmoothMoveTo(Vector2 target, float speed)
    {
        yield return StartCoroutine(SmoothMoveTo(new Vector3(target.x, target.y, transform.position.z), speed));
    }

    protected IEnumerator SmoothRotateTo(float targetAngleZ, float duration)
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

    protected IEnumerator ImpactBounceRoutine(Vector2 dir, float bounceDistance)
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

    protected IEnumerator ShakeRoutine(Vector2 dir, float shakeDistance)
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
    // SPRITE HELPERS
    // =========================================================

    protected void EnsureTopSortingOrder(int minOrder = 5)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sortingOrder < minOrder)
        {
            sr.sortingOrder = minOrder;
        }
    }
}