using UnityEngine;
using UnityEngine.UI;

public class SettingButtonController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Kéo PopupPanel (lớp nền mờ full màn hình) vào đây")]
    public GameObject popupPanel;

    [Tooltip("Kéo SettingContainer (khung bảng cài đặt ở giữa) vào đây - (Tùy chọn)")]
    public GameObject settingContainer;

    private void Start()
    {
        // Đảm bảo khi mới vào game, panel cài đặt sẽ được ẩn đi
        if (popupPanel != null && popupPanel.activeSelf)
        {
            popupPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Hàm này dùng để gán vào sự kiện On Click() của SettingButton
    /// </summary>
    public void OnClickOpenSetting()
    {
        // Bật Panel chứa cài đặt lên
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        // Nếu bạn muốn animate hoặc đảm bảo container bật lên cùng lúc
        if (settingContainer != null && !settingContainer.activeSelf)
        {
            settingContainer.SetActive(true);
        }
    }

    /// <summary>
    /// Hàm này dùng để gán vào nút Đóng (Nút X) hoặc nút Hủy bên trong bảng setting
    /// </summary>
    public void OnClickCloseSetting()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }
}