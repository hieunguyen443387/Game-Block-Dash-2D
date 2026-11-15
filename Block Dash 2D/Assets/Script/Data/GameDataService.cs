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
            session.removeBoosterCount = 0;
            session.reverseBoosterCount = 0;
            session.shuffleBoosterCount = 0;
        }

        return session;
    }

    /// <summary>
    /// Level hiện tại đang lưu trong PlayerPrefs (đọc nhanh, không cần parse JSON).
    /// Dùng cho các UI hiển thị số level như PixelNumberDisplay (Main Menu và trong game).
    /// </summary>
    public static int GetSavedLevel()
    {
        return PlayerPrefs.GetInt(PREF_CURRENT_LEVEL, 1);
    }

    public static void ClearActiveSession(int nextLevel)
    {
        PlayerPrefs.DeleteKey(PREF_SESSION_KEY);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, nextLevel);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Bắt đầu một level hoàn toàn mới, xóa dữ liệu chơi dở cũ.
    /// </summary>
    public static GameSessionData StartNewSessionAtLevel(int targetLevel)
    {
        PlayerPrefs.DeleteKey(PREF_SESSION_KEY);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, targetLevel);
        PlayerPrefs.Save();

        GameSessionData newSession = new GameSessionData
        {
            currentLevel = targetLevel
        };
        newSession.ResetToInitialState(0);
        return newSession;
    }

    /// <summary>
    /// Chức năng RESTART: Xóa tiến trình đang chơi dở của level hiện tại và trả về phiên chơi sạch từ đầu level.
    /// </summary>
    public static GameSessionData RestartCurrentSession()
    {
        int currentLevel = PlayerPrefs.GetInt(PREF_CURRENT_LEVEL, 1);
        PlayerPrefs.DeleteKey(PREF_SESSION_KEY);
        PlayerPrefs.SetInt(PREF_CURRENT_LEVEL, currentLevel);
        PlayerPrefs.Save();

        GameSessionData restartedSession = new GameSessionData
        {
            currentLevel = currentLevel
        };
        restartedSession.ResetToInitialState(0);

        Debug.Log($"<color=cyan>[GameDataService]</color> Đã reset dữ liệu phiên chơi để Restart Level {currentLevel}");
        return restartedSession;
    }
}