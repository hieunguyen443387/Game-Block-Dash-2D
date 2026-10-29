
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoardGenerator : MonoBehaviour
{
    [Header("Board Settings")]
    public int rows = 12;
    public int columns = 12;

    [Header("Cell Settings")]
    public GameObject cellPrefab;
    public float cellSizePixels = 36f;
    public float pixelsPerUnit = 100f;
    public float spacingPixels = 0f;

    [Header("Board Position")]
    public Vector2 startPosition = Vector2.zero;

    [Header("Checkerboard Style")]
    public bool useCheckerboard = false;
    public Color cellColorA = new Color(0.88f, 0.78f, 0.52f, 1f);
    public Color cellColorB = new Color(0.58f, 0.78f, 0.38f, 1f);

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        GenerateBoard(null);
    }

    public void GenerateBoard(System.Collections.Generic.List<CellData> cells)
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Chua gan Cell Prefab!");
            return;
        }

        if (rows <= 0 || columns <= 0 ||
            pixelsPerUnit <= 0 || cellSizePixels <= 0)
        {
            Debug.LogError("Thong so Board khong hop le!");
            return;
        }

        ClearOldBoard();

        float cellSize = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;
        float step = cellSize + spacing;

        float boardWidth =
            columns * cellSize + (columns - 1) * spacing;

        float boardHeight =
            rows * cellSize + (rows - 1) * spacing;

        Vector2 offset = new Vector2(
            -boardWidth / 2f + cellSize / 2f,
            -boardHeight / 2f + cellSize / 2f
        );

        if (cells != null && cells.Count > 0)
        {
            foreach (var c in cells)
            {
                SpawnCell(c.x, c.y, offset, step, cellSize);
            }
            Debug.Log($"Da tao board tuy bien voi {cells.Count} o.");
        }
        else
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    SpawnCell(col, row, offset, step, cellSize);
                }
            }
            Debug.Log("Da tao board " + rows + "x" + columns);
        }
    }

    public void ClearOldBoard()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(transform.GetChild(i).gameObject);
            else
                Destroy(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }
    }

    private void SpawnCell(int col, int row, Vector2 offset, float step, float cellSize)
    {
        Vector2 position = startPosition + offset + new Vector2(col * step, row * step);

        GameObject cell = Instantiate(
            cellPrefab,
            position,
            Quaternion.identity,
            transform
        );

        cell.name = "Cell_" + row + "_" + col;
        cell.transform.localScale = Vector3.one * cellSize;

        if (useCheckerboard)
        {
            SpriteRenderer sr = cell.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = (row + col) % 2 == 0 ? cellColorA : cellColorB;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RegisterCreatedObjectUndo(cell, "Generate Board");
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BoardGenerator))]
public class BoardGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        BoardGenerator board = (BoardGenerator)target;

        if (GUILayout.Button("Generate Board", GUILayout.Height(35)))
        {
            Undo.RegisterFullObjectHierarchyUndo(
                board.gameObject, "Generate Board");

            board.GenerateBoard();

            EditorUtility.SetDirty(board);
        }
    }
}
#endif
