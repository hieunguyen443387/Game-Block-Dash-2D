using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class BoosterBadgeDisplay : MonoBehaviour
{
    [Header("Cấu hình hiển thị Booster")]
    [Range(0, 99)]
    public int previewCount = 3;

    public Color numberColor = Color.white;
    public Color circleColor = new Color32(213, 42, 40, 255); // #D52A28
    public int digitSpacing = 1;
    public float pixelScale = 6f;
    public bool hideWhenZero = true;

    private Image targetImage;

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
        UpdateEditorDisplay();
    }

    public void UpdateEditorDisplay()
    {
        if (!Application.isPlaying)
        {
            SetBoosterCount(previewCount);
        }
    }

    public void SetBoosterCount(int count)
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetImage == null) return;

        // Xử lý khi số lượng về 0 hoặc nhỏ hơn
        if (hideWhenZero && count <= 0)
        {
            targetImage.sprite = null; // Xóa sạch hình ảnh cũ
            if (gameObject.activeSelf) 
            {
                gameObject.SetActive(false); // Ẩn hẳn object badge đi
            }
            return;
        }

        // Nếu số lượng > 0 thì đảm bảo object phải được bật lên
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        string numStr = Mathf.Max(0, count).ToString();
        int digitWidth = 4;
        int digitHeight = 7;

        int textWidth = numStr.Length * digitWidth + (numStr.Length - 1) * digitSpacing;
        int paddingX = 4; 
        int paddingY = 3; 

        int totalWidth = textWidth + paddingX * 2;
        int totalHeight = digitHeight + paddingY * 2;

        Texture2D tex = new Texture2D(totalWidth, totalHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] clearPixels = new Color[totalWidth * totalHeight];
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.clear;
        tex.SetPixels(clearPixels);

        // 1. Tô kín hình tròn nền đỏ (#D52A28)
        float centerX = (totalWidth - 1) / 2f;
        float centerY = (totalHeight - 1) / 2f;
        float radiusX = centerX - 0.5f;
        float radiusY = centerY - 0.5f;

        for (int y = 0; y < totalHeight; y++)
        {
            for (int x = 0; x < totalWidth; x++)
            {
                float dx = (x - centerX) / radiusX;
                float dy = (y - centerY) / radiusY;
                float distSq = dx * dx + dy * dy;

                if (distSq <= 1.0f)
                {
                    tex.SetPixel(x, y, circleColor);
                }
            }
        }

        // 2. Vẽ chữ số đè lên trên
        int startX = paddingX;
        int startY = paddingY;

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
                            tex.SetPixel(startX + x, startY + y, numberColor);
                        }
                    }
                }
            }
            startX += digitWidth + digitSpacing;
        }

        tex.Apply();

        Sprite badgeSprite = Sprite.Create(
            tex,
            new Rect(0, 0, totalWidth, totalHeight),
            new Vector2(0.5f, 0.5f),
            100f
        );

        targetImage.sprite = badgeSprite;
        targetImage.rectTransform.sizeDelta = new Vector2(totalWidth * pixelScale, totalHeight * pixelScale);
    }
}