using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BoosterController : MonoBehaviour
{
    public static BoosterController Instance;

    [Header("References (GameScene)")]
    public Transform blockContainer;

    private MonoBehaviour currentActiveBooster;
    private int lastActionFrame = -1;

    /// <summary>
    /// True nếu một block vừa được xử lý bởi booster trong frame hiện tại.
    /// Dùng để block không kích hoạt cơ chế di chuyển ngay sau khi booster vừa thực thi
    /// (tránh trường hợp cùng một cú click vừa dùng booster vừa làm block chạy).
    /// </summary>
    public bool IsBlockClickConsumedThisFrame => Time.frameCount == lastActionFrame;
    
    // Đánh dấu thời điểm bật booster để tránh nhầm lẫn click kích hoạt nút với click chọn block
    private int activationFrame = -1; 

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
        if (currentActiveBooster == null) return;

        if (Time.frameCount == activationFrame) return;

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

                        // Chỉ reset input legacy khi project có bật Input Manager cũ.
                        // Nếu project chỉ dùng Input System mới, gọi Input.ResetInputAxes() sẽ ném exception.
#if ENABLE_LEGACY_INPUT_MANAGER
                        Input.ResetInputAxes();
#endif
                    }
                }
            }
        }
    }

    public void SetActiveBooster(MonoBehaviour booster)
    {
        currentActiveBooster = booster;
        activationFrame = Time.frameCount; // Ghi nhận frame được bật
        Debug.Log($"<color=yellow>[BoosterController]</color> Đã kích hoạt Booster: {(booster != null ? booster.GetType().Name : "None")}.");
    }

    public void ClearActiveBooster()
    {
        currentActiveBooster = null;
        Debug.Log("<color=yellow>[BoosterController]</color> Đã tắt chế độ Booster.");
    }

    public bool IsAnyBoosterActive()
    {
        return currentActiveBooster != null;
    }

    public void OnBlockClicked(BlockItem clickedBlock)
    {
        if (currentActiveBooster == null || clickedBlock == null) return;

        if (Time.frameCount == lastActionFrame) return;
        lastActionFrame = Time.frameCount;

        if (currentActiveBooster is RemoveBlockBooster removeBooster)
        {
            removeBooster.Execute(clickedBlock);
        }
        else if (currentActiveBooster is ReverseBooster reverseBooster)
        {
            reverseBooster.Execute(clickedBlock);
        }
        else if (currentActiveBooster is ShuffleBooster shuffleBooster)
        {
            shuffleBooster.Execute(clickedBlock);
        }
    }
}