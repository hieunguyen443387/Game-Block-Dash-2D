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
    public int selectedBlockIndex = 0;
    public Vector2 spawnPosition = Vector2.zero;
    public float blockSizePixels = 36f;
    public float pixelsPerUnit = 100f;

    [Header("Freeze Settings")]
    [Tooltip("Tự động đóng băng khối khi vừa được tạo ra")]
    public bool spawnAsFrozen = false;
    [Tooltip("Số đếm trên khối băng khi sinh ra")]
    public int spawnFreezeCount = 3;

    [Header("Active Blocks Tracking")]
    // Danh sách lưu trữ các block đang hiển thị trong scene
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

        // Truyền thêm 'transform' vào đây để nhận BlockSpawner làm đối tượng cha
        GameObject newBlock = Instantiate(
            prefab,
            new Vector3(spawnPosition.x, spawnPosition.y, 0f),
            Quaternion.identity,
            transform
        );

        newBlock.name = blockList[selectedBlockIndex].blockName;

        float size = blockSizePixels / pixelsPerUnit;
        newBlock.transform.localScale = Vector3.one * size;

        // Đảm bảo block có gắn component BlockItem
        BlockItem blockItem = newBlock.GetComponent<BlockItem>();
        if (blockItem != null)
        {
            if (!activeBlocks.Contains(blockItem))
            {
                activeBlocks.Add(blockItem);
            }
        }

        // Cập nhật trạng thái đóng băng nếu bật tùy chọn spawnAsFrozen
        FreezableBlock freezable = newBlock.GetComponent<FreezableBlock>();
        if (freezable != null && spawnAsFrozen)
        {
            freezable.SetFrozen(true, spawnFreezeCount > 0 ? spawnFreezeCount : 1);
        }

        Debug.Log("Da tao Block: " + newBlock.name);
    }

    /// <summary>
    /// Xóa toàn bộ các block đang quản lý và các object con trực tiếp
    /// </summary>
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

    // Hàm hỗ trợ xóa block khỏi danh sách quản lý khi nó bị hủy
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
        DrawDefaultInspector();

        BlockSpawner spawner = (BlockSpawner)target;

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