using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

public static class PixelNumberSprites
{
    private static readonly Dictionary<int, Sprite> cachedSprites = new Dictionary<int, Sprite>();

    // Dữ liệu ma trận 5x7 cho các chữ số từ 0 đến 9 (mỗi số gồm 7 hàng, từ đỉnh xuống đáy)
    private static readonly byte[][] digitBitmaps = new byte[][]
    {
        // 0
        new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
        // 1
        new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
        // 2
        new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F },
        // 3
        new byte[] { 0x1E, 0x01, 0x01, 0x0E, 0x01, 0x11, 0x0E },
        // 4
        new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x01, 0x01, 0x01 },
        // 5
        new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
        // 6
        new byte[] { 0x0E, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x0E },
        // 7
        new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
        // 8
        new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
        // 9
        new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x0E }
    };

    /// <summary>
    /// Lấy Sprite số Pixel Art theo chữ số (0 đến 9)
    /// </summary>
    public static Sprite GetSprite(int digit)
    {
        digit = Mathf.Clamp(digit, 0, 9);

        if (cachedSprites.TryGetValue(digit, out Sprite existing) && existing != null && existing.texture != null)
        {
            return existing;
        }

        Sprite created = CreateDigitSprite(digit);
        cachedSprites[digit] = created;
        return created;
    }

    /// <summary>
    /// Tạo Sprite Pixel Art sắc nét có viền viền tương phản cao
    /// </summary>
    private static Sprite CreateDigitSprite(int digit)
    {
        const int fontW = 5;
        const int fontH = 7;
        const int border = 1;
        const int outW = fontW + border * 2; // 7
        const int outH = fontH + border * 2; // 9
        const int scale = 2; // Phóng to 2x pixel để nét căng chuẩn retro

        int texW = outW * scale; // 14
        int texH = outH * scale; // 18

        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        byte[] rows = digitBitmaps[digit];

        // Màu thân số: Trắng tinh
        Color fillColor = Color.white;
        // Màu viền outline: Xanh băng đậm tạo chiều sâu tương phản nổi bật
        Color outlineColor = new Color(0.04f, 0.20f, 0.32f, 1f);
        Color emptyColor = Color.clear;

        // Bảng bitmap tạm (outW x outH)
        bool[,] rawGrid = new bool[outW, outH];
        for (int y = 0; y < fontH; y++)
        {
            // Hàng 0 ở đáy texture, hàng 6 ở đỉnh
            byte rowByte = rows[(fontH - 1) - y];
            for (int x = 0; x < fontW; x++)
            {
                // Bit cao nhất ở bên trái (bit 4)
                bool isSet = ((rowByte >> (fontW - 1 - x)) & 1) == 1;
                rawGrid[x + border, y + border] = isSet;
            }
        }

        // Tạo mảng màu cho texture sau khi mở rộng viền và scale 2x
        for (int py = 0; py < outH; py++)
        {
            for (int px = 0; px < outW; px++)
            {
                Color pixelColor = emptyColor;

                if (rawGrid[px, py])
                {
                    pixelColor = fillColor;
                }
                else
                {
                    // Kiểm tra xem có nằm sát pixel thân số không để vẽ viền
                    bool isBorder = false;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = px + dx;
                            int ny = py + dy;
                            if (nx >= 0 && nx < outW && ny >= 0 && ny < outH && rawGrid[nx, ny])
                            {
                                isBorder = true;
                                break;
                            }
                        }
                        if (isBorder) break;
                    }

                    if (isBorder)
                    {
                        pixelColor = outlineColor;
                    }
                }

                // Tô pixel vào texture theo kích thước phóng to scale x scale
                for (int sy = 0; sy < scale; sy++)
                {
                    for (int sx = 0; sx < scale; sx++)
                    {
                        tex.SetPixel(px * scale + sx, py * scale + sy, pixelColor);
                    }
                }
            }
        }

        tex.Apply();

        // Tạo Sprite với PPU = 38 (đồng bộ với sprite của game)
        Sprite sp = Sprite.Create(
            tex,
            new Rect(0, 0, texW, texH),
            new Vector2(0.5f, 0.5f),
            38f,
            0,
            SpriteMeshType.FullRect
        );
        sp.name = $"PixelNum_{digit}";
        return sp;
    }
}

