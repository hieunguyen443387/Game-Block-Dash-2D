
using UnityEngine;

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

    private void Start()
    {
        GenerateBoard();
    }

    public void GenerateBoard()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Chua gan Cell Prefab!");
            return;
        }

        // Xoa cac Cell cu
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        // Chuyen pixel sang Unity Units
        float cellSize = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;
        float step = cellSize + spacing;

        // Tinh kich thuoc tong cua board
        float boardWidth = columns * cellSize + (columns - 1) * spacing;
        float boardHeight = rows * cellSize + (rows - 1) * spacing;

        // Can giua board quanh startPosition
        Vector2 offset = new Vector2(
            -boardWidth / 2f + cellSize / 2f,
            -boardHeight / 2f + cellSize / 2f
        );

        // Tao board
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                Vector2 position = startPosition + offset +
                    new Vector2(col * step, row * step);

                GameObject cell = Instantiate(
                    cellPrefab,
                    position,
                    Quaternion.identity,
                    transform
                );

                cell.name = "Cell_" + row + "_" + col;

                // Dat kich thuoc Cell
                cell.transform.localScale = Vector3.one * cellSize;
            }
        }

        Debug.Log(
            "Da tao board " + rows + "x" + columns +
            ", kich thuoc moi Cell: " + cellSizePixels + " pixels"
        );
    }
}
