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

    /// <summary>
    /// Level sẽ chơi sau khi thắng level hiện tại (level vừa thắng + 1).
    /// Nếu đã là level cuối cùng thì trả về chính level đó để chơi lại.
    /// </summary>
    public int GetNextLevelAfterWin()
    {
        int completedLevel = currentSession != null ? currentSession.currentLevel : 1;
        int nextLevel = completedLevel + 1;

        if (mapLoader != null)
        {
            int maxLevel = mapLoader.GetMaxAvailableLevel();
            if (nextLevel > maxLevel) nextLevel = maxLevel;
        }

        return nextLevel;
    }

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

        foreach (Transform child in targetContainer)
        {
            BlockItem item = child.GetComponent<BlockItem>();
            if (item != null)
            {
                // 🔹 Không lưu những block đang phát nổ: chúng đã bị loại khỏi bàn chơi,
                // nếu lưu lại thì khi bấm Continue sẽ xuất hiện lại dưới dạng block thường (mất bom).
                HitCountdownBoomBlock hitBoom = child.GetComponent<HitCountdownBoomBlock>();
                if (hitBoom != null && hitBoom.IsExploding) continue;

                TimeCountdownBoomBlock timeBoom = child.GetComponent<TimeCountdownBoomBlock>();
                if (timeBoom != null && timeBoom.IsExploding) continue;

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

        // Đánh dấu đã hoàn thành level này: xóa session dở và ghi nhớ level kế tiếp vào PlayerPrefs
        GameDataService.ClearActiveSession(GetNextLevelAfterWin());

        if (winPanel != null)
        {
            winPanel.SetActive(true);
            WinPanelUI winUI = winPanel.GetComponent<WinPanelUI>();
            if (winUI != null) winUI.SetupWin(currentSession.currentLevel);
        }
    }

    // 🔹 Thêm tham số delay vào TriggerLoseLevel
    public void TriggerLoseLevel(float delay = 0f)
    {
        if (!isPlaying) return;
        isPlaying = false;

        if (inGamePanel != null) inGamePanel.SetActive(false);

        // 🔹 THAY VÌ XÓA SESSION (ClearActiveSession), HÃY LƯU LẠI TRẠNG THÁI HIỆN TẠI.
        // Lúc này quả bom đã bị hủy (DestroySelf) nên danh sách block lưu trữ sẽ KHÔNG CÒN quả bom đó nữa.
        SaveCurrentSession();

        if (losePanel != null)
        {
            if (delay > 0f)
            {
                StartCoroutine(DelayedShowLosePanel(delay));
            }
            else
            {
                ShowLosePanelDirectly();
            }
        }
    }

    private System.Collections.IEnumerator DelayedShowLosePanel(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowLosePanelDirectly();
    }

    private void ShowLosePanelDirectly()
    {
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

        // 🔹 LƯU TIẾN TRÌNH: level vừa thắng đã hoàn thành -> mở level kế tiếp.
        // Trước đây hàm này xóa session tại currentSession.currentLevel (level CŨ) nên ghi đè
        // ngược tiến trình mà TriggerWinLevel() vừa lưu -> vào chơi lại đúng level vừa thắng.
        GameDataService.ClearActiveSession(GetNextLevelAfterWin());

        // 🔹 Nạp lại session trên RAM để Main Menu hiển thị đúng level kế tiếp
        currentSession = GameDataService.LoadSession();

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