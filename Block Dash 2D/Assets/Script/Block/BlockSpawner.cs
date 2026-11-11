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
    /// Tự động sinh block phủ kín toàn bộ các ô Cell có sẵn trên bàn cờ
    /// </summary>
    public void SpawnAllGridBlocks()
    {
        if (blockList.Count == 0 || selectedBlockIndex < 0 || selectedBlockIndex >= blockList.Count)
        {
            Debug.LogWarning("Vui lòng chọn loại Block hợp lệ trong danh sách trước khi tạo hàng loạt!");
            return;
        }

        GameObject prefab = blockList[selectedBlockIndex].blockPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("Prefab của Block được chọn đang bị trống!");
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

        Transform[] allCells = boardTransform.GetComponentsInChildren<Transform>(true);
        int spawnedCount = 0;

        foreach (Transform cell in allCells)
        {
            if (cell == null || !cell.name.StartsWith("Cell_"))
                continue;

            // Tạo block tại vị trí của cell tạm thời
            GameObject newBlock = Instantiate(
                prefab,
                cell.position,
                Quaternion.identity,
                transform
            );

            newBlock.name = $"{blockList[selectedBlockIndex].blockName}_{cell.name.Replace("Cell_", "")}";

            // Căn chỉnh chuẩn vào lưới dựa trên logic SnapToGrid
            SpriteRenderer spriteRenderer = newBlock.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                Bounds bounds = spriteRenderer.bounds;
                Vector3 blockBottomLeftOffset = new Vector3(
                    bounds.min.x - newBlock.transform.position.x,
                    bounds.min.y - newBlock.transform.position.y,
                    0f
                );

                Vector3 cellBottomLeft = new Vector3(
                    cell.position.x - cellSize * 0.5f,
                    cell.position.y - cellSize * 0.5f,
                    newBlock.transform.position.z
                );

                Vector3 targetPosition = cellBottomLeft - blockBottomLeftOffset;
                targetPosition.z = newBlock.transform.position.z;
                newBlock.transform.position = targetPosition;
            }

            // Scale kích thước
            float size = blockSizePixels / pixelsPerUnit;
            newBlock.transform.localScale = Vector3.one * size;

            BlockItem blockItem = newBlock.GetComponent<BlockItem>();
            if (blockItem != null && !activeBlocks.Contains(blockItem))
            {
                activeBlocks.Add(blockItem);
            }

            FreezableBlock freezable = newBlock.GetComponent<FreezableBlock>();
            if (freezable != null && spawnAsFrozen)
            {
                freezable.SetFrozen(true, spawnFreezeCount > 0 ? spawnFreezeCount : 1);
            }

            spawnedCount++;
        }

        Debug.Log($"<color=green>Đã tự động sinh thành công {spawnedCount} block lên toàn bộ bàn cờ!</color>");
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

        // Nút Tạo Block phủ kín toàn bộ bản đồ (Grid Auto-Fill)
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("⚡ Spawn Grid Blocks (Full Map)", GUILayout.Height(38)))
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