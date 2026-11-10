using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameProgressManager : MonoBehaviour
{
    [Header("1. Giao diện (UI Panels)")]
    public GameObject mainMenuPanel;
    public GameObject inGamePanel;
    public GameObject winPanel;

    [Header("2. UI Elements (Số Level & Chiến thắng)")]
    public PixelNumberDisplay levelNumberDisplay;
    public TextMeshProUGUI winLevelText;

    [Header("3. Tham chiếu hệ thống & Container chứa Block")]
    public MapLoader mapLoader;
    
    [Tooltip("Kéo object BlocksContainer độc lập bên ngoài vào đây")]
    public Transform blocksContainer; 

    private GameSessionData currentSession;
    private bool isPlaying = false;
    private bool hasSpawnedBlocks = false;

    private void Start()
    {
        currentSession = GameDataService.LoadSession();

        UpdateLevelButtonUI();

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.previewLevel = currentSession.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        // LUÔN MỞ MAIN MENU KHI VỪA VÀO GAME VÀ CHƯA LOAD MAP GÌ CẢ
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        isPlaying = false;
        hasSpawnedBlocks = false;
    }

    private void Update()
    {
        // Chỉ làm nhiệm vụ kiểm tra điều kiện thắng khi game đang thực sự chơi
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

    /// <summary>
    /// Nút Play thông thường (tiếp tục phiên chơi hiện tại hoặc load level đang lưu)
    /// </summary>
    /// <summary>
    /// Nút Play thông thường (tiếp tục phiên chơi hiện tại hoặc load level đang lưu)
    /// </summary>
    public void OnClickPlay()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);

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
                mapLoader.ClearMap(); // Xóa bàn cờ cũ trước khi load mới
                mapLoader.LoadLevel(); // ⬅️ Bổ sung gọi lệnh load map tại đây để block được sinh ra!
            }
        }

        isPlaying = true;
        hasSpawnedBlocks = false;
    }

    /// <summary>
    /// Bắt đầu chơi một level cụ thể được chọn (Ví dụ: Chọn thẳng Level 1 từ menu chọn màn).
    /// </summary>
    public void SelectLevelAndPlay(int targetLevel)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);

        currentSession = GameDataService.StartNewSessionAtLevel(targetLevel);
        UpdateLevelButtonUI();

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

    /// <summary>
    /// Kiểm tra xem trên BlocksContainer độc lập còn block nào không. Nếu hết sạch -> Thắng!
    /// </summary>
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

        // Nếu đã từng có block và bây giờ số block về 0 -> Thắng!
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

        GameDataService.SaveSession(currentSession.currentLevel, true, blocksToSave);
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
        // 1. Tăng level lên đúng 1 đơn vị so với level hiện tại
        int nextLevel = currentSession.currentLevel + 1;

        // 2. Kiểm tra xem file map tiếp theo có tồn tại không trong thư mục Resources/Maps
        TextAsset nextMapTest = Resources.Load<TextAsset>("Maps/Level_" + nextLevel);
        if (nextMapTest == null)
        {
            // Nếu đã hết map (đạt màn cuối), giữ nguyên ở level hiện tại (hoặc lặp lại màn cuối)
            nextLevel = currentSession.currentLevel; 
        }

        // Xóa session cũ và lưu level tiếp theo
        GameDataService.ClearActiveSession(nextLevel);
        
        currentSession = GameDataService.LoadSession();
        UpdateLevelButtonUI();

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
            mapLoader.LoadLevel(); // Load map của level mới
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