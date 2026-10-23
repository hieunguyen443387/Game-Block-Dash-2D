using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum GatePlacement
{
    [Tooltip("Nằm ở PHÍA TRÊN HÀNG SỐ 2 của Sideline (Bên ngoài trên đỉnh của viền)")]
    AboveRow2OfSideline,

    [Tooltip("Nằm ở HÀNG SỐ 2 của Sideline (Lớp ngoài cùng của viền)")]
    Row2OfSideline,

    [Tooltip("Nằm ngay trên mép đỉnh ngoài cùng của Hàng số 2")]
    OnTopEdgeOfRow2,

    [Tooltip("Nằm ở Hàng số 1 của Sideline (Lớp dưới sát bàn cờ)")]
    Row1OfSideline
}

[SelectionBase]
[DefaultExecutionOrder(100)]
public class ExitGateGenerator : MonoBehaviour
{
    [Header("1. Bàn cờ & Sideline (Tự động đồng bộ)")]
    [Tooltip("Tự động đọc thông số từ SideLine hoặc BoardGenerator trong Scene")]
    public bool autoDetectBoard = true;

    [Tooltip("Số cột của bàn cờ")]
    public int columns = 8;

    [Tooltip("Số hàng của bàn cờ")]
    public int rows = 12;

    [Tooltip("Kích thước mỗi ô Cell theo pixel (38px)")]
    public float cellSizePixels = 38f;

    [Tooltip("Khoảng cách giữa các ô (0px)")]
    public float spacingPixels = 0f;

    [Tooltip("Pixels Per Unit của Project / Sprite")]
    public float pixelsPerUnit = 38f;

    [Tooltip("Tọa độ tâm bàn cờ")]
    public Vector2 startPosition = Vector2.zero;

    [Tooltip("Độ dày đường viền Sideline (số lớp ô bao quanh, ví dụ: 2)")]
    public int borderThickness = 2;

    [Tooltip("Kích thước ô đường viền (pixels)")]
    public float borderSizePixels = 38f;

    [Header("2. Độ cao Cổng (Nâng cao lên trên Sideline)")]
    [Tooltip("Vị trí đặt cổng (Mặc định: AboveRow2OfSideline - phía trên hàng số 2)")]
    public GatePlacement placement = GatePlacement.AboveRow2OfSideline;

    [Tooltip("Số ô muốn đẩy cổng lên cao thêm (Nhập 0 để sát mép viền, 1 để cao hơn 1 ô...)")]
    public float extraCellsUp = 0f;

    [Tooltip("Dịch chuyển bù trừ thêm vị trí cổng (tính theo World Units, vi chỉnh tọa độ X, Y tự do)")]
    public Vector2 positionOffset = Vector2.zero;

    [Header("3. Cột & Kích thước Cổng")]
    [Tooltip("Tự động căn cổng vào chính giữa bàn cờ theo chiều ngang")]
    public bool autoCenter = true;

    [Tooltip("Cột bắt đầu của cổng nếu tắt tự động căn giữa (0 đến columns - gateWidthInCells)")]
    public int startColumn = 3;

    [Tooltip("Độ rộng của cổng theo số ô cột (mặc định 2 ô vì sprite Exite dài 74px ~ 2 ô 38px)")]
    [Range(1, 8)]
    public int gateWidthInCells = 2;

    [Tooltip("Chiều cao cổng theo số ô hàng (mặc định 1 ô)")]
    [Range(1, 4)]
    public int gateHeightInCells = 1;

    [Header("4. Cấu hình Sprite & Visual Cổng")]
    [Tooltip("Prefab cổng nếu bạn đã tạo Prefab (Ưu tiên dùng nếu có)")]
    public GameObject gatePrefab;

    [Tooltip("Sprite của cổng (kéo Exite.png vào đây, hoặc script sẽ tự động tìm)")]
    public Sprite gateSprite;

    [Tooltip("Tự động tìm Sprite Exite trong thư mục Assets nếu ô trên bị trống")]
    public bool autoFindExiteSprite = true;

    [Tooltip("Sorting Layer cho Sprite cổng")]
    public string sortingLayerName = "Default";

    [Tooltip("Sorting Order cho Sprite cổng (đặt 10 để luôn nổi bật trên nền Board và Sideline)")]
    public int sortingOrder = 10;

    [Tooltip("Giữ nguyên 100% kích thước pixel gốc của ảnh vẽ (Scale = 1,1,1) thay vì ép co dãn theo số ô. Giữ nguyên hình ảnh gốc không bị méo hay biến dạng!")]
    public bool keepExactSpriteSize = true;

