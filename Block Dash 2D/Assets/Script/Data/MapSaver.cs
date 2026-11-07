using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapSaver : MonoBehaviour
{
    [Header("Map Level Settings")]
    [Tooltip("Nhập số Level cần lưu (Ví dụ: 1 -> lưu thành Level_1.txt)")]
    public int levelNumber = 1;

    [Header("Board Info (Tự động đồng bộ từ Scene)")]
    public int rows = 12;
    public int columns = 12;
    public float cellSizePixels = 36f;
    public float spacingPixels = 0f;
    public float pixelsPerUnit = 100f;
    public Vector2 startPosition = Vector2.zero;

    [Header("Border & Surround Settings (Tự động đồng bộ)")]
    public int sidelineThickness = 1;
    public float sidelineSizePixels = 36f;
    public int surroundThickness = 1;
    public int surroundMode = 0;

    [ContextMenu("2. Save Map to TXT (Tự động quét toàn bộ Scene)")]
    public void SaveMap()
    {
        string finalMapName = "Level_" + levelNumber;

        // 1. Tự động lấy thông số Board từ BoardGenerator, Board hoặc SideLine
#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
        Board board = FindFirstObjectByType<Board>();
        SideLine sideline = FindFirstObjectByType<SideLine>();
        SidelineSurround surround = FindFirstObjectByType<SidelineSurround>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
        Board board = FindObjectOfType<Board>();
        SideLine sideline = FindObjectOfType<SideLine>();
        SidelineSurround surround = FindObjectOfType<SidelineSurround>();
#endif

        if (boardGen != null)
        {
            rows = boardGen.rows;
            columns = boardGen.columns;
            cellSizePixels = boardGen.cellSizePixels;
            spacingPixels = boardGen.spacingPixels;
            pixelsPerUnit = boardGen.pixelsPerUnit;
            startPosition = boardGen.startPosition;
        }
        else if (board != null)
        {
            rows = board.rows;
            columns = board.columns;
            cellSizePixels = board.cellSizePixels;
            spacingPixels = board.spacingPixels;
            pixelsPerUnit = board.pixelsPerUnit;
            startPosition = board.startPosition;
        }
        else if (sideline != null)
        {
            rows = sideline.rows;
            columns = sideline.columns;
            cellSizePixels = sideline.cellSizePixels;
            spacingPixels = sideline.spacingPixels;
            pixelsPerUnit = sideline.pixelsPerUnit;
            startPosition = sideline.startPosition;
        }

        if (sideline != null)
        {
            sidelineThickness = sideline.borderThickness;
            sidelineSizePixels = sideline.borderSizePixels;
        }

        if (surround != null)
        {
            surroundThickness = surround.surroundThickness;
            surroundMode = (int)surround.surroundMode;
        }

        // 2. Quét danh sách các ô Cell của bàn cờ
        List<CellData> cells = new List<CellData>();
        Transform cellContainer = (boardGen != null) ? boardGen.transform : ((board != null) ? board.transform : null);
        if (cellContainer != null && cellContainer.childCount > 0)
        {
            for (int i = 0; i < cellContainer.childCount; i++)
            {
                Transform child = cellContainer.GetChild(i);
                if (child.name.StartsWith("Cell_"))
                {
                    Vector2Int coord = WorldToGrid(child.position);
                    cells.Add(new CellData { x = coord.x, y = coord.y });
                }
            }
        }

        // Nếu trên Scene chưa sinh các ô Cell con, mặc định lưu đầy đủ hình chữ nhật rows x columns
        if (cells.Count == 0)
        {
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    cells.Add(new CellData { x = c, y = r });
                }
            }
        }

        // Tạo khung dữ liệu MapData
        MapData mapData = new MapData
        {
            mapName = finalMapName,
            rows = rows,
            columns = columns,
            cellSizePixels = cellSizePixels,
            spacingPixels = spacingPixels,
            pixelsPerUnit = pixelsPerUnit,
            startPosX = startPosition.x,
            startPosY = startPosition.y,
            boardCells = cells,
            sidelineThickness = sidelineThickness,
            sidelineSizePixels = sidelineSizePixels,
            surroundThickness = surroundThickness,
            surroundMode = surroundMode,
            exitGates = new List<ExitGateData>(),
            blocks = new List<BlockData>()
        };

        // 2. Tự động quét ExitGateManager (ExitGateGenerator) hoặc ExitGate trong Scene
#if UNITY_2023_1_OR_NEWER
        ExitGateGenerator gateGen = FindFirstObjectByType<ExitGateGenerator>();
        ExitGate[] allGates = FindObjectsByType<ExitGate>(FindObjectsSortMode.None);
#else
        ExitGateGenerator gateGen = FindObjectOfType<ExitGateGenerator>();
        ExitGate[] allGates = FindObjectsOfType<ExitGate>();
