using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways] // Cho phép script chạy ngay cả khi không Play game
[RequireComponent(typeof(Image))]
public class PixelNumberDisplay : MonoBehaviour
{
    [Header("Cấu hình hiển thị")]
    [Tooltip("Số level muốn hiển thị xem trước trong Editor")]
    public int previewLevel = 1;

    [Tooltip("Màu của số (Mặc định là màu kem sáng giống chữ LEVEL)")]
    public Color numberColor = new Color32(250, 238, 212, 255);

    [Tooltip("Khoảng cách (tính bằng pixel) giữa các chữ số")]
    public int digitSpacing = 1;

    [Tooltip("Độ phóng đại của mỗi ô pixel khi hiển thị lên UI")]
    public float pixelScale = 8f;

    private Image targetImage;

    // Ma trận pixel 4x7 cho từng chữ số (1 = tô màu, 0 = trong suốt)
    private static readonly Dictionary<char, string[]> DigitPatterns = new Dictionary<char, string[]>
    {
        { '0', new[] { "0110", "1001", "1001", "1001", "1001", "1001", "0110" }},
        { '1', new[] { "0010", "0110", "0010", "0010", "0010", "0010", "0111" }},
        { '2', new[] { "0110", "1001", "0001", "0010", "0100", "1000", "1111" }},
        { '3', new[] { "1110", "0001", "0001", "0110", "0001", "0001", "1110" }},
        { '4', new[] { "1001", "1001", "1001", "1111", "0001", "0001", "0001" }},
        { '5', new[] { "1111", "1000", "1000", "1110", "0001", "0001", "1110" }},
        { '6', new[] { "0110", "1000", "1000", "1110", "1001", "1001", "0110" }},
        { '7', new[] { "1111", "0001", "0001", "0010", "0100", "0100", "0100" }},
        { '8', new[] { "0110", "1001", "1001", "0110", "1001", "1001", "0110" }},
        { '9', new[] { "0110", "1001", "1001", "0111", "0001", "0001", "0110" }}
    };

    private void OnEnable()
    {
        UpdateEditorDisplay();
    }

    private void OnValidate()
    {
        // Tự động cập nhật khi bạn thay đổi thông số trên Inspector
        UpdateEditorDisplay();
    }

    public void UpdateEditorDisplay()
    {
        // Khi không chạy game, đọc trực tiếp từ PlayerPrefs hoặc dùng số preview
        int levelToDraw = previewLevel;
        if (!Application.isPlaying)
        {
            levelToDraw = PlayerPrefs.GetInt("Game_CurrentLevel", 1);
        }
        SetNumber(levelToDraw);
    }

    public void SetNumber(int number)
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetImage == null) return;

        string numStr = Mathf.Max(0, number).ToString();
        int digitWidth = 4;
        int digitHeight = 7;

        int totalWidth = numStr.Length * digitWidth + (numStr.Length - 1) * digitSpacing;
        int totalHeight = digitHeight;

        Texture2D tex = new Texture2D(totalWidth, totalHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] clearPixels = new Color[totalWidth * totalHeight];
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.clear;
        tex.SetPixels(clearPixels);

        int offsetX = 0;
        foreach (char c in numStr)
        {
            if (DigitPatterns.TryGetValue(c, out string[] pattern))
            {
                for (int y = 0; y < digitHeight; y++)
                {
                    string row = pattern[digitHeight - 1 - y];
                    for (int x = 0; x < digitWidth; x++)
                    {
                        if (row[x] == '1')
                        {
                            tex.SetPixel(offsetX + x, y, numberColor);
                        }
                    }
                }
            }
            offsetX += digitWidth + digitSpacing;
        }

        tex.Apply();

        Sprite numberSprite = Sprite.Create(
            tex,
            new Rect(0, 0, totalWidth, totalHeight),
            new Vector2(0.5f, 0.5f),
            100f
        );

        targetImage.sprite = numberSprite;
        targetImage.rectTransform.sizeDelta = new Vector2(totalWidth * pixelScale, totalHeight * pixelScale);
    }
}