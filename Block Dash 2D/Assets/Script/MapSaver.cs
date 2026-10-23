using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapSaver : MonoBehaviour
{
    [Header("Map File Settings")]
    [Tooltip("Tên file map cần lưu (Ví dụ: Level_1, Level_2...)")]
    public string mapName = "Level_1";

    [Header("Board Settings (Kích thước Bàn cờ)")]
    public int rows = 12;
    public int columns = 12;
    public float cellSizePixels = 36f;
    public float spacingPixels = 0f;
    public float pixelsPerUnit = 100f;
    public Vector2 startPosition = Vector2.zero;

    [Header("Blocks Container")]
    [Tooltip("GameObject cha chứa toàn bộ các block được xếp trên bàn cờ. Nếu để trống sẽ quét các object con của chính GameObject này.")]
    public Transform blocksContainer;

    [Header("Palette để test Load ngay tại Scene này (Tùy chọn)")]
    public List<BlockPaletteEntry> blockPalette = new List<BlockPaletteEntry>();

    private void Awake()
    {
        if (blocksContainer == null)
        {
            blocksContainer = transform;
        }
    }

    [ContextMenu("1. Snap All Blocks to Grid (Hút vào đúng ô)")]
    public void SnapAllBlocksToGrid()
    {
        Transform container = (blocksContainer != null) ? blocksContainer : transform;
        int count = 0;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform blockTr = container.GetChild(i);
            Vector2Int gridCoord = WorldToGrid(blockTr.position);
            Vector2 snapPos = GridToWorld(gridCoord.x, gridCoord.y);

            blockTr.position = new Vector3(snapPos.x, snapPos.y, blockTr.position.z);

            BlockItem item = blockTr.GetComponent<BlockItem>();
            if (item != null)
            {
                item.gridX = gridCoord.x;
                item.gridY = gridCoord.y;
            }

            count++;
        }

        Debug.Log($"Đã Snap {count} blocks vào đúng tâm các ô trên bàn cờ {rows}x{columns}!");
    }

    [ContextMenu("2. Save Map to JSON (Lưu map)")]
    public void SaveMap()
    {
        if (string.IsNullOrEmpty(mapName))
        {
            Debug.LogError("Chưa nhập Tên Map (mapName)!");
            return;
        }

        Transform container = (blocksContainer != null) ? blocksContainer : transform;

        // Tạo dữ liệu MapData
        MapData mapData = new MapData
        {
            mapName = mapName.Trim(),
            rows = rows,
            columns = columns,
            cellSizePixels = cellSizePixels,
            spacingPixels = spacingPixels,
            pixelsPerUnit = pixelsPerUnit,
            startPosX = startPosition.x,
            startPosY = startPosition.y,
            blocks = new List<BlockData>()
        };

        // Quét tất cả các block nằm trong container
        for (int i = 0; i < container.childCount; i++)
        {
            Transform blockTr = container.GetChild(i);

            // Bỏ qua các object ẩn nếu có
            if (!blockTr.gameObject.activeSelf) continue;

            string typeName = "";
            BlockItem item = blockTr.GetComponent<BlockItem>();
            if (item != null)
            {
                typeName = item.GetBlockType();
            }
            else
            {
                typeName = blockTr.gameObject.name.Replace("(Clone)", "").Trim();
            }

            Vector2Int gridCoord = WorldToGrid(blockTr.position);

            BlockData bData = new BlockData
            {
                blockType = typeName,
                gridX = gridCoord.x,
                gridY = gridCoord.y,
                posX = blockTr.position.x,
                posY = blockTr.position.y,
                rotationZ = blockTr.eulerAngles.z
            };

            mapData.blocks.Add(bData);
        }

        // Chuyển sang chuỗi JSON đẹp mắt
        string json = JsonUtility.ToJson(mapData, true);

        // Lưu vào thư mục Assets/Resources/Maps để scene khác có thể load được mọi lúc
        string folderPath = Path.Combine(Application.dataPath, "Resources", "Maps");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, mapData.mapName + ".json");
        File.WriteAllText(filePath, json);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif

        Debug.Log($"<color=green>LƯU THÀNH CÔNG!</color> Đã lưu {mapData.blocks.Count} blocks vào file: {filePath}");
    }

    [ContextMenu("3. Load Map from JSON (Editor Test)")]
    public void LoadMapInEditor()
    {
        string filePath = Path.Combine(Application.dataPath, "Resources", "Maps", mapName + ".json");
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Không tìm thấy file: {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        MapData mapData = JsonUtility.FromJson<MapData>(json);
        if (mapData == null)
        {
            Debug.LogError("Dữ liệu JSON bị lỗi!");
            return;
        }

        ClearAllBlocks();

        Transform container = (blocksContainer != null) ? blocksContainer : transform;

        // Tạo map tra cứu palette
        Dictionary<string, GameObject> paletteDict = new Dictionary<string, GameObject>();
        foreach (var entry in blockPalette)
        {
            if (!string.IsNullOrEmpty(entry.blockType) && entry.prefab != null)
            {
                paletteDict[entry.blockType] = entry.prefab;
            }
        }

        foreach (var b in mapData.blocks)
        {
            Vector2 spawnPos = GridToWorld(b.gridX, b.gridY);
            Quaternion rot = Quaternion.Euler(0, 0, b.rotationZ);

            if (paletteDict.TryGetValue(b.blockType, out GameObject prefab))
            {
                GameObject obj = Instantiate(prefab, spawnPos, rot, container);
                obj.name = b.blockType;
                BlockItem item = obj.GetComponent<BlockItem>();
                if (item != null)
                {
                    item.blockType = b.blockType;
                    item.gridX = b.gridX;
                    item.gridY = b.gridY;
                }
            }
            else
            {
                Debug.LogWarning($"Chưa gán Prefab cho loại block: '{b.blockType}' trong Block Palette!");
            }
        }

        Debug.Log($"Đã Load xong {mapData.blocks.Count} blocks từ file {mapName}.json!");
    }

    [ContextMenu("Clear All Blocks")]
    public void ClearAllBlocks()
    {
        Transform container = (blocksContainer != null) ? blocksContainer : transform;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(container.GetChild(i).gameObject);
                continue;
            }
#endif
            Destroy(container.GetChild(i).gameObject);
        }
    }

    // Các hàm tính toán tọa độ lưới
    public Vector2 GridToWorld(int col, int row)
    {
        float cellSize = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;
        float step = cellSize + spacing;

        float boardWidth = columns * cellSize + (columns - 1) * spacing;
        float boardHeight = rows * cellSize + (rows - 1) * spacing;

        Vector2 offset = new Vector2(
            -boardWidth / 2f + cellSize / 2f,
            -boardHeight / 2f + cellSize / 2f
        );

        return startPosition + offset + new Vector2(col * step, row * step);
    }

    public Vector2Int WorldToGrid(Vector2 worldPos)
    {
        float cellSize = cellSizePixels / pixelsPerUnit;
        float spacing = spacingPixels / pixelsPerUnit;
        float step = cellSize + spacing;

        float boardWidth = columns * cellSize + (columns - 1) * spacing;
        float boardHeight = rows * cellSize + (rows - 1) * spacing;

        Vector2 offset = new Vector2(
            -boardWidth / 2f + cellSize / 2f,
            -boardHeight / 2f + cellSize / 2f
        );

        Vector2 relative = worldPos - (startPosition + offset);

        int col = Mathf.Clamp(Mathf.RoundToInt(relative.x / step), 0, columns - 1);
        int row = Mathf.Clamp(Mathf.RoundToInt(relative.y / step), 0, rows - 1);

        return new Vector2Int(col, row);
    }
}