#endif
        if (gateGen != null)
        {
            float gatePosX = gateGen.transform.position.x;
            float gatePosY = gateGen.transform.position.y;
            string direction = "Up";
            int exitRow = rows;

            if (gateGen.CurrentGateObject != null)
            {
                gatePosX = gateGen.CurrentGateObject.transform.position.x;
                gatePosY = gateGen.CurrentGateObject.transform.position.y;
                ExitGate gateComp = gateGen.CurrentGateObject.GetComponent<ExitGate>();
                if (gateComp != null)
                {
                    direction = gateComp.gateDirection.ToString();
                    exitRow = gateComp.exitRow;
                }
            }
            else if (allGates != null && allGates.Length > 0 && allGates[0] != null)
            {
                gatePosX = allGates[0].transform.position.x;
                gatePosY = allGates[0].transform.position.y;
                direction = allGates[0].gateDirection.ToString();
                exitRow = allGates[0].exitRow;
            }

            ExitGateData gData = new ExitGateData
            {
                startColumn = gateGen.startColumn,
                columnSpan = gateGen.gateWidthInCells,
                exitRow = exitRow,
                gateDirection = direction,
                posX = gatePosX,
                posY = gatePosY,
                autoCenter = gateGen.autoCenter,
                placement = (int)gateGen.placement,
                extraCellsUp = gateGen.extraCellsUp,
                offsetX = gateGen.positionOffset.x,
                offsetY = gateGen.positionOffset.y
            };
            mapData.exitGates.Add(gData);
        }
        else
        {
            foreach (var gate in allGates)
            {
                if (gate == null) continue;
                ExitGateData gData = new ExitGateData
                {
                    startColumn = gate.startColumn,
                    columnSpan = gate.columnSpan,
                    exitRow = gate.exitRow,
                    gateDirection = gate.gateDirection.ToString(),
                    posX = gate.transform.position.x,
                    posY = gate.transform.position.y
                };
                mapData.exitGates.Add(gData);
            }
        }

        // 3. Tự động quét toàn bộ các Block đang có trên Scene
#if UNITY_2023_1_OR_NEWER
        BlockItem[] allBlocksInScene = FindObjectsByType<BlockItem>(FindObjectsSortMode.None);
#else
        BlockItem[] allBlocksInScene = FindObjectsOfType<BlockItem>();
#endif

        foreach (var item in allBlocksInScene)
        {
            if (item == null || !item.gameObject.activeSelf) continue;

            Transform blockTr = item.transform;

            // FIX: Lấy trực tiếp tên GameObject (chính là tên Prefab) làm blockType để phân biệt các block dùng chung script
            string rawName = blockTr.gameObject.name;
            rawName = rawName.Replace("(Clone)", "").Trim();
            int bracketIdx = rawName.IndexOf('(');
            if (bracketIdx > 0)
            {
                rawName = rawName.Substring(0, bracketIdx).Trim();
            }
            string typeName = rawName;

            Vector2Int gridCoord = WorldToGrid(blockTr.position);

            // Kiểm tra trạng thái đóng băng từ FreezableBlock hoặc ArrowBlock
            bool isBlockFrozen = false;
            int freezeCount = 0;
            FreezableBlock freezable = item.GetComponent<FreezableBlock>();
            if (freezable != null)
            {
                isBlockFrozen = freezable.isFrozen;
                freezeCount = freezable.freezeCount;
            }
            else if (item is ArrowBlock arrow && arrow.IsFrozen)
            {
                isBlockFrozen = true;
                if (arrow.freezableBlock != null)
                {
                    freezeCount = arrow.freezableBlock.freezeCount;
                }
            }

            BlockData bData = new BlockData
            {
                blockType = typeName,
                gridX = gridCoord.x,
                gridY = gridCoord.y,
                posX = blockTr.position.x,
                posY = blockTr.position.y,
                rotationZ = blockTr.eulerAngles.z,
                isFrozen = isBlockFrozen,
                freezeCount = freezeCount
            };

            mapData.blocks.Add(bData);
        }

        // 4. Lưu ra file .txt vào thư mục Resources/Maps
        string json = JsonUtility.ToJson(mapData, true);

        string folderPath = Path.Combine(Application.dataPath, "Resources", "Maps");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, finalMapName + ".txt");
        File.WriteAllText(filePath, json);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif

        Debug.Log($"<color=green>LƯU MAP THÀNH CÔNG!</color> Level {levelNumber} đã lưu: {mapData.blocks.Count} blocks, {mapData.exitGates.Count} cổng thoát vào file: {filePath}");
    }

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

#if UNITY_EDITOR
[CustomEditor(typeof(MapSaver))]
public class MapSaverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapSaver saver = (MapSaver)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("Nhập số Level và nhấn nút Save để tự động quét Board, ExitGate và toàn bộ Block trong Scene ra file .txt.", MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button($"💾 LƯU LEVEL {saver.levelNumber} (SAVE MAP)", GUILayout.Height(42)))
        {
            saver.SaveMap();
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif