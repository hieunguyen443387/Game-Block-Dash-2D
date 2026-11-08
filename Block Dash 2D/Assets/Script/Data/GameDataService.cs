using UnityEngine;
using System.Collections.Generic;

public static class GameDataService
{
    private const string PREF_SESSION_KEY = "Game_SavedSessionData";
    private const string PREF_CURRENT_LEVEL = "Game_CurrentLevel";

    /// <summary>
    /// Lưu lại tiến trình tổng (Level hiện tại) và trạng thái ván chơi dở
    /// </summary>
    public static void SaveSession(int level, bool hasSession, List<SavedBlockState> blocks)
    {
        GameSessionData session = new GameSessionData
        {
            currentLevel = level,
            hasActiveSession = hasSession,
            savedBlocks = blocks ?? new List<SavedBlockState>()
        };

        string jsonSession = JsonUtility.ToJson(session);
        PlayerPrefs.SetString(PREF_SESSION_KEY, jsonSession);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, level);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Tải dữ liệu phiên chơi đã lưu
    /// </summary>
    public static GameSessionData LoadSession()
    {
        GameSessionData session = new GameSessionData();
        
        // Đọc level hiện tại (mặc định là 1)
        session.currentLevel = PlayerPrefs.GetInt(PREF_CURRENT_LEVEL, 1);

        // Đọc chuỗi JSON ván chơi dở nếu có
        string jsonSession = PlayerPrefs.GetString(PREF_SESSION_KEY, "");
        if (!string.IsNullOrEmpty(jsonSession))
        {
            GameSessionData loadedSession = JsonUtility.FromJson<GameSessionData>(jsonSession);
            if (loadedSession != null)
            {
                session.hasActiveSession = loadedSession.hasActiveSession;
                session.savedBlocks = loadedSession.savedBlocks;
            }
        }
        else
        {
            session.hasActiveSession = false;
        }

        return session;
    }

    /// <summary>
    /// Xóa ván chơi dở khi hoàn thành màn
    /// </summary>
    public static void ClearActiveSession(int nextLevel)
    {
        PlayerPrefs.DeleteKey(PREF_SESSION_KEY);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, nextLevel);
        PlayerPrefs.Save();
    }
}