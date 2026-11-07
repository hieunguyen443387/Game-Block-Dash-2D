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
            Debug.LogWarning("Chua co Block nao trong danh sach!");
            return;
        }

        if (selectedBlockIndex < 0 || selectedBlockIndex >= blockList.Count)
        {
            Debug.LogWarning("Vui long chon Block hop le!");
            return;
        }

        GameObject prefab = blockList[selectedBlockIndex].blockPrefab;

        if (prefab == null)
        {
            Debug.LogWarning("Block Prefab chua duoc gan!");
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

        Debug.Log("Da tao Block: " + newBlock.name);
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

            // Đảm bảo index không bị tràn ra ngoài giới hạn list
            if (selectedIndexProp.intValue >= spawner.blockList.Count)
            {
                selectedIndexProp.intValue = 0;
            }
        }

        // Hiển thị Dropdown trong Inspector (Sửa thành EditorGUI.EndChangeCheck())
        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Selected Block", selectedIndexProp.intValue, displayOptions);
        if (EditorGUI.EndChangeCheck() || newIndex != selectedIndexProp.intValue)
        {
            if (spawner.blockList != null && spawner.blockList.Count > 0)
            {
                selectedIndexProp.intValue = Mathf.Clamp(newIndex, 0, spawner.blockList.Count - 1);
            }
        }

        // 3. Vẽ các thuộc tính còn lại của Spawner tự động (trừ script, blockList và index cũ)
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

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("Create Block", GUILayout.Height(35)))
        {
            spawner.CreateBlock();
        }

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
