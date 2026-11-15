using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GameProgressManager : MonoBehaviour
{
    [Header("1. Giao diện (UI Panels)")]
    public GameObject mainMenuPanel;
    public GameObject inGamePanel;
    public GameObject winPanel;
    public GameObject losePanel;
    public GameObject watchAdPanel;          

    [Header("2. Tham chiếu hệ thống & Container chứa Block")]
    public MapLoader mapLoader;
    public Transform blocksContainer; 

    private GameSessionData currentSession;
    private bool isPlaying = false;
    private bool hasSpawnedBlocks = false;
    
    public static bool hasReturnedFromGame { get; set; } = false;

    private void Start()
    {
        // 🔹 Ép buộc reset trạng thái khi khởi động game mới
        hasReturnedFromGame = false;

        // 🔹 Đọc session hiện tại
        currentSession = GameDataService.LoadSession();

        // ❌ HÃY XÓA HOẶC COMMENT DÒNG NÀY ĐI ĐỂ KHÔNG BỊ XÓA SESSION LẦN ĐẦU:
        // GameDataService.ClearActiveSession(currentSession.currentLevel);
        // currentSession = GameDataService.LoadSession();

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
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

    public GameSessionData GetCurrentSession() => currentSession;

    public void StartPlaySession()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        currentSession = GameDataService.LoadSession();

        // 🔹 SỬA LẠI ĐIỀU KIỆN TẠI ĐÂY: Miễn là có session dở dang và có block lưu thì phục hồi, 
        // không phụ thuộc vào việc vừa bấm Back hay vừa mở game nữa.
        if (currentSession.hasActiveSession && currentSession.savedBlocks != null && currentSession.savedBlocks.Count > 0)
        {
            RestoreSavedSession();
        }
        else
        {
            GameDataService.ClearActiveSession(currentSession.currentLevel);
            currentSession = GameDataService.LoadSession();
            
            if (mapLoader != null)
            {
                mapLoader.levelNumber = currentSession.currentLevel;
                mapLoader.ClearMap();
                mapLoader.LoadLevel();
            }
        }

        hasReturnedFromGame = false;
        isPlaying = true;
        hasSpawnedBlocks = false;
    }

    public void StartLevelAt(int targetLevel)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        currentSession = GameDataService.StartNewSessionAtLevel(targetLevel);
        hasReturnedFromGame = false;

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
                if (item != null) remainingBlocks++;
                totalCount++;
            }
        }

        if (totalCount > 0) hasSpawnedBlocks = true;

        if (hasSpawnedBlocks && remainingBlocks == 0)
        {
            isPlaying = false;
            hasSpawnedBlocks = false;
            TriggerWinLevel();
        }
    }

    public void SaveCurrentSession()
    {
        // 🔹 Tự động lấy container từ mapLoader nếu blocksContainer trên Inspector chưa được gán
        Transform targetContainer = blocksContainer != null ? blocksContainer : (mapLoader != null ? mapLoader.blocksContainer : null);
        if (targetContainer == null) return;

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

        // 🔹 Đọc dữ liệu mới nhất từ ổ cứng để không bị ghi đè mất số booster đã trừ/cộng trong trận
        GameSessionData latestSession = GameDataService.LoadSession();

        GameDataService.SaveSession(
            latestSession.currentLevel, 
            true, 
            blocksToSave, 
            latestSession.removeBoosterCount, 
            latestSession.reverseBoosterCount, 
            latestSession.shuffleBoosterCount
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
        hasReturnedFromGame = false;
        GameDataService.ClearActiveSession(currentSession.currentLevel + 1);

        if (winPanel != null)
        {
            winPanel.SetActive(true);
            WinPanelUI winUI = winPanel.GetComponent<WinPanelUI>();
            if (winUI != null) winUI.SetupWin(currentSession.currentLevel);
        }
    }

    public void TriggerLoseLevel()
    {
        if (!isPlaying) return;
        isPlaying = false;

        if (inGamePanel != null) inGamePanel.SetActive(false);
        GameDataService.ClearActiveSession(currentSession.currentLevel);

        if (losePanel != null)
        {
            losePanel.SetActive(true);
            LosePanelUI loseUI = losePanel.GetComponent<LosePanelUI>();
            if (loseUI != null) loseUI.SetupLose(currentSession.currentLevel);
        }
    }

    public void ReturnToMainMenu()
    {
        SaveCurrentSession();
        
        // 🔹 BẮT BUỘC THÊM DÒNG NÀY: Cập nhật lại biến currentSession trên RAM ngay lập tức
        // để MainMenuUI đọc được trạng thái hasActiveSession = true vừa lưu!
        currentSession = GameDataService.LoadSession();
        
        // 🔹 Đánh dấu là vừa từ trong game back ra để Main Menu hiện nút Continue
        hasReturnedFromGame = true; 

        if (mapLoader != null) mapLoader.ClearMap();

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        isPlaying = false;
        hasSpawnedBlocks = false;
    }

    /// <summary>
    /// 🔹 Về Main Menu khi người chơi vừa chiến thắng (Không lưu session dở, hiện nút Play)
    /// </summary>
    public void ReturnHomeFromWin()
    {
        hasReturnedFromGame = false; // Đảm bảo không hiện nút Continue ở Main Menu
        
        // Xóa sạch session của level vừa hoàn thành để lần sau vào chơi level mới
        GameDataService.ClearActiveSession(currentSession.currentLevel);

        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        
        isPlaying = false;
        hasSpawnedBlocks = false;
    }

    /// <summary>
    /// Tiếp tục chơi lại phiên hiện tại ngay tại thời điểm thua (giữ nguyên hiện trạng map và vị trí block)
    /// </summary>
    public void ContinueCurrentSessionAfterLose()
    {
        if (losePanel != null) losePanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);

        // Tải lại session hiện tại
        currentSession = GameDataService.LoadSession();

        // Xóa map cũ trên màn hình để tránh chồng chất
        if (mapLoader != null)
        {
            mapLoader.ClearMap();
        }

        // Khôi phục lại đúng nguyên trạng các block đang lưu trong session
        RestoreSavedSession();

        isPlaying = true;
        hasSpawnedBlocks = false;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && inGamePanel != null && inGamePanel.activeSelf && isPlaying) SaveCurrentSession();
    }

    private void OnApplicationQuit()
    {
        if (inGamePanel != null && inGamePanel.activeSelf && isPlaying) SaveCurrentSession();
    }
}