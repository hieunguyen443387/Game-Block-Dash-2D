using UnityEngine;

public enum SurroundMode
{
    [Tooltip("Xếp nhiều viên 16x24 quanh viền (tự động tính số lượng viên sao cho vừa khít Board)")]
    TileSurroundByPixelSize,

    [Tooltip("Mỗi ô viền tương ứng 1 ô Cell của Board (12x12 viên, tự động co giãn vừa Cell)")]
    MatchBoardCells
}

public class BoardSurround : MonoBehaviour
{
    [Header("Board Settings (Đồng bộ với BoardGenerator)")]
    public int rows = 12;
    public int columns = 12;

    [Tooltip("Kích thước mỗi ô Cell trên Board theo pixels (ví dụ 36px)")]
    public float cellSizePixels = 36f;
    public float pixelsPerUnit = 100f;
    public float spacingPixels = 0f;

    [Header("Surround Prefab Settings (Kích thước Prefab Viền)")]
    public GameObject surroundPrefab;

    [Tooltip("Kích thước thực của Prefab Surround (Width: 16px, Height: 24px)")]
    public Vector2 surroundPrefabPixels = new Vector2(16f, 24f);

    [Tooltip("Tự động đọc kích thước pixel từ SpriteRenderer của Prefab nếu có")]
    public bool autoDetectSpriteSize = true;

    [Header("Surround Layout Settings")]
    [Tooltip("Chế độ xếp viền: Xếp các viên 16x24 hoặc co giãn theo ô Cell")]
    public SurroundMode surroundMode = SurroundMode.TileSurroundByPixelSize;

    [Tooltip("Độ dày viền (số lớp surround bao quanh)")]
    public int borderThickness = 1;

    [Tooltip("Tự động tạo viền khi bắt đầu chạy game")]
    public bool generateOnStart = false;

    [Header("Board Position")]
    public Vector2 startPosition = Vector2.zero;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateSurround();
        }
    }

    [ContextMenu("Generate Surround")]
    public void GenerateSurround()
    {
        if (surroundPrefab == null)
        {
            Debug.LogError("Chưa gán Surround Prefab!");
            return;
        }

        if (pixelsPerUnit <= 0 || rows <= 0 || columns <= 0 || borderThickness <= 0)
        {
            Debug.LogError("Thông số Board không hợp lệ!");
            return;
        }

        // Tự động đọc kích thước pixel nếu Prefab có SpriteRenderer
        if (autoDetectSpriteSize)
        {
            SpriteRenderer prefabSr = surroundPrefab.GetComponent<SpriteRenderer>();
            if (prefabSr != null && prefabSr.sprite != null)
            {
                surroundPrefabPixels = new Vector2(prefabSr.sprite.rect.width, prefabSr.sprite.rect.height);
            }
        }

        if (surroundPrefabPixels.x <= 0 || surroundPrefabPixels.y <= 0)
        {
            Debug.LogError("Kích thước Surround Prefab không hợp lệ! (phải > 0)");
            return;
        }

        // Xóa Surround cũ
        ClearOldSurrounds();

        // 1. Chuyển đổi kích thước Board sang Unity Units
        float cellW = cellSizePixels / pixelsPerUnit;
        float cellH = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;

        float boardWidth = columns * cellW + (columns - 1) * spacing;
        float boardHeight = rows * cellH + (rows - 1) * spacing;

        // 2. Kích thước viên Surround theo Unity Units
        float tileW = surroundPrefabPixels.x / pixelsPerUnit;
        float tileH = surroundPrefabPixels.y / pixelsPerUnit;

        int countX;
        int countY;
        float stepX;
        float stepY;
        Vector2 topBottomTileSize;
        Vector2 leftRightTileSize;
        Vector2 cornerTileSize;

        if (surroundMode == SurroundMode.TileSurroundByPixelSize)
        {
            // Tự động tính số lượng viên 16x24 sao cho vừa khít với kích thước Board
            // Với Board 12x36px = 432px:
            // Chiều ngang: 432 / 16 = 27 viên
            // Chiều dọc:   432 / 24 = 18 viên
            countX = Mathf.Max(1, Mathf.RoundToInt(boardWidth / tileW));
            countY = Mathf.Max(1, Mathf.RoundToInt(boardHeight / tileH));

            stepX = boardWidth / countX;
            stepY = boardHeight / countY;

            topBottomTileSize = new Vector2(stepX, tileH);
            leftRightTileSize = new Vector2(tileW, stepY);
            cornerTileSize = new Vector2(tileW, tileH);
        }
        else
        {
            // Mỗi ô viền tương ứng 1 ô Cell của Board (co giãn theo Cell)
            countX = columns;
            countY = rows;

            stepX = cellW + spacing;
            stepY = cellH + spacing;

            topBottomTileSize = new Vector2(cellW, cellH);
            leftRightTileSize = new Vector2(cellW, cellH);
            cornerTileSize = new Vector2(cellW, cellH);
        }

        Vector2 center = startPosition;
        float halfBoardW = boardWidth / 2f;
        float halfBoardH = boardHeight / 2f;

        // 3. Tạo viền Trên và Dưới
        for (int layer = 0; layer < borderThickness; layer++)
        {
            float yTop = center.y + halfBoardH + topBottomTileSize.y / 2f + layer * topBottomTileSize.y;
            float yBottom = center.y - halfBoardH - topBottomTileSize.y / 2f - layer * topBottomTileSize.y;

            for (int col = 0; col < countX; col++)
            {
                float x = center.x - halfBoardW + stepX / 2f + col * stepX;

                CreateSurround(new Vector2(x, yTop), topBottomTileSize);
                CreateSurround(new Vector2(x, yBottom), topBottomTileSize);
            }
        }

        // 4. Tạo viền Trái và Phải
        for (int layer = 0; layer < borderThickness; layer++)
        {
            float xLeft = center.x - halfBoardW - leftRightTileSize.x / 2f - layer * leftRightTileSize.x;
            float xRight = center.x + halfBoardW + leftRightTileSize.x / 2f + layer * leftRightTileSize.x;

            for (int row = 0; row < countY; row++)
            {
                float y = center.y - halfBoardH + stepY / 2f + row * stepY;

                CreateSurround(new Vector2(xLeft, y), leftRightTileSize);
                CreateSurround(new Vector2(xRight, y), leftRightTileSize);
            }
        }

        // 5. Tạo 4 góc viền
        for (int layerX = 0; layerX < borderThickness; layerX++)
        {
            for (int layerY = 0; layerY < borderThickness; layerY++)
            {
                float xCornerLeft = center.x - halfBoardW - cornerTileSize.x / 2f - layerX * cornerTileSize.x;
                float xCornerRight = center.x + halfBoardW + cornerTileSize.x / 2f + layerX * cornerTileSize.x;
                float yCornerTop = center.y + halfBoardH + cornerTileSize.y / 2f + layerY * cornerTileSize.y;
                float yCornerBottom = center.y - halfBoardH - cornerTileSize.y / 2f - layerY * cornerTileSize.y;

                CreateSurround(new Vector2(xCornerLeft, yCornerTop), cornerTileSize);
                CreateSurround(new Vector2(xCornerRight, yCornerTop), cornerTileSize);
                CreateSurround(new Vector2(xCornerLeft, yCornerBottom), cornerTileSize);
                CreateSurround(new Vector2(xCornerRight, yCornerBottom), cornerTileSize);
            }
        }

        Debug.Log($"Đã tạo Surround bao quanh Board. Kích thước Board: {boardWidth * pixelsPerUnit}x{boardHeight * pixelsPerUnit}px, " +
                  $"Viền ngang: {countX} ô ({topBottomTileSize.x * pixelsPerUnit}x{topBottomTileSize.y * pixelsPerUnit}px), " +
                  $"Viền dọc: {countY} ô ({leftRightTileSize.x * pixelsPerUnit}x{leftRightTileSize.y * pixelsPerUnit}px).");
    }

    [ContextMenu("Clear Surround")]
    public void ClearOldSurrounds()
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

    private GameObject CreateSurround(Vector2 position, Vector2 targetSizeUnits)
    {
        GameObject surround = Instantiate(
            surroundPrefab,
            position,
            Quaternion.identity,
            transform
        );

        surround.name = "Surround";

        // Tự động điều chỉnh localScale dựa trên kích thước thật của Sprite trong Prefab
        SpriteRenderer sr = surround.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            // Kích thước sprite trong Unity Units khi localScale = (1, 1, 1)
            float spriteWUnits = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            float spriteHUnits = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;

            float scaleX = spriteWUnits > 0 ? (targetSizeUnits.x / spriteWUnits) : 1f;
            float scaleY = spriteHUnits > 0 ? (targetSizeUnits.y / spriteHUnits) : 1f;

            surround.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            // Nếu không có SpriteRenderer (Quad hoặc 3D Object chuẩn 1x1)
            surround.transform.localScale = new Vector3(targetSizeUnits.x, targetSizeUnits.y, 1f);
        }

        return surround;
    }
}
