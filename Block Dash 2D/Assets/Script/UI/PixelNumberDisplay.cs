using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways] // Cho phép script chạy ngay cả khi không Play game
[RequireComponent(typeof(Image))]
public class PixelNumberDisplay : MonoBehaviour
{
    [Header("Cấu hình hiển thị")]
    [Tooltip("Số level xem trước (chỉ dùng khi TẮT 'Tự động lấy level')")]
    public int previewLevel = 1;

    [Tooltip("Màu của số (Mặc định là màu kem sáng giống chữ LEVEL)")]
    public Color numberColor = new Color32(250, 238, 212, 255);

    [Tooltip("Khoảng cách (tính bằng pixel) giữa các chữ số")]
    public int digitSpacing = 1;

    [Tooltip("Độ phóng đại của mỗi ô pixel khi hiển thị lên UI")]
    public float pixelScale = 8f;

    [Header("Tự động lấy level (dùng được ở cả Main Menu và trong game)")]
    [Tooltip("BẬT: tự đọc level hiện tại đã lưu trong dữ liệu game. TẮT: chỉ hiển thị previewLevel hoặc số do script khác truyền vào qua SetNumber().")]
    public bool autoLoadLevel = true;

    [Tooltip("Thời gian (giây) giữa 2 lần kiểm tra level. 0 = kiểm tra mỗi khung hình")]
    public float refreshInterval = 0.1f;

    private Image targetImage;

    // Cache texture/sprite đã tạo để tránh rò rỉ bộ nhớ khi cập nhật nhiều lần
    private Sprite generatedSprite;
    private Texture2D generatedTexture;
    private int lastDrawnNumber = int.MinValue;
    private float nextRefreshTime;

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
        RefreshDisplay(true);
    }

    private void OnValidate()
    {
        // Tự động cập nhật khi bạn thay đổi thông số trên Inspector
        RefreshDisplay(true);
    }

    private void Update()
    {
        // Trong game: tự bám theo level hiện tại -> dùng được cho cả HUD trong game
        if (!Application.isPlaying || !autoLoadLevel) return;

        if (refreshInterval > 0f)
        {
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + refreshInterval;
        }

        RefreshDisplay(false);
    }

    private void OnDestroy()
    {
        DestroyGenerated();
    }

    /// <summary>
    /// Cập nhật ngay số đang hiển thị theo level hiện tại.
    /// Có thể gọi từ script khác sau khi chuyển level nếu không muốn chờ tự động cập nhật.
    /// </summary>
    public void RefreshFromData()
    {
        RefreshDisplay(true);
    }

    /// <summary>
    /// Giữ lại hàm cũ để các script đang gọi không bị lỗi.
    /// </summary>
    public void UpdateEditorDisplay()
    {
        RefreshDisplay(false);
    }

    private void RefreshDisplay(bool force)
    {
        int levelToDraw = autoLoadLevel ? GameDataService.GetSavedLevel() : previewLevel;
        SetNumber(levelToDraw, force);
    }

    public void SetNumber(int number, bool forceRebuild = false)
    {
        if (targetImage == null) targetImage = GetComponent<Image>();
        if (targetImage == null) return;

        number = Mathf.Max(0, number);

        // Số không đổi và sprite vẫn còn -> không cần tạo lại texture
        if (!forceRebuild &&
            number == lastDrawnNumber &&
            generatedSprite != null &&
            targetImage.sprite == generatedSprite)
        {
            return;
        }

        string numStr = number.ToString();
        int digitWidth = 4;
        int digitHeight = 7;

        int totalWidth = numStr.Length * digitWidth + (numStr.Length - 1) * digitSpacing;
        int totalHeight = digitHeight;

        Texture2D tex = new Texture2D(totalWidth, totalHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = $"PixelNumber_{number}"
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
        numberSprite.name = $"PixelNumberSprite_{number}";

        // Gán sprite mới trước, sau đó mới huỷ sprite/texture cũ để tránh nháy hình
        Sprite oldSprite = generatedSprite;
        Texture2D oldTexture = generatedTexture;

        generatedSprite = numberSprite;
        generatedTexture = tex;
        lastDrawnNumber = number;

        targetImage.sprite = numberSprite;
        targetImage.rectTransform.sizeDelta = new Vector2(totalWidth * pixelScale, totalHeight * pixelScale);

        DestroyObjectInternal(oldSprite);
        DestroyObjectInternal(oldTexture);
    }

    private void DestroyGenerated()
    {
        DestroyObjectInternal(generatedSprite);
        DestroyObjectInternal(generatedTexture);
        generatedSprite = null;
        generatedTexture = null;
        lastDrawnNumber = int.MinValue;
    }

    private static void DestroyObjectInternal(Object obj)
    {
        if (obj == null) return;

        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}
