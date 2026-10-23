using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CameraFit2D : MonoBehaviour
{
    [Header("1. Căn chỉnh theo Bàn cờ")]
    [Tooltip("Tự động tìm kiếm thông số Bàn cờ từ BoardGenerator hoặc MapSaver trong Scene")]
    public bool autoDetectBoard = true;

    [Tooltip("Tọa độ tâm của Bàn cờ (mặc định 0, 0)")]
    public Vector2 boardCenter = Vector2.zero;

    [Tooltip("Chiều rộng vùng bàn cờ cần hiển thị (bao gồm cả viền, tính theo Unity Units)")]
    public float targetWidth = 5.2f;

    [Tooltip("Chiều cao vùng bàn cờ cần hiển thị (bao gồm cả viền, tính theo Unity Units)")]
    public float targetHeight = 5.2f;

    [Header("2. Căn lề & Vị trí màn hình điện thoại (Tạo độ thoáng như game mẫu)")]
    [Tooltip("Khoảng cách hở 2 bên mép màn hình điện thoại (Tăng lên để bàn cờ thu nhỏ lại, không bị tràn màn hình)")]
    public float horizontalPadding = 0.8f;

    [Tooltip("Khoảng cách hở trên/dưới để chừa chỗ cho UI Header (Level, Settings) và Footer (Nút kỹ năng)")]
    public float verticalPadding = 2.2f;

    [Tooltip("Dịch chuyển bàn cờ lên/xuống (Số âm: nhích bàn cờ lên một chút cho cân đối giữa màn hình)")]
    public float verticalOffset = 0f;

    [Header("3. Giới hạn Camera Size")]
    [Tooltip("Kích thước tối thiểu của Camera")]
    public float minOrthographicSize = 5.5f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    private void Start()
    {
        if (autoDetectBoard)
        {
            DetectBoardBounds();
        }
        AdjustCamera();
    }

    private void LateUpdate()
    {
        // Luôn cập nhật để khi bạn đổi tỉ lệ màn hình ở Game View (ví dụ 9:16), Camera tự co giãn ngay lập tức
        if (autoDetectBoard && !Application.isPlaying)
        {
            DetectBoardBounds();
        }
        AdjustCamera();
    }

    /// <summary>
    /// Tự động lấy kích thước bàn cờ và viền từ BoardGenerator / MapSaver
    /// </summary>
    public void DetectBoardBounds()
    {
        float cellSize = 0.36f;
        int cols = 12;
        int rows = 12;

#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
#endif
        if (boardGen != null)
        {
            cols = boardGen.columns;
            rows = boardGen.rows;
            float ppu = (boardGen.pixelsPerUnit > 0) ? boardGen.pixelsPerUnit : 100f;
            cellSize = (boardGen.cellSizePixels + boardGen.spacingPixels) / ppu;
            boardCenter = boardGen.startPosition;
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            MapSaver saver = FindFirstObjectByType<MapSaver>();
#else
            MapSaver saver = FindObjectOfType<MapSaver>();
#endif
            if (saver != null)
            {
                cols = saver.columns;
                rows = saver.rows;
                float ppu = (saver.pixelsPerUnit > 0) ? saver.pixelsPerUnit : 100f;
                cellSize = (saver.cellSizePixels + saver.spacingPixels) / ppu;
                boardCenter = saver.startPosition;
            }
        }

        // Tính kích thước bàn cờ + cộng thêm các lớp viền ngoài (Sideline, SidelineSurround)
        float boardW = cols * cellSize;
        float boardH = rows * cellSize;
        float borderExtra = cellSize * 2.5f; // Mặc định

#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline != null)
        {
            borderExtra = (sideline.borderThickness * 2f + 1f) * cellSize;
        }

#if UNITY_2023_1_OR_NEWER
        SidelineSurround sidelineSurround = FindFirstObjectByType<SidelineSurround>();
#else
        SidelineSurround sidelineSurround = FindObjectOfType<SidelineSurround>();
#endif
        if (sidelineSurround != null)
        {
            borderExtra += (sidelineSurround.surroundThickness * 2f) * cellSize;
        }

        targetWidth = boardW + borderExtra;
        targetHeight = boardH + borderExtra;
    }

    /// <summary>
    /// Tự động tính toán Orthographic Size để vừa vặn màn hình điện thoại dọc (Portrait 9:16, 9:19.5, etc.)
    /// </summary>
    [ContextMenu("Adjust Camera Now")]
    public void AdjustCamera()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        cam.orthographic = true;

        // Đảm bảo Z luôn là -10 để nhìn được các Sprite ở Z = 0
        cam.transform.position = new Vector3(
            boardCenter.x,
            boardCenter.y + verticalOffset,
            -10f
        );

        float totalW = targetWidth + horizontalPadding * 2f;
        float totalH = targetHeight + verticalPadding * 2f;

        float currentAspect = cam.aspect; // Tỉ lệ màn hình (Chiều rộng / Chiều cao)
        if (currentAspect <= 0) return;

        // Với màn hình điện thoại dọc: Chiều rộng hẹp hơn chiều cao
        // Để bàn cờ không bị cắt 2 bên: orthographicSize phải đủ lớn theo chiều ngang
        float requiredSizeByWidth = (totalW / currentAspect) * 0.5f;
        float requiredSizeByHeight = totalH * 0.5f;

        // Lấy giá trị lớn hơn để đảm bảo bàn cờ luôn lọt hoàn toàn trong khung nhìn
        float finalSize = Mathf.Max(requiredSizeByWidth, requiredSizeByHeight);
        cam.orthographicSize = Mathf.Max(finalSize, minOrthographicSize);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Vẽ khung bàn cờ mục tiêu trong Scene View để dễ quan sát
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(boardCenter.x, boardCenter.y, 0f), new Vector3(targetWidth, targetHeight, 0.1f));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(boardCenter.x, boardCenter.y + verticalOffset, 0f),
            new Vector3(targetWidth + horizontalPadding * 2f, targetHeight + verticalPadding * 2f, 0.1f));
    }
#endif
}
