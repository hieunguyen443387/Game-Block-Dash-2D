using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public class GameProgressManager : MonoBehaviour
{
    [Header("1. Giao diện (UI Panels)")]
    public GameObject mainMenuPanel;
    public GameObject inGamePanel;
    public GameObject winPanel;
    
    [Header("Mock Watch Ad Panel")]
    public GameObject watchAdPanel;          // Kéo Panel xem quảng cáo vào đây (chỉ có nút ad)
    private string currentAdBoosterType = "";// Lưu loại booster đang cần xem quảng cáo

    [Header("2. UI Elements (Số Level & Chiến thắng)")]
    public PixelNumberDisplay levelNumberDisplay;
    public TextMeshProUGUI winLevelText;

    [Header("3. Booster Badge Displays (Số lượng lượt dùng trên nút)")]
    public BoosterBadgeDisplay removeBadgeDisplay;
    public BoosterBadgeDisplay reverseBadgeDisplay;
    public BoosterBadgeDisplay shuffleBadgeDisplay;

    [Header("4. Tham chiếu hệ thống & Container chứa Block")]
    public MapLoader mapLoader;
    
    [Tooltip("Kéo object BlocksContainer độc lập bên ngoài vào đây")]
    public Transform blocksContainer; 

    private GameSessionData currentSession;
    private bool isPlaying = false;
    private bool hasSpawnedBlocks = false;

    private void Start()
    {
        currentSession = GameDataService.LoadSession();
        Debug.Log($"<color=cyan>[Booster Status] Load session thành công - Level: {currentSession.currentLevel} | Remove: {currentSession.removeBoosterCount} | Reverse: {currentSession.reverseBoosterCount} | Shuffle: {currentSession.shuffleBoosterCount}</color>");

        UpdateLevelButtonUI();
        UpdateBoosterUI();

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (watchAdPanel != null) watchAdPanel.SetActive(false);

        isPlaying = false;
        hasSpawnedBlocks = false;
    }

    private void Update()
    {
        if (isPlaying)
        {
            CheckWinCondition();
        }
    }

    private void UpdateLevelButtonUI()
    {
        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.SetNumber(currentSession.currentLevel);
        }
    }

    private void UpdateBoosterUI()
    {
        if (removeBadgeDisplay != null) removeBadgeDisplay.SetBoosterCount(currentSession.removeBoosterCount);
        if (reverseBadgeDisplay != null) reverseBadgeDisplay.SetBoosterCount(currentSession.reverseBoosterCount);
        if (shuffleBadgeDisplay != null) shuffleBadgeDisplay.SetBoosterCount(currentSession.shuffleBoosterCount);
    }

    public void OnClickPlay()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);

        currentSession = GameDataService.LoadSession();
        UpdateBoosterUI();

        if (currentSession.hasActiveSession && currentSession.savedBlocks != null && currentSession.savedBlocks.Count > 0)
        {
            RestoreSavedSession();
        }
        else
        {
            if (mapLoader != null)
            {
                mapLoader.levelNumber = currentSession.currentLevel;
                mapLoader.ClearMap();
                mapLoader.LoadLevel();
            }
        }

        isPlaying = true;
        hasSpawnedBlocks = false;
    }

    public void SelectLevelAndPlay(int targetLevel)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);

        currentSession = GameDataService.StartNewSessionAtLevel(targetLevel);
        UpdateLevelButtonUI();
        UpdateBoosterUI();

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        if (mapLoader != null)
        {
            mapLoader.ClearMap();
            mapLoader.levelNumber = currentSession.currentLevel;
            mapLoader.LoadLevel();
        }

        isPlaying = true;
        hasSpawnedBlocks = false;
    }

    private void CheckWinCondition()
    {
        if (blocksContainer == null) return;

        int remainingBlocks = 0;
        int totalCount = 0;

        foreach (Transform child in blocksContainer)
        {
            if (child != null && child.gameObject.activeSelf)
            {
                BlockItem item = child.GetComponent<BlockItem>();
                if (item != null)
                {
                    remainingBlocks++;
                }
                totalCount++;
            }
        }

        if (totalCount > 0)
        {
            hasSpawnedBlocks = true;
        }

        if (hasSpawnedBlocks && remainingBlocks == 0)
        {
            isPlaying = false;
            hasSpawnedBlocks = false;
            TriggerWinLevel();
        }
    }

    public void SaveCurrentSession()
    {
        if (blocksContainer == null) return;

        List<SavedBlockState> blocksToSave = new List<SavedBlockState>();

        foreach (Transform child in blocksContainer)
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

        GameDataService.SaveSession(
            currentSession.currentLevel, 
            true, 
            blocksToSave, 
            currentSession.removeBoosterCount, 
            currentSession.reverseBoosterCount, 
            currentSession.shuffleBoosterCount
        );
    }

    private void RestoreSavedSession()
    {
        if (mapLoader == null) return;

        mapLoader.LoadBoardAndGatesOnly(currentSession.currentLevel);

        Transform targetContainer = blocksContainer != null ? blocksContainer : mapLoader.blocksContainer;

        if (targetContainer != null)
        {
            foreach (var b in currentSession.savedBlocks)
            {
                GameObject prefab = mapLoader.GetPrefabFromPalette(b.blockType);
                if (prefab != null)
                {
                    Vector2 spawnPos = new Vector2(b.posX, b.posY);
                    Quaternion rotation = Quaternion.Euler(0, 0, b.rotationZ);

                    GameObject blockObj = Instantiate(prefab, spawnPos, rotation, targetContainer);
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

    public void TriggerWinLevel()
    {
        if (inGamePanel != null) inGamePanel.SetActive(false);

        GameDataService.ClearActiveSession(currentSession.currentLevel + 1);

        if (winLevelText != null)
        {
            winLevelText.text = $"Hoàn thành cấp độ {currentSession.currentLevel}!";
        }

        if (winPanel != null) winPanel.SetActive(true);
    }

    public void OnClickNextLevel()
    {
        int nextLevel = currentSession.currentLevel + 1;

        string nextMapPath = Path.Combine(Application.persistentDataPath, "Levels", "Level_" + nextLevel + ".txt");
        if (!File.Exists(nextMapPath))
        {
            nextLevel = currentSession.currentLevel; 
        }

        GameDataService.ClearActiveSession(nextLevel);
        
        currentSession = GameDataService.LoadSession();
        UpdateLevelButtonUI();
        UpdateBoosterUI();

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        if (winPanel != null) winPanel.SetActive(false);
        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (mapLoader != null)
        {
            mapLoader.levelNumber = currentSession.currentLevel;
            mapLoader.LoadLevel();
        }

        isPlaying = true;
        hasSpawnedBlocks = false;
    }

    public void OnClickHomeFromWin()
    {
        int nextLevel = currentSession.currentLevel + 1;
        GameDataService.ClearActiveSession(nextLevel);

        currentSession = GameDataService.LoadSession();
        UpdateLevelButtonUI();
        UpdateBoosterUI();

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        if (winPanel != null) winPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        
        isPlaying = false;
        hasSpawnedBlocks = false;
    }

    public void ShowMainMenu()
    {
        SaveCurrentSession();

        currentSession = GameDataService.LoadSession();
        UpdateLevelButtonUI();
        UpdateBoosterUI();

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        isPlaying = false;
        hasSpawnedBlocks = false;
    }

    // ==========================================
    // CÁC HÀM XỬ LÝ SỬ DỤNG BOOSTER & XEM QUẢNG CÁO
    // ==========================================

    public void UseRemoveBooster()
    {
        currentSession = GameDataService.LoadSession();

        if (currentSession.removeBoosterCount > 0)
        {
            currentSession.removeBoosterCount--;
            Debug.Log($"<color=yellow>[Booster] Đã dùng REMOVE! Số lượt còn lại: {currentSession.removeBoosterCount}</color>");
            SaveCurrentSession();
            UpdateBoosterUI();
        }
        else
        {
            Debug.Log("<color=red>[Booster] Đã HẾT lượt sử dụng Remove! Hiển thị nút quảng cáo.</color>");
            TriggerWatchAdPopup("Remove");
        }
    }

    public void UseReverseBooster()
    {
        currentSession = GameDataService.LoadSession();

        if (currentSession.reverseBoosterCount > 0)
        {
            currentSession.reverseBoosterCount--;
            Debug.Log($"<color=yellow>[Booster] Đã dùng REVERSE! Số lượt còn lại: {currentSession.reverseBoosterCount}</color>");
            SaveCurrentSession();
            UpdateBoosterUI();
        }
        else
        {
            Debug.Log("<color=red>[Booster] Đã HẾT lượt sử dụng Reverse! Hiển thị nút quảng cáo.</color>");
            TriggerWatchAdPopup("Reverse");
        }
    }

    public void UseShuffleBooster()
    {
        currentSession = GameDataService.LoadSession();

        if (currentSession.shuffleBoosterCount > 0)
        {
            currentSession.shuffleBoosterCount--;
            Debug.Log($"<color=yellow>[Booster] Đã dùng SHUFFLE! Số lượt còn lại: {currentSession.shuffleBoosterCount}</color>");
            SaveCurrentSession();
            UpdateBoosterUI();
        }
        else
        {
            Debug.Log("<color=red>[Booster] Đã HẾT lượt sử dụng Shuffle! Hiển thị nút quảng cáo.</color>");
            TriggerWatchAdPopup("Shuffle");
        }
    }

    private void TriggerWatchAdPopup(string boosterType)
    {
        currentAdBoosterType = boosterType;
        if (watchAdPanel != null)
        {
            watchAdPanel.SetActive(true); // Chỉ bật khung chứa nút ad lên
        }
    }

    public void OnClickWatchAd()
    {
        currentSession = GameDataService.LoadSession();

        if (currentAdBoosterType == "Remove")
        {
            currentSession.removeBoosterCount++;
        }
        else if (currentAdBoosterType == "Reverse")
        {
            currentSession.reverseBoosterCount++;
        }
        else if (currentAdBoosterType == "Shuffle")
        {
            currentSession.shuffleBoosterCount++;
        }

        SaveCurrentSession();
        UpdateBoosterUI(); // Cập nhật lại giao diện, badge sẽ tự hiện lên và số lượng tăng lên 1

        Debug.Log($"<color=green>[Ads] Xem quảng cáo thành công! Đã cộng 1 lượt {currentAdBoosterType} Booster.</color>");

        if (watchAdPanel != null)
        {
            watchAdPanel.SetActive(false);
        }
    }

    public void OnClickCloseAdPanel()
    {
        if (watchAdPanel != null)
        {
            watchAdPanel.SetActive(false);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && inGamePanel != null && inGamePanel.activeSelf && isPlaying)
        {
            SaveCurrentSession();
        }
    }

    private void OnApplicationQuit()
    {
        if (inGamePanel != null && inGamePanel.activeSelf && isPlaying)
        {
            SaveCurrentSession();
        }
    }
}