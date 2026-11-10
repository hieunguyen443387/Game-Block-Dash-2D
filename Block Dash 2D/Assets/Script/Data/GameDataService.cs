using UnityEngine;
using System.Collections.Generic;

public static class GameDataService
{
    private const string PREF_SESSION_KEY = "Game_SavedSessionData";
    private const string PREF_CURRENT_LEVEL = "Game_CurrentLevel";

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

    public static GameSessionData LoadSession()
    {
        GameSessionData session = new GameSessionData();
        
        session.currentLevel = PlayerPrefs.GetInt(PREF_CURRENT_LEVEL, 1);

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

    public static void ClearActiveSession(int nextLevel)
    {
        PlayerPrefs.DeleteKey(PREF_SESSION_KEY);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, nextLevel);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Bắt đầu một level hoàn toàn mới (ví dụ người chơi chọn thẳng level 1), 
    /// xóa bỏ session cũ để load map mới tinh mà không bị kẹt phiên chơi dở.
    /// </summary>
    public static GameSessionData StartNewSessionAtLevel(int targetLevel)
    {
        PlayerPrefs.DeleteKey(PREF_SESSION_KEY);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, targetLevel);
        PlayerPrefs.Save();

        return new GameSessionData
        {
            currentLevel = targetLevel,
            hasActiveSession = false,
            savedBlocks = new List<SavedBlockState>()
        };
    }
}