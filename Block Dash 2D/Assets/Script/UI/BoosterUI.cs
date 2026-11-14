using UnityEngine;
using UnityEngine.Events;

public class BoosterUI : MonoBehaviour
{
    [Header("1. Tham chiếu UI Badges")]
    public BoosterBadgeDisplay removeBadgeDisplay;
    public BoosterBadgeDisplay reverseBadgeDisplay;
    public BoosterBadgeDisplay shuffleBadgeDisplay;
    public GameObject watchAdPanel;

    [Header("2. Sự kiện thực thi tính năng Booster (Kéo script xử lý vào đây)")]
    public UnityEvent onExecuteRemove;
    public UnityEvent onExecuteReverse;
    public UnityEvent onExecuteShuffle;

    private string currentAdBoosterType = "";

    private void OnEnable()
    {
        UpdateBoosterUI();
    }

    public void UpdateBoosterUI()
    {
        GameSessionData session = GameDataService.LoadSession();

        if (removeBadgeDisplay != null) removeBadgeDisplay.SetBoosterCount(session.removeBoosterCount);
        if (reverseBadgeDisplay != null) reverseBadgeDisplay.SetBoosterCount(session.reverseBoosterCount);
        if (shuffleBadgeDisplay != null) shuffleBadgeDisplay.SetBoosterCount(session.shuffleBoosterCount);
    }

    public void UseRemoveBooster()
    {
        GameSessionData session = GameDataService.LoadSession();
        if (session.removeBoosterCount > 0)
        {
            session.removeBoosterCount--;
            SaveAndRefresh(session);
            onExecuteRemove?.Invoke(); // Chỉ kích hoạt tính năng khi còn lượt
        }
        else
        {
            TriggerWatchAd("Remove");
        }
    }

    public void UseReverseBooster()
    {
        GameSessionData session = GameDataService.LoadSession();
        if (session.reverseBoosterCount > 0)
        {
            session.reverseBoosterCount--;
            SaveAndRefresh(session);
            onExecuteReverse?.Invoke(); // Chỉ kích hoạt tính năng khi còn lượt
        }
        else
        {
            TriggerWatchAd("Reverse");
        }
    }

    public void UseShuffleBooster()
    {
        GameSessionData session = GameDataService.LoadSession();
        if (session.shuffleBoosterCount > 0)
        {
            session.shuffleBoosterCount--;
            SaveAndRefresh(session);
            onExecuteShuffle?.Invoke(); // Chỉ kích hoạt tính năng khi còn lượt
        }
        else
        {
            TriggerWatchAd("Shuffle");
        }
    }

    private void SaveAndRefresh(GameSessionData session)
    {
        GameDataService.SaveSession(
            session.currentLevel, 
            session.hasActiveSession, 
            session.savedBlocks, 
            session.removeBoosterCount, 
            session.reverseBoosterCount, 
            session.shuffleBoosterCount
        );
        UpdateBoosterUI();
    }

    private void TriggerWatchAd(string boosterType)
    {
        currentAdBoosterType = boosterType;
        if (watchAdPanel != null) watchAdPanel.SetActive(true);
    }

    public void OnClickWatchAd()
    {
        GameSessionData session = GameDataService.LoadSession();

        if (currentAdBoosterType == "Remove") session.removeBoosterCount++;
        else if (currentAdBoosterType == "Reverse") session.reverseBoosterCount++;
        else if (currentAdBoosterType == "Shuffle") session.shuffleBoosterCount++;

        SaveAndRefresh(session);

        if (watchAdPanel != null) watchAdPanel.SetActive(false);
    }

    public void OnClickCloseAdPanel()
    {
        if (watchAdPanel != null) watchAdPanel.SetActive(false);
    }
}