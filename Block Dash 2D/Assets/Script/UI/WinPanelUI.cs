using UnityEngine;
using TMPro;

public class WinPanelUI : MonoBehaviour
{
    public GameProgressManager progressManager;
    public TextMeshProUGUI winLevelText;

    public void SetupWin(int level)
    {
        if (winLevelText != null)
        {
            winLevelText.text = $"Hoàn thành cấp độ {level}!";
        }
    }

    // Gắn vào sự kiện OnClick của nút Next Level trên Win Panel
    public void OnClickNextLevel()
    {
        if (progressManager == null) return;

        // Lấy level kế tiếp (đã được chặn ở level cuối) từ GameProgressManager
        progressManager.StartLevelAt(progressManager.GetNextLevelAfterWin());
    }

    // Gắn vào sự kiện OnClick của nút Home trên Win Panel
    public void OnClickHome()
    {
        if (progressManager != null)
        {
            progressManager.ReturnHomeFromWin();
        }
    }
}