    [Tooltip("Tự động đồng bộ với SidelineSurround trong Scene để khoảng trống surround tự ôm khít 100% vào mép Cổng")]
    public bool autoSyncSurround = true;

    [Header("5. Viền Sideline & Collider")]
    [Tooltip("Tắt tùy chọn này để viền Sideline luôn LIỀN MẠCH NGUYÊN VẸN, KHÔNG BỊ LẸM/KHOÉT LỖ")]
    public bool openSidelineDoorway = false;

    [Tooltip("Tự động gắn BoxCollider2D dạng Trigger cho cổng")]
    public bool addTriggerCollider = true;

    [Header("6. Runtime (Khi chạy game)")]
    [Tooltip("Tự động tạo cổng thoát khi bắt đầu chạy game (Start)")]
    public bool generateOnStart = true;

    // GameObject cổng hiện tại được sinh ra
    [SerializeField, HideInInspector] private GameObject currentGateObject;
    private List<GameObject> hiddenSidelineTiles = new List<GameObject>();

    public GameObject CurrentGateObject => currentGateObject;

    private void Awake()
    {
        if (autoDetectBoard)
        {
            DetectBoardSettings();
        }
    }

    private IEnumerator Start()
    {
        if (Application.isPlaying && generateOnStart)
        {
            // Chờ 1 frame để đảm bảo SideLine và BoardGenerator đã hoàn tất sinh các ô viền
            yield return null;
            GenerateExitGate();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (autoFindExiteSprite && gateSprite == null && gatePrefab == null)
        {
            FindExiteSpriteInAssets();
        }

        if (autoCenter && columns > 0 && gateWidthInCells > 0)
        {
            startColumn = Mathf.Max(0, (columns - gateWidthInCells) / 2);
        }

        // Nếu tắt khoét lỗ viền, tự động khôi phục toàn bộ các viên Sideline nguyên vẹn
        if (!openSidelineDoorway)
        {
            RestoreAllSidelineTiles();
        }
    }

    private void Reset()
    {
        openSidelineDoorway = false;
        DetectBoardSettings();
        FindExiteSpriteInAssets();
        RestoreAllSidelineTiles();
    }

    /// <summary>
    /// Tự động tìm Sprite Exite trong Assets
    /// </summary>
    public void FindExiteSpriteInAssets()
    {
        string[] guids = AssetDatabase.FindAssets("Exite t:Sprite");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            gateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (gateSprite != null) return;
        }

        guids = AssetDatabase.FindAssets("Exite t:Texture2D");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            gateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
#endif

    /// <summary>
    /// Tự động lấy các thông số bàn cờ từ SideLine hoặc BoardGenerator trong Scene
    /// </summary>
    [ContextMenu("Detect Board Settings")]
    public void DetectBoardSettings()
    {
#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline != null)
        {
            rows = sideline.rows;
            columns = sideline.columns;
            cellSizePixels = sideline.cellSizePixels;
            spacingPixels = sideline.spacingPixels;
            pixelsPerUnit = sideline.pixelsPerUnit;
            startPosition = sideline.startPosition;
            borderThickness = sideline.borderThickness;
            borderSizePixels = sideline.borderSizePixels;
            return;
        }

#if UNITY_2023_1_OR_NEWER
        BoardGenerator boardGen = FindFirstObjectByType<BoardGenerator>();
#else
        BoardGenerator boardGen = FindObjectOfType<BoardGenerator>();
#endif
        if (boardGen != null)
        {
            rows = boardGen.rows;
            columns = boardGen.columns;
            cellSizePixels = boardGen.cellSizePixels;
            spacingPixels = boardGen.spacingPixels;
            pixelsPerUnit = boardGen.pixelsPerUnit;
            startPosition = boardGen.startPosition;
        }
    }

    /// <summary>
    /// Tính toán tọa độ World Position của Cổng thoát ở trên hàng số 2 của Sideline
    /// </summary>
    public Vector2 CalculateGateWorldPosition()
    {
        float ppu = (pixelsPerUnit > 0) ? pixelsPerUnit : 100f;
        float cellSize = cellSizePixels / ppu;
        float spacing = spacingPixels / ppu;
        float step = cellSize + spacing;

        float boardWidth = columns * cellSize + (columns - 1) * spacing;
        float boardHeight = rows * cellSize + (rows - 1) * spacing;
        float borderUnitSize = borderSizePixels / ppu;

        Vector2 center = startPosition;
        float halfBoardW = boardWidth / 2f;
        float halfBoardH = boardHeight / 2f;

        // 1. Tọa độ X: Nếu autoCenter thì căn chuẩn 100% vào tâm bàn cờ / viền
        float posX;
        if (autoCenter)
        {
            posX = center.x;
        }
        else
        {
            int effectiveStartCol = startColumn;
            float leftColX = center.x - halfBoardW + cellSize / 2f + effectiveStartCol * step;
            float rightColX = center.x - halfBoardW + cellSize / 2f + (effectiveStartCol + gateWidthInCells - 1) * step;
            posX = (leftColX + rightColX) / 2f;
        }

        // 2. Tọa độ Y: Căn theo các hàng của Sideline và chiều cao thật của Sprite cổng
        float actualGateH = (gateHeightInCells * cellSizePixels) / ppu;
        if (gateSprite != null)
        {
            float sprPPU = (gateSprite.pixelsPerUnit > 0) ? gateSprite.pixelsPerUnit : ppu;
            actualGateH = gateSprite.rect.height / sprPPU;
        }

        float boardTopEdge = center.y + halfBoardH; // Mép trên của Board
        float sidelineTopEdge = boardTopEdge + borderThickness * borderUnitSize; // Mép trên của Sideline

        float basePosY;

        switch (placement)
        {
            case GatePlacement.AboveRow2OfSideline:
                // Nằm ở PHÍA TRÊN HÀNG SỐ 2 của Sideline (Mép dưới chạm mép đỉnh viền, trùng với vị trí hàng viền Surround đỉnh)
                basePosY = sidelineTopEdge + actualGateH / 2f;
                break;

            case GatePlacement.Row2OfSideline:
                // Nằm ở HÀNG SỐ 2 của Sideline (Lớp ngoài cùng của viền)
                basePosY = sidelineTopEdge - actualGateH / 2f;
                break;

            case GatePlacement.OnTopEdgeOfRow2:
                // Nằm ngay trên mép đỉnh ngoài cùng của Hàng số 2
                basePosY = sidelineTopEdge;
                break;

            case GatePlacement.Row1OfSideline:
                // Nằm ở Hàng số 1 của Sideline (Lớp dưới sát bàn cờ)
                basePosY = boardTopEdge + borderUnitSize / 2f;
                break;

            default:
                basePosY = sidelineTopEdge + actualGateH / 2f;
                break;
        }

        // Đẩy cao thêm số ô theo extraCellsUp
        float posY = basePosY + (extraCellsUp * cellSize);

        return new Vector2(posX, posY) + positionOffset;
    }

    /// <summary>
    /// Sinh Cổng thoát ở trên hàng số 2 của Sideline
    /// </summary>
    [ContextMenu("Generate Exit Gate")]
    public void GenerateExitGate()
    {
        if (autoDetectBoard)
        {
            DetectBoardSettings();
        }

#if UNITY_EDITOR
        if (gateSprite == null && gatePrefab == null)
        {
            FindExiteSpriteInAssets();
        }
#endif

        if (gatePrefab == null && gateSprite == null)
        {
            Debug.LogError("Chưa gán Gate Prefab hoặc Gate Sprite trong ExitGateGenerator!");
            return;
        }

        // Xóa cổng cũ nếu đã có
        ClearExitGate();

        // 1. Tính toán vị trí và kích thước
        Vector2 gatePos = CalculateGateWorldPosition();
        int effectiveStartCol = autoCenter ? Mathf.Max(0, (columns - gateWidthInCells) / 2) : startColumn;

        float ppu = (pixelsPerUnit > 0) ? pixelsPerUnit : 100f;
        float cellSize = cellSizePixels / ppu;
        float targetWidthUnits = gateWidthInCells * cellSize;
        float targetHeightUnits = gateHeightInCells * cellSize;

        // 2. Khởi tạo GameObject của Cổng
        if (gatePrefab != null)
        {
            currentGateObject = Instantiate(gatePrefab, gatePos, Quaternion.identity, transform);
        }
        else
        {
            currentGateObject = new GameObject("ExitGate_Top");
            currentGateObject.transform.SetParent(transform);
            currentGateObject.transform.position = gatePos;
            currentGateObject.transform.rotation = Quaternion.identity;

            SpriteRenderer sr = currentGateObject.AddComponent<SpriteRenderer>();
            sr.sprite = gateSprite;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
        }

        currentGateObject.name = "ExitGate_Top";

        // 3. Tự động điều chỉnh Scale để cổng phủ đúng số ô (ví dụ 2 ô ngang, 1 ô dọc)
        SpriteRenderer renderer = currentGateObject.GetComponent<SpriteRenderer>();
        float spriteW = 1f;
        float spriteH = 1f;

        if (renderer != null && renderer.sprite != null)
        {
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            spriteW = renderer.sprite.rect.width / renderer.sprite.pixelsPerUnit;
            spriteH = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit;

            if (keepExactSpriteSize)
            {
                // Giữ nguyên tỷ lệ 100% kích thước pixel gốc mà bạn đã vẽ (Scale = 1,1,1)
                currentGateObject.transform.localScale = Vector3.one;
            }
            else
            {
                float scaleX = spriteW > 0 ? (targetWidthUnits / spriteW) : 1f;
                float scaleY = spriteH > 0 ? (targetHeightUnits / spriteH) : 1f;
                currentGateObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }
        else
        {
            currentGateObject.transform.localScale = new Vector3(targetWidthUnits, targetHeightUnits, 1f);
        }

        // 4. Thêm BoxCollider2D dạng Trigger nếu cần
        if (addTriggerCollider)
        {
            BoxCollider2D boxCol = currentGateObject.GetComponent<BoxCollider2D>();
            if (boxCol == null)
            {
                boxCol = currentGateObject.AddComponent<BoxCollider2D>();
            }
            boxCol.isTrigger = true;
            // Đặt size bằng đúng kích thước Sprite trong local space để viền Collider ôm khít 100% ảnh cổng
            boxCol.size = (renderer != null && renderer.sprite != null) ? new Vector2(spriteW, spriteH) : Vector2.one;
            boxCol.offset = Vector2.zero;
        }

        // 5. Gắn và cấu hình component ExitGate
        ExitGate exitGateComp = currentGateObject.GetComponent<ExitGate>();
        if (exitGateComp == null)
        {
            exitGateComp = currentGateObject.AddComponent<ExitGate>();
        }

        exitGateComp.gateDirection = ArrowDirection.Up;
        exitGateComp.startColumn = effectiveStartCol;
        exitGateComp.columnSpan = (keepExactSpriteSize && cellSize > 0)
            ? Mathf.Max(1, Mathf.RoundToInt(spriteW / cellSize))
            : gateWidthInCells;
        exitGateComp.exitRow = rows; // Hàng trên cùng vượt ra ngoài bàn cờ

        // 6. Xử lý viền Sideline bên dưới: Khôi phục liền mạch hoặc mở lối
        if (openSidelineDoorway)
        {
            OpenDoorwayInSideline(effectiveStartCol, gateWidthInCells);
        }
        else
        {
            RestoreAllSidelineTiles();
        }

        // 7. Tự động đồng bộ với SidelineSurround để khoảng trống viền ôm khít 100% vào mép cổng (0 gap, 0 overlap)
        if (autoSyncSurround)
        {
            // Ensure gate keeps its original sprite size when syncing with the surround opening
            keepExactSpriteSize = true;
            SyncWithSidelineSurround();
        }

        Debug.Log($"<color=green>[ExitGateGenerator]</color> Đã tạo Cổng thoát thành công tại vị trí {gatePos} (Cột {effectiveStartCol} đến {effectiveStartCol + gateWidthInCells - 1}, Viền liền mạch: {!openSidelineDoorway}).");
    }

    /// <summary>
    /// Tìm và ẩn các viên viền Sideline tại vị trí cổng để mở lối thoát thông suốt
    /// </summary>
    private void OpenDoorwayInSideline(int colStart, int colSpan)
    {
        hiddenSidelineTiles.Clear();

#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline == null) return;

        for (int layer = 0; layer < borderThickness; layer++)
        {
            for (int c = colStart; c < colStart + colSpan; c++)
            {
                // Format tên viên viền trong SideLine.cs: Border_Top_L{layer}_{col} hoặc Sideline_Top_{col}
                string[] possibleNames = new string[]
                {
                    $"Border_Top_L{layer}_{c}",
                    $"Sideline_Top_{c}",
                    $"Border_Top_{c}"
                };

                foreach (var n in possibleNames)
                {
                    Transform tileTr = sideline.transform.Find(n);
                    if (tileTr != null && tileTr.gameObject.activeSelf)
                    {
                        tileTr.gameObject.SetActive(false);
                        hiddenSidelineTiles.Add(tileTr.gameObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Khôi phục toàn bộ các viên viền Sideline nguyên vẹn, đảm bảo không bị lẹm/khoét lỗ
    /// </summary>
    [ContextMenu("Khôi Phục Viền Liền Mạch (Không Lẹm)")]
    public void RestoreAllSidelineTiles()
    {
#if UNITY_2023_1_OR_NEWER
        SideLine sideline = FindFirstObjectByType<SideLine>();
#else
        SideLine sideline = FindObjectOfType<SideLine>();
#endif
        if (sideline == null) return;

        for (int i = 0; i < sideline.transform.childCount; i++)
        {
            Transform child = sideline.transform.GetChild(i);
            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
            }
        }

        hiddenSidelineTiles.Clear();
    }

    /// <summary>
    /// Xóa Cổng thoát và khôi phục lại các ô Sideline đã bị ẩn
    /// </summary>
    [ContextMenu("Clear Exit Gate")]
    public void ClearExitGate()
    {
        RestoreAllSidelineTiles();

        // Xóa các object cổng con
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("ExitGate"))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(child.gameObject);
                    continue;
                }
#endif
                Destroy(child.gameObject);
            }
        }

        if (currentGateObject != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(currentGateObject);
            }
            else
#endif
            {
                Destroy(currentGateObject);
            }
            currentGateObject = null;
        }

        // Cập nhật lại Surround để phủ kín viền khi không còn cổng
        if (autoSyncSurround)
        {
#if UNITY_2023_1_OR_NEWER
            SidelineSurround surround = FindFirstObjectByType<SidelineSurround>();
#else
            SidelineSurround surround = FindObjectOfType<SidelineSurround>();
#endif
            if (surround != null)
            {
                surround.GenerateSurround();
            }
        }
    }

    /// <summary>
    /// Tự động tìm và cập nhật viền Surround trong Scene để khoảng trống ôm khít rịt 100% vào mép Cổng vừa tạo (0 gap, 0 overlap)
    /// </summary>
    public void SyncWithSidelineSurround()
    {
#if UNITY_2023_1_OR_NEWER
        SidelineSurround surround = FindFirstObjectByType<SidelineSurround>();
#else
        SidelineSurround surround = FindObjectOfType<SidelineSurround>();
#endif
        if (surround != null)
        {
            surround.leaveGateOpening = true;
            // Ensure the opening margin is zero so the gate fits exactly the gap
            surround.gateOpeningMargin = 0f;
            surround.GenerateSurround();
            Debug.Log("<color=cyan>[ExitGateGenerator]</color> Đã tự động cập nhật Viền Surround ôm khít 100% vào mép Cổng!");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector2 pos = CalculateGateWorldPosition();
        float ppu = (pixelsPerUnit > 0) ? pixelsPerUnit : 100f;
        float cellSize = cellSizePixels / ppu;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireCube(pos, new Vector3(gateWidthInCells * cellSize, gateHeightInCells * cellSize, 0.1f));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos + Vector2.up * (cellSize * 0.8f));
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(ExitGateGenerator))]
public class ExitGateGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ExitGateGenerator gen = (ExitGateGenerator)target;

        EditorGUILayout.Space(5);

        // Nút khôi phục viền liền mạch không bị lẹm
        GUI.backgroundColor = new Color(0.3f, 1f, 0.6f);
        if (GUILayout.Button("🛡️ KHÔI PHỤC VIỀN NGUYÊN VẸN (KHÔNG BỊ LẸM)", GUILayout.Height(36)))
        {
            Undo.RecordObject(gen, "Restore Sideline Tiles");
            gen.openSidelineDoorway = false;
            gen.RestoreAllSidelineTiles();
            gen.GenerateExitGate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("⚡ Điều chỉnh độ cao của Cổng:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("▲ LÊN CAO 1 Ô NỮA (+1)", GUILayout.Height(36)))
        {
            Undo.RecordObject(gen, "Raise Gate 1 Cell");
            gen.extraCellsUp += 1f;
            gen.GenerateExitGate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(1f, 0.8f, 0.3f);
        if (GUILayout.Button("▼ HẠ XUỐNG 1 Ô (-1)", GUILayout.Height(36)))
        {
            Undo.RecordObject(gen, "Lower Gate 1 Cell");
            gen.extraCellsUp -= 1f;
            gen.GenerateExitGate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
        if (GUILayout.Button("⚡ TẠO / CẬP NHẬT CỔNG", GUILayout.Height(34)))
        {
            Undo.RecordObject(gen, "Generate Exit Gate");
            gen.GenerateExitGate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("✕ XÓA CỔNG", GUILayout.Height(34)))
        {
            Undo.RecordObject(gen, "Clear Exit Gate");
            gen.ClearExitGate();
            EditorUtility.SetDirty(gen);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        DrawDefaultInspector();
    }
}
#endif
