using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BlockSpawner : MonoBehaviour
{
    [System.Serializable]
    public class BlockOption
    {
        public string blockName;
        public GameObject blockPrefab;
    }

    [Header("Block List")]
    public List<BlockOption> blockList = new List<BlockOption>();

    [Header("Spawn Settings")]
    [HideInInspector] public int selectedBlockIndex = 0;
    public Vector2 spawnPosition = Vector2.zero;
    public float blockSizePixels = 36f;
    public float pixelsPerUnit = 100f;

    [Header("Grid Auto-Fill Settings (Tạo hàng loạt lên Map)")]
    [Tooltip("Kéo Board Transform chứa các Cell vào đây (Nếu để trống script sẽ tự tìm kiếm trong Scene)")]
    public Transform boardTransform;
    [Tooltip("Kích thước mỗi ô Cell trên Board (thường là 1f nếu scale chuẩn)")]
    public float cellSize = 1f;
    [Tooltip("Tự động xoay ngẫu nhiên các hướng (0, 90, 180, 270 độ) khi tạo full map")]
    public bool randomRotationOnGridSpawn = true;

    [Header("Freeze Settings")]
    [Tooltip("Tự động đóng băng khối khi vừa được tạo ra")]
    public bool spawnAsFrozen = false;
    [Tooltip("Số đếm trên khối băng khi sinh ra")]
    public int spawnFreezeCount = 3;

    [Header("Active Blocks Tracking")]
    [HideInInspector] public List<BlockItem> activeBlocks = new List<BlockItem>();

    public void CreateBlock()
    {
        if (blockList.Count == 0)
        {
            Debug.LogWarning("Chưa có Block nào trong danh sách!");
            return;
        }

        if (selectedBlockIndex < 0 || selectedBlockIndex >= blockList.Count)
        {
            Debug.LogWarning("Vui lòng chọn Block hợp lệ!");
            return;
        }

        GameObject prefab = blockList[selectedBlockIndex].blockPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Block Prefab chưa được gán!");
            return;
        }

        GameObject newBlock = Instantiate(
            prefab,
            new Vector3(spawnPosition.x, spawnPosition.y, 0f),
            Quaternion.identity,
            transform
        );

        newBlock.name = blockList[selectedBlockIndex].blockName;

        float size = blockSizePixels / pixelsPerUnit;
        newBlock.transform.localScale = Vector3.one * size;

        BlockItem blockItem = newBlock.GetComponent<BlockItem>();
        if (blockItem != null)
        {
            if (!activeBlocks.Contains(blockItem))
            {
                activeBlocks.Add(blockItem);
            }
        }

        FreezableBlock freezable = newBlock.GetComponent<FreezableBlock>();
        if (freezable != null && spawnAsFrozen)
        {
            freezable.SetFrozen(true, spawnFreezeCount > 0 ? spawnFreezeCount : 1);
        }

        Debug.Log("Đã tạo Block: " + newBlock.name);
    }

    /// <summary>
    /// Tự động rải block 2 cell (Arrow Block) phủ kín bàn cờ, tự động kiểm tra khoảng trống để không bị chồng lấp
    /// </summary>
    public void SpawnAllGridBlocks()
    {
        if (blockList.Count == 0)
        {
            Debug.LogWarning("Danh sách Block đang trống!");
            return;
        }

        // 1. Dọn sạch các block cũ trước khi rải mới
        ClearAllBlocks();

        // Mặc định luôn lấy block đầu tiên (Index 0 - Block Arrow 2 cell)
        int targetIndex = 0;
        GameObject prefab = blockList[targetIndex].blockPrefab;
        
        if (prefab == null)
        {
            Debug.LogWarning("Prefab của Block đầu tiên (Index 0) đang bị trống!");
            return;
        }

        // Tự động tìm Board nếu chưa gán
        if (boardTransform == null)
        {
            AutoDetectBoard();
        }

        if (boardTransform == null)
        {
            Debug.LogWarning("Không tìm thấy Board Transform trong Scene để tạo block!");
            return;
        }

        // Lấy tất cả các cell và sắp xếp/lọc theo hệ thống tọa độ Cell_Row_Col
        Transform[] allCells = boardTransform.GetComponentsInChildren<Transform>(true);
        
        // Dùng Dictionary để lưu trạng thái các ô cell theo tọa độ (row, col) xem đã bị chiếm chưa
        // Key: "row_col", Value: true/false
        Dictionary<string, bool> occupiedCells = new Dictionary<string, bool>();
        Dictionary<string, Transform> cellLookup = new Dictionary<string, Transform>();

        foreach (Transform cell in allCells)
        {
            if (cell == null || !cell.name.StartsWith("Cell_"))
                continue;

            cellLookup[cell.name] = cell;
            occupiedCells[cell.name] = false;
        }

        int spawnedCount = 0;

        // Duyệt qua từng cell để lấp đầy bằng các block 2 cell
        foreach (var kvp in cellLookup)
        {
            string cellName = kvp.Key; // Ví dụ: Cell_0_0 (Row_Col hoặc tùy theo format của bạn)
            if (occupiedCells[cellName]) continue; // Nếu ô này đã bị chiếm thì bỏ qua

            Transform currentCell = kvp.Value;
            
            // Tách tên để lấy tọa độ row và col (Format: Cell_Row_Col)
            string[] parts = cellName.Split('_');
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int col))
                continue;

            // Thử tìm ô bên cạnh (Cột tiếp theo col + 1 hoặc hàng tiếp theo row + 1) để ghép thành block 2 cell
            string rightCellName = $"Cell_{row}_{col + 1}";
            string topCellName = $"Cell_{row + 1}_{col}";

            bool canPlaceHorizontal = cellLookup.ContainsKey(rightCellName) && !occupiedCells[rightCellName];
            bool canPlaceVertical = cellLookup.ContainsKey(topCellName) && !occupiedCells[topCellName];

            if (!canPlaceHorizontal && !canPlaceVertical) 
            {
                // Nếu không đủ chỗ cho 2 cell thì tạm bỏ qua ô lẻ này để tránh lỗi
                continue; 
            }

            // Ưu tiên xếp ngang hoặc dọc ngẫu nhiên
            bool isHorizontal = canPlaceHorizontal && (!canPlaceVertical || Random.value > 0.5f);

            Transform secondCell = isHorizontal ? cellLookup[rightCellName] : cellLookup[topCellName];
            string secondCellName = isHorizontal ? rightCellName : topCellName;

            // Tính vị trí tâm nằm giữa 2 ô cell
            Vector3 spawnPos = (currentCell.position + secondCell.position) / 2f;

            // Góc xoay: Ngang (0 độ hoặc 180 độ), Dọc (90 độ hoặc 270 độ)
            float rotZ = isHorizontal ? (Random.value > 0.5f ? 0f : 180f) : (Random.value > 0.5f ? 90f : 270f);
            Quaternion spawnRot = Quaternion.Euler(0f, 0f, rotZ);

            // Tạo block 2 cell tại vị trí trung tâm
            GameObject newBlock = Instantiate(
                prefab,
                spawnPos,
                spawnRot,
                transform
            );

            newBlock.name = $"{blockList[targetIndex].blockName}_{row}_{col}";

            // Scale kích thước chuẩn
            float size = blockSizePixels / pixelsPerUnit;
            newBlock.transform.localScale = Vector3.one * size;

            // Đánh dấu cả 2 ô cell này đã bị chiếm chỗ
            occupiedCells[cellName] = true;
            occupiedCells[secondCellName] = true;

            // Xử lý hướng cho TwoWayBlock hoặc ArrowBlock nếu có
            TwoWayBlock twoWay = newBlock.GetComponent<TwoWayBlock>();
            if (twoWay != null)
            {
                if (Mathf.Abs(rotZ - 0f) < 5f) twoWay.horizontalStartDir = HorizontalStartDirection.Right;
                else if (Mathf.Abs(rotZ - 180f) < 5f) twoWay.horizontalStartDir = HorizontalStartDirection.Left;
                else if (Mathf.Abs(rotZ - 90f) < 5f) { twoWay.moveAxis = TwoWayAxis.Vertical; twoWay.verticalStartDir = VerticalStartDirection.Top; }
                else if (Mathf.Abs(rotZ - 270f) < 5f) { twoWay.moveAxis = TwoWayAxis.Vertical; twoWay.verticalStartDir = VerticalStartDirection.Down; }

                twoWay.InitializeCurrentDirection();
                twoWay.ApplyRotationFromDirection();
            }

            BlockItem blockItem = newBlock.GetComponent<BlockItem>();
            if (blockItem != null)
            {
                blockItem.blockType = blockList[targetIndex].blockName;
                if (!activeBlocks.Contains(blockItem))
                {
                    activeBlocks.Add(blockItem);
                }
            }

            FreezableBlock freezable = newBlock.GetComponent<FreezableBlock>();
            if (freezable != null && spawnAsFrozen)
            {
                freezable.SetFrozen(true, spawnFreezeCount > 0 ? spawnFreezeCount : 1);
            }

            spawnedCount++;
        }

        Debug.Log($"<color=green>Đã rải thành công {spawnedCount} Arrow Block (2 cell) khít bàn cờ, không bị chồng lấp!</color>");
    }

    public void AutoDetectBoard()
    {
#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
        if (boardGen != null) { boardTransform = boardGen.transform; return; }

        BoardSideline sideline = FindFirstObjectByType<BoardSideline>();
        if (sideline != null) { boardTransform = sideline.transform; return; }
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
        if (boardGen != null) { boardTransform = boardGen.transform; return; }

        BoardSideline sideline = FindObjectOfType<BoardSideline>();
        if (sideline != null) { boardTransform = sideline.transform; return; }
#endif

        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name.Contains("Board") || obj.name.Contains("Grid"))
            {
                foreach (Transform child in obj.transform)
                {
                    if (child.name.StartsWith("Cell_"))
                    {
                        boardTransform = obj.transform;
                        return;
                    }
                }
            }
        }
    }

    [ContextMenu("Clear All Blocks")]
    public void ClearAllBlocks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                continue;
            }
#endif
            Destroy(child.gameObject);
        }

        activeBlocks.Clear();
        Debug.Log("<color=orange>Đã xóa toàn bộ block trên màn hình!</color>");
    }

    public void RemoveBlockFromList(BlockItem blockItem)
    {
        if (activeBlocks.Contains(blockItem))
        {
            activeBlocks.Remove(blockItem);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BlockSpawner))]
public class BlockSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BlockSpawner spawner = (BlockSpawner)target;
        serializedObject.Update();

        // 1. Vẽ danh sách Block List trước
        SerializedProperty blockListProp = serializedObject.FindProperty("blockList");
        EditorGUILayout.PropertyField(blockListProp, true);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);

        // 2. Tạo Dropdown tự động dựa trên blockList hiện có
        SerializedProperty selectedIndexProp = serializedObject.FindProperty("selectedBlockIndex");
        string[] displayOptions;

        if (spawner.blockList == null || spawner.blockList.Count == 0)
        {
            displayOptions = new string[] { "Danh sách trống (Hãy thêm Block phía trên)" };
            selectedIndexProp.intValue = 0;
        }
        else
        {
            displayOptions = new string[spawner.blockList.Count];
            for (int i = 0; i < spawner.blockList.Count; i++)
            {
                string blockName = spawner.blockList[i].blockName;
                if (string.IsNullOrEmpty(blockName)) blockName = $"Block Không Tên {i}";
                displayOptions[i] = $"{i}: {blockName}";
            }

            if (selectedIndexProp.intValue >= spawner.blockList.Count)
            {
                selectedIndexProp.intValue = 0;
            }
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Selected Block", selectedIndexProp.intValue, displayOptions);
        if (EditorGUI.EndChangeCheck() || newIndex != selectedIndexProp.intValue)
        {
            if (spawner.blockList != null && spawner.blockList.Count > 0)
            {
                selectedIndexProp.intValue = Mathf.Clamp(newIndex, 0, spawner.blockList.Count - 1);
            }
        }

        // 3. Vẽ các thuộc tính còn lại tự động
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "m_Script" || prop.name == "blockList" || prop.name == "selectedBlockIndex")
                continue;

            EditorGUILayout.PropertyField(prop, true);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);

        // Nút Tạo 1 Block đơn lẻ tại tọa độ tùy chỉnh
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button("Create Single Block", GUILayout.Height(30)))
        {
            spawner.CreateBlock();
        }

        // Nút Tạo Block phủ kín toàn bộ bản đồ (Grid Auto-Fill) - Mặc định lấy Block 0 và xoay ngẫu nhiên các hướng
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("⚡ Spawn Grid Blocks (Full Map - Random Hướng)", GUILayout.Height(38)))
        {
            Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Spawn Grid Blocks");
            spawner.SpawnAllGridBlocks();
            EditorUtility.SetDirty(spawner);
        }

        // Nút Xóa toàn bộ block
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ Clear All Blocks", GUILayout.Height(35)))
        {
            Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Clear All Blocks");
            spawner.ClearAllBlocks();
            EditorUtility.SetDirty(spawner);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif