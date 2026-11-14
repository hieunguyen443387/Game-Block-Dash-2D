using System.IO;
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
        GameSessionData session = progressManager.GetCurrentSession();
        
        int nextLevel = session.currentLevel + 1;
        string nextMapPath = Path.Combine(Application.persistentDataPath, "Levels", "Level_" + nextLevel + ".txt");
        if (!File.Exists(nextMapPath)) nextLevel = session.currentLevel; 

        progressManager.StartLevelAt(nextLevel);
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