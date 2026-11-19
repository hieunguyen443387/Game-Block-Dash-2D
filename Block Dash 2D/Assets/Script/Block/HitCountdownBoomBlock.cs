using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
[DisallowMultipleComponent]
public class HitCountdownBoomBlock : MonoBehaviour
{
    [Header("1. Cấu hình Boom")]
    [Tooltip("Tích chọn để bật chế độ Boom đếm ngược trên Block này.")]
    public bool hasBoom = false;

    [Tooltip("Số lượt chịu lực (N). Mỗi khi xảy ra va chạm sẽ giảm đi 1. Về 0 sẽ phát nổ.")]
    [Range(0, 99)]
    public int hitCount = 3;

    [Header("2. Đối tượng con & Giao diện")]
    [Tooltip("Object hiển thị hình ảnh/viền Boom phủ lên block")]
    public GameObject boomOverlay;

    [Tooltip("Object chứa hiệu ứng tàn lửa")]
    public GameObject sparkEffectObject;

    [Tooltip("SpriteRenderer hiển thị số đếm Pixel Art")]
    public SpriteRenderer numberRenderer;

    [Tooltip("SpriteRenderer hiển thị hàng chục nếu số đếm >= 10")]
    public SpriteRenderer numberRendererTens;

    [Header("3. Liên kết ArrowBlock")]
    public ArrowBlock arrowBlock;

    [Header("4. Hiệu ứng khi nổ & Sự kiện")]
    public GameObject boomEffectPrefab;

    public UnityEvent onBoomExploded;
    public UnityEvent<int> onHitCountChanged;

    [Header("5. Hiển thị số đếm")]
    [Tooltip("Giữ số đếm luôn xuôi theo màn hình người chơi (không bị xoay theo block)")]
    public bool keepNumberUpright = true;

    [Tooltip("Vị trí số đơn vị so với tâm Overlay. Khi bật Keep Number Upright, X/Y tính theo trục màn hình")]
    public Vector3 numberOffset = new Vector3(0f, 0f, -0.1f);

    [Tooltip("Vị trí số hàng chục (chỉ dùng khi số >= 10). Khi bật Keep Number Upright, X/Y tính theo trục màn hình")]
    public Vector3 tensOffset = new Vector3(-0.16f, 0f, -0.1f);

    private bool isExploding = false;

    /// <summary>Block đang trong quá trình nổ (đã bị loại khỏi bàn chơi, chờ hiệu ứng kết thúc)</summary>
    public bool IsExploding => isExploding;

    private void Awake()
    {
        AutoConnect();
        EnsureNumberDisplay();
        UpdateBoomState();
    }

    private void Start()
    {
        AutoConnect();
        EnsureNumberDisplay();
        UpdateBoomState();
    }

    private void LateUpdate()
    {
        // Chạy sau khi block/overlay đã xoay trong frame này -> số luôn được chỉnh lại cho xuôi
        if (keepNumberUpright) ApplyUprightNumberTransform();
    }

    // =========================================================
    // XỬ LÝ VA CHẠM
    // =========================================================

    // 🔹 Va chạm vật lý thông thường (chỉ chạy khi có Rigidbody2D).
    // Chỉ dùng Enter (không dùng Stay) để tránh bị trừ liên tục mỗi frame.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        NotifyCollision(gameObject, collision.gameObject);
    }

    // 🔹 Va chạm dạng xuyên qua (chỉ chạy khi có Rigidbody2D).
    private void OnTriggerEnter2D(Collider2D collision)
    {
        NotifyCollision(gameObject, collision.gameObject);
    }

    /// <summary>
    /// Thông báo 2 block vừa va chạm nhau và trừ lượt cho MỌI HitCountdownBoomBlock
    /// ở CẢ HAI phía (bên chủ động đâm và bên bị đâm).
    /// Hàm này được gọi từ logic phát hiện vật cản của các block di chuyển
    /// (ArrowBlock, RetreatArrowBlock, TwoWayBlock...) nên boom luôn bị trừ
    /// bất kể block nào là bên tông vào.
    /// </summary>
    public static void NotifyCollision(GameObject a, GameObject b, int amount = 1)
    {
        if (a == null || b == null) return;

        // Chỉ tính va chạm giữa các Block thật sự (bỏ qua tường, viền, cổng...)
        if (!IsBlockObject(a) || !IsBlockObject(b)) return;

        // Tránh trừ 2 lần cho cùng một cặp va chạm trong cùng 1 frame
        // (Unity có thể gọi OnCollisionEnter2D trên cả 2 object).
        if (IsDuplicateThisFrame(a, b)) return;

        HitCountdownBoomBlock boomA = a.GetComponentInParent<HitCountdownBoomBlock>();
        HitCountdownBoomBlock boomB = b.GetComponentInParent<HitCountdownBoomBlock>();

        // Nếu 2 collider thuộc cùng một block (ví dụ collider con) thì chỉ trừ 1 lần
        if (boomA != null && boomA == boomB)
        {
            boomA.TakeHit(amount);
            return;
        }

        if (boomA != null) boomA.TakeHit(amount);
        if (boomB != null) boomB.TakeHit(amount);
    }

    private static readonly System.Collections.Generic.Dictionary<long, int> lastHitFrameByPair =
        new System.Collections.Generic.Dictionary<long, int>();

    private static bool IsDuplicateThisFrame(GameObject a, GameObject b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();

        long key = ((long)Mathf.Min(idA, idB) << 32) | (uint)Mathf.Max(idA, idB);
        int frame = Time.frameCount;

        if (lastHitFrameByPair.TryGetValue(key, out int lastFrame) && lastFrame == frame)
        {
            return true;
        }

        lastHitFrameByPair[key] = frame;
        return false;
    }

    /// <summary>
    /// Kiểm tra GameObject (hoặc cha của nó) có phải là một Block hay không.
    /// </summary>
    public static bool IsBlockObject(GameObject obj)
    {
        if (obj == null) return false;

        return obj.GetComponentInParent<BlockItem>() != null ||
               obj.GetComponentInParent<ArrowBlock>() != null ||
               obj.GetComponentInParent<FreezableBlock>() != null ||
               obj.GetComponentInParent<HitCountdownBoomBlock>() != null ||
               obj.GetComponentInParent<TimeCountdownBoomBlock>() != null;
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
    private void OnDisableEditor()
    {
        EditorApplication.delayCall -= SafeEditorUpdate;
    }

    public void SafeEditorUpdate()
    {
        if (this == null) return;
        EnsureNumberDisplay();
        UpdateBoomState();
    }
#endif

    private void Reset()
    {
        hasBoom = false;
        hitCount = 3;
        AutoConnect();
        EnsureNumberDisplay();
        UpdateBoomState();
    }

    public void AutoConnect()
    {
        if (arrowBlock == null)
        {
            arrowBlock = GetComponent<ArrowBlock>();
        }

        if (boomOverlay == null)
        {
            Transform t = transform.Find("Boom Overlay");
            if (t == null) t = transform.Find("BoomOverlay");
            if (t == null) t = transform.Find("Boom");
            if (t == null) t = transform.Find("Boom Block");
            if (t != null) boomOverlay = t.gameObject;
        }

        if (sparkEffectObject == null && boomOverlay != null)
        {
            Transform t = boomOverlay.transform.Find("SparkEffect");
            if (t != null) sparkEffectObject = t.gameObject;
        }
    }

    public void EnsureNumberDisplay()
    {
        Transform parentTr = (boomOverlay != null) ? boomOverlay.transform : transform;

        if (numberRenderer == null)
        {
            Transform existing = parentTr.Find("BoomPixelNumber");
            if (existing != null) numberRenderer = existing.GetComponent<SpriteRenderer>();

            if (numberRenderer == null)
            {
                GameObject numObj = new GameObject("BoomPixelNumber");
                numObj.transform.SetParent(parentTr, false);
                numObj.transform.localPosition = new Vector3(0, 0, -0.1f);
                numObj.transform.localScale = Vector3.one;
                numberRenderer = numObj.AddComponent<SpriteRenderer>();
            }
        }

        if (numberRendererTens == null)
        {
            Transform existingTens = parentTr.Find("BoomPixelNumber_Tens");
            if (existingTens != null)
            {
                numberRendererTens = existingTens.GetComponent<SpriteRenderer>();
            }
            else if (hitCount >= 10)
            {
                GameObject tensObj = new GameObject("BoomPixelNumber_Tens");
                tensObj.transform.SetParent(parentTr, false);
                tensObj.transform.localPosition = new Vector3(-0.16f, 0, -0.1f);
                tensObj.transform.localScale = Vector3.one;
                numberRendererTens = tensObj.AddComponent<SpriteRenderer>();
            }
        }

        SpriteRenderer overlaySr = (boomOverlay != null) ? boomOverlay.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
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

    public void UpdateBoomState()
    {
        if (isExploding) return;

        if (boomOverlay != null && boomOverlay.activeSelf != hasBoom)
        {
            boomOverlay.SetActive(hasBoom);
        }

        if (sparkEffectObject != null)
        {
            if (sparkEffectObject.activeSelf != hasBoom)
            {
                sparkEffectObject.SetActive(hasBoom);
            }
        }

        UpdateCountDisplay();
    }

    public void UpdateCountDisplay()
    {
        bool shouldShow = hasBoom && hitCount > 0;

        if (shouldShow)
        {
            if (numberRenderer != null)
            {
                if (!numberRenderer.gameObject.activeSelf)
                    numberRenderer.gameObject.SetActive(true);

                if (hitCount < 10)
                {
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(hitCount);

                    if (numberRendererTens != null && numberRendererTens.gameObject.activeSelf)
                    {
                        numberRendererTens.gameObject.SetActive(false);
                    }
                }
                else
                {
                    EnsureNumberDisplay();

                    if (numberRendererTens != null)
                    {
                        if (!numberRendererTens.gameObject.activeSelf)
                            numberRendererTens.gameObject.SetActive(true);

                        numberRendererTens.sprite = PixelNumberSprites.GetSprite(hitCount / 10);
                    }

                    numberRenderer.sprite = PixelNumberSprites.GetSprite(hitCount % 10);
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

        // Đặt vị trí + chỉnh hướng số ngay sau khi đã bật/tắt các chữ số
        ApplyUprightNumberTransform();
    }

    /// <summary>
    /// Đặt lại vị trí/xoay cho các chữ số để chúng luôn xuôi theo màn hình người chơi.
    /// - Vị trí: offset thiết kế được quy đổi theo trục màn hình (phải/lên), nên số hàng chục
    ///   luôn nằm bên trái số hàng đơn vị dù block xoay hướng nào.
    /// - Xoay: đặt world rotation bằng rotation của camera để chữ không bị nghiêng/xoay theo block.
    /// </summary>
    private void ApplyUprightNumberTransform()
    {
        bool showTens = numberRendererTens != null && numberRendererTens.gameObject.activeSelf;

        // Khi có 2 chữ số, số đơn vị lấy đối xứng X của hàng chục qua tâm overlay
        Vector3 unitOffset = showTens
            ? new Vector3(Mathf.Abs(tensOffset.x), tensOffset.y, tensOffset.z)
            : numberOffset;

        ApplyDigitTransform(numberRenderer, unitOffset);
        ApplyDigitTransform(numberRendererTens, tensOffset);
    }

    private void ApplyDigitTransform(SpriteRenderer digitRenderer, Vector3 designOffset)
    {
        if (digitRenderer == null) return;

        Transform digitTr = digitRenderer.transform;
        Transform parentTr = digitTr.parent;

        // Tắt tính năng -> giữ nguyên hành vi cũ (số xoay theo block)
        if (!keepNumberUpright || parentTr == null)
        {
            digitTr.localPosition = designOffset;
            digitTr.localRotation = Quaternion.identity;
            return;
        }

        // Offset thiết kế đang theo trục của block -> đổi sang độ dài world rồi
        // đưa về local space để world offset luôn nằm theo trục màn hình
        Vector3 worldOffset = Vector3.Scale(designOffset, parentTr.lossyScale);
        digitTr.localPosition = parentTr.InverseTransformVector(worldOffset);

        // Không xoay theo block: chữ số song song với màn hình
        Camera cam = Camera.main;
        digitTr.rotation = (cam != null) ? cam.transform.rotation : Quaternion.identity;
    }

    public void SetBoom(bool active, int count = -1)
    {
        hasBoom = active;

        if (count >= 0)
        {
            hitCount = count;
        }
        else if (hasBoom && hitCount <= 0)
        {
            hitCount = 3;
        }

        isExploding = false;
        UpdateBoomState();
    }

    public void TakeHit(int amount = 1)
    {
        if (!hasBoom || isExploding) return;

        hitCount -= amount;
        onHitCountChanged?.Invoke(hitCount);

        if (hitCount <= 0)
        {
            hitCount = 0;
            UpdateCountDisplay();
            TriggerBoom();
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

    private void TriggerBoom()
    {
        isExploding = true;
        hasBoom = false;

        if (sparkEffectObject != null) sparkEffectObject.SetActive(false);

        onBoomExploded?.Invoke();

        if (boomEffectPrefab != null)
        {
            Instantiate(boomEffectPrefab, transform.position, Quaternion.identity);
        }

        // 🔹 QUAN TRỌNG: Tách block bom ra khỏi container TRƯỚC khi gọi TriggerLoseLevel.
        // TriggerLoseLevel sẽ gọi SaveCurrentSession(); nếu lúc đó block vẫn còn trong container
        // thì nó bị lưu lại với hasBoom = false / hitCount = 0.
        // Hậu quả: bấm Continue thì quả bom "sống lại" thành block thường (mất cơ chế bom).
        RemoveFromSpawner();
        transform.SetParent(null);

        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        if (mainSr != null) mainSr.enabled = false;
        Collider2D col2d = GetComponent<Collider2D>();
        if (col2d != null) col2d.enabled = false;

        ArrowBlock arrow = GetComponent<ArrowBlock>();
        if (arrow != null) arrow.enabled = false;

#if UNITY_2023_1_OR_NEWER
        GameProgressManager progressManager = Object.FindFirstObjectByType<GameProgressManager>();
#else
        GameProgressManager progressManager = Object.FindObjectOfType<GameProgressManager>();
#endif
        if (progressManager != null)
        {
            // Gọi SAU khi block đã ra khỏi container => session lưu lại không còn quả bom này
            progressManager.TriggerLoseLevel(0.45f);
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(BoomExplosionRoutine());
        }
        else
        {
            DestroySelf();
        }
    }

    private IEnumerator BoomExplosionRoutine()
    {
        if (numberRenderer != null) numberRenderer.gameObject.SetActive(false);
        if (numberRendererTens != null) numberRendererTens.gameObject.SetActive(false);

        SpriteRenderer sr = (boomOverlay != null) ? boomOverlay.GetComponent<SpriteRenderer>() : null;
        Sprite boomSprite = (sr != null) ? sr.sprite : null;

        Color sparkColor;
        ColorUtility.TryParseHtmlString("#FD0505", out sparkColor);

        if (boomOverlay != null)
        {
            boomOverlay.SetActive(false);
        }

        int particleCount = 26;
        GameObject[] particles = new GameObject[particleCount];
        Vector2[] velocities = new Vector2[particleCount];

        Texture2D squareTex = CreateSquareTexture(4);
        Sprite partSprite = Sprite.Create(squareTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);

        Vector3 spawnPos = boomOverlay != null ? boomOverlay.transform.position : transform.position;

        for (int i = 0; i < particleCount; i++)
        {
            GameObject p = new GameObject($"BoomParticle_{i}");
            p.transform.position = spawnPos;

            float scale = Random.Range(0.15f, 0.32f);
            p.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer pSr = p.AddComponent<SpriteRenderer>();
            pSr.sprite = (boomSprite != null) ? boomSprite : partSprite;
            pSr.color = sparkColor;

            if (sr != null)
            {
                pSr.sortingLayerID = sr.sortingLayerID;
                pSr.sortingOrder = sr.sortingOrder + 5;
            }

            particles[i] = p;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(4f, 8.5f);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        }

        float duration = 0.45f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = elapsed / duration;

            for (int i = 0; i < particleCount; i++)
            {
                if (particles[i] != null)
                {
                    particles[i].transform.position += (Vector3)(velocities[i] * dt);
                    velocities[i] *= (1f - dt * 3.5f);

                    SpriteRenderer pSr = particles[i].GetComponent<SpriteRenderer>();
                    if (pSr != null)
                    {
                        Color c = pSr.color;
                        c.a = Mathf.Lerp(1f, 0f, t);
                        pSr.color = c;
                    }
                }
            }
            yield return null;
        }

        for (int i = 0; i < particleCount; i++)
        {
            if (particles[i] != null)
            {
                Destroy(particles[i]);
            }
        }

        DestroySelf();
    }

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

    private void RemoveFromSpawner()
    {
#if UNITY_2023_1_OR_NEWER
        BlockSpawner spawner = Object.FindFirstObjectByType<BlockSpawner>();
#else
        BlockSpawner spawner = Object.FindObjectOfType<BlockSpawner>();
#endif
        if (spawner != null && arrowBlock != null)
        {
            spawner.RemoveBlockFromList(arrowBlock);
        }
    }

    private void DestroySelf()
    {
        RemoveFromSpawner();
        Destroy(gameObject);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HitCountdownBoomBlock))]
public class HitCountdownBoomBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        HitCountdownBoomBlock targetBlock = (HitCountdownBoomBlock)target;

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
        if (targetBlock.hasBoom)
        {
            EditorGUILayout.HelpBox($"💣 Block ĐANG GẮN BOOM (Số đếm = {targetBlock.hitCount})", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG (Không gắn boom)", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        string btnText = targetBlock.hasBoom ? "🛑 Tắt Boom (Disable Boom)" : "💣 Bật Boom (Enable Boom)";
        GUI.backgroundColor = targetBlock.hasBoom ? new Color(1f, 0.7f, 0.4f) : new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button(btnText, GUILayout.Height(30)))
        {
            Undo.RecordObject(targetBlock, "Toggle Boom State");
            targetBlock.SetBoom(!targetBlock.hasBoom);
            EditorUtility.SetDirty(targetBlock);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif