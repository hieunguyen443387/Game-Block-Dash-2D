using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BoosterController : MonoBehaviour
{
    public static BoosterController Instance;

    [Header("References (GameScene)")]
    [Tooltip("Container chứa các Block trong GameScene (nếu để trống script sẽ tự tìm)")]
    public Transform blockContainer;

    // Lưu trữ booster đang được kích hoạt hiện tại
    private MonoBehaviour currentActiveBooster;
    private int lastActionFrame = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Tự động tìm BlockContainer nếu chưa gán
        EnsureBlockContainer();
    }

    private void EnsureBlockContainer()
    {
        if (blockContainer != null) return;

        GameObject containerObj = GameObject.Find("BlocksContainer");
        if (containerObj == null) containerObj = GameObject.Find("BlockContainer");

        if (containerObj != null)
        {
            blockContainer = containerObj.transform;
        }
        else
        {
            // Thử tìm từ MapLoader trong Scene
#if UNITY_2023_1_OR_NEWER
            MapLoader loader = FindFirstObjectByType<MapLoader>();
#else
            MapLoader loader = FindObjectOfType<MapLoader>();
#endif
            if (loader != null && loader.blocksContainer != null)
            {
                blockContainer = loader.blocksContainer;
            }
        }
    }

    private void Update()
    {
        // Khi không có booster nào bật, không làm gì cả
        if (currentActiveBooster == null) return;

        // Bắt sự kiện click / tap chuột khi đang kích hoạt Booster
        bool pointerDown = false;
        Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerDown = true;
            screenPos = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pointerDown = true;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            pointerDown = true;
            screenPos = Input.mousePosition;
        }
#endif

        if (pointerDown)
        {
            // Bỏ qua nếu người dùng click vào phần tử UI (ví dụ nút bấm UI khác)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 worldPoint = cam.ScreenToWorldPoint(screenPos);
                Collider2D hit = Physics2D.OverlapPoint(worldPoint);
                if (hit != null)
                {
                    BlockItem blockItem = hit.GetComponentInParent<BlockItem>();
                    if (blockItem != null)
                    {
                        OnBlockClicked(blockItem);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Đăng ký booster đang được chọn
    /// </summary>
    public void SetActiveBooster(MonoBehaviour booster)
    {
        currentActiveBooster = booster;
        Debug.Log($"<color=yellow>[BoosterController]</color> Đã kích hoạt Booster: {(booster != null ? booster.GetType().Name : "None")}. Hãy chọn block trên bàn cờ!");
    }

    /// <summary>
    /// Hủy kích hoạt booster hiện tại
    /// </summary>
    public void ClearActiveBooster()
    {
        currentActiveBooster = null;
        Debug.Log("<color=yellow>[BoosterController]</color> Đã tắt chế độ Booster.");
    }

    /// <summary>
    /// Kiểm tra xem có đang dùng booster nào yêu cầu click vào block hay không
    /// </summary>
    public bool IsAnyBoosterActive()
    {
        return currentActiveBooster != null;
    }

    /// <summary>
    /// Trả về booster hiện đang active
    /// </summary>
    public MonoBehaviour GetActiveBooster()
    {
        return currentActiveBooster;
    }

    /// <summary>
    /// Được gọi từ BlockItem khi người dùng click vào block trên màn hình
    /// </summary>
    public void OnBlockClicked(BlockItem clickedBlock)
    {
        if (currentActiveBooster == null || clickedBlock == null) return;

        // Tránh bị gọi lặp lại 2 lần trong cùng 1 frame (do vừa Update vừa EventSystem cùng bắt)
        if (Time.frameCount == lastActionFrame) return;
        lastActionFrame = Time.frameCount;

        // 1. Nếu booster hiện tại là RemoveBlockBooster
        if (currentActiveBooster is RemoveBlockBooster removeBooster)
        {
            removeBooster.Execute(clickedBlock);
        }
        // 2. Nếu booster hiện tại là ReverseBooster
        else if (currentActiveBooster is ReverseBooster reverseBooster)
        {
            reverseBooster.Execute(clickedBlock);
        }
        // 3. Nếu booster hiện tại là ShuffleBooster
        else if (currentActiveBooster is ShuffleBooster shuffleBooster)
        {
            shuffleBooster.Execute(clickedBlock);
        }
    }
}
