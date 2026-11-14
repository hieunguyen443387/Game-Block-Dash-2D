using UnityEngine;
using TMPro;

public class LosePanelUI : MonoBehaviour
{
    public GameProgressManager progressManager;
    public TextMeshProUGUI loseLevelText;

    public void SetupLose(int level)
    {
        if (loseLevelText != null)
        {
            loseLevelText.text = $"Thua cuộc ở cấp độ {level}!";
        }
    }

    // Gắn vào sự kiện OnClick của nút Chơi lại / Retry trên Lose Panel
    public void OnClickRetry()
    {
        if (progressManager == null) return;
        GameSessionData session = progressManager.GetCurrentSession();
        progressManager.StartLevelAt(session.currentLevel);
    }

    // Gắn vào sự kiện OnClick của nút Home trên Lose Panel
    public void OnClickHome()
    {
        if (progressManager != null)
        {
            progressManager.ReturnToMainMenu();
        }
    }
}