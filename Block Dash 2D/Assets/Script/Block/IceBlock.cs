using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class IceBlock : MonoBehaviour
{
    public IceBlockConfig config;
    
    [Tooltip("Thứ tự loại băng được chọn")]
    public int selectedSizeIndex = 0;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Hàm cập nhật hình ảnh và kích thước
    [ContextMenu("Apply Ice Size")]
    public void ApplySize()
    {
        if (config == null || config.iceSizes == null || config.iceSizes.Count == 0) return;

        selectedSizeIndex = Mathf.Clamp(selectedSizeIndex, 0, config.iceSizes.Count - 1);
        var data = config.iceSizes[selectedSizeIndex];

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && data.iceSprite != null)
        {
            spriteRenderer.sprite = data.iceSprite;
        }
        
        transform.localScale = new Vector3(data.boxSize.x, data.boxSize.y, 1f);
    }

    private void OnValidate()
    {
        ApplySize();
    }
}

#if UNITY_EDITOR
// Phần giao diện tùy chỉnh Dropdown trên Inspector nằm chung trong file này luôn
[CustomEditor(typeof(IceBlock))]
public class IceBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        IceBlock iceBlock = (IceBlock)target;

        // Vẽ các thuộc tính mặc định của component
        DrawDefaultInspector();

        if (iceBlock.config != null && iceBlock.config.iceSizes != null && iceBlock.config.iceSizes.Count > 0)
        {
            string[] options = new string[iceBlock.config.iceSizes.Count];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = string.IsNullOrEmpty(iceBlock.config.iceSizes[i].sizeName) 
                    ? $"Size {i}" 
                    : iceBlock.config.iceSizes[i].sizeName;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Loại Băng (Dropdown)", iceBlock.selectedSizeIndex, options);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(iceBlock, "Change Ice Size");
                iceBlock.selectedSizeIndex = newIndex;
                iceBlock.ApplySize();
                EditorUtility.SetDirty(iceBlock);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Hãy kéo file MainIceConfig vào ô Config ở trên!", MessageType.Warning);
        }
    }
}
#endif