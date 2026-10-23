using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SidelineSurroundMode
{
    [Tooltip("Xếp nhiều viên nhỏ theo kích thước pixel (ví dụ 16x24px, tự động tính số viên chia đều vừa khít mép ngoài Sideline)")]
    TileByPixelSize,

    [Tooltip("Căn theo từng ô của Sideline (mỗi viên ngoài thẳng hàng với 1 ô của Sideline)")]
    MatchSidelineCells,

    [Tooltip("Giữ nguyên kích thước chuẩn của Sprite (Pixel-Perfect)")]
    ExactSpriteSize
}

[SelectionBase]
[ExecuteAlways]
public class SidelineSurround : MonoBehaviour
{
    [Header("1. Bàn cờ & Sideline (Tự động đồng bộ)")]
    [Tooltip("Tự động đọc thông số Bàn cờ và độ dày viền Sideline từ Scene")]
    public bool autoDetectFromScene = true;

    [Tooltip("Số hàng của Board bên trong")]
    public int boardRows = 12;

    [Tooltip("Số cột của Board bên trong")]
    public int boardColumns = 8;

    [Tooltip("Kích thước mỗi ô Cell trên Board theo pixels")]
    public float cellSizePixels = 38f;

    [Tooltip("Khoảng cách giữa các ô trên Board (pixels)")]
    public float spacingPixels = 0f;

    [Tooltip("Pixels Per Unit của Project / Sprite")]
    public float pixelsPerUnit = 38f;

    [Tooltip("Tọa độ tâm của Bàn cờ")]
    public Vector2 startPosition = Vector2.zero;

    [Tooltip("Độ dày đường viền Sideline (số lớp ô bao quanh Board, ví dụ: 2)")]
    public int sidelineThickness = 2;

    [Tooltip("Kích thước mỗi ô Sideline theo pixels (ví dụ: 38px)")]
    public float sidelineSizePixels = 38f;

    [Header("2. Surround Prefab & Sprite (Viền ngoài cùng)")]
    [Tooltip("Prefab của viên Surround ngoài cùng")]
    public GameObject surroundPrefab;

    [Tooltip("Sprite của viên Surround (dùng khi bạn chưa tạo Prefab, chỉ cần kéo Sprite vào đây)")]
    public Sprite surroundSprite;

    [Tooltip("Kích thước thực của 1 viên Surround theo pixels (ví dụ: 16px x 24px)")]
    public Vector2 surroundPrefabPixels = new Vector2(16f, 24f);

    [Tooltip("Tự động đọc kích thước pixel từ Sprite của Prefab nếu có")]
    public bool autoDetectSpriteSize = true;

    [Tooltip("Tùy chọn Prefab riêng cho 4 góc ngoài (nếu để trống sẽ dùng chung surroundPrefab)")]
    public GameObject cornerPrefab;

    [Tooltip("Tùy chọn Sprite riêng cho 4 góc ngoài")]
    public Sprite cornerSprite;

    [Header("3. Chế độ sắp xếp (Layout)")]
    [Tooltip("Chế độ sắp xếp viền Surround quanh Sideline")]
    public SidelineSurroundMode surroundMode = SidelineSurroundMode.TileByPixelSize;

    [Tooltip("Độ dày viền Surround (số lớp bao quanh ngoài cùng, mặc định: 1)")]
    [Range(1, 5)]
    public int surroundThickness = 1;

    [Tooltip("Tự động xoay viên viền theo từng cạnh (Trên: 0°, Phải: 90°, Dưới: 180°, Trái: 270°)")]
    public bool autoRotateByDirection = false;

    [Tooltip("Góc bù trừ thêm nếu sprite vẽ theo hướng khác")]
    public float rotationOffset = 0f;

    [Header("4. Cổng thoát (Exit Gate Opening)")]
    [Tooltip("Chừa khoảng trống tại vị trí Cổng thoát ở cạnh trên (không đặt viền chắn ngang cổng)")]
    public bool leaveGateOpening = true;

    [Tooltip("Khoảng nới rộng lối mở cổng thoát (Unity Units). Mặc định là 0 để viền Surround chạm sát mép ảnh Cổng 100%, không bị hở khe")]
    public float gateOpeningMargin = 0f;

    [Header("5. Hiển thị & Vật lý (Sorting & Collider)")]
    [Tooltip("Sorting Layer cho các viên viền")]
    public string sortingLayerName = "Default";

    [Tooltip("Sorting Order cho viền ngoài (đặt nhỏ hơn block để không che khuất block)")]
    public int sortingOrder = -1;

    [Tooltip("Tự động thêm BoxCollider2D vào mỗi viên để tạo tường cứng bao bọc ngoài cùng")]
    public bool addBoxCollider = false;

    [Header("6. Runtime Settings")]
    [Tooltip("Tự động tạo viền Surround khi bắt đầu chạy game (Start)")]
    public bool generateOnStart = false;

    private void Start()
    {
        if (Application.isPlaying && generateOnStart)
        {
            GenerateSurround();
        }
    }

    /// <summary>
    /// Tự động lấy các thông số bàn cờ và độ dày viền Sideline từ các script trong Scene
    /// </summary>
    [ContextMenu("Auto Detect From Scene")]
    public void AutoDetectSettings()
    {
#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline != null)
        {
            boardRows = sideline.rows;
            boardColumns = sideline.columns;
            cellSizePixels = sideline.cellSizePixels;
            spacingPixels = sideline.spacingPixels;
            pixelsPerUnit = (sideline.pixelsPerUnit > 0) ? sideline.pixelsPerUnit : 38f;
            startPosition = sideline.startPosition;
            sidelineThickness = Mathf.Max(1, sideline.borderThickness);
            sidelineSizePixels = (sideline.borderSizePixels > 0) ? sideline.borderSizePixels : cellSizePixels;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
#endif
        if (boardGen != null)
        {
            boardRows = boardGen.rows;
            boardColumns = boardGen.columns;
            cellSizePixels = boardGen.cellSizePixels;
            spacingPixels = boardGen.spacingPixels;
            pixelsPerUnit = (boardGen.pixelsPerUnit > 0) ? boardGen.pixelsPerUnit : 38f;
            startPosition = boardGen.startPosition;
        }

#if UNITY_2023_1_OR_NEWER
        ExitGateGenerator gateGen = FindFirstObjectByType<ExitGateGenerator>();
#else
        ExitGateGenerator gateGen = FindObjectOfType<ExitGateGenerator>();
#endif
        if (gateGen != null && gateGen.borderThickness > 0)
        {
            sidelineThickness = gateGen.borderThickness;
        }
    }

    /// <summary>
    /// Sinh toàn bộ viền Surround bao bọc quanh dải Sideline
    /// </summary>
    [ContextMenu("Generate Sideline Surround")]
    public void GenerateSurround()
    {
        if (surroundPrefab == null && surroundSprite == null)
        {
            Debug.LogError("[SidelineSurround] Vui lòng gán 'Surround Prefab' hoặc 'Surround Sprite' trong Inspector!");
            return;
        }

        if (autoDetectFromScene)
        {
            AutoDetectSettings();
        }

        float ppu = (pixelsPerUnit > 0) ? pixelsPerUnit : 38f;
        if (ppu <= 0 || boardRows <= 0 || boardColumns <= 0 || surroundThickness <= 0)
        {
            Debug.LogError("[SidelineSurround] Các thông số kích thước Board hoặc Surround không hợp lệ!");
            return;
        }

        // Tự động đọc kích thước Sprite nếu có
        if (autoDetectSpriteSize)
        {
            Sprite activeSprite = surroundSprite;
            if (activeSprite == null && surroundPrefab != null)
            {
                SpriteRenderer sr = surroundPrefab.GetComponent<SpriteRenderer>();
                if (sr != null) activeSprite = sr.sprite;
            }

            if (activeSprite != null)
            {
                surroundPrefabPixels = new Vector2(activeSprite.rect.width, activeSprite.rect.height);
            }
        }

        if (surroundPrefabPixels.x <= 0 || surroundPrefabPixels.y <= 0)
        {
            surroundPrefabPixels = new Vector2(16f, 24f);
        }

        // Xóa viền cũ
        ClearOldSurrounds();

        // 1. Kích thước Board bên trong (Unity Units)
        float cellW = cellSizePixels / ppu;
        float cellH = cellSizePixels / ppu;
        float spacing = spacingPixels / ppu;
        float boardWidth = boardColumns * cellW + (boardColumns - 1) * spacing;
        float boardHeight = boardRows * cellH + (boardRows - 1) * spacing;

        // 2. Kích thước dải Sideline bao quanh Board
        float sidelineUnitSize = (sidelineSizePixels > 0 ? sidelineSizePixels : cellSizePixels) / ppu;
        float halfSidelineW = boardWidth / 2f + (sidelineThickness * sidelineUnitSize);
        float halfSidelineH = boardHeight / 2f + (sidelineThickness * sidelineUnitSize);
        float totalSidelineW = halfSidelineW * 2f;
        float totalSidelineH = halfSidelineH * 2f;

        // 3. Kích thước 1 viên Surround theo Unity Units
        float tileW = surroundPrefabPixels.x / ppu;
        float tileH = surroundPrefabPixels.y / ppu;

        int countX;
        int countY;
        float stepX;
        float stepY;
        Vector2 topBottomTileSize;
        Vector2 leftRightTileSize;
        Vector2 cornerTileSize;

        switch (surroundMode)
        {
            case SidelineSurroundMode.MatchSidelineCells:
                // Căn chuẩn theo từng ô của Sideline
                countX = boardColumns + (2 * sidelineThickness);
                countY = boardRows + (2 * sidelineThickness);
                stepX = sidelineUnitSize;
                stepY = sidelineUnitSize;
                topBottomTileSize = new Vector2(sidelineUnitSize, tileH);
                leftRightTileSize = new Vector2(tileW, sidelineUnitSize);
                cornerTileSize = new Vector2(tileW, tileH);
                break;

            case SidelineSurroundMode.ExactSpriteSize:
                // Giữ nguyên kích thước pixel chuẩn của Sprite
                stepX = tileW;
                stepY = tileH;
                countX = Mathf.Max(1, Mathf.CeilToInt(totalSidelineW / tileW));
                countY = Mathf.Max(1, Mathf.CeilToInt(totalSidelineH / tileH));
                topBottomTileSize = new Vector2(tileW, tileH);
                leftRightTileSize = new Vector2(tileW, tileH);
                cornerTileSize = new Vector2(tileW, tileH);
                break;

            case SidelineSurroundMode.TileByPixelSize:
            default:
                // Tự động tính số lượng viên chia đều vừa khít dải Sideline
                countX = Mathf.Max(1, Mathf.RoundToInt(totalSidelineW / tileW));
                countY = Mathf.Max(1, Mathf.RoundToInt(totalSidelineH / tileH));
                stepX = totalSidelineW / countX;
                stepY = totalSidelineH / countY;
                topBottomTileSize = new Vector2(stepX, tileH);
                leftRightTileSize = new Vector2(tileW, stepY);
                cornerTileSize = new Vector2(tileW, tileH);
                break;
        }

        Vector2 center = startPosition;

        float sidelineLeftX = center.x - halfSidelineW;
        float sidelineRightX = center.x + halfSidelineW;

        // Lấy thông tin cổng thoát để chừa lối nếu cần
        bool hasGateOpening = false;
        float gateMinX = 0f;
        float gateMaxX = 0f;

        if (leaveGateOpening)
        {
#if UNITY_2023_1_OR_NEWER
            ExitGate gate = FindFirstObjectByType<ExitGate>();
#else
            ExitGate gate = FindObjectOfType<ExitGate>();
#endif
            if (gate != null)
            {
                hasGateOpening = true;
                SpriteRenderer gateSr = gate.GetComponent<SpriteRenderer>();
                Collider2D gateCol = gate.GetComponent<Collider2D>();

                if (gateSr != null && gateSr.sprite != null)
                {
                    // Lấy chính xác mép trái/phải hiển thị thực tế của Sprite cổng
                    gateMinX = gateSr.bounds.min.x - gateOpeningMargin;
                    gateMaxX = gateSr.bounds.max.x + gateOpeningMargin;
                }
                else if (gateCol != null)
                {
                    gateMinX = gateCol.bounds.min.x - gateOpeningMargin;
                    gateMaxX = gateCol.bounds.max.x + gateOpeningMargin;
                }
                else
                {
                    float gateCenterX = gate.transform.position.x;
                    float gateWidth = 2f * cellW; // Mặc định 2 ô
                    gateMinX = gateCenterX - (gateWidth / 2f) - gateOpeningMargin;
                    gateMaxX = gateCenterX + (gateWidth / 2f) + gateOpeningMargin;
                }

                // Giới hạn trong phạm vi viền Sideline
                gateMinX = Mathf.Clamp(gateMinX, sidelineLeftX, sidelineRightX);
                gateMaxX = Mathf.Clamp(gateMaxX, sidelineLeftX, sidelineRightX);
            }
        }

        int totalTiles = 0;

        // 4. Tạo viền TRÊN và DƯỚI
        for (int layer = 0; layer < surroundThickness; layer++)
        {
            float yTop = center.y + halfSidelineH + topBottomTileSize.y / 2f + (layer * topBottomTileSize.y);
            float yBottom = center.y - halfSidelineH - topBottomTileSize.y / 2f - (layer * topBottomTileSize.y);

            Quaternion topRot = autoRotateByDirection ? Quaternion.Euler(0, 0, 0f + rotationOffset) : Quaternion.identity;
            Quaternion bottomRot = autoRotateByDirection ? Quaternion.Euler(0, 0, 180f + rotationOffset) : Quaternion.identity;

            // 4a. Tạo viền TRÊN: Nếu có Cổng thoát thì chia làm 2 đoạn (Trái và Phải) ôm khít 100% vào 2 mép cổng, không để hở khe
            if (hasGateOpening && (gateMaxX > gateMinX))
            {
                // Đoạn TRÁI: từ mép ngoài Sideline bên trái (sidelineLeftX) đến sát mép trái cổng (gateMinX)
                float leftSpan = gateMinX - sidelineLeftX;
                if (leftSpan > 0.001f)
                {
                    if (surroundMode == SidelineSurroundMode.ExactSpriteSize)
                    {
                        int countLeft = Mathf.Max(1, Mathf.FloorToInt(leftSpan / tileW));
                        Vector2 sizeLeft = new Vector2(tileW, topBottomTileSize.y);
                        for (int col = 0; col < countLeft; col++)
                        {
                            float x = gateMinX - tileW / 2f - (col * tileW);
                            CreateTile(new Vector2(x, yTop), topRot, sizeLeft, $"Surround_Top_L{layer}_Left_{col}", surroundPrefab, surroundSprite);
                            totalTiles++;
                        }
                    }
                    else
                    {
                        int countLeft = Mathf.Max(1, Mathf.RoundToInt(leftSpan / tileW));
                        float stepLeft = leftSpan / countLeft;
                        Vector2 sizeLeft = new Vector2(stepLeft, topBottomTileSize.y);
                        for (int col = 0; col < countLeft; col++)
                        {
                            float x = sidelineLeftX + stepLeft / 2f + (col * stepLeft);
                            CreateTile(new Vector2(x, yTop), topRot, sizeLeft, $"Surround_Top_L{layer}_Left_{col}", surroundPrefab, surroundSprite);
                            totalTiles++;
                        }
                    }
                }

                // Đoạn PHẢI: từ sát mép phải cổng (gateMaxX) đến mép ngoài Sideline bên phải (sidelineRightX)
                float rightSpan = sidelineRightX - gateMaxX;
                if (rightSpan > 0.001f)
                {
                    if (surroundMode == SidelineSurroundMode.ExactSpriteSize)
                    {
                        int countRight = Mathf.Max(1, Mathf.FloorToInt(rightSpan / tileW));
                        Vector2 sizeRight = new Vector2(tileW, topBottomTileSize.y);
                        for (int col = 0; col < countRight; col++)
                        {
                            float x = gateMaxX + tileW / 2f + (col * tileW);
                            CreateTile(new Vector2(x, yTop), topRot, sizeRight, $"Surround_Top_L{layer}_Right_{col}", surroundPrefab, surroundSprite);
                            totalTiles++;
                        }
                    }
                    else
                    {
                        int countRight = Mathf.Max(1, Mathf.RoundToInt(rightSpan / tileW));
                        float stepRight = rightSpan / countRight;
                        Vector2 sizeRight = new Vector2(stepRight, topBottomTileSize.y);
                        for (int col = 0; col < countRight; col++)
                        {
                            float x = gateMaxX + stepRight / 2f + (col * stepRight);
                            CreateTile(new Vector2(x, yTop), topRot, sizeRight, $"Surround_Top_L{layer}_Right_{col}", surroundPrefab, surroundSprite);
                            totalTiles++;
                        }
                    }
                }
            }
            else
            {
                // Không có cổng: phủ kín viền trên toàn bộ chiều ngang
                for (int col = 0; col < countX; col++)
                {
                    float x = sidelineLeftX + stepX / 2f + (col * stepX);
                    CreateTile(new Vector2(x, yTop), topRot, topBottomTileSize, $"Surround_Top_L{layer}_{col}", surroundPrefab, surroundSprite);
                    totalTiles++;
                }
            }

            // 4b. Tạo viền DƯỚI (luôn liền mạch toàn bộ chiều ngang)
            for (int col = 0; col < countX; col++)
            {
                float x = sidelineLeftX + stepX / 2f + (col * stepX);
                CreateTile(new Vector2(x, yBottom), bottomRot, topBottomTileSize, $"Surround_Bottom_L{layer}_{col}", surroundPrefab, surroundSprite);
                totalTiles++;
            }
        }

        // 5. Tạo viền TRÁI và PHẢI
        for (int layer = 0; layer < surroundThickness; layer++)
        {
            float xLeft = center.x - halfSidelineW - leftRightTileSize.x / 2f - (layer * leftRightTileSize.x);
            float xRight = center.x + halfSidelineW + leftRightTileSize.x / 2f + (layer * leftRightTileSize.x);

            Quaternion leftRot = autoRotateByDirection ? Quaternion.Euler(0, 0, 90f + rotationOffset) : Quaternion.identity;
            Quaternion rightRot = autoRotateByDirection ? Quaternion.Euler(0, 0, 270f + rotationOffset) : Quaternion.identity;

            for (int row = 0; row < countY; row++)
            {
                float y = center.y - halfSidelineH + stepY / 2f + (row * stepY);

                CreateTile(new Vector2(xLeft, y), leftRot, leftRightTileSize, $"Surround_Left_L{layer}_{row}", surroundPrefab, surroundSprite);
                CreateTile(new Vector2(xRight, y), rightRot, leftRightTileSize, $"Surround_Right_L{layer}_{row}", surroundPrefab, surroundSprite);
                totalTiles += 2;
            }
        }

        // 6. Tạo 4 GÓC của Surround
        GameObject activeCornerPrefab = (cornerPrefab != null) ? cornerPrefab : surroundPrefab;
        Sprite activeCornerSprite = (cornerSprite != null) ? cornerSprite : surroundSprite;

        for (int layerX = 0; layerX < surroundThickness; layerX++)
        {
            for (int layerY = 0; layerY < surroundThickness; layerY++)
            {
                float xCornerLeft = center.x - halfSidelineW - cornerTileSize.x / 2f - (layerX * cornerTileSize.x);
                float xCornerRight = center.x + halfSidelineW + cornerTileSize.x / 2f + (layerX * cornerTileSize.x);
                float yCornerTop = center.y + halfSidelineH + cornerTileSize.y / 2f + (layerY * cornerTileSize.y);
                float yCornerBottom = center.y - halfSidelineH - cornerTileSize.y / 2f - (layerY * cornerTileSize.y);

                CreateTile(new Vector2(xCornerLeft, yCornerTop), Quaternion.identity, cornerTileSize, $"Corner_TopLeft_{layerX}_{layerY}", activeCornerPrefab, activeCornerSprite);
                CreateTile(new Vector2(xCornerRight, yCornerTop), Quaternion.identity, cornerTileSize, $"Corner_TopRight_{layerX}_{layerY}", activeCornerPrefab, activeCornerSprite);
                CreateTile(new Vector2(xCornerLeft, yCornerBottom), Quaternion.identity, cornerTileSize, $"Corner_BottomLeft_{layerX}_{layerY}", activeCornerPrefab, activeCornerSprite);
                CreateTile(new Vector2(xCornerRight, yCornerBottom), Quaternion.identity, cornerTileSize, $"Corner_BottomRight_{layerX}_{layerY}", activeCornerPrefab, activeCornerSprite);
                totalTiles += 4;
            }
        }

        Debug.Log($"<color=cyan>[SidelineSurround]</color> Đã tạo thành công {totalTiles} viên Surround bao bọc toàn bộ mép ngoài Sideline! " +
                  $"Kích thước Sideline: {totalSidelineW * ppu:F0}x{totalSidelineH * ppu:F0}px ({totalSidelineW:F2}x{totalSidelineH:F2} Units).");
    }

    /// <summary>
    /// Xóa toàn bộ viền Surround cũ
    /// </summary>
    [ContextMenu("Clear Sideline Surround")]
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

    private GameObject CreateTile(Vector2 position, Quaternion rotation, Vector2 targetSizeUnits, string tileName, GameObject prefab, Sprite sprite)
    {
        GameObject tile;

        if (prefab != null)
        {
            tile = Instantiate(prefab, position, rotation, transform);
        }
        else
        {
            tile = new GameObject(tileName);
            tile.transform.position = position;
            tile.transform.rotation = rotation;
            tile.transform.SetParent(transform);

            SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
        }

        tile.name = tileName;

        // Cân đối lại kích thước hiển thị theo targetSizeUnits
        SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
        {
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            float sprW = renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit;
            float sprH = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit;

            float scaleX = (sprW > 0) ? (targetSizeUnits.x / sprW) : 1f;
            float scaleY = (sprH > 0) ? (targetSizeUnits.y / sprH) : 1f;
            tile.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            tile.transform.localScale = new Vector3(targetSizeUnits.x, targetSizeUnits.y, 1f);
        }

        // Tùy chọn thêm BoxCollider2D tạo tường chặn
        if (addBoxCollider)
        {
            BoxCollider2D col = tile.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = tile.AddComponent<BoxCollider2D>();
            }
        }

        return tile;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float ppu = (pixelsPerUnit > 0) ? pixelsPerUnit : 38f;
        float cellW = cellSizePixels / ppu;
        float cellH = cellSizePixels / ppu;
        float spacing = spacingPixels / ppu;

        float boardW = boardColumns * cellW + (boardColumns - 1) * spacing;
        float boardH = boardRows * cellH + (boardRows - 1) * spacing;
        float sidelineUnitSize = (sidelineSizePixels > 0 ? sidelineSizePixels : cellSizePixels) / ppu;

        Vector2 center = startPosition;

        // 1. Viền ngoài của Sideline (màu xanh lơ)
        float halfSideW = boardW / 2f + (sidelineThickness * sidelineUnitSize);
        float halfSideH = boardH / 2f + (sidelineThickness * sidelineUnitSize);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireCube(center, new Vector3(halfSideW * 2f, halfSideH * 2f, 0.1f));

        // 2. Viền ngoài cùng của Surround (màu vàng lá)
        float tileW = surroundPrefabPixels.x / ppu;
        float tileH = surroundPrefabPixels.y / ppu;
        float halfSurroundW = halfSideW + (surroundThickness * tileW);
        float halfSurroundH = halfSideH + (surroundThickness * tileH);

        Gizmos.color = new Color(0.4f, 1f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(center, new Vector3(halfSurroundW * 2f, halfSurroundH * 2f, 0.1f));
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SidelineSurround))]
public class SidelineSurroundEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SidelineSurround surround = (SidelineSurround)target;

        EditorGUILayout.Space(6);

        // Nút Tạo Surround Nổi Bật
        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
        if (GUILayout.Button("🛡️ TẠO SURROUND BAO QUANH SIDELINE", GUILayout.Height(40)))
        {
            Undo.RecordObject(surround, "Generate Sideline Surround");
            surround.GenerateSurround();
            EditorUtility.SetDirty(surround);
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ XÓA TOÀN BỘ SURROUND", GUILayout.Height(30)))
        {
            Undo.RecordObject(surround, "Clear Sideline Surround");
            surround.ClearOldSurrounds();
            EditorUtility.SetDirty(surround);
        }

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("🔄 ĐỒNG BỘ THÔNG SỐ TỪ SCENE (BOARD & SIDELINE)", GUILayout.Height(30)))
        {
            Undo.RecordObject(surround, "Sync Settings From Scene");
            surround.AutoDetectSettings();
            EditorUtility.SetDirty(surround);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }
}
#endif
