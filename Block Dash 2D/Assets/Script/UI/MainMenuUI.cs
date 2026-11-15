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
    }

    private void OnEnable()
    {
        // ❌ XÓA BỎ dòng reset 'GameProgressManager.hasReturnedFromGame = false;' ở đây 
        // để không bị ghi đè trạng thái khi bấm Back từ trong game ra!
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

        // 🔹 Sửa lại phần này: Nút Continue hiển thị khi showContinue = true, nút Play hiển thị khi ngược lại
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