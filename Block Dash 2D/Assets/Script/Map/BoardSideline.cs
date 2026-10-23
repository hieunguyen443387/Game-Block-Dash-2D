using UnityEngine;

public class BoardSideline : MonoBehaviour
{
    [Header("Board Settings (Đồng bộ với Board)")]
    [Tooltip("Số hàng của Board")]
    public int rows = 12;

    [Tooltip("Số cột của Board")]
    public int columns = 12;

    [Tooltip("Kích thước mỗi ô Cell trên Board theo pixels (36px)")]
    public float cellSizePixels = 36f;

    [Tooltip("Khoảng cách giữa các ô trên Board (pixels)")]
    public float spacingPixels = 0f;

    [Tooltip("Pixels Per Unit của Project / Sprite (100)")]
    public float pixelsPerUnit = 100f;

    [Tooltip("Tọa độ tâm của Board")]
    public Vector2 startPosition = Vector2.zero;

    [Header("Sideline Settings (Đường biên)")]
    [Tooltip("Gán Prefab Sideline HOẶC kéo trực tiếp Sprite Sideline vào ô bên dưới")]
    public GameObject sidelinePrefab;

    [Tooltip("Sprite của Sideline (dùng nếu bạn chưa tạo Prefab)")]
    public Sprite sidelineSprite;

    [Tooltip("Kích thước mỗi ô đường biên theo pixels (36px, bằng kích thước ô Board)")]
    public float sidelineSizePixels = 36f;

    [Tooltip("Có tạo 4 ô ở 4 góc viền không")]
    public bool includeCorners = true;

    [Tooltip("Tùy chọn Prefab/Sprite riêng cho 4 góc (nếu để trống sẽ dùng chung Sideline)")]
    public GameObject cornerPrefab;
    public Sprite cornerSprite;

    [Header("Rotation Settings (Góc xoay)")]
    [Tooltip("Tự động xoay Sideline theo từng cạnh (Trên: 0°, Phải: 90°, Dưới: 180°, Trái: 270°)")]
    public bool autoRotate = false;

    [Tooltip("Góc xoay bù trừ thêm nếu sprite vẽ theo hướng khác")]
    public float rotationOffset = 0f;

    [Header("Runtime")]
    [Tooltip("Tự động tạo đường biên khi chạy game (Start)")]
    public bool generateOnStart = false;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateSideline();
        }
    }

    [ContextMenu("Generate Sideline")]
    public void GenerateSideline()
    {
        if (sidelinePrefab == null && sidelineSprite == null)
        {
            Debug.LogError("Vui lòng gán 'Sideline Prefab' hoặc 'Sideline Sprite' trong Inspector!");
            return;
        }

        if (pixelsPerUnit <= 0 || rows <= 0 || columns <= 0 || sidelineSizePixels <= 0)
        {
            Debug.LogError("Các thông số kích thước Board hoặc Sideline không hợp lệ!");
            return;
        }

        // Xóa các ô Sideline cũ
        ClearOldSideline();

        // 1. Chuyển đổi sang đơn vị Unity (World Units)
        float cellSize = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;
        float step = cellSize + spacing;

        float boardWidth = columns * cellSize + (columns - 1) * spacing;
        float boardHeight = rows * cellSize + (rows - 1) * spacing;

        float sidelineSize = sidelineSizePixels / pixelsPerUnit;

        Vector2 center = startPosition;
        float halfBoardW = boardWidth / 2f;
        float halfBoardH = boardHeight / 2f;

        // 2. Tạo Sideline CẠNH TRÊN (Nằm ngay mép ngoài đỉnh của Board, không lấn vào Board)
        float yTop = center.y + halfBoardH + sidelineSize / 2f;
        Quaternion topRot = autoRotate ? Quaternion.Euler(0, 0, 0f + rotationOffset) : Quaternion.identity;

        for (int col = 0; col < columns; col++)
        {
            float x = center.x - halfBoardW + cellSize / 2f + col * step;
            CreateTile(new Vector2(x, yTop), topRot, $"Sideline_Top_{col}", sidelinePrefab, sidelineSprite);
        }

        // 3. Tạo Sideline CẠNH DƯỚI (Nằm ngay mép ngoài đáy của Board)
        float yBottom = center.y - halfBoardH - sidelineSize / 2f;
        Quaternion bottomRot = autoRotate ? Quaternion.Euler(0, 0, 180f + rotationOffset) : Quaternion.identity;

        for (int col = 0; col < columns; col++)
        {
            float x = center.x - halfBoardW + cellSize / 2f + col * step;
            CreateTile(new Vector2(x, yBottom), bottomRot, $"Sideline_Bottom_{col}", sidelinePrefab, sidelineSprite);
        }

        // 4. Tạo Sideline CẠNH TRÁI (Nằm ngay mép ngoài bên trái của Board)
        float xLeft = center.x - halfBoardW - sidelineSize / 2f;
        Quaternion leftRot = autoRotate ? Quaternion.Euler(0, 0, 90f + rotationOffset) : Quaternion.identity;

        for (int row = 0; row < rows; row++)
        {
            float y = center.y - halfBoardH + cellSize / 2f + row * step;
            CreateTile(new Vector2(xLeft, y), leftRot, $"Sideline_Left_{row}", sidelinePrefab, sidelineSprite);
        }

        // 5. Tạo Sideline CẠNH PHẢI (Nằm ngay mép ngoài bên phải của Board)
        float xRight = center.x + halfBoardW + sidelineSize / 2f;
        Quaternion rightRot = autoRotate ? Quaternion.Euler(0, 0, 270f + rotationOffset) : Quaternion.identity;

        for (int row = 0; row < rows; row++)
        {
            float y = center.y - halfBoardH + cellSize / 2f + row * step;
            CreateTile(new Vector2(xRight, y), rightRot, $"Sideline_Right_{row}", sidelinePrefab, sidelineSprite);
        }

        // 6. Tạo 4 Ô GÓC (Nếu bật includeCorners)
        if (includeCorners)
        {
            GameObject cornerPf = (cornerPrefab != null) ? cornerPrefab : sidelinePrefab;
            Sprite cornerSp = (cornerSprite != null) ? cornerSprite : sidelineSprite;

            CreateTile(new Vector2(xLeft, yTop), Quaternion.identity, "Sideline_Corner_TopLeft", cornerPf, cornerSp);
            CreateTile(new Vector2(xRight, yTop), Quaternion.identity, "Sideline_Corner_TopRight", cornerPf, cornerSp);
            CreateTile(new Vector2(xLeft, yBottom), Quaternion.identity, "Sideline_Corner_BottomLeft", cornerPf, cornerSp);
            CreateTile(new Vector2(xRight, yBottom), Quaternion.identity, "Sideline_Corner_BottomRight", cornerPf, cornerSp);
        }

        int totalCount = (columns * 2 + rows * 2) + (includeCorners ? 4 : 0);
        Debug.Log($"Đã tạo thành công Sideline bao quanh Board ({totalCount} ô 36px). Board ({columns}x{rows}) bên trong hoàn toàn không bị chiếm chỗ.");
    }

    [ContextMenu("Clear Sideline")]
    public void ClearOldSideline()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
                continue;
            }
#endif
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private GameObject CreateTile(Vector2 position, Quaternion rotation, string tileName, GameObject prefab, Sprite sprite)
    {
        GameObject tile;
        if (prefab != null)
        {
            tile = Instantiate(prefab, position, rotation, transform);
        }
        else if (sprite != null)
        {
            tile = new GameObject(tileName);
            tile.transform.SetParent(transform);
            tile.transform.position = position;
            tile.transform.rotation = rotation;

            SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
        }
        else
        {
            return null;
        }

        tile.name = tileName;

        // Tự động scale đúng 36px theo đơn vị Unity
        float targetSizeUnits = sidelineSizePixels / pixelsPerUnit;
        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();

        if (renderer != null && renderer.sprite != null)
        {
            float spriteW = renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit;
            float spriteH = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit;

            float scaleX = spriteW > 0 ? (targetSizeUnits / spriteW) : 1f;
            float scaleY = spriteH > 0 ? (targetSizeUnits / spriteH) : 1f;

            tile.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            tile.transform.localScale = new Vector3(targetSizeUnits, targetSizeUnits, 1f);
        }

        return tile;
    }
}
