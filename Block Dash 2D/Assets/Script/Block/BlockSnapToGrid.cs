using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlockSnapToGrid : MonoBehaviour
{
    [Header("Board Reference")]
    [Tooltip("Kéo GameObject Board chứa các Cell vào đây (Tự động tìm nếu để trống)")]
    public Transform boardTransform;

    [Header("Cell Settings")]
    [Tooltip("Kích thước mỗi Cell trong Unity World Units")]
    public float cellSize = 1f;

    [Header("Position Offset")]
    [Tooltip("Tinh chỉnh vị trí sau khi căn vào lưới")]
    public Vector2 positionOffset = Vector2.zero;

    private void Awake()
    {
        // Tự động tìm board nếu chưa được gán
        AutoDetectBoard();
    }

    private void Reset()
    {
        // Hàm này chạy ngay khi bạn Add Component mới vào Object trong Editor
        AutoDetectBoard();
    }

    /// <summary>
    /// Tự động tìm Board hoặc các component quản lý lưới trong Scene
    /// </summary>
    public void AutoDetectBoard()
    {
        if (boardTransform != null) return;

        // 1. Tìm theo tên phổ biến trong Scene
        GameObject boardObj = GameObject.Find("Board");
        if (boardObj == null) boardObj = GameObject.Find("BoardGrid");
        if (boardObj == null) boardObj = GameObject.Find("Grid");

        if (boardObj != null)
        {
            boardTransform = boardObj.transform;
            return;
        }

        // 2. Nếu không tìm thấy tên, tìm thông qua các script tạo bàn cờ (tương tự như ArrowBlock)
#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
        if (sideline != null) { boardTransform = sideline.transform; return; }

        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
        if (boardGen != null) { boardTransform = boardGen.transform; return; }

        MapSaver saver = FindFirstObjectByType<MapSaver>();
        if (saver != null) { boardTransform = saver.transform; return; }
#else
        SideLine sideline = FindObjectOfType<SideLine>();
        if (sideline != null) { boardTransform = sideline.transform; return; }

        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
        if (boardGen != null) { boardTransform = boardGen.transform; return; }

        MapSaver saver = FindObjectOfType<MapSaver>();
        if (saver != null) { boardTransform = saver.transform; return; }
#endif
    }

    [ContextMenu("Set Position")]
    public void SetPosition()
    {
        // Đảm bảo luôn tự động tìm board trước khi thực thi nếu chưa gán
        if (boardTransform == null)
        {
            AutoDetectBoard();
        }

        if (boardTransform == null)
        {
            Debug.LogWarning("Vẫn không tìm thấy Board Transform trong Scene! Hãy kiểm tra lại tên Object Board.");
            return;
        }

        // Tìm SpriteRenderer để lấy kích thước thực tế của Block.
        SpriteRenderer spriteRenderer =
            GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            Debug.LogWarning(
                "Không tìm thấy SpriteRenderer trên Block!"
            );
            return;
        }

        Transform[] allObjects =
            boardTransform.GetComponentsInChildren<Transform>(true);

        Transform nearestCell = null;
        float minDistance = float.MaxValue;

        Vector3 blockPosition = transform.position;

        // Tìm Cell gần tâm Block nhất.
        foreach (Transform cell in allObjects)
        {
            if (!cell.name.StartsWith("Cell_"))
                continue;

            float distance = (
                (Vector2)cell.position -
                (Vector2)blockPosition
            ).sqrMagnitude;

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestCell = cell;
            }
        }

        if (nearestCell == null)
        {
            Debug.LogWarning(
                "Không tìm thấy Cell nào trong Board!"
            );
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(transform, "Set Block Position");
        }
#endif

        // Lấy kích thước Block theo World Units.
        Bounds bounds = spriteRenderer.bounds;

        float blockWidth = bounds.size.x;
        float blockHeight = bounds.size.y;

        // Tính góc dưới bên trái của Block so với Transform.
        Vector3 blockBottomLeftOffset = new Vector3(
            bounds.min.x - transform.position.x,
            bounds.min.y - transform.position.y,
            0f
        );

        // Transform của Cell nằm ở tâm ô.
        // Chuyển sang góc dưới bên trái của Cell.
        Vector3 cellBottomLeft = new Vector3(
            nearestCell.position.x - cellSize * 0.5f,
            nearestCell.position.y - cellSize * 0.5f,
            blockPosition.z
        );

        // Đặt góc dưới bên trái Block trùng góc dưới bên trái Cell.
        Vector3 targetPosition = cellBottomLeft
            - blockBottomLeftOffset;

        targetPosition.x += positionOffset.x;
        targetPosition.y += positionOffset.y;
        targetPosition.z = blockPosition.z;

        transform.position = targetPosition;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(transform);
        }
#endif

        Debug.Log(
            $"Block đã căn vào {nearestCell.name}\n" +
            $"Kích thước Sprite: {blockWidth:F2} x {blockHeight:F2}\n" +
            $"Vị trí mới: {transform.position}"
        );
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BlockSnapToGrid))]
public class BlockSnapToGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        BlockSnapToGrid block = (BlockSnapToGrid)target;

        if (GUILayout.Button("Set Position", GUILayout.Height(35)))
        {
            block.SetPosition();
        }
    }
}
#endif
```[cite: 8]

### Cách cải tiến này hoạt động:
1. **Hàm `Reset()`**: Ngay khi bạn kéo thả một Prefab mới ra Scene hoặc Add component này vào một Object, Unity sẽ tự động kích hoạt hàm `Reset()` và tự tìm kiếm GameObject có tên `Board` (hoặc các component sinh bàn cờ như `BoardGenerator`, `SideLine`) để điền sẵn vào ô `Board Transform` cho bạn.
2. **Hàm `Awake()`**: Khi game chạy hoặc block được sinh ra (Instantiate), script sẽ tự động quét tìm board nếu ô `Board Transform` đang để trống.
3. **Tự động phòng hộ trong `SetPosition()`**: Nếu bạn quên gán, ngay khi bấm nút **Set Position**, code cũng sẽ tự động đi tìm board trước khi tính toán tọa độ mà không báo lỗi `Warning` gián đoạn nữa[cite: 8].