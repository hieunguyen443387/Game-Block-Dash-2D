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
public class ExitGateData
{
    public int startColumn = 3;
    public int columnSpan = 2;
    public int exitRow = 12;
    public string gateDirection = "Up"; // Lưu hướng cổng (Up, Down, Left, Right)
    
    // Tọa độ thế giới để load chính xác 100% như lúc thiết kế
    public float posX;
    public float posY;

    // Cấu hình từ ExitGateManager (ExitGateGenerator)
    public bool autoCenter = false;
    public int placement = 0; // GatePlacement enum
    public float extraCellsUp = 0f;
    public float offsetX = 0f;
    public float offsetY = 0f;
}

[System.Serializable]
public class CellData
{
    public int x;
    public int y;
}

[System.Serializable]
public class MapData
{
    public string mapName = "Level_1";
    [Header("Board Settings")]
    public int rows;
    public int columns;
    public float cellSizePixels = 36f;
    public float spacingPixels = 0f;
    public float pixelsPerUnit = 100f;
    public float startPosX = 0f;
    public float startPosY = 0f;

    [Header("Board Cells (Danh sách ô sàn)")]
    public List<CellData> boardCells = new List<CellData>();

    [Header("SideLine Settings")]
    public int sidelineThickness = 1;
    public float sidelineSizePixels = 36f;

    [Header("SidelineSurround Settings")]
    public int surroundThickness = 1;
    public int surroundMode = 0; // 0: TileByPixelSize, 1: MatchCells, 2: ExactSprite

    public List<BlockData> blocks = new List<BlockData>();
    public List<ExitGateData> exitGates = new List<ExitGateData>();
}

[Serializable]
public class BlockPaletteEntry
{
    [Tooltip("Tên định danh của Block (ví dụ: BlockThuong, Block2Chieu, Gate, Trap...)")]
    public string blockType;

    [Tooltip("Prefab tương ứng sẽ được sinh ra khi Load Map")]
    public GameObject prefab;
}