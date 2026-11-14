using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public GameProgressManager progressManager;
    public PixelNumberDisplay levelNumberDisplay;
    public GameObject playButtonObj;     
    public GameObject continueButtonObj; 

    private void Awake()
    {
        if (progressManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            progressManager = Object.FindFirstObjectByType<GameProgressManager>();
#else
            progressManager = Object.FindObjectOfType<GameProgressManager>();
#endif
        }

        // 🔹 Đảm bảo khi mới vào game, cờ này luôn là false để hiện nút Play/Level đầu tiên
        GameProgressManager.hasReturnedFromGame = false;
    }

    private void OnEnable()
    {
        // 🔹 Ép reset lại cờ này mỗi khi MainMenuPanel được bật lên lần đầu
        GameProgressManager.hasReturnedFromGame = false;
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (progressManager == null) return;
        
        GameSessionData session = progressManager.GetCurrentSession();
        if (session == null) return;

        if (levelNumberDisplay != null)
        {
            levelNumberDisplay.SetNumber(session.currentLevel);
            levelNumberDisplay.previewLevel = session.currentLevel;
            levelNumberDisplay.UpdateEditorDisplay();
        }

        bool showContinue = GameProgressManager.hasReturnedFromGame && session.hasActiveSession && session.savedBlocks != null && session.savedBlocks.Count > 0;

        if (playButtonObj != null) playButtonObj.SetActive(!showContinue);
        if (continueButtonObj != null) continueButtonObj.SetActive(showContinue);
    }

    public void OnClickPlay()
    {
        if (progressManager != null) progressManager.StartPlaySession();
    }

    public void OnClickContinue()
    {
        if (progressManager != null) progressManager.StartPlaySession();
    }

    public void SelectLevel(int targetLevel)
    {
        if (progressManager != null) progressManager.StartLevelAt(targetLevel);
    }
}