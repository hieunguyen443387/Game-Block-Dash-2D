
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlockSnapToGrid : MonoBehaviour
{
    [Header("Board Reference")]
    [Tooltip("Kéo GameObject Board chứa các Cell vào đây")]
    public Transform boardTransform;

    [Header("Cell Settings")]
    [Tooltip("Kích thước mỗi Cell trong Unity World Units")]
    public float cellSize = 1f;

    [Header("Position Offset")]
    [Tooltip("Tinh chỉnh vị trí sau khi căn vào lưới")]
    public Vector2 positionOffset = Vector2.zero;

    [ContextMenu("Set Position")]
    public void SetPosition()
    {
        if (boardTransform == null)
        {
            Debug.LogWarning("Chưa gán Board Transform!");
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
