using UnityEngine;

public class GameProgressManager : MonoBehaviour
{
    [Header("1. Giao diện (UI Panels)")]
    [Tooltip("Panel màn hình chính (Menu)")]
    public GameObject mainMenuPanel;

    [Tooltip("Panel giao diện khi đang chơi (Nút booster,...)")]
    public GameObject inGamePanel;

    [Header("2. Tham chiếu hệ thống")]
    [Tooltip("Kéo object gắn script MapLoader vào đây")]
    public MapLoader mapLoader;

    private GameSessionData currentSession;

    private void Start()
    {
        // 1. Gọi dữ liệu từ file Data độc lập (GameDataService)
        currentSession = GameDataService.LoadSession();

        // 2. Đồng bộ số level hiện tại sang MapLoader giống như ý bạn muốn
        if (mapLoader != null)
        {
            mapLoader.levelNumber = currentSession.currentLevel;
            
            // Xóa sạch bàn cờ cũ lúc mới mở game để hiển thị trạng thái sạch sẽ
            mapLoader.ClearMap();
        }

        // 3. Bật Menu lên, tắt UI chơi game đi
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (inGamePanel != null) inGamePanel.SetActive(false);
    }

    /// <summary>
    /// Gắn hàm này vào nút bấm Play/LEVEL ngoài Main Menu
    /// </summary>
    public void OnClickPlay()
    {
        // 1. Tắt Menu, bật UI chơi game
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (inGamePanel != null) inGamePanel.SetActive(true);

        // 2. Tải lại dữ liệu phiên mới nhất từ Service
        currentSession = GameDataService.LoadSession();

        // 3. Kiểm tra nếu có ván chơi dở -> Khôi phục, ngược lại -> Gọi MapLoader load chuẩn level từ file .txt
        if (currentSession.hasActiveSession && currentSession.savedBlocks.Count > 0)
        {
            RestoreSavedSession();
        }
        else
        {
            if (mapLoader != null)
            {
                mapLoader.levelNumber = currentSession.currentLevel;
                mapLoader.LoadLevel(); // MapLoader tự động load đúng level hiện tại từ file Data
            }
        }
    }

    /// <summary>
    /// Lưu lại trạng thái ván chơi hiện tại khi thoát giữa chừng
    /// </summary>
    public void SaveCurrentSession()
    {
        if (mapLoader == null || mapLoader.blocksContainer == null) return;

        System.Collections.Generic.List<SavedBlockState> blocksToSave = new System.Collections.Generic.List<SavedBlockState>();
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

        mapLoader.levelNumber = currentSession.currentLevel;
        mapLoader.LoadLevel(); 

        if (mapLoader.blocksContainer != null)
        {
            foreach (Transform child in mapLoader.blocksContainer)
            {
                Destroy(child.gameObject);
            }

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
                }
            }
        }
    }

    public void CompleteCurrentLevel()
    {
        int nextLevel = currentSession.currentLevel + 1;
        GameDataService.ClearActiveSession(nextLevel);
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        SaveCurrentSession();

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