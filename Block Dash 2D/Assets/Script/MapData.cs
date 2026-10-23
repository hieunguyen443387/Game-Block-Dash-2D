using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BlockData
{
    [Tooltip("Tên định danh loại block (khớp với Prefab)")]
    public string blockType;

    [Tooltip("Tọa độ cột trên bàn cờ (0 đến columns - 1)")]
    public int gridX;

    [Tooltip("Tọa độ hàng trên bàn cờ (0 đến rows - 1)")]
    public int gridY;

    [Tooltip("Tọa độ thế giới X")]
    public float posX;

    [Tooltip("Tọa độ thế giới Y")]
    public float posY;

    [Tooltip("Góc xoay quanh trục Z")]
    public float rotationZ;
}

[Serializable]
public class MapData
{
    public string mapName = "Level_1";
    public int rows = 12;
    public int columns = 12;
    public float cellSizePixels = 36f;
    public float spacingPixels = 0f;
    public float pixelsPerUnit = 100f;
    public float startPosX = 0f;
    public float startPosY = 0f;

    public List<BlockData> blocks = new List<BlockData>();
}

[Serializable]
public class BlockPaletteEntry
{
    [Tooltip("Tên định danh của Block (ví dụ: BlockThuong, Block2Chieu, Gate, Trap...)")]
    public string blockType;

    [Tooltip("Prefab tương ứng sẽ được sinh ra khi Load Map")]
    public GameObject prefab;
}
