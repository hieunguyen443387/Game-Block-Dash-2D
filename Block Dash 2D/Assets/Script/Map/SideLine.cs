
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SideLine : MonoBehaviour
{
    [Header("Board Settings")]
    public int rows = 12;
    public int columns = 12;
    public float cellSizePixels = 36f;
    public float spacingPixels = 0f;
    public float pixelsPerUnit = 100f;
    public Vector2 startPosition = Vector2.zero;

    [Header("Border Prefab Settings")]
    public GameObject borderPrefab;
    public GameObject cornerPrefab;
    public float borderSizePixels = 36f;
    public int borderThickness = 1;
    public bool addBoxCollider = false;
    public bool autoRotateByDirection = false;

    [Header("Runtime Settings")]
    public bool generateOnStart = false;

    private void Start()
    {
        if (Application.isPlaying && generateOnStart)
            GenerateBorder();
    }

    [ContextMenu("Generate Border")]
    public void GenerateBorder()
    {
        if (borderPrefab == null)
        {
            Debug.LogError("Chưa gán Border Prefab!");
            return;
        }

        if (rows <= 0 || columns <= 0 ||
            cellSizePixels <= 0 || borderSizePixels <= 0 ||
            pixelsPerUnit <= 0 || borderThickness <= 0)
        {
            Debug.LogError("Thông số cấu hình không hợp lệ!");
            return;
        }

        ClearOldBorder();

        float cellSize = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;
        float step = cellSize + spacing;
        float borderSize = borderSizePixels / pixelsPerUnit;

        float boardWidth = columns * cellSize + (columns - 1) * spacing;
        float boardHeight = rows * cellSize + (rows - 1) * spacing;

        float halfW = boardWidth / 2f;
        float halfH = boardHeight / 2f;

        Vector2 center = startPosition;

        // Viền trên và dưới
        for (int layer = 0; layer < borderThickness; layer++)
        {
            float yTop = center.y + halfH + borderSize / 2f
                         + layer * borderSize;
            float yBottom = center.y - halfH - borderSize / 2f
                            - layer * borderSize;

            for (int col = 0; col < columns; col++)
            {
                float x = center.x - halfW + cellSize / 2f + col * step;

                CreateBorderTile(
                    new Vector2(x, yTop), borderSize,
                    $"Border_Top_L{layer}_{col}",
                    borderPrefab,
                    Quaternion.Euler(0, 0, 0)
                );

                CreateBorderTile(
                    new Vector2(x, yBottom), borderSize,
                    $"Border_Bottom_L{layer}_{col}",
                    borderPrefab,
                    Quaternion.Euler(0, 0, 180)
                );
            }
        }

        // Viền trái và phải
        for (int layer = 0; layer < borderThickness; layer++)
        {
            float xLeft = center.x - halfW - borderSize / 2f
                          - layer * borderSize;
            float xRight = center.x + halfW + borderSize / 2f
                           + layer * borderSize;

            for (int row = 0; row < rows; row++)
            {
                float y = center.y - halfH + cellSize / 2f + row * step;

                CreateBorderTile(
                    new Vector2(xLeft, y), borderSize,
                    $"Border_Left_L{layer}_{row}",
                    borderPrefab,
                    Quaternion.Euler(0, 0, 90)
                );

                CreateBorderTile(
                    new Vector2(xRight, y), borderSize,
                    $"Border_Right_L{layer}_{row}",
                    borderPrefab,
                    Quaternion.Euler(0, 0, 270)
                );
            }
        }

        // Bốn góc
        GameObject activeCornerPrefab =
            cornerPrefab != null ? cornerPrefab : borderPrefab;

        for (int lx = 0; lx < borderThickness; lx++)
        {
            for (int ly = 0; ly < borderThickness; ly++)
            {
                float xLeft = center.x - halfW - borderSize / 2f
                              - lx * borderSize;
                float xRight = center.x + halfW + borderSize / 2f
                               + lx * borderSize;
                float yTop = center.y + halfH + borderSize / 2f
                             + ly * borderSize;
                float yBottom = center.y - halfH - borderSize / 2f
                                - ly * borderSize;

                CreateBorderTile(
                    new Vector2(xLeft, yTop), borderSize,
                    $"Corner_TopLeft_{lx}_{ly}",
                    activeCornerPrefab, Quaternion.identity
                );

                CreateBorderTile(
                    new Vector2(xRight, yTop), borderSize,
                    $"Corner_TopRight_{lx}_{ly}",
                    activeCornerPrefab, Quaternion.identity
                );

                CreateBorderTile(
                    new Vector2(xLeft, yBottom), borderSize,
                    $"Corner_BottomLeft_{lx}_{ly}",
                    activeCornerPrefab, Quaternion.identity
                );

                CreateBorderTile(
                    new Vector2(xRight, yBottom), borderSize,
                    $"Corner_BottomRight_{lx}_{ly}",
                    activeCornerPrefab, Quaternion.identity
                );
            }
        }

        int totalTiles =
            (columns * 2 + rows * 2) * borderThickness
            + 4 * borderThickness * borderThickness;

        Debug.Log($"Đã tạo đường biên gồm {totalTiles} ô.");
    }

    [ContextMenu("Clear Border")]
    public void ClearOldBorder()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (!child.name.StartsWith("Border_") &&
                !child.name.StartsWith("Corner_"))
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                continue;
            }
#endif
            Destroy(child.gameObject);
        }
    }

    private void CreateBorderTile(
        Vector2 position,
        float targetSize,
        string tileName,
        GameObject prefab,
        Quaternion rotation)
    {
        GameObject tile;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            tile = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab, transform
            );

            if (tile == null)
                tile = Instantiate(prefab, transform);

            Undo.RegisterCreatedObjectUndo(tile, "Generate Border");
        }
        else
        {
            tile = Instantiate(prefab, transform);
        }
#else
        tile = Instantiate(prefab, transform);
#endif

        tile.name = tileName;
        tile.transform.position = position;

        tile.transform.rotation = autoRotateByDirection
            ? rotation
            : Quaternion.identity;

        SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();

        if (sr != null && sr.sprite != null)
        {
            float spriteW = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            float spriteH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;

            float scaleX = spriteW > 0 ? targetSize / spriteW : 1f;
            float scaleY = spriteH > 0 ? targetSize / spriteH : 1f;

            tile.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            tile.transform.localScale =
                new Vector3(targetSize, targetSize, 1f);
        }

        if (addBoxCollider && tile.GetComponent<BoxCollider2D>() == null)
            tile.AddComponent<BoxCollider2D>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Không tự tạo hoặc xóa border khi chỉnh thông số.
        // Người dùng chủ động bấm nút Generate Border.
    }

    [CustomEditor(typeof(SideLine))]
    private class SideLineInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            SideLine sideLine = (SideLine)target;

            if (GUILayout.Button("Generate Border", GUILayout.Height(35)))
            {
                Undo.RegisterFullObjectHierarchyUndo(
                    sideLine.gameObject, "Generate Border"
                );

                sideLine.GenerateBorder();
                EditorUtility.SetDirty(sideLine);
            }

            if (GUILayout.Button("Clear Border", GUILayout.Height(30)))
            {
                Undo.RegisterFullObjectHierarchyUndo(
                    sideLine.gameObject, "Clear Border"
                );

                sideLine.ClearOldBorder();
                EditorUtility.SetDirty(sideLine);
            }
        }
    }
#endif
}
