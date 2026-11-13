using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[SelectionBase]
[DisallowMultipleComponent]
public class TimeCountdownBoomBlock : MonoBehaviour
{
    [Header("1. Cấu hình Time Boom")]
    [Tooltip("Tích chọn để bật chế độ Boom đếm ngược thời gian trên Block này.")]
    public bool hasBoom = false;

    [Tooltip("Thời gian đếm ngược tính bằng giây trước khi bom nổ.")]
    [Range(1f, 99f)]
    public float countdownDuration = 5f;

    [HideInInspector]
    public float remainingTime;

    [Header("2. Đối tượng con & Giao diện")]
    [Tooltip("Object hiển thị hình ảnh/viền Boom phủ lên block")]
    public GameObject boomOverlay;

    [Tooltip("Object chứa hiệu ứng tàn lửa")]
    public GameObject sparkEffectObject;

    [Tooltip("SpriteRenderer hiển thị số đếm Pixel Art")]
    public SpriteRenderer numberRenderer;

    [Tooltip("SpriteRenderer hiển thị hàng chục nếu số giây >= 10")]
    public SpriteRenderer numberRendererTens;

    [Header("3. Liên kết ArrowBlock")]
    public ArrowBlock arrowBlock;

    [Header("4. Hiệu ứng khi nổ & Sự kiện")]
    public GameObject boomEffectPrefab;

    public UnityEvent onBoomExploded;
    public UnityEvent<float> onTimeChanged;

    private bool isExploding = false;

    private void Awake()
    {
        AutoConnect();
        EnsureNumberDisplay();
        remainingTime = countdownDuration;
        UpdateBoomState();
    }

    private void Start()
    {
        AutoConnect();
        EnsureNumberDisplay();
        if (remainingTime <= 0) remainingTime = countdownDuration;
        UpdateBoomState();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (hasBoom && !isExploding)
        {
            remainingTime -= Time.deltaTime;
            onTimeChanged?.Invoke(remainingTime);
            UpdateCountDisplay();

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                TriggerBoom();
            }
        }
    }

    private void OnValidate()
    {
        AutoConnect();

        // XUNG ĐỘT FIX: Nếu bật Boom thì tự động tắt Đóng Băng để tránh xung đột
        if (hasBoom)
        {
            FreezableBlock freeze = GetComponent<FreezableBlock>();
            if (freeze != null && freeze.isFrozen)
            {
                freeze.isFrozen = false;
                freeze.UpdateFreezeState();
#if UNITY_EDITOR
                EditorUtility.SetDirty(freeze);
#endif
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            remainingTime = countdownDuration;
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
        countdownDuration = 5f;
        remainingTime = 5f;
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

        if (sparkEffectObject == null)
        {
            Transform t = transform.Find("SparkEffect");
            if (t == null && boomOverlay != null) t = boomOverlay.transform.Find("SparkEffect");
            if (t != null) sparkEffectObject = t.gameObject;
        }
    }

    public void EnsureNumberDisplay()
    {
        Transform parentTr = (boomOverlay != null) ? boomOverlay.transform : transform;

        if (numberRenderer == null)
        {
            Transform existing = parentTr.Find("TimeBoomPixelNumber");
            if (existing != null) numberRenderer = existing.GetComponent<SpriteRenderer>();

            if (numberRenderer == null)
            {
                GameObject numObj = new GameObject("TimeBoomPixelNumber");
                numObj.transform.SetParent(parentTr, false);
                numObj.transform.localPosition = new Vector3(0, 0, -0.1f);
                numObj.transform.localScale = Vector3.one;
                numberRenderer = numObj.AddComponent<SpriteRenderer>();
            }
        }

        if (numberRendererTens == null)
        {
            Transform existingTens = parentTr.Find("TimeBoomPixelNumber_Tens");
            if (existingTens != null)
            {
                numberRendererTens = existingTens.GetComponent<SpriteRenderer>();
            }
            else if (countdownDuration >= 10f)
            {
                GameObject tensObj = new GameObject("TimeBoomPixelNumber_Tens");
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
        int displaySeconds = Mathf.Max(0, Mathf.CeilToInt(remainingTime));
        bool shouldShow = hasBoom && remainingTime > 0f;

        if (shouldShow)
        {
            if (numberRenderer != null)
            {
                if (!numberRenderer.gameObject.activeSelf)
                    numberRenderer.gameObject.SetActive(true);

                if (displaySeconds < 10)
                {
                    // 🔻 Chỉnh số Y từ 0 xuống số âm (ví dụ: -0.08f) để dịch xuống dưới
                    numberRenderer.transform.localPosition = new Vector3(0, -0.08f, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(displaySeconds);

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

                        // 🔻 Chỉnh số Y của hàng chục xuống
                        numberRendererTens.transform.localPosition = new Vector3(-0.16f, -0.155f, -0.1f);
                        numberRendererTens.sprite = PixelNumberSprites.GetSprite(displaySeconds / 10);
                    }

                    // 🔻 Chỉnh số Y của hàng đơn vị xuống
                    numberRenderer.transform.localPosition = new Vector3(0.16f, -0.155f, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(displaySeconds % 10);
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

    public void SetBoom(bool active, float customDuration = -1f)
    {
        hasBoom = active;

        if (hasBoom)
        {
            FreezableBlock freeze = GetComponent<FreezableBlock>();
            if (freeze != null)
            {
                freeze.SetFrozen(false);
            }
        }

        if (customDuration > 0f)
        {
            countdownDuration = customDuration;
        }

        remainingTime = countdownDuration;
        isExploding = false;
        UpdateBoomState();
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
            GameObject p = new GameObject($"TimeBoomParticle_{i}");
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

    private void DestroySelf()
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

        Destroy(gameObject);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TimeCountdownBoomBlock))]
public class TimeCountdownBoomBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TimeCountdownBoomBlock targetBlock = (TimeCountdownBoomBlock)target;

        FreezableBlock freeze = targetBlock.GetComponent<FreezableBlock>();
        if (freeze != null && freeze.isFrozen && targetBlock.hasBoom)
        {
            EditorGUILayout.HelpBox("⚠️ CẢNH BÁO: Block đang bị xung đột (vừa bật Đóng Băng vừa bật Time Boom). Hãy tắt 1 trong 2!", MessageType.Error);
        }

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
            EditorGUILayout.HelpBox($"💣 TIME BOOM ĐANG CHẠY (Còn lại: {targetBlock.remainingTime:F1}s)", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG (Không gắn Time Boom)", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        string btnText = targetBlock.hasBoom ? "🛑 Tắt Time Boom (Disable)" : "💣 Bật Time Boom (Enable)";
        GUI.backgroundColor = targetBlock.hasBoom ? new Color(1f, 0.7f, 0.4f) : new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button(btnText, GUILayout.Height(30)))
        {
            Undo.RecordObject(targetBlock, "Toggle Time Boom State");
            targetBlock.SetBoom(!targetBlock.hasBoom);
            EditorUtility.SetDirty(targetBlock);
        }

        GUI.backgroundColor = Color.white;
    }
}
#endif