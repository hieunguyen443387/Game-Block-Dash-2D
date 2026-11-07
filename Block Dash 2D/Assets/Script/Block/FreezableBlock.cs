using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
[DisallowMultipleComponent]
public class FreezableBlock : MonoBehaviour, IPointerDownHandler
{
    [Header("1. Cấu hình Đóng Băng")]
    [Tooltip("Tích chọn để bật lớp băng và khóa di chuyển của ArrowBlock. Bỏ chọn để mở khóa di chuyển.")]
    public bool isFrozen = false;

    [Tooltip("Số đếm trên khối băng (mỗi khi có 1 block vào cổng hoặc dùng Booster Remove sẽ giảm đi 1). Khi về 0 sẽ tan băng.")]
    [Range(0, 99)]
    public int freezeCount = 1;

    [Header("2. Đối tượng con & Giao diện")]
    [Tooltip("Object hiển thị lớp băng phủ lên block")]
    public GameObject iceOverlay;

    [Tooltip("SpriteRenderer hiển thị số đếm Pixel Art (tự động tạo hoặc tìm)")]
    public SpriteRenderer numberRenderer;

    [Tooltip("SpriteRenderer hiển thị hàng chục nếu số đếm >= 10")]
    public SpriteRenderer numberRendererTens;

    [Header("3. Liên kết ArrowBlock")]
    [Tooltip("Tham chiếu ArrowBlock (tự động tìm trên cùng GameObject nếu để trống)")]
    public ArrowBlock arrowBlock;

    [Header("4. Sự kiện")]
    public UnityEvent onIceBroken;
    public UnityEvent<int> onFreezeCountChanged;

    private bool isMeltAnimating = false;
    private Coroutine currentMeltRoutine;

    private void Awake()
    {
        AutoConnect();
        CleanupLegacyText();
        EnsureNumberDisplay();
        UpdateFreezeState();
    }

    private void Start()
    {
        AutoConnect();
        CleanupLegacyText();
        EnsureNumberDisplay();
        UpdateFreezeState();
    }

    private void OnValidate()
    {
        AutoConnect();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= SafeEditorUpdate;
            EditorApplication.delayCall += SafeEditorUpdate;
        }
#endif
    }

#if UNITY_EDITOR
    private void OnDisable()
    {
        EditorApplication.delayCall -= SafeEditorUpdate;
    }

    public void SafeEditorUpdate()
    {
        if (this == null) return;
        CleanupLegacyText();
        EnsureNumberDisplay();
        UpdateFreezeState();
    }
