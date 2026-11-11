using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class BlockSnapToGrid : MonoBehaviour
{
    [Header("Board Reference")]
    [Tooltip("Kéo GameObject Board chứa các Cell vào đây (Tự động tìm nếu để trống)")]
    public Transform boardTransform;

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

        Transform[] allCells = boardTransform.GetComponentsInChildren<Transform>(true);

        Transform nearestCell = null;
        float minDistance = float.MaxValue;
        Vector3 blockPosition = transform.position;

        Dictionary<string, Transform> cellLookup = new Dictionary<string, Transform>();

        foreach (Transform cell in allCells)
        {
            if (cell == null || !cell.name.StartsWith("Cell_"))
                continue;

            cellLookup[cell.name] = cell;

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

        // Tách tên cell gần nhất để lấy row và col (Format: Cell_Row_Col)
        string[] parts = nearestCell.name.Split('_');
        if (parts.Length < 3 || !int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int col))
        {
            Debug.LogWarning("Tên Cell không đúng định dạng Cell_Row_Col!");
            return;
        }

        // Xác định hướng dựa trên góc xoay hiện tại của Block (Z rotation)
        float rotZ = transform.eulerAngles.z;
        rotZ = (rotZ % 360f + 360f) % 360f;

        // Góc gần 0 hoặc 180 là nằm ngang (Horizontal), gần 90 hoặc 270 là nằm dọc (Vertical)
        bool isHorizontal = (rotZ >= 315f || rotZ < 45f) || (rotZ >= 135f && rotZ < 225f);

        Transform secondCell = null;

        if (isHorizontal)
        {
            string rightCellName = $"Cell_{row}_{col + 1}";
            string leftCellName = $"Cell_{row}_{col - 1}";

            Transform rightCell = cellLookup.ContainsKey(rightCellName) ? cellLookup[rightCellName] : null;
            Transform leftCell = cellLookup.ContainsKey(leftCellName) ? cellLookup[leftCellName] : null;

            if (rightCell != null && leftCell != null)
            {
                float distRight = Vector2.Distance(rightCell.position, blockPosition);
                float distLeft = Vector2.Distance(leftCell.position, blockPosition);
                secondCell = distRight < distLeft ? rightCell : leftCell;
            }
            else
            {
                secondCell = rightCell != null ? rightCell : leftCell;
            }
        }
        else
        {
            string topCellName = $"Cell_{row + 1}_{col}";
            string bottomCellName = $"Cell_{row - 1}_{col}";

            Transform topCell = cellLookup.ContainsKey(topCellName) ? cellLookup[topCellName] : null;
            Transform bottomCell = cellLookup.ContainsKey(bottomCellName) ? cellLookup[bottomCellName] : null;

            if (topCell != null && bottomCell != null)
            {
                float distTop = Vector2.Distance(topCell.position, blockPosition);
                float distBottom = Vector2.Distance(bottomCell.position, blockPosition);
                secondCell = distTop < distBottom ? topCell : bottomCell;
            }
            else
            {
                secondCell = topCell != null ? topCell : bottomCell;
            }
        }

        if (secondCell == null)
        {
            Debug.LogWarning("Không tìm thấy ô cell thứ 2 để ghép block 2 cell!");
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(transform, "Set Block Position");
        }
#endif

        // Tính vị trí tâm nằm chính giữa 2 ô cell (giống hệt thuật toán của BlockSpawner)
        Vector3 targetPosition = (nearestCell.position + secondCell.position) / 2f;

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

        Debug.Log($"Block đã căn giữa 2 cell: {nearestCell.name} và {secondCell.name}\nVị trí mới: {transform.position}");
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