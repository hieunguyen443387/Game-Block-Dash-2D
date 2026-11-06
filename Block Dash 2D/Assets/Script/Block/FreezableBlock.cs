using UnityEngine;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
[DisallowMultipleComponent]
public class FreezableBlock : MonoBehaviour, IPointerDownHandler
{
    [Header("Trạng thái đóng băng")]
    [Tooltip("Tích chọn để bật lớp băng và khóa di chuyển của ArrowBlock. Bỏ chọn để mở khóa di chuyển.")]
    public bool isFrozen = false;

    [Header("Đối tượng con chứa hình băng")]
    [Tooltip("Object hiển thị lớp băng phủ lên block")]
    public GameObject iceOverlay;

    [Header("Liên kết ArrowBlock")]
    [Tooltip("Tham chiếu ArrowBlock (tự động tìm trên cùng GameObject nếu để trống)")]
    public ArrowBlock arrowBlock;

    private void Awake()
    {
        AutoConnect();
        UpdateFreezeState();
    }

    private void Start()
    {
        AutoConnect();
        UpdateFreezeState();
    }

    private void OnValidate()
    {
        AutoConnect();
        UpdateFreezeState();
    }

    private void Reset()
    {
        AutoConnect();
        UpdateFreezeState();
    }

    /// <summary>
    /// Tự động kết nối 2 chiều giữa FreezableBlock và ArrowBlock, tìm iceOverlay nếu chưa gán
    /// </summary>
    public void AutoConnect()
    {
        if (arrowBlock == null)
        {
            arrowBlock = GetComponent<ArrowBlock>();
        }

        if (arrowBlock != null && arrowBlock.freezableBlock == null)
        {
            arrowBlock.freezableBlock = this;
        }

        if (iceOverlay == null)
        {
            Transform t = transform.Find("IceOverlay");
            if (t == null) t = transform.Find("Ice");
            if (t == null) t = transform.Find("Ice Overlay");
            if (t != null) iceOverlay = t.gameObject;
        }
    }

    /// <summary>
    /// Cập nhật hiển thị lớp băng theo trạng thái isFrozen
    /// </summary>
    public void UpdateFreezeState()
    {
        if (iceOverlay != null)
        {
            // Bật/tắt trực tiếp object con
            iceOverlay.SetActive(isFrozen);
        }

        // Đảm bảo ArrowBlock có tham chiếu đến FreezableBlock
        if (arrowBlock != null && arrowBlock.freezableBlock != this)
        {
            arrowBlock.freezableBlock = this;
        }
    }

    /// <summary>
    /// Bật/tắt trạng thái đóng băng
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        UpdateFreezeState();
    }

    /// <summary>
    /// Làm tan băng (mở khóa di chuyển cho block)
    /// </summary>
    public void Unfreeze()
    {
        SetFrozen(false);
    }

    // Xử lý click nếu FreezableBlock được đặt ở object con khác với ArrowBlock
    public void OnPointerDown(PointerEventData eventData)
    {
        if (arrowBlock != null && arrowBlock.gameObject != gameObject)
        {
            arrowBlock.OnPointerDown(eventData);
        }
    }

    private void OnMouseDown()
    {
        if (arrowBlock != null && arrowBlock.gameObject != gameObject)
        {
            arrowBlock.TriggerMove();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FreezableBlock))]
public class FreezableBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FreezableBlock targetBlock = (FreezableBlock)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck())
        {
            targetBlock.AutoConnect();
            targetBlock.UpdateFreezeState();
            EditorUtility.SetDirty(targetBlock);
        }

        EditorGUILayout.Space(6);
        if (targetBlock.isFrozen)
        {
            EditorGUILayout.HelpBox("🧊 Block ĐANG BỊ ĐÓNG BĂNG:\n- Hiển thị lớp băng (Ice Overlay).\n- KHÓA tính năng click di chuyển của ArrowBlock.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG:\n- Ẩn lớp băng.\n- ArrowBlock có thể click di chuyển bình thường.", MessageType.Info);
        }

        // Nút toggle nhanh trong Editor
        EditorGUILayout.Space(2);
        string btnText = targetBlock.isFrozen ? "🔓 Mở Khóa Băng (Unfreeze)" : "🧊 Đóng Băng Khối Này (Freeze)";
        GUI.backgroundColor = targetBlock.isFrozen ? new Color(1f, 0.7f, 0.4f) : new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button(btnText, GUILayout.Height(30)))
        {
            Undo.RecordObject(targetBlock, "Toggle Freeze State");
            targetBlock.SetFrozen(!targetBlock.isFrozen);
            EditorUtility.SetDirty(targetBlock);
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif