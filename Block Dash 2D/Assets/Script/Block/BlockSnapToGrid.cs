using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
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

    private void OnEnable()
    {
        if (boardTransform == null)
        {
            AutoDetectBoard();
        }
    }

    private void Reset()
    {
        AutoDetectBoard();
    }

    public void AutoDetectBoard()
    {
        // 1. Ưu tiên tìm BoardGenerator hoặc MapSaver trước
#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
        if (boardGen != null) { boardTransform = boardGen.transform; return; }

        MapSaver saver = FindFirstObjectByType<MapSaver>();
        if (saver != null) { boardTransform = saver.transform; return; }

        SideLine sideline = FindFirstObjectByType<SideLine>();
        if (sideline != null) { boardTransform = sideline.transform; return; }
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
        if (boardGen != null) { boardTransform = boardGen.transform; return; }

        MapSaver saver = FindObjectOfType<MapSaver>();
        if (saver != null) { boardTransform = saver.transform; return; }

        SideLine sideline = FindObjectOfType<SideLine>();
        if (sideline != null) { boardTransform = sideline.transform; return; }
#endif

        // 2. Tìm theo tên Object chứa chữ "Board" hoặc "Grid"
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if ((obj.name.Contains("Board") || obj.name.Contains("Grid")) && !obj.name.Contains("SideLine"))
            {
                if (obj.GetComponentInChildren<Transform>() != null)
                {
                    foreach (Transform child in obj.transform)
                    {
                        if (child.name.StartsWith("Cell_"))
                        {
                            boardTransform = obj.transform;
                            return;
                        }
                    }
                }
            }
        }
    }

    [ContextMenu("Set Position")]
    public void SetPosition()
    {
        if (boardTransform == null)
        {
            AutoDetectBoard();
        }

        if (boardTransform == null)
        {
            Debug.LogWarning("Vẫn không tìm thấy Board Transform trong Scene!");
            return;
        }

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            Debug.LogWarning("Không tìm thấy SpriteRenderer trên Block!");
            return;
        }

        Transform[] allObjects = boardTransform.GetComponentsInChildren<Transform>(true);

        Transform nearestCell = null;
        float minDistance = float.MaxValue;

        Vector3 blockPosition = transform.position;

        foreach (Transform cell in allObjects)
        {
            if (!cell.name.StartsWith("Cell_"))
                continue;

            float distance = ((Vector2)cell.position - (Vector2)blockPosition).sqrMagnitude;

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestCell = cell;
            }
        }

        if (nearestCell == null)
        {
            Debug.LogWarning("Không tìm thấy Cell nào có tên bắt đầu bằng 'Cell_' trong Board!");
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(transform, "Set Block Position");
        }
#endif

        Bounds bounds = spriteRenderer.bounds;

        float blockWidth = bounds.size.x;
        float blockHeight = bounds.size.y;

        Vector3 blockBottomLeftOffset = new Vector3(
            bounds.min.x - transform.position.x,
            bounds.min.y - transform.position.y,
            0f
        );

        Vector3 cellBottomLeft = new Vector3(
            nearestCell.position.x - cellSize * 0.5f,
            nearestCell.position.y - cellSize * 0.5f,
            blockPosition.z
        );

        Vector3 targetPosition = cellBottomLeft - blockBottomLeftOffset;

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

        Debug.Log($"Block đã căn vào {nearestCell.name}\nKích thước Sprite: {blockWidth:F2} x {blockHeight:F2}\nVị trí mới: {transform.position}");
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