#endif

    private void Reset()
    {
        AutoConnect();
        CleanupLegacyText();
        EnsureNumberDisplay();
        UpdateFreezeState();
    }

    /// <summary>
    /// Xóa bỏ đối tượng TextMeshPro cũ nếu có để loại bỏ hoàn toàn icon chữ 'T' của Unity Scene View
    /// </summary>
    public void CleanupLegacyText()
    {
        Transform parentTr = (iceOverlay != null) ? iceOverlay.transform : transform;
        Transform oldText = parentTr.Find("IceCountText");
        if (oldText == null) oldText = transform.Find("IceCountText");

        if (oldText != null)
        {
            if (Application.isPlaying)
            {
                Destroy(oldText.gameObject);
            }
            else
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(oldText.gameObject);
#endif
            }
        }
    }

    /// <summary>
    /// Tự động kết nối 2 chiều giữa FreezableBlock và ArrowBlock, tìm iceOverlay nếu chưa gán
    /// </summary>
    public void AutoConnect()
    {
        if (arrowBlock == null)
        {
            arrowBlock = GetComponent<ArrowBlock>();
        }

        if (arrowBlock != null && arrowBlock.freezableBlock == null)
        {
            arrowBlock.freezableBlock = this;
        }

        if (iceOverlay == null)
        {
            Transform t = transform.Find("Ice Block");
            if (t == null) t = transform.Find("IceOverlay");
            if (t == null) t = transform.Find("Ice");
            if (t == null) t = transform.Find("Ice Overlay");
            if (t != null) iceOverlay = t.gameObject;
        }
    }

    /// <summary>
    /// Đảm bảo luôn có SpriteRenderer hiển thị số đếm Pixel Art (không dùng Text, không có chữ T)
    /// </summary>
    public void EnsureNumberDisplay()
    {
        Transform parentTr = (iceOverlay != null) ? iceOverlay.transform : transform;

        // 1. Tìm hoặc tạo SpriteRenderer cho số hàng đơn vị / số chính
        if (numberRenderer == null)
        {
            Transform existing = parentTr.Find("IcePixelNumber");
            if (existing != null)
            {
                numberRenderer = existing.GetComponent<SpriteRenderer>();
            }

            if (numberRenderer == null)
            {
                GameObject numObj = new GameObject("IcePixelNumber");
                numObj.transform.SetParent(parentTr, false);
                numObj.transform.localPosition = new Vector3(0, 0, -0.1f);
                numObj.transform.localScale = Vector3.one;

                numberRenderer = numObj.AddComponent<SpriteRenderer>();

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(numObj, "Create IcePixelNumber");
                }
#endif
            }
        }

        // 2. Tìm hoặc tạo SpriteRenderer cho số hàng chục (nếu freezeCount >= 10)
        if (numberRendererTens == null)
        {
            Transform existingTens = parentTr.Find("IcePixelNumber_Tens");
            if (existingTens != null)
            {
                numberRendererTens = existingTens.GetComponent<SpriteRenderer>();
            }
            else if (freezeCount >= 10)
            {
                GameObject tensObj = new GameObject("IcePixelNumber_Tens");
                tensObj.transform.SetParent(parentTr, false);
                tensObj.transform.localPosition = new Vector3(-0.16f, 0, -0.1f);
                tensObj.transform.localScale = Vector3.one;

                numberRendererTens = tensObj.AddComponent<SpriteRenderer>();

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Undo.RegisterCreatedObjectUndo(tensObj, "Create IcePixelNumber_Tens");
                }
#endif
            }
        }

        // Đồng bộ Sorting Layer với Sprite băng để số luôn nổi rõ nét
        SpriteRenderer overlaySr = (iceOverlay != null) ? iceOverlay.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
        if (overlaySr != null)
        {
            if (numberRenderer != null)
            {
                numberRenderer.sortingLayerID = overlaySr.sortingLayerID;
                numberRenderer.sortingOrder = overlaySr.sortingOrder + 2;
            }
            if (numberRendererTens != null)
            {
                numberRendererTens.sortingLayerID = overlaySr.sortingLayerID;
                numberRendererTens.sortingOrder = overlaySr.sortingOrder + 2;
            }
        }
    }

    /// <summary>
    /// Cập nhật hiển thị lớp băng và số đếm pixel theo trạng thái isFrozen & freezeCount
    /// </summary>
    public void UpdateFreezeState()
    {
        if (isMeltAnimating) return;

        if (iceOverlay != null)
        {
            if (iceOverlay.activeSelf != isFrozen)
            {
                iceOverlay.SetActive(isFrozen);
            }
        }

        if (arrowBlock != null && arrowBlock.freezableBlock != this)
        {
            arrowBlock.freezableBlock = this;
        }

        UpdateCountDisplay();
    }

    /// <summary>
    /// Cập nhật hình ảnh Sprite số Pixel Art
    /// </summary>
    public void UpdateCountDisplay()
    {
        bool shouldShow = isFrozen && freezeCount > 0;

        if (shouldShow)
        {
            if (numberRenderer != null)
            {
                if (!numberRenderer.gameObject.activeSelf)
                    numberRenderer.gameObject.SetActive(true);

                if (freezeCount < 10)
                {
                    // 1 chữ số: căn chính giữa
                    numberRenderer.transform.localPosition = new Vector3(0, 0, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(freezeCount);

                    if (numberRendererTens != null && numberRendererTens.gameObject.activeSelf)
                    {
                        numberRendererTens.gameObject.SetActive(false);
                    }
                }
                else
                {
                    // 2 chữ số: hàng chục bên trái, hàng đơn vị bên phải
                    EnsureNumberDisplay();

                    if (numberRendererTens != null)
                    {
                        if (!numberRendererTens.gameObject.activeSelf)
                            numberRendererTens.gameObject.SetActive(true);

                        numberRendererTens.transform.localPosition = new Vector3(-0.16f, 0, -0.1f);
                        numberRendererTens.sprite = PixelNumberSprites.GetSprite(freezeCount / 10);
                    }

                    numberRenderer.transform.localPosition = new Vector3(0.16f, 0, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(freezeCount % 10);
                }
            }
        }
        else
        {
            if (numberRenderer != null && numberRenderer.gameObject.activeSelf)
            {
                numberRenderer.gameObject.SetActive(false);
            }
            if (numberRendererTens != null && numberRendererTens.gameObject.activeSelf)
            {
                numberRendererTens.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Bật/tắt trạng thái đóng băng kèm số đếm
    /// </summary>
    public void SetFrozen(bool frozen, int count = -1)
    {
        isFrozen = frozen;
        if (count >= 0)
        {
            freezeCount = count;
        }
        else if (isFrozen && freezeCount <= 0)
        {
            freezeCount = 1;
        }

        isMeltAnimating = false;
        UpdateFreezeState();
    }

    /// <summary>
    /// Giảm số đếm trên khối băng khi có block vào cổng hoặc dùng Booster Remove
    /// </summary>
    public void DecreaseFreezeCount(int amount = 1)
    {
        if (!isFrozen) return;

        freezeCount -= amount;
        onFreezeCountChanged?.Invoke(freezeCount);

        if (freezeCount <= 0)
        {
            freezeCount = 0;
            UpdateCountDisplay();
            BreakIce();
        }
        else
        {
            UpdateCountDisplay();
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(PlayHitRoutine());
            }
        }
    }

    /// <summary>
    /// Phá băng hoàn toàn: khôi phục di chuyển cho block và kích hoạt hiệu ứng tan biến
    /// </summary>
    public void BreakIce()
    {
        isFrozen = false; // Ngay lập tức mở khóa di chuyển cho ArrowBlock

        onIceBroken?.Invoke();

        if (gameObject.activeInHierarchy)
        {
            if (currentMeltRoutine != null) StopCoroutine(currentMeltRoutine);
            currentMeltRoutine = StartCoroutine(MeltIceRoutine());
        }
        else
        {
            if (iceOverlay != null) iceOverlay.SetActive(false);
            if (numberRenderer != null) numberRenderer.gameObject.SetActive(false);
            if (numberRendererTens != null) numberRendererTens.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Hiệu ứng rung nảy số pixel khi bị trừ 1 số
    /// </summary>
    private IEnumerator PlayHitRoutine()
    {
        Transform t1 = (numberRenderer != null) ? numberRenderer.transform : null;
        Transform t2 = (numberRendererTens != null) ? numberRendererTens.transform : null;

        Vector3 scalePunch = Vector3.one * 1.4f;
        if (t1 != null) t1.localScale = scalePunch;
        if (t2 != null) t2.localScale = scalePunch;

        float elapsed = 0f;
        float duration = 0.12f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t1 != null) t1.localScale = Vector3.Lerp(scalePunch, Vector3.one, t);
            if (t2 != null) t2.localScale = Vector3.Lerp(scalePunch, Vector3.one, t);
            yield return null;
        }

        if (t1 != null) t1.localScale = Vector3.one;
        if (t2 != null) t2.localScale = Vector3.one;
    }

    /// <summary>
    /// Hiệu ứng băng vỡ: Tung tóe các mảnh pixel băng vụn ra xung quanh rồi tan biến
    /// </summary>
    private IEnumerator MeltIceRoutine()
    {
        isMeltAnimating = true;

        if (numberRenderer != null) numberRenderer.gameObject.SetActive(false);
        if (numberRendererTens != null) numberRendererTens.gameObject.SetActive(false);

        if (iceOverlay != null)
        {
            // Lấy sprite và màu của lớp băng hiện tại để tạo các mảnh vỡ đồng màu
            SpriteRenderer sr = iceOverlay.GetComponent<SpriteRenderer>();
            Sprite iceSprite = (sr != null) ? sr.sprite : null;
            Color iceColor = (sr != null) ? sr.color : new Color(0.7f, 0.9f, 1f, 1f);

            // Tắt sprite băng gốc ngay lập tức để chuyển sang hiệu ứng các mảnh vỡ văng ra
            iceOverlay.SetActive(false);

            // Tạo hiệu ứng văng mảnh pixel băng (Spawn các mảnh vụn)
            int pixelCount = 24; // Số lượng mảnh băng văng ra
            GameObject[] shardObjects = new GameObject[pixelCount];
            Vector2[] shardVelocities = new Vector2[pixelCount];

            // Tạo texture hạt vuông nhỏ cho mảnh vỡ nếu không có sprite chi tiết
            Texture2D squareTex = CreateSquareTexture(4);
            Sprite shardSprite = Sprite.Create(squareTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);

            Vector3 spawnPos = iceOverlay.transform.position;

            for (int i = 0; i < pixelCount; i++)
            {
                GameObject shard = new GameObject($"IceShard_{i}");
                shard.transform.position = spawnPos;

                // Random kích thước nhỏ cho mảnh băng vụn
                float scale = Random.Range(0.12f, 0.28f);
                shard.transform.localScale = new Vector3(scale, scale, 1f);

                SpriteRenderer shardSr = shard.AddComponent<SpriteRenderer>();
                shardSr.sprite = iceSprite != null ? iceSprite : shardSprite;
                shardSr.color = iceColor;
                
                // Đồng bộ layer hiển thị để các mảnh băng luôn nổi lên trên cùng
                if (sr != null)
                {
                    shardSr.sortingLayerID = sr.sortingLayerID;
                    shardSr.sortingOrder = sr.sortingOrder + 5;
                }

                shardObjects[i] = shard;

                // Hướng bay ngẫu nhiên 360 độ với vận tốc ngẫu nhiên
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(3f, 7f);
                shardVelocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }

            // Chạy animation bay và mờ dần trong 0.4 giây
            float duration = 0.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                float t = elapsed / duration;

                for (int i = 0; i < pixelCount; i++)
                {
                    if (shardObjects[i] != null)
                    {
                        // Di chuyển hạt theo vận tốc
                        shardObjects[i].transform.position += (Vector3)(shardVelocities[i] * dt);
                        // Giảm dần vận tốc (độ cản gió)
                        shardVelocities[i] *= (1f - dt * 3f);

                        // Mờ dần alpha và thu nhỏ hạt
                        SpriteRenderer sSr = shardObjects[i].GetComponent<SpriteRenderer>();
                        if (sSr != null)
                        {
                            Color c = sSr.color;
                            c.a = Mathf.Lerp(1f, 0f, t);
                            sSr.color = c;
                        }
                    }
                }
                yield return null;
            }

            // Dọn dẹp toàn bộ các mảnh băng sau khi bay xong
            for (int i = 0; i < pixelCount; i++)
            {
                if (shardObjects[i] != null)
                {
                    Destroy(shardObjects[i]);
                }
            }
        }

        isMeltAnimating = false;
        Debug.Log($"<color=cyan>[FreezableBlock]</color> Băng trên khối '{name}' đã vỡ tan tành! Mở khóa di chuyển thành công.");
    }

    /// <summary>
    /// Hàm phụ trợ tạo texture ô vuông đơn giản cho mảnh vỡ băng
    /// </summary>
    private Texture2D CreateSquareTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.white;
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }
    /// <summary>
    /// Hàm thông báo toàn cục: Gọi khi có bất kỳ khối block nào đi qua cổng thoát
    /// </summary>
    public static void OnBlockEnteredGate()
    {
#if UNITY_2023_1_OR_NEWER
        FreezableBlock[] blocks = Object.FindObjectsByType<FreezableBlock>(FindObjectsSortMode.None);
#else
        FreezableBlock[] blocks = Object.FindObjectsOfType<FreezableBlock>();
#endif
        if (blocks == null || blocks.Length == 0) return;

        int affectedCount = 0;
        foreach (var b in blocks)
        {
            if (b != null && b.gameObject.activeInHierarchy && b.isFrozen && b.freezeCount > 0)
            {
                b.DecreaseFreezeCount(1);
                affectedCount++;
            }
        }

        if (affectedCount > 0)
        {
            Debug.Log($"<color=cyan>[FreezableBlock]</color> Có block đi qua cổng -> Đã giảm 1 số trên {affectedCount} khối băng trong màn chơi.");
        }
    }

    /// <summary>
    /// Làm tan băng ngay lập tức từ code
    /// </summary>
    public void Unfreeze()
    {
        freezeCount = 0;
        BreakIce();
    }

    // Chuyển tiếp click nếu FreezableBlock hoặc collider con nhận tương tác chuột
    public void OnPointerDown(PointerEventData eventData)
    {
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BlockItem item = GetComponentInParent<BlockItem>();
            if (item != null)
            {
                BoosterController.Instance.OnBlockClicked(item);
                return;
            }
        }

        if (arrowBlock != null && arrowBlock.gameObject != gameObject)
        {
            arrowBlock.OnPointerDown(eventData);
        }
    }

    private void OnMouseDown()
    {
        if (BoosterController.Instance != null && BoosterController.Instance.IsAnyBoosterActive())
        {
            BlockItem item = GetComponentInParent<BlockItem>();
            if (item != null)
            {
                BoosterController.Instance.OnBlockClicked(item);
                return;
            }
        }

        if (arrowBlock != null && arrowBlock.gameObject != gameObject)
        {
            arrowBlock.TriggerMove();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(FreezableBlock))]
public class FreezableBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        FreezableBlock targetBlock = (FreezableBlock)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        if (EditorGUI.EndChangeCheck())
        {
            targetBlock.AutoConnect();
            EditorUtility.SetDirty(targetBlock);

            EditorApplication.delayCall -= targetBlock.SafeEditorUpdate;
            EditorApplication.delayCall += targetBlock.SafeEditorUpdate;
        }

        EditorGUILayout.Space(6);
        if (targetBlock.isFrozen)
        {
            EditorGUILayout.HelpBox($"🧊 Block ĐANG BỊ ĐÓNG BĂNG (Số Pixel = {targetBlock.freezeCount}):\n- Số đếm hiển thị dạng Pixel Art sắc nét.\n- Không dùng TextMeshPro nên không bị biểu tượng chữ T che.\n- Mỗi khi 1 block vào cổng hoặc dùng Booster Remove, số này sẽ giảm đi 1.\n- Khi về 0, băng sẽ tan biến và mở khóa di chuyển cho block.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG:\n- Không bị đóng băng.\n- ArrowBlock có thể click di chuyển bình thường.", MessageType.Info);
        }

        // Các nút hỗ trợ thao tác nhanh trong Editor
        EditorGUILayout.Space(4);
        string btnText = targetBlock.isFrozen ? "🔓 Mở Khóa Băng (Unfreeze)" : "🧊 Đóng Băng Khối Này (Freeze)";
        GUI.backgroundColor = targetBlock.isFrozen ? new Color(1f, 0.7f, 0.4f) : new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button(btnText, GUILayout.Height(30)))
        {
            Undo.RecordObject(targetBlock, "Toggle Freeze State");
            targetBlock.SetFrozen(!targetBlock.isFrozen);
            EditorUtility.SetDirty(targetBlock);
        }

        if (Application.isPlaying && targetBlock.isFrozen)
        {
            GUI.backgroundColor = new Color(0.3f, 1f, 0.6f);
            if (GUILayout.Button("➖ Test Trừ 1 Số (Giảm Băng)", GUILayout.Height(28)))
            {
                targetBlock.DecreaseFreezeCount(1);
            }
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif
