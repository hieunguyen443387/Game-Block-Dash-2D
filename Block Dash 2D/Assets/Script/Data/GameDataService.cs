using UnityEngine;
using System.Collections.Generic;

public static class GameDataService
{
    private const string PREF_SESSION_KEY = "Game_SavedSessionData";
    private const string PREF_CURRENT_LEVEL = "Game_CurrentLevel";

    // Cập nhật hàm SaveSession để nhận thêm số lượt của 3 booster
    public static void SaveSession(int level, bool hasSession, List<SavedBlockState> blocks, int removeCount, int reverseCount, int shuffleCount)
    {
        GameSessionData session = new GameSessionData
        {
            currentLevel = level,
            hasActiveSession = hasSession,
            savedBlocks = blocks ?? new List<SavedBlockState>(),
            removeBoosterCount = removeCount,
            reverseBoosterCount = reverseCount,
            shuffleBoosterCount = shuffleCount
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
                session.removeBoosterCount = loadedSession.removeBoosterCount;
                session.reverseBoosterCount = loadedSession.reverseBoosterCount;
                session.shuffleBoosterCount = loadedSession.shuffleBoosterCount;
            }
        }
        else
        {
            session.hasActiveSession = false;
            // Giá trị mặc định khi chưa có session (mỗi màn chơi mới có 3 lượt cho mỗi booster)
            session.removeBoosterCount = 0;
            session.reverseBoosterCount = 0;
            session.shuffleBoosterCount = 0;
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
    /// Bắt đầu một level hoàn toàn mới, reset lại số lượt booster về mặc định (ví dụ: 3 lượt/mỗi loại).
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
            savedBlocks = new List<SavedBlockState>(),
            removeBoosterCount = 0,
            reverseBoosterCount = 0,
            shuffleBoosterCount = 0
        };
    }
}