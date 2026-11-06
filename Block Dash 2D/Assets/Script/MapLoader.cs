using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapLoader : MonoBehaviour
{
    [Header("Level Settings")]
    [Tooltip("Nhập số Level cần tải (Ví dụ: 1 -> tải file Level_1.txt)")]
    public int levelNumber = 1;

    [Header("Block Palette (Bảng tra cứu Prefab)")]
    [Tooltip("Kéo các Prefab tương ứng với tên định danh loại block vào đây")]
    public List<BlockPaletteEntry> blockPalette = new List<BlockPaletteEntry>();

    [Header("Board & Surround Prefabs (Tùy chọn)")]
    [Tooltip("Prefab của từng ô Cell sàn bàn cờ")]
    public GameObject cellPrefab;

    [Tooltip("Prefab của BoardGenerator (tự sinh ra nếu Scene chưa có)")]
    public GameObject boardGeneratorPrefab;

    [Tooltip("Prefab của SideLine (tự sinh ra nếu Scene chưa có)")]
    public GameObject sideLinePrefab;

    [Tooltip("Prefab của SidelineSurround (tự sinh ra nếu Scene chưa có)")]
    public GameObject sidelineSurroundPrefab;

    [Header("Exit Gate Prefabs (Tùy chọn)")]
    [Tooltip("Prefab của ExitGateManager (chứa script ExitGateGenerator) - Khuyên dùng")]
    public GameObject exitGateManagerPrefab;

    [Tooltip("Prefab cổng thoát đơn lẻ ExitGate (dự phòng)")]
    public GameObject exitGatePrefab;

    [Header("Blocks Container (Tùy chọn)")]
    [Tooltip("GameObject cha chứa các block được sinh ra (nếu để trống sẽ dùng chính GameObject này)")]
    public Transform blocksContainer;

    [Header("Loaded Info (Chỉ đọc)")]
    [SerializeField] private int loadedRows;
    [SerializeField] private int loadedColumns;

    private Dictionary<string, GameObject> paletteDictionary;

    private void Awake()
    {
        if (blocksContainer == null)
        {
            blocksContainer = transform;
        }
        BuildPaletteDictionary();
    }

    private string NormalizeKey(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace(" ", "").Replace("_", "").ToLowerInvariant().Trim();
    }

    private void BuildPaletteDictionary()
    {
        paletteDictionary = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var entry in blockPalette)
        {
            if (entry == null || entry.prefab == null) continue;

            if (!string.IsNullOrEmpty(entry.blockType))
            {
                string key = entry.blockType.Trim();
                paletteDictionary[key] = entry.prefab;
                paletteDictionary[key.Replace(" ", "")] = entry.prefab;
                paletteDictionary[NormalizeKey(key)] = entry.prefab;
            }

            // Đăng ký cả tên Prefab để dù blockType lưu theo tên Prefab hay tên Palette đều tìm thấy
            string pName = entry.prefab.name.Trim();
            paletteDictionary[pName] = entry.prefab;
            paletteDictionary[pName.Replace(" ", "")] = entry.prefab;
            paletteDictionary[NormalizeKey(pName)] = entry.prefab;
        }
    }

    public GameObject GetPrefabFromPalette(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return null;

        // 1. Thử tra cứu trong Dictionary đã build
        if (paletteDictionary != null)
        {
            if (paletteDictionary.TryGetValue(blockType, out GameObject direct)) return direct;
            if (paletteDictionary.TryGetValue(blockType.Trim(), out GameObject trimmed)) return trimmed;
            if (paletteDictionary.TryGetValue(blockType.Replace(" ", ""), out GameObject noSpace)) return noSpace;
            if (paletteDictionary.TryGetValue(NormalizeKey(blockType), out GameObject normMatch)) return normMatch;
        }

        // 2. Quét trực tiếp danh sách blockPalette (Fuzzy matching)
        string normTarget = NormalizeKey(blockType);
        foreach (var entry in blockPalette)
        {
            if (entry == null || entry.prefab == null) continue;

            if (NormalizeKey(entry.blockType) == normTarget || NormalizeKey(entry.prefab.name) == normTarget)
            {
                return entry.prefab;
            }
        }

#if UNITY_EDITOR
        // 3. Dự phòng cho Editor: Tự động tìm trong project nếu người dùng chưa kịp gán vào Block Palette
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            string assetName = Path.GetFileNameWithoutExtension(path);
            if (NormalizeKey(assetName) == normTarget)
            {
                GameObject found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (found != null)
                {
                    Debug.Log($"[MapLoader] Tự động tìm thấy Prefab '{assetName}' khớp với '{blockType}'!");
                    return found;
                }
            }
        }
#endif

        return null;
    }

    [ContextMenu("Load Level By Number")]
    public void LoadLevel()
    {
        string targetMapName = "Level_" + levelNumber;
        BuildPaletteDictionary();

        // 1. Đọc nội dung file map .txt từ thư mục Resources/Maps
        TextAsset textAsset = Resources.Load<TextAsset>("Maps/" + targetMapName);
        if (textAsset == null)
        {
            Debug.LogError($"<color=red>KHÔNG TÌM THẤY MAP:</color> Resources/Maps/{targetMapName}.txt");
            return;
        }

        // 2. Giải mã JSON thành MapData
        MapData mapData = JsonUtility.FromJson<MapData>(textAsset.text);
        if (mapData == null)
        {
            Debug.LogError("Dữ liệu file map bị lỗi định dạng!");
            return;
        }

        loadedRows = mapData.rows;
        loadedColumns = mapData.columns;

        // 3. Tự động tái tạo lại Board và Surround theo thông số trong file map
        RebuildBoardAndSurround(mapData);

        // 4. Xóa block cũ trước khi load map mới
        ClearMap();

        Transform container = (blocksContainer != null) ? blocksContainer : transform;

        // 5. Sinh lại Cổng thoát (ExitGate)
        SpawnExitGates(mapData, container);

        // 6. Sinh lại toàn bộ các Block
        int spawnedCount = 0;
        foreach (var b in mapData.blocks)
        {
            GameObject prefab = GetPrefabFromPalette(b.blockType);
            if (prefab != null)
            {
                Vector2 spawnPos = new Vector2(b.posX, b.posY);
                Quaternion rotation = Quaternion.Euler(0, 0, b.rotationZ);

                GameObject blockObj = Instantiate(prefab, spawnPos, rotation, container);
                blockObj.name = $"{b.blockType}_{b.gridX}_{b.gridY}";

                BlockItem item = blockObj.GetComponent<BlockItem>();
                if (item != null)
                {
                    item.blockType = b.blockType;
                    item.gridX = b.gridX;
                    item.gridY = b.gridY;
                }

                // Cập nhật trạng thái đóng băng từ dữ liệu đã lưu
                FreezableBlock freezable = blockObj.GetComponent<FreezableBlock>();
                if (freezable != null)
                {
                    freezable.SetFrozen(b.isFrozen);
                }
                else if (b.isFrozen)
                {
                    // Nếu prefab chưa có component FreezableBlock nhưng dữ liệu map lưu là đóng băng
                    FreezableBlock addedFreezable = blockObj.AddComponent<FreezableBlock>();
                    addedFreezable.SetFrozen(true);
                }

                spawnedCount++;
            }
            else
            {
                Debug.LogWarning($"[MapLoader] Chưa gán Prefab cho loại block: '{b.blockType}' trong Block Palette!");
            }
        }

        Debug.Log($"<color=green>LOAD LEVEL {levelNumber} THÀNH CÔNG!</color> Đã tái tạo Board, Surround, {spawnedCount}/{mapData.blocks.Count} blocks và {mapData.exitGates.Count} cổng thoát.");
    }

    private void RebuildBoardAndSurround(MapData mapData)
    {
        Vector2 startPos = new Vector2(mapData.startPosX, mapData.startPosY);

        // 1. Xử lý Board (Sàn bàn cờ & các ô Cell)
#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
        Board board = FindFirstObjectByType<Board>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
        Board board = FindObjectOfType<Board>();
#endif
        if (boardGen == null && board == null)
        {
            if (boardGeneratorPrefab != null)
            {
                GameObject obj = Instantiate(boardGeneratorPrefab, Vector3.zero, Quaternion.identity);
                obj.name = "BoardGenerator";
                boardGen = obj.GetComponent<BoardGenerator>();
            }
            else if (cellPrefab != null)
            {
                GameObject obj = new GameObject("BoardGenerator");
                boardGen = obj.AddComponent<BoardGenerator>();
            }
        }

        if (boardGen != null)
        {
            boardGen.rows = mapData.rows;
            boardGen.columns = mapData.columns;
            boardGen.cellSizePixels = mapData.cellSizePixels;
            boardGen.spacingPixels = mapData.spacingPixels;
            boardGen.pixelsPerUnit = mapData.pixelsPerUnit;
            boardGen.startPosition = startPos;
            if (cellPrefab != null)
            {
                boardGen.cellPrefab = cellPrefab;
            }

            boardGen.GenerateBoard(mapData.boardCells);
            Debug.Log("[MapLoader] Đã vẽ lại Board (Cell sàn) thành công!");
        }
        else if (board != null)
        {
            board.rows = mapData.rows;
            board.columns = mapData.columns;
            board.cellSizePixels = mapData.cellSizePixels;
            board.spacingPixels = mapData.spacingPixels;
            board.pixelsPerUnit = mapData.pixelsPerUnit;
            board.startPosition = startPos;
            if (cellPrefab != null)
            {
                board.cellPrefab = cellPrefab;
            }

            board.GenerateBoard();
            Debug.Log("[MapLoader] Đã vẽ lại Board thành công!");
        }

        // 2. Xử lý SideLine (Viền bàn cờ)
#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline == null)
        {
            if (sideLinePrefab != null)
            {
                SideLine compOnPrefab = sideLinePrefab.GetComponent<SideLine>();
                if (compOnPrefab != null)
                {
                    GameObject obj = Instantiate(sideLinePrefab, Vector3.zero, Quaternion.identity);
                    obj.name = "SideLine";
                    sideline = obj.GetComponent<SideLine>();
                }
                else
                {
                    // Người dùng kéo trực tiếp viên gạch viền (Tile) vào ô sideLinePrefab
                    GameObject obj = new GameObject("SideLine");
                    sideline = obj.AddComponent<SideLine>();
                    sideline.borderPrefab = sideLinePrefab;
                    sideline.cornerPrefab = sideLinePrefab;
                }
            }
        }

        if (sideline != null)
        {
            // Tự động gán borderPrefab nếu người dùng kéo Tile Prefab vào MapLoader
            if (sideLinePrefab != null)
            {
                SideLine compOnPrefab = sideLinePrefab.GetComponent<SideLine>();
                if (compOnPrefab == null)
                {
                    sideline.borderPrefab = sideLinePrefab;
                    if (sideline.cornerPrefab == null) sideline.cornerPrefab = sideLinePrefab;
                }
            }

            sideline.rows = mapData.rows;
            sideline.columns = mapData.columns;
            sideline.cellSizePixels = mapData.cellSizePixels;
            sideline.spacingPixels = mapData.spacingPixels;
            sideline.pixelsPerUnit = mapData.pixelsPerUnit;
            sideline.startPosition = startPos;
            
            // Gán thêm thông số viền đã lưu trong file map
            if (mapData.sidelineThickness > 0)
                sideline.borderThickness = mapData.sidelineThickness;
            if (mapData.sidelineSizePixels > 0)
                sideline.borderSizePixels = mapData.sidelineSizePixels;
            
            // Gọi lệnh vẽ viền bàn cờ
            sideline.GenerateBorder(); 
            Debug.Log("[MapLoader] Đã vẽ lại SideLine (Border) thành công!");
        }

        // 3. Xử lý SidelineSurround (Viền ngoài cùng)
#if UNITY_2023_1_OR_NEWER
        SidelineSurround surround = FindFirstObjectByType<SidelineSurround>();
#else
        SidelineSurround surround = FindObjectOfType<SidelineSurround>();
#endif
        if (surround == null)
        {
            if (sidelineSurroundPrefab != null)
            {
                SidelineSurround compOnPrefab = sidelineSurroundPrefab.GetComponent<SidelineSurround>();
                if (compOnPrefab != null)
                {
                    GameObject obj = Instantiate(sidelineSurroundPrefab, Vector3.zero, Quaternion.identity);
                    obj.name = "SidelineSurround";
                    surround = obj.GetComponent<SidelineSurround>();
                }
                else
                {
                    // Người dùng kéo trực tiếp viên gạch viền ngoài vào ô sidelineSurroundPrefab
                    GameObject obj = new GameObject("SidelineSurround");
                    surround = obj.AddComponent<SidelineSurround>();
                    surround.surroundPrefab = sidelineSurroundPrefab;
                    surround.cornerPrefab = sidelineSurroundPrefab;
                }
            }
        }

        if (surround != null)
        {
            // Tự động gán surroundPrefab nếu người dùng kéo Tile Prefab vào MapLoader
            if (sidelineSurroundPrefab != null)
            {
                SidelineSurround compOnPrefab = sidelineSurroundPrefab.GetComponent<SidelineSurround>();
                if (compOnPrefab == null)
                {
                    surround.surroundPrefab = sidelineSurroundPrefab;
                    if (surround.cornerPrefab == null) surround.cornerPrefab = sidelineSurroundPrefab;
                }
            }

            surround.boardRows = mapData.rows;
            surround.boardColumns = mapData.columns;
            surround.cellSizePixels = mapData.cellSizePixels;
            surround.spacingPixels = mapData.spacingPixels;
            surround.pixelsPerUnit = mapData.pixelsPerUnit;
            surround.startPosition = startPos;

            if (mapData.sidelineThickness > 0)
                surround.sidelineThickness = mapData.sidelineThickness;
            if (mapData.sidelineSizePixels > 0)
                surround.sidelineSizePixels = mapData.sidelineSizePixels;

            surround.autoDetectFromScene = true;
            surround.AutoDetectSettings();
            
            // Gán thêm thông số viền ngoài đã lưu trong file map
            if (mapData.surroundThickness > 0)
                surround.surroundThickness = mapData.surroundThickness;
            surround.surroundMode = (SidelineSurroundMode)mapData.surroundMode;
            
            // Gọi lệnh vẽ viền ngoài Surround
            surround.GenerateSurround(); 
            Debug.Log("[MapLoader] Đã vẽ lại SidelineSurround thành công!");
        }
    }
    
    private void SpawnExitGates(MapData mapData, Transform container)
    {
        if (mapData.exitGates == null || mapData.exitGates.Count == 0) return;

#if UNITY_2023_1_OR_NEWER
        ExitGateGenerator gateGen = FindFirstObjectByType<ExitGateGenerator>();
#else
        ExitGateGenerator gateGen = FindObjectOfType<ExitGateGenerator>();
#endif
        // Nếu trong Scene chưa có ExitGateManager mà có gán Prefab ở MapLoader thì tự sinh ra
        if (gateGen == null && exitGateManagerPrefab != null)
        {
            GameObject managerObj = Instantiate(exitGateManagerPrefab, Vector3.zero, Quaternion.identity);
            managerObj.name = "ExitGateManager";
            gateGen = managerObj.GetComponent<ExitGateGenerator>();
        }

        foreach (var gateData in mapData.exitGates)
        {
            // 1. Ưu tiên tuyệt đối tọa độ thế giới (posX, posY) đã lưu chính xác lúc thiết kế Map
            Vector3 gatePos;
            if (gateData.posX != 0f || gateData.posY != 0f)
            {
                gatePos = new Vector3(gateData.posX, gateData.posY, 0f);
            }
            else
            {
                // Fallback tính toán nếu file map cũ chưa lưu posX, posY
                float ppu = (mapData.pixelsPerUnit > 0) ? mapData.pixelsPerUnit : 100f;
                float cellSize = mapData.cellSizePixels / ppu;
                float sidelineUnitSize = (mapData.sidelineSizePixels > 0 ? mapData.sidelineSizePixels : mapData.cellSizePixels) / ppu;
                float borderOffset = mapData.sidelineThickness * sidelineUnitSize;

                float boardHeight = mapData.rows * cellSize;
                float startY = mapData.startPosY + boardHeight / 2f + borderOffset + cellSize / 2f;

                float boardWidth = mapData.columns * cellSize;
                float leftColX = mapData.startPosX - boardWidth / 2f + cellSize / 2f + gateData.startColumn * cellSize;
                float rightColX = mapData.startPosX - boardWidth / 2f + cellSize / 2f + (gateData.startColumn + gateData.columnSpan - 1) * cellSize;
                float startX = (leftColX + rightColX) / 2f;

                gatePos = new Vector3(startX, startY, 0f);
            }

            // 2. Sinh cổng bằng ExitGateGenerator (ExitGateManager)
            if (gateGen != null)
            {
                gateGen.rows = mapData.rows;
                gateGen.columns = mapData.columns;
                gateGen.cellSizePixels = mapData.cellSizePixels;
                gateGen.pixelsPerUnit = mapData.pixelsPerUnit;
                gateGen.startPosition = new Vector2(mapData.startPosX, mapData.startPosY);
                gateGen.borderThickness = mapData.sidelineThickness;
                gateGen.borderSizePixels = mapData.sidelineSizePixels;
                gateGen.autoCenter = gateData.autoCenter;
                gateGen.startColumn = gateData.startColumn;
                gateGen.gateWidthInCells = gateData.columnSpan;
                gateGen.placement = (GatePlacement)gateData.placement;
                gateGen.extraCellsUp = gateData.extraCellsUp;
                gateGen.positionOffset = new Vector2(gateData.offsetX, gateData.offsetY);
                gateGen.GenerateExitGate();

                // Đảm bảo vị trí cổng khớp 100% với lúc thiết kế và cập nhật component ExitGate
                if (gateGen.CurrentGateObject != null)
                {
                    gateGen.CurrentGateObject.transform.position = gatePos;
                    ExitGate gateComp = gateGen.CurrentGateObject.GetComponent<ExitGate>();
                    if (gateComp != null)
                    {
                        gateComp.startColumn = gateData.startColumn;
                        gateComp.columnSpan = gateData.columnSpan;
                        gateComp.exitRow = gateData.exitRow > 0 ? gateData.exitRow : mapData.rows;
                        if (System.Enum.TryParse(gateData.gateDirection, out ArrowDirection dir))
                        {
                            gateComp.gateDirection = dir;
                        }
                    }
                }
            }
            else if (exitGatePrefab != null)
            {
                GameObject gateObj = Instantiate(exitGatePrefab, gatePos, Quaternion.identity, container);
                gateObj.name = $"Loaded_ExitGate_{gateData.startColumn}";
                gateObj.transform.position = gatePos;

                // ĐẢM BẢO LUÔN CÓ COMPONENT ExitGate VÀ BoxCollider2D ĐỂ BLOCK TÌM THẤY VÀ CHẠY VÀO
                ExitGate gateComp = gateObj.GetComponent<ExitGate>();
                if (gateComp == null)
                {
                    gateComp = gateObj.AddComponent<ExitGate>();
                }
                BoxCollider2D boxCol = gateObj.GetComponent<BoxCollider2D>();
                if (boxCol == null)
                {
                    boxCol = gateObj.AddComponent<BoxCollider2D>();
                }
                boxCol.isTrigger = true;

                gateComp.startColumn = gateData.startColumn;
                gateComp.columnSpan = gateData.columnSpan;
                gateComp.exitRow = gateData.exitRow > 0 ? gateData.exitRow : mapData.rows;
                if (System.Enum.TryParse(gateData.gateDirection, out ArrowDirection dir))
                {
                    gateComp.gateDirection = dir;
                }
            }
        }
    }

    [ContextMenu("Clear Map")]
    public void ClearMap()
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

#if UNITY_2023_1_OR_NEWER
        ExitGateGenerator gateGen = FindFirstObjectByType<ExitGateGenerator>();
#else
        ExitGateGenerator gateGen = FindObjectOfType<ExitGateGenerator>();
#endif
        if (gateGen != null)
        {
            gateGen.ClearExitGate();
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

#if UNITY_EDITOR
[CustomEditor(typeof(MapLoader))]
public class MapLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapLoader loader = (MapLoader)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("Chỉ cần nhập số Level và nhấn Load để hiển thị toàn bộ Board, Surround, Cổng và Block lên Scene!", MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button($"📂 LOAD LEVEL {loader.levelNumber}", GUILayout.Height(42)))
        {
            loader.LoadLevel();
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif