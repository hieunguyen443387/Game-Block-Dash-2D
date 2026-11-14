using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro; // Dùng cho TMP_InputField

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapLoader : MonoBehaviour
{
    [Header("Level Settings")]
    [Tooltip("Nhập số Level cần tải (Ví dụ: 1 -> tải file Level_1.txt)")]
    public int levelNumber = 1;

    [Header("UI Setting Controls (Chức năng chọn & Restart Level)")]
    [Tooltip("Kéo ô TMP_InputField nhập số level trong SettingContainer vào đây")]
    public TMP_InputField levelInputField;

    [Tooltip("Kéo PopupPanel cài đặt vào đây để tự đóng bảng sau khi bấm Load/Restart (Tùy chọn)")]
    public GameObject settingPopupPanel;

    [Header("Max Level Auto Detection (Chỉ đọc)")]
    [Tooltip("Cấp độ cao nhất được tự động quét trong thư mục LocalLow/Levels")]
    [SerializeField] private int maxAvailableLevel = 1;

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
        AutoDetectMaxLevel();
    }

    /// <summary>
    /// Tự động quét thư mục persistentDataPath/Levels để tìm level có số thứ tự lớn nhất
    /// </summary>
    public void AutoDetectMaxLevel()
    {
        string folderPath = Path.Combine(Application.persistentDataPath, "Levels");
        int highestLevel = 1;

        if (Directory.Exists(folderPath))
        {
            string[] files = Directory.GetFiles(folderPath, "Level_*.txt");
            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string levelStr = fileName.Replace("Level_", "");
                if (int.TryParse(levelStr, out int lvlNum))
                {
                    if (lvlNum > highestLevel)
                    {
                        highestLevel = lvlNum;
                    }
                }
            }
        }

        maxAvailableLevel = highestLevel;
    }

    // =========================================================================
    // CÁC HÀM XỬ LÝ CHO SETTING POPUP (CHỌN LEVEL & RESTART)
    // =========================================================================

    /// <summary>
    /// Gắn hàm này vào sự kiện On Click() của nút LOAD trong SettingContainer
    /// </summary>
    public void OnClickLoadLevelFromInput()
    {
        if (levelInputField == null)
        {
            Debug.LogWarning("[MapLoader] Chưa gán levelInputField trong Inspector!");
            return;
        }

        LoadLevelByInputString(levelInputField.text);
    }

    /// <summary>
    /// Tải level dựa trên chuỗi số truyền vào (có thể gắn vào OnEndEdit của InputField hoặc gọi qua code)
    /// </summary>
    public void LoadLevelByInputString(string inputStr)
    {
        if (string.IsNullOrWhiteSpace(inputStr))
        {
            Debug.LogWarning("[MapLoader] Ô nhập level đang trống!");
            return;
        }

        if (int.TryParse(inputStr.Trim(), out int targetLevel))
        {
            if (targetLevel < 1) targetLevel = 1;

            AutoDetectMaxLevel();
            if (targetLevel > maxAvailableLevel)
            {
                Debug.LogWarning($"[MapLoader] Level {targetLevel} vượt quá giới hạn. Tự động chuyển về Level cao nhất: {maxAvailableLevel}");
                targetLevel = maxAvailableLevel;
            }

            // Đồng bộ dữ liệu phiên chơi mới sang GameDataService
            GameDataService.StartNewSessionAtLevel(targetLevel);

            levelNumber = targetLevel;
            LoadLevel();

            if (levelInputField != null)
            {
                levelInputField.text = ""; // Xóa chữ đã nhập sau khi load xong
            }

            if (settingPopupPanel != null)
            {
                settingPopupPanel.SetActive(false);
            }
        }
        else
        {
            Debug.LogWarning($"[MapLoader] Giá trị '{inputStr}' không phải là số level hợp lệ!");
        }
    }

    /// <summary>
    /// Gắn hàm này vào sự kiện On Click() của nút RESTART trong SettingContainer.
    /// Sử dụng GameDataService để reset phiên chơi hiện tại và tải lại map từ đầu.
    /// </summary>
    public void OnClickRestartLevel()
    {
        // Reset phiên chơi hiện tại trong GameDataService và GameSessionData
        GameSessionData cleanSession = GameDataService.RestartCurrentSession();

        // Cập nhật lại levelNumber theo đúng level hiện tại từ service và load lại map
        levelNumber = cleanSession.currentLevel;
        LoadLevel();

        if (settingPopupPanel != null)
        {
            settingPopupPanel.SetActive(false);
        }
    }

    // =========================================================================

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

            string pName = entry.prefab.name.Trim();
            paletteDictionary[pName] = entry.prefab;
            paletteDictionary[pName.Replace(" ", "")] = entry.prefab;
            paletteDictionary[NormalizeKey(pName)] = entry.prefab;
        }
    }

    public GameObject GetPrefabFromPalette(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return null;

        if (paletteDictionary != null)
        {
            if (paletteDictionary.TryGetValue(blockType, out GameObject direct)) return direct;
            if (paletteDictionary.TryGetValue(blockType.Trim(), out GameObject trimmed)) return trimmed;
            if (paletteDictionary.TryGetValue(blockType.Replace(" ", ""), out GameObject noSpace)) return noSpace;
            if (paletteDictionary.TryGetValue(NormalizeKey(blockType), out GameObject normMatch)) return normMatch;
        }

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
                    return found;
                }
            }
        }
#endif

        return null;
    }

    public void LoadBoardAndGatesOnly(int level)
    {
        AutoDetectMaxLevel();

        if (level > maxAvailableLevel)
        {
            level = maxAvailableLevel;
        }

        levelNumber = level;
        string targetMapName = "Level_" + levelNumber;
        BuildPaletteDictionary();

        string folderPath = Path.Combine(Application.persistentDataPath, "Levels");
        string filePath = Path.Combine(folderPath, targetMapName + ".txt");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"<color=red>KHÔNG TÌM THẤY MAP:</color> {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        MapData mapData = JsonUtility.FromJson<MapData>(json);
        if (mapData == null)
        {
            Debug.LogError("Dữ liệu file map bị lỗi định dạng!");
            return;
        }

        loadedRows = mapData.rows;
        loadedColumns = mapData.columns;

        RebuildBoardAndSurround(mapData);
        ClearMap(); 

        Transform container = (blocksContainer != null) ? blocksContainer : transform;
        SpawnExitGates(mapData, container);
    }

    [ContextMenu("Load Level By Number")]
    public void LoadLevel()
    {
        AutoDetectMaxLevel();

        if (levelNumber > maxAvailableLevel)
        {
            levelNumber = maxAvailableLevel;
        }

        // Lưu lại level hiện tại vào PlayerPrefs thông qua GameDataService khi load trực tiếp
        if (Application.isPlaying)
        {
            PlayerPrefs.SetInt("Game_CurrentLevel", levelNumber);
            PlayerPrefs.Save();
        }

        string targetMapName = "Level_" + levelNumber;
        BuildPaletteDictionary();

        string folderPath = Path.Combine(Application.persistentDataPath, "Levels");
        string filePath = Path.Combine(folderPath, targetMapName + ".txt");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"<color=red>KHÔNG TÌM THẤY MAP:</color> {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        MapData mapData = JsonUtility.FromJson<MapData>(json);
        if (mapData == null)
        {
            Debug.LogError("Dữ liệu file map bị lỗi định dạng!");
            return;
        }

        loadedRows = mapData.rows;
        loadedColumns = mapData.columns;

        RebuildBoardAndSurround(mapData);
        ClearMap(); 

        Transform container = (blocksContainer != null) ? blocksContainer : transform;
        SpawnExitGates(mapData, container);

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

                FreezableBlock freezable = blockObj.GetComponent<FreezableBlock>();
                int fCount = (b.freezeCount > 0) ? b.freezeCount : (b.isFrozen ? 1 : 0);
                if (freezable != null)
                {
                    freezable.freezeCount = fCount;
                    freezable.SetFrozen(b.isFrozen, fCount);
                }
                else if (b.isFrozen)
                {
                    FreezableBlock addedFreezable = blockObj.AddComponent<FreezableBlock>();
                    addedFreezable.freezeCount = fCount;
                    addedFreezable.SetFrozen(true, fCount);
                }

                HitCountdownBoomBlock boomBlock = blockObj.GetComponent<HitCountdownBoomBlock>();
                int hCount = (b.hitCount > 0) ? b.hitCount : (b.hasBoom ? 3 : 0);
                if (boomBlock != null)
                {
                    boomBlock.hitCount = hCount;
                    boomBlock.SetBoom(b.hasBoom, hCount);
                }
                else if (b.hasBoom)
                {
                    HitCountdownBoomBlock addedBoom = blockObj.AddComponent<HitCountdownBoomBlock>();
                    addedBoom.hitCount = hCount;
                    addedBoom.SetBoom(true, hCount);
                }

                TwoWayBlock twoWay = blockObj.GetComponent<TwoWayBlock>();
                if (twoWay != null)
                {
                    if (Mathf.Abs(b.rotationZ - 0f) < 5f) twoWay.horizontalStartDir = HorizontalStartDirection.Right;
                    else if (Mathf.Abs(b.rotationZ - 180f) < 5f) twoWay.horizontalStartDir = HorizontalStartDirection.Left;
                    else if (Mathf.Abs(b.rotationZ - 90f) < 5f) { twoWay.moveAxis = TwoWayAxis.Vertical; twoWay.verticalStartDir = VerticalStartDirection.Top; }
                    else if (Mathf.Abs(b.rotationZ - 270f) < 5f) { twoWay.moveAxis = TwoWayAxis.Vertical; twoWay.verticalStartDir = VerticalStartDirection.Down; }

                    twoWay.InitializeCurrentDirection();
                    twoWay.ApplyRotationFromDirection();
                }

                spawnedCount++;
            }
            else
            {
                Debug.LogWarning($"[MapLoader] Chưa gán Prefab cho loại block: '{b.blockType}' trong Block Palette!");
            }
        }

        if (Application.isPlaying)
        {
#if UNITY_2023_1_OR_NEWER
            GameProgressManager progressManager = FindFirstObjectByType<GameProgressManager>();
#else
            GameProgressManager progressManager = FindObjectOfType<GameProgressManager>();
#endif
            if (progressManager != null)
            {
                if (progressManager.mainMenuPanel != null) progressManager.mainMenuPanel.SetActive(false);
                if (progressManager.inGamePanel != null) progressManager.inGamePanel.SetActive(true);
                if (progressManager.winPanel != null) progressManager.winPanel.SetActive(false);
            }
        }

        Debug.Log($"<color=green>LOAD LEVEL {levelNumber} THÀNH CÔNG!</color> Đã tái tạo Board, Surround, {spawnedCount}/{mapData.blocks.Count} blocks và {mapData.exitGates.Count} cổng thoát từ AppData.");
    }

    private void RebuildBoardAndSurround(MapData mapData)
    {
        Vector2 startPos = new Vector2(mapData.startPosX, mapData.startPosY);

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
            if (cellPrefab != null) boardGen.cellPrefab = cellPrefab;

            boardGen.GenerateBoard(mapData.boardCells);
        }
        else if (board != null)
        {
            board.rows = mapData.rows;
            board.columns = mapData.columns;
            board.cellSizePixels = mapData.cellSizePixels;
            board.spacingPixels = mapData.spacingPixels;
            board.pixelsPerUnit = mapData.pixelsPerUnit;
            board.startPosition = startPos;
            if (cellPrefab != null) board.cellPrefab = cellPrefab;

            board.GenerateBoard();
        }

#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline == null && sideLinePrefab != null)
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
                GameObject obj = new GameObject("SideLine");
                sideline = obj.AddComponent<SideLine>();
                sideline.borderPrefab = sideLinePrefab;
                sideline.cornerPrefab = sideLinePrefab;
            }
        }

        if (sideline != null)
        {
            if (sideLinePrefab != null && sideLinePrefab.GetComponent<SideLine>() == null)
            {
                sideline.borderPrefab = sideLinePrefab;
                if (sideline.cornerPrefab == null) sideline.cornerPrefab = sideLinePrefab;
            }

            sideline.rows = mapData.rows;
            sideline.columns = mapData.columns;
            sideline.cellSizePixels = mapData.cellSizePixels;
            sideline.spacingPixels = mapData.spacingPixels;
            sideline.pixelsPerUnit = mapData.pixelsPerUnit;
            sideline.startPosition = startPos;
            
            if (mapData.sidelineThickness > 0) sideline.borderThickness = mapData.sidelineThickness;
            if (mapData.sidelineSizePixels > 0) sideline.borderSizePixels = mapData.sidelineSizePixels;
            
            sideline.GenerateBorder(); 
        }

#if UNITY_2023_1_OR_NEWER
        SidelineSurround surround = FindFirstObjectByType<SidelineSurround>();
#else
        SidelineSurround surround = FindObjectOfType<SidelineSurround>();
