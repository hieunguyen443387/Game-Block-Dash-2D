using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MapLoader : MonoBehaviour
{
    [Header("Level Settings")]
    [Tooltip("Tên file map cần tải (Không cần đuôi .json, ví dụ: Level_1)")]
    public string mapName = "Level_1";

    [Tooltip("Tự động đọc tên level từ PlayerPrefs nếu có (tiện khi làm menu chọn level)")]
    public bool loadFromPlayerPrefs = false;
    public string playerPrefsKey = "SelectedLevel";

    [Tooltip("Tự động load map khi vào scene (Start)")]
    public bool autoLoadOnStart = true;

    [Header("Block Palette (Bảng tra cứu Prefab)")]
    [Tooltip("Kéo các Prefab tương ứng với tên định danh loại block vào đây")]
    public List<BlockPaletteEntry> blockPalette = new List<BlockPaletteEntry>();

    [Header("Blocks Container")]
    [Tooltip("GameObject cha để chứa các block được sinh ra. Nếu để trống sẽ sinh trực tiếp vào GameObject này.")]
    public Transform blocksContainer;

    [Header("Loaded Board Info (Tự động cập nhật từ JSON)")]
    [SerializeField] private int loadedRows;
    [SerializeField] private int loadedColumns;
    [SerializeField] private float loadedCellSizePixels;

    private Dictionary<string, GameObject> paletteDictionary;

    private void Awake()
    {
        if (blocksContainer == null)
        {
            blocksContainer = transform;
        }

        BuildPaletteDictionary();
    }

    private void Start()
    {
        if (autoLoadOnStart)
        {
            string targetMap = mapName;
            if (loadFromPlayerPrefs && PlayerPrefs.HasKey(playerPrefsKey))
            {
                targetMap = PlayerPrefs.GetString(playerPrefsKey, mapName);
            }

            LoadMap(targetMap);
        }
    }

    private void BuildPaletteDictionary()
    {
        paletteDictionary = new Dictionary<string, GameObject>();
        foreach (var entry in blockPalette)
        {
            if (!string.IsNullOrEmpty(entry.blockType) && entry.prefab != null)
            {
                paletteDictionary[entry.blockType.Trim()] = entry.prefab;
            }
        }
    }

    [ContextMenu("Load Map")]
    public void LoadMapFromInspector()
    {
        LoadMap(mapName);
    }

    public void LoadMap(string targetMapName)
    {
        if (string.IsNullOrEmpty(targetMapName))
        {
            Debug.LogError("Chưa chỉ định tên map cần load!");
            return;
        }

        BuildPaletteDictionary();

        // 1. Đọc nội dung file JSON (Ưu tiên Resources để chạy được trên mọi nền tảng build)
        string jsonContent = "";
        TextAsset textAsset = Resources.Load<TextAsset>("Maps/" + targetMapName);

        if (textAsset != null)
        {
            jsonContent = textAsset.text;
        }
        else
        {
            // Fallback đọc từ thư mục Assets hoặc persistentDataPath
            string filePath = Path.Combine(Application.dataPath, "Resources", "Maps", targetMapName + ".json");
            if (File.Exists(filePath))
            {
                jsonContent = File.ReadAllText(filePath);
            }
            else
            {
                string persistentPath = Path.Combine(Application.persistentDataPath, "Maps", targetMapName + ".json");
                if (File.Exists(persistentPath))
                {
                    jsonContent = File.ReadAllText(persistentPath);
                }
            }
        }

        if (string.IsNullOrEmpty(jsonContent))
        {
            Debug.LogError($"<color=red>KHÔNG TÌM THẤY MAP:</color> '{targetMapName}' trong Resources/Maps!");
            return;
        }

        // 2. Chuyển JSON thành đối tượng MapData
        MapData mapData = JsonUtility.FromJson<MapData>(jsonContent);
        if (mapData == null)
        {
            Debug.LogError("Dữ liệu JSON của map bị lỗi, không thể đọc!");
            return;
        }

        loadedRows = mapData.rows;
        loadedColumns = mapData.columns;
        loadedCellSizePixels = mapData.cellSizePixels;

        // 3. Xóa các block cũ trước khi nạp mới
        ClearOldBlocks();

        Transform container = (blocksContainer != null) ? blocksContainer : transform;

        // 4. Sinh các Block lên Scene dựa vào tọa độ đã lưu
        int spawnedCount = 0;
        foreach (var b in mapData.blocks)
        {
            if (paletteDictionary.TryGetValue(b.blockType, out GameObject prefab))
            {
                Vector2 spawnPos = GridToWorld(b.gridX, b.gridY, mapData);
                Quaternion rotation = Quaternion.Euler(0, 0, b.rotationZ);

                GameObject blockObj = Instantiate(prefab, spawnPos, rotation, container);
                blockObj.name = $"{b.blockType}_{b.gridX}_{b.gridY}";

                BlockItem item = blockObj.GetComponent<BlockItem>();
                if (item == null)
                {
                    item = blockObj.AddComponent<BlockItem>();
                }
                item.blockType = b.blockType;
                item.gridX = b.gridX;
                item.gridY = b.gridY;

                spawnedCount++;
            }
            else
            {
                Debug.LogWarning($"<color=yellow>Cảnh báo:</color> Block '{b.blockType}' tại ({b.gridX}, {b.gridY}) chưa được gán Prefab trong Block Palette của MapLoader!");
            }
        }

        Debug.Log($"<color=green>LOAD MAP THÀNH CÔNG:</color> Đã tạo {spawnedCount}/{mapData.blocks.Count} blocks cho map '{mapData.mapName}' ({mapData.rows}x{mapData.columns}).");
    }

    [ContextMenu("Clear Map")]
    public void ClearOldBlocks()
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

    private Vector2 GridToWorld(int col, int row, MapData mapData)
    {
        float cellSize = mapData.cellSizePixels / mapData.pixelsPerUnit;
        float spacing = mapData.spacingPixels / mapData.pixelsPerUnit;
        float step = cellSize + spacing;

        float boardWidth = mapData.columns * cellSize + (mapData.columns - 1) * spacing;
        float boardHeight = mapData.rows * cellSize + (mapData.rows - 1) * spacing;

        Vector2 offset = new Vector2(
            -boardWidth / 2f + cellSize / 2f,
            -boardHeight / 2f + cellSize / 2f
        );

        Vector2 startPos = new Vector2(mapData.startPosX, mapData.startPosY);
        return startPos + offset + new Vector2(col * step, row * step);
    }
}
