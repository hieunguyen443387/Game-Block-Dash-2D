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

    /// <summary>Một cách đặt block trong lưới full map: cặp 2 ô + hướng xoay tương ứng.</summary>
    private class GridPlacementOption
    {
        public readonly Transform secondCell;
        public readonly string secondCellName;
        public readonly float rotationZ;

        public GridPlacementOption(Transform secondCell, string secondCellName, float rotationZ)
        {
            this.secondCell = secondCell;
            this.secondCellName = secondCellName;
            this.rotationZ = rotationZ;
        }
    }

    /// <summary>Thông tin block đã đặt, dùng để tránh spawn ra 2 block đối đầu nhau.</summary>
    private class PlacedBlockInfo
    {
        public Vector2 position;
        public Vector2 direction;
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
    [Tooltip("Không tạo ra 2 block có hướng di chuyển đâm thẳng vào nhau (đối đầu trực diện)")]
    public bool avoidHeadOnBlocks = true;

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

    public void SpawnAllGridBlocks()
    {
        if (blockList.Count == 0)
        {
            Debug.LogWarning("Danh sách Block đang trống!");
            return;
        }

        // 1. Xóa sạch mọi block cũ trước khi rải
        ClearAllBlocks();

        int targetIndex = 0;
        GameObject prefab = blockList[targetIndex].blockPrefab;
        
        if (prefab == null)
        {
            Debug.LogWarning("Prefab của Block đầu tiên (Index 0) đang bị trống!");
            return;
        }

        if (boardTransform == null)
        {
            AutoDetectBoard();
        }

        if (boardTransform == null)
        {
            Debug.LogWarning("Không tìm thấy Board Transform trong Scene để tạo block!");
            return;
        }

        Transform[] allCells = boardTransform.GetComponentsInChildren<Transform>(true);
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
        List<PlacedBlockInfo> placedBlocks = new List<PlacedBlockInfo>();

        foreach (var kvp in cellLookup)
        {
            string cellName = kvp.Key;
            if (occupiedCells[cellName]) continue;

            Transform currentCell = kvp.Value;

            string[] parts = cellName.Split('_');
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int col))
                continue;

            string rightCellName = $"Cell_{row}_{col + 1}";
            string topCellName = $"Cell_{row + 1}_{col}";

            bool canPlaceHorizontal = cellLookup.ContainsKey(rightCellName) && !occupiedCells[rightCellName];
            bool canPlaceVertical = cellLookup.ContainsKey(topCellName) && !occupiedCells[topCellName];

            if (!canPlaceHorizontal && !canPlaceVertical)
            {
                continue;
            }

            // Kích thước 1 ô cờ (world units) để xác định 2 block có nằm "cùng làn" hay không
            Transform referenceNeighbor = canPlaceHorizontal ? cellLookup[rightCellName] : cellLookup[topCellName];
            float stepSize = Mathf.Max(0.0001f, Vector3.Distance(currentCell.position, referenceNeighbor.position));

            // Mỗi cặp ô có 2 hướng xoay. Nếu hướng của cặp ngang bị đối đầu thì thử cặp dọc (và ngược lại).
            List<GridPlacementOption> options = new List<GridPlacementOption>();

            if (canPlaceHorizontal)
            {
                options.Add(new GridPlacementOption(cellLookup[rightCellName], rightCellName, 0f));   // sang phải
                options.Add(new GridPlacementOption(cellLookup[rightCellName], rightCellName, 180f)); // sang trái
            }

            if (canPlaceVertical)
            {
                options.Add(new GridPlacementOption(cellLookup[topCellName], topCellName, 90f));  // đi lên
                options.Add(new GridPlacementOption(cellLookup[topCellName], topCellName, 270f)); // đi xuống
            }

            // Xáo trộn để hướng tạo ra vẫn ngẫu nhiên như trước
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                GridPlacementOption tmp = options[i];
                options[i] = options[j];
                options[j] = tmp;
            }

            GridPlacementOption chosen = null;
            Vector3 spawnPos = Vector3.zero;

            foreach (GridPlacementOption option in options)
            {
                Vector3 candidatePos = (currentCell.position + option.secondCell.position) / 2f;
                Vector2 candidateDir = RotationToDirectionVector(option.rotationZ);

                // Bỏ qua hướng sẽ đâm thẳng vào một block đã đặt trước đó
                if (avoidHeadOnBlocks && WouldBlockHeadOn(candidatePos, candidateDir, stepSize, placedBlocks))
                    continue;

                chosen = option;
                spawnPos = candidatePos;
                break;
            }

            if (chosen == null)
            {
                // Cả 2 cặp ô đều chỉ tạo ra hướng đối đầu -> để trống, các lượt sau có thể lấp bằng cặp khác
                continue;
            }

            Transform secondCell = chosen.secondCell;
            string secondCellName = chosen.secondCellName;

            float rotZ = chosen.rotationZ;
            Quaternion spawnRot = Quaternion.Euler(0f, 0f, rotZ);

            GameObject newBlock = Instantiate(
                prefab,
                spawnPos,
                spawnRot,
                transform
            );

            newBlock.name = $"{blockList[targetIndex].blockName}_{row}_{col}";

            float size = blockSizePixels / pixelsPerUnit;
            newBlock.transform.localScale = Vector3.one * size;

            occupiedCells[cellName] = true;
            occupiedCells[secondCellName] = true;

            // Ghi nhận block vừa đặt để các block sau không chọn hướng đâm thẳng vào nó
            placedBlocks.Add(new PlacedBlockInfo
            {
                position = spawnPos,
                direction = RotationToDirectionVector(rotZ)
            });

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

        string headOnNote = avoidHeadOnBlocks ? ", không đối đầu trực diện" : "";
        Debug.Log($"<color=green>Đã rải thành công {spawnedCount} Arrow Block (2 cell) khít bàn cờ, không bị chồng lấp{headOnNote}!</color>");
    }

    /// <summary>
    /// Chuyển góc xoay Z của block thành vector hướng di chuyển (khớp với ArrowDirection).
    /// </summary>
    private static Vector2 RotationToDirectionVector(float rotationZ)
    {
        float z = Mathf.Repeat(rotationZ, 360f);
        int quarter = Mathf.RoundToInt(z / 90f) % 4;

        switch (quarter)
        {
            case 1:  return Vector2.up;
            case 2:  return Vector2.left;
            case 3:  return Vector2.down;
            default: return Vector2.right;
        }
    }

    /// <summary>
    /// Kiểm tra nếu đặt block tại 'position' với hướng 'direction' thì nó sẽ đâm thẳng
    /// vào một block đã đặt trước đó đang chĩa ngược lại (đối đầu trực diện).
    /// </summary>
    private static bool WouldBlockHeadOn(Vector2 position, Vector2 direction, float stepSize, List<PlacedBlockInfo> placedBlocks)
    {
        if (placedBlocks == null || placedBlocks.Count == 0) return false;

        // Hai block được coi là cùng làn khi lệch ngang nhỏ hơn nửa ô cờ
        // (block đặt ở tâm 2 ô nên có thể lệch đúng nửa ô giữa cặp ngang và cặp dọc)
        float lateralLimit = stepSize * 0.55f;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        PlacedBlockInfo nearestInFront = null;
        float nearestForward = float.MaxValue;

        foreach (PlacedBlockInfo other in placedBlocks)
        {
            Vector2 delta = other.position - position;
            float forward = Vector2.Dot(delta, direction);

            if (forward <= stepSize * 0.01f) continue;                                  // không nằm phía trước
            if (Mathf.Abs(Vector2.Dot(delta, perpendicular)) > lateralLimit) continue;  // khác làn

            if (forward < nearestForward)
            {
                nearestForward = forward;
                nearestInFront = other;
            }
        }

        if (nearestInFront == null) return false;

        // Chỉ tính là đối đầu khi block gần nhất phía trước cũng đang chĩa ngược về phía block mới
        return Vector2.Dot(nearestInFront.direction, direction) < -0.5f;
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

    /// <summary>
    /// Xóa toàn bộ block trên Scene (Đồng bộ hóa quét toàn bộ BlockItem)
    /// </summary>
    [ContextMenu("Clear All Blocks")]
    public void ClearAllBlocks()
    {
        // 1. Xóa các con trực tiếp của spawner
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

        // 2. Quét và tiêu diệt tất cả các BlockItem còn sót lại trên toàn Scene
#if UNITY_2023_1_OR_NEWER
        BlockItem[] allSceneBlocks = FindObjectsByType<BlockItem>(FindObjectsSortMode.None);
        MapLoader loader = FindFirstObjectByType<MapLoader>();
#else
        BlockItem[] allSceneBlocks = FindObjectsOfType<BlockItem>();
        MapLoader loader = FindObjectOfType<MapLoader>();
#endif

        foreach (var block in allSceneBlocks)
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

        if (loader != null && loader.transform != transform)
        {
            for (int i = loader.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = loader.transform.GetChild(i);
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

        activeBlocks.Clear();
        Debug.Log("<color=orange>Đã quét và xóa sạch toàn bộ block trên màn hình!</color>");
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

        SerializedProperty blockListProp = serializedObject.FindProperty("blockList");
        EditorGUILayout.PropertyField(blockListProp, true);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);

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

        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button("Create Single Block", GUILayout.Height(30)))
        {
            spawner.CreateBlock();
        }

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("⚡ Spawn Grid Blocks (Full Map - Random Hướng)", GUILayout.Height(38)))
        {
            Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Spawn Grid Blocks");
            spawner.SpawnAllGridBlocks();
            EditorUtility.SetDirty(spawner);
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ Clear All Blocks (Xóa tất cả block)", GUILayout.Height(35)))
        {
            Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Clear All Blocks");
            spawner.ClearAllBlocks();
            EditorUtility.SetDirty(spawner);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif