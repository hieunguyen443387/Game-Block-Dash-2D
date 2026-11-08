using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SavedBlockState
{
    public string blockType;
    public float posX;
    public float posY;
    public float rotationZ;
    public int gridX;
    public int gridY;
    public bool isFrozen;
    public int freezeCount;
    public bool hasBoom;
    public int hitCount;
}

[Serializable]
public class GameSessionData
{
    [Header("Tiến trình màn chơi")]
    public int currentLevel = 1; // Đã thêm biến lưu số level hiện tại ở đây
    public bool hasActiveSession = false;
    
    [Header("Danh sách block đang chơi dở")]
    public List<SavedBlockState> savedBlocks = new List<SavedBlockState>();
}