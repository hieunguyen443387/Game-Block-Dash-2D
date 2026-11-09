using System.Collections.Generic;
using UnityEngine;

public class GameProgressManager : MonoBehaviour
{
    [Header("1. Giao diện (UI Panels)")]
    public GameObject mainMenuPanel;
    public GameObject inGamePanel;

    [Header("2. UI Elements (Số Level Pixel)")]
    [Tooltip("Kéo object gắn script PixelNumberDisplay vào đây")]
    public PixelNumberDisplay levelNumberDisplay;

    [Header("3. Tham chiếu hệ thống")]
    public MapLoader mapLoader;

    private GameSessionData currentSession;

    private void Start()
    {
        currentSession = GameDataService.LoadSession();

        if (mapLoader != null)
        {
            mapLoader.levelNumber = currentSession.currentLevel;
            mapLoader.ClearMap();
        }

        UpdateLevelButtonUI();

        // THÊM DÒNG NÀY ĐỂ ÉP SỐ HIỂN THỊ CHUẨN XÁC KHI VÀO GAME
        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
    }

    /// <summary>
    /// Cập nhật số level hiển thị trên nút bằng PixelNumberDisplay
    /// </summary>
    private void UpdateLevelButtonUI()
    {
        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.SetNumber(currentSession.currentLevel);
        }
    }

    public void OnClickPlay()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);

        currentSession = GameDataService.LoadSession();

        if (currentSession.hasActiveSession && currentSession.savedBlocks != null && currentSession.savedBlocks.Count > 0)
        {
            RestoreSavedSession();
        }
        else
        {
            if (mapLoader != null)
            {
                mapLoader.levelNumber = currentSession.currentLevel;
                mapLoader.LoadLevel();
            }
        }
    }

    public void SaveCurrentSession()
    {
        if (mapLoader == null || mapLoader.blocksContainer == null) return;

        List<SavedBlockState> blocksToSave = new List<SavedBlockState>();
        Transform container = mapLoader.blocksContainer;

        foreach (Transform child in container)
        {
            BlockItem item = child.GetComponent<BlockItem>();
            if (item != null)
            {
                SavedBlockState bState = new SavedBlockState
                {
                    blockType = item.blockType,
                    posX = child.position.x,
                    posY = child.position.y,
                    rotationZ = child.eulerAngles.z,
                    gridX = item.gridX,
                    gridY = item.gridY
                };

                FreezableBlock freezable = child.GetComponent<FreezableBlock>();
                if (freezable != null)
                {
                    bState.isFrozen = freezable.isFrozen;
                    bState.freezeCount = freezable.freezeCount;
                }

                HitCountdownBoomBlock boom = child.GetComponent<HitCountdownBoomBlock>();
                if (boom != null)
                {
                    bState.hasBoom = boom.hasBoom;
                    bState.hitCount = boom.hitCount;
                }

                blocksToSave.Add(bState);
            }
        }

        GameDataService.SaveSession(currentSession.currentLevel, true, blocksToSave);
    }

    private void RestoreSavedSession()
    {
        if (mapLoader == null) return;

        mapLoader.LoadBoardAndGatesOnly(currentSession.currentLevel);

        if (mapLoader.blocksContainer != null)
        {
            Transform container = mapLoader.blocksContainer;

            foreach (var b in currentSession.savedBlocks)
            {
                GameObject prefab = mapLoader.GetPrefabFromPalette(b.blockType);
                if (prefab != null)
                {
                    Vector2 spawnPos = new Vector2(b.posX, b.posY);
                    Quaternion rotation = Quaternion.Euler(0, 0, b.rotationZ);

                    GameObject blockObj = Instantiate(prefab, spawnPos, rotation, container);
                    blockObj.name = $"{b.blockType}_{b.gridX}_{b.gridY}";

                    BlockItem item = blockObj.GetComponent<BlockItem>();
                    if (item != null)
                    {
                        item.blockType = b.blockType;
                        item.gridX = b.gridX;
                        item.gridY = b.gridY;
                    }

                    if (b.isFrozen)
                    {
                        FreezableBlock freezable = blockObj.GetComponent<FreezableBlock>() ?? blockObj.AddComponent<FreezableBlock>();
                        freezable.freezeCount = b.freezeCount;
                        freezable.SetFrozen(true, b.freezeCount);
                    }

                    if (b.hasBoom)
                    {
                        HitCountdownBoomBlock boom = blockObj.GetComponent<HitCountdownBoomBlock>() ?? blockObj.AddComponent<HitCountdownBoomBlock>();
                        boom.hitCount = b.hitCount;
                        boom.SetBoom(true, b.hitCount);
                    }

                    TwoWayBlock twoWay = blockObj.GetComponent<TwoWayBlock>();
                    if (twoWay != null)
                    {
                        if (Mathf.Abs(b.rotationZ - 0f) < 5f) twoWay.horizontalStartDir = HorizontalStartDirection.Right;
                        else if (Mathf.Abs(b.rotationZ - 180f) < 5f) twoWay.horizontalStartDir = HorizontalStartDirection.Left;
                        else if (Mathf.Abs(b.rotationZ - 90f) < 5f) { twoWay.moveAxis = TwoWayAxis.Vertical; twoWay.verticalStartDir = VerticalStartDirection.Top; }
                        else if (Mathf.Abs(b.rotationZ - 270f) < 5f) { twoWay.moveAxis = TwoWayAxis.Vertical; twoWay.verticalStartDir = VerticalStartDirection.Down; }

                        twoWay.InitializeCurrentDirection();
                        twoWay.ApplyRotationFromDirection();
                    }
                }
            }
        }
    }

    public void CompleteCurrentLevel()
    {
        int nextLevel = currentSession.currentLevel + 1;
        GameDataService.ClearActiveSession(nextLevel);
        
        currentSession = GameDataService.LoadSession();
        UpdateLevelButtonUI();

        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
    }

    public void ShowMainMenu()
    {
        SaveCurrentSession();

        currentSession = GameDataService.LoadSession();
        UpdateLevelButtonUI();

        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && inGamePanel != null && inGamePanel.activeSelf)
        {
            SaveCurrentSession();
        }
    }

    private void OnApplicationQuit()
    {
        if (inGamePanel != null && inGamePanel.activeSelf)
        {
            SaveCurrentSession();
        }
    }
}