#endif
        if (surround == null && sidelineSurroundPrefab != null)
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
                GameObject obj = new GameObject("SidelineSurround");
                surround = obj.AddComponent<SidelineSurround>();
                surround.surroundPrefab = sidelineSurroundPrefab;
                surround.cornerPrefab = sidelineSurroundPrefab;
            }
        }

        if (surround != null)
        {
            if (sidelineSurroundPrefab != null && sidelineSurroundPrefab.GetComponent<SidelineSurround>() == null)
            {
                surround.surroundPrefab = sidelineSurroundPrefab;
                if (surround.cornerPrefab == null) surround.cornerPrefab = sidelineSurroundPrefab;
            }

            surround.boardRows = mapData.rows;
            surround.boardColumns = mapData.columns;
            surround.cellSizePixels = mapData.cellSizePixels;
            surround.spacingPixels = mapData.spacingPixels;
            surround.pixelsPerUnit = mapData.pixelsPerUnit;
            surround.startPosition = startPos;

            if (mapData.sidelineThickness > 0) surround.sidelineThickness = mapData.sidelineThickness;
            if (mapData.sidelineSizePixels > 0) surround.sidelineSizePixels = mapData.sidelineSizePixels;

            surround.autoDetectFromScene = true;
            surround.AutoDetectSettings();
            
            if (mapData.surroundThickness > 0) surround.surroundThickness = mapData.surroundThickness;
            surround.surroundMode = (SidelineSurroundMode)mapData.surroundMode;
            
            surround.GenerateSurround(); 
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
        if (gateGen == null && exitGateManagerPrefab != null)
        {
            GameObject managerObj = Instantiate(exitGateManagerPrefab, Vector3.zero, Quaternion.identity);
            managerObj.name = "ExitGateManager";
            gateGen = managerObj.GetComponent<ExitGateGenerator>();
        }

        foreach (var gateData in mapData.exitGates)
        {
            Vector3 gatePos;
            if (gateData.posX != 0f || gateData.posY != 0f)
            {
                gatePos = new Vector3(gateData.posX, gateData.posY, 0f);
            }
            else
            {
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

                ExitGate gateComp = gateObj.GetComponent<ExitGate>() ?? gateObj.AddComponent<ExitGate>();
                BoxCollider2D boxCol = gateObj.GetComponent<BoxCollider2D>() ?? gateObj.AddComponent<BoxCollider2D>();
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

    /// <summary>
    /// Xóa toàn bộ các block đang có trong Scene (Đồng bộ hóa với BlockSpawner)
    /// </summary>
    [ContextMenu("Clear Blocks Only")]
    public void ClearMap()
    {
        Transform container = (blocksContainer != null) ? blocksContainer : transform;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                continue;
            }
#endif
            Destroy(child.gameObject);
        }

#if UNITY_2023_1_OR_NEWER
        BlockSpawner spawner = FindFirstObjectByType<BlockSpawner>();
#else
        BlockSpawner spawner = FindObjectOfType<BlockSpawner>();
#endif
        if (spawner != null)
        {
            spawner.ClearAllBlocks();
        }

#if UNITY_2023_1_OR_NEWER
        BlockItem[] remainingBlocks = FindObjectsByType<BlockItem>(FindObjectsSortMode.None);
#else
        BlockItem[] remainingBlocks = FindObjectsOfType<BlockItem>();
#endif
        foreach (var block in remainingBlocks)
        {
            if (block != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.DestroyObjectImmediate(block.gameObject);
                    continue;
                }
#endif
                Destroy(block.gameObject);
            }
        }

        Debug.Log("<color=orange>Đã quét và xóa sạch toàn bộ Block trên bàn cờ!</color>");
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
        EditorGUILayout.HelpBox("Tự động quét thư mục AppData\\LocalLow\\Levels để tải map!", MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Nút Load Level
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button($"📂 LOAD LEVEL {loader.levelNumber}", GUILayout.Height(42)))
        {
            loader.LoadLevel();
        }

        // Nút Clear Blocks Only (Đồng bộ toàn diện)
        EditorGUILayout.Space(-2);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ CLEAR BLOCKS ONLY (Xóa tất cả block)", GUILayout.Height(35)))
        {
            Undo.RegisterFullObjectHierarchyUndo(loader.gameObject, "Clear All Blocks Only");
            loader.ClearMap();
            EditorUtility.SetDirty(loader);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif