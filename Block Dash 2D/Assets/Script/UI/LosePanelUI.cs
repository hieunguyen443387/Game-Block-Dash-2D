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

    /// <summary>
    /// Gắn vào nút CONTINUE: Giữ nguyên hiện trạng map đang chơi dở (không reset bom, giữ nguyên vị trí block)
    /// </summary>
    public void OnClickContinue()
    {
        if (progressManager == null) return;
        gameObject.SetActive(false);
        progressManager.ContinueCurrentSessionAfterLose();
    }

    /// <summary>
    /// Gắn vào nút REVIVE: Khởi động lại map hoàn toàn từ đầu (clear map, load lại từ file gốc)
    /// </summary>
    public void OnClickRevive()
    {
        if (progressManager == null) return;
        gameObject.SetActive(false);

        GameSessionData session = progressManager.GetCurrentSession();
        progressManager.StartLevelAt(session.currentLevel);
    }

    /// <summary>
    /// Gắn vào nút HOME: Trở về màn hình chính (Main Menu)
    /// </summary>
    public void OnClickHome()
    {
        if (progressManager != null)
        {
            progressManager.ReturnToMainMenu();
        }
    }
}