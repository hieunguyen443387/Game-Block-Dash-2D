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
    public bool hasBoom = false;
    [Range(1f, 99f)] public float countdownDuration = 5f;
    [HideInInspector] public float remainingTime;

    [Header("2. Đối tượng con & Giao diện")]
    public GameObject boomOverlay;
    public GameObject sparkEffectObject;
    public SpriteRenderer numberRenderer;
    public SpriteRenderer numberRendererTens;

    [Header("3. Liên kết ArrowBlock")]
    public ArrowBlock arrowBlock;

    [Header("4. Hiệu ứng khi nổ & Sự kiện")]
    public GameObject boomEffectPrefab;
    public UnityEvent onBoomExploded;
    public UnityEvent<float> onTimeChanged;

    private bool isExploding = false;

    private void Awake() { AutoConnect(); EnsureNumberDisplay(); remainingTime = countdownDuration; UpdateBoomState(); }
    private void Start() { AutoConnect(); EnsureNumberDisplay(); if (remainingTime <= 0) remainingTime = countdownDuration; UpdateBoomState(); }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (hasBoom && !isExploding)
        {
            remainingTime -= Time.deltaTime;
            onTimeChanged?.Invoke(remainingTime);
            UpdateCountDisplay();
            if (remainingTime <= 0f) { remainingTime = 0f; TriggerBoom(); }
        }
    }

    private void OnValidate()
    {
        AutoConnect();
        if (hasBoom)
        {
            FreezableBlock freeze = GetComponent<FreezableBlock>();
            if (freeze != null && freeze.isFrozen) { freeze.isFrozen = false; freeze.UpdateFreezeState(); EditorUtility.SetDirty(freeze); }

            HitCountdownBoomBlock boom = GetComponent<HitCountdownBoomBlock>();
            if (boom != null && boom.hasBoom) { boom.hasBoom = false; boom.UpdateBoomState(); EditorUtility.SetDirty(boom); }
        }
#if UNITY_EDITOR
        if (!Application.isPlaying) { remainingTime = countdownDuration; EditorApplication.delayCall -= SafeEditorUpdate; EditorApplication.delayCall += SafeEditorUpdate; }
#endif
    }

#if UNITY_EDITOR
    private void OnDisableEditor() { EditorApplication.delayCall -= SafeEditorUpdate; }
    public void SafeEditorUpdate() { if (this == null) return; EnsureNumberDisplay(); UpdateBoomState(); }
#endif

    private void Reset() { hasBoom = false; countdownDuration = 5f; remainingTime = 5f; AutoConnect(); EnsureNumberDisplay(); UpdateBoomState(); }

    public void AutoConnect()
    {
        if (arrowBlock == null) arrowBlock = GetComponent<ArrowBlock>();
        if (boomOverlay == null)
        {
            Transform t = transform.Find("Boom Overlay") ?? transform.Find("BoomOverlay") ?? transform.Find("Boom") ?? transform.Find("Boom Block");
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
            if (existingTens != null) numberRendererTens = existingTens.GetComponent<SpriteRenderer>();
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
            if (numberRenderer != null) { numberRenderer.sortingLayerID = overlaySr.sortingLayerID; numberRenderer.sortingOrder = overlaySr.sortingOrder + 2; }
            if (numberRendererTens != null) { numberRendererTens.sortingLayerID = overlaySr.sortingLayerID; numberRendererTens.sortingOrder = overlaySr.sortingOrder + 2; }
        }
    }

    public void UpdateBoomState()
    {
        if (isExploding) return;
        if (boomOverlay != null && boomOverlay.activeSelf != hasBoom) boomOverlay.SetActive(hasBoom);
        if (sparkEffectObject != null && sparkEffectObject.activeSelf != hasBoom) sparkEffectObject.SetActive(hasBoom);
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
                if (!numberRenderer.gameObject.activeSelf) numberRenderer.gameObject.SetActive(true);
                if (displaySeconds < 10)
                {
                    numberRenderer.transform.localPosition = new Vector3(0, -0.08f, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(displaySeconds);
                    if (numberRendererTens != null && numberRendererTens.gameObject.activeSelf) numberRendererTens.gameObject.SetActive(false);
                }
                else
                {
                    EnsureNumberDisplay();
                    if (numberRendererTens != null)
                    {
                        if (!numberRendererTens.gameObject.activeSelf) numberRendererTens.gameObject.SetActive(true);
                        numberRendererTens.transform.localPosition = new Vector3(-0.16f, -0.155f, -0.1f);
                        numberRendererTens.sprite = PixelNumberSprites.GetSprite(displaySeconds / 10);
                    }
                    numberRenderer.transform.localPosition = new Vector3(0.16f, -0.155f, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(displaySeconds % 10);
                }
            }
        }
        else
        {
            if (numberRenderer != null && numberRenderer.gameObject.activeSelf) numberRenderer.gameObject.SetActive(false);
            if (numberRendererTens != null && numberRendererTens.gameObject.activeSelf) numberRendererTens.gameObject.SetActive(false);
        }
    }

    public void SetBoom(bool active, float customDuration = -1f)
    {
        hasBoom = active;
        if (hasBoom)
        {
            FreezableBlock freeze = GetComponent<FreezableBlock>();
            if (freeze != null) freeze.SetFrozen(false);
            HitCountdownBoomBlock boom = GetComponent<HitCountdownBoomBlock>();
            if (boom != null) boom.SetBoom(false);
        }
        if (customDuration > 0f) countdownDuration = customDuration;
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

#if UNITY_2023_1_OR_NEWER
        GameProgressManager progressManager = Object.FindFirstObjectByType<GameProgressManager>();
#else
        GameProgressManager progressManager = Object.FindObjectOfType<GameProgressManager>();
#endif
        if (progressManager != null) progressManager.TriggerLoseLevel(0.45f);
        if (boomEffectPrefab != null) Instantiate(boomEffectPrefab, transform.position, Quaternion.identity);

        RemoveFromSpawner();
        transform.SetParent(null);
        SpriteRenderer mainSr = GetComponent<SpriteRenderer>();
        if (mainSr != null) mainSr.enabled = false;
        Collider2D col2d = GetComponent<Collider2D>();
        if (col2d != null) col2d.enabled = false;
        ArrowBlock arrow = GetComponent<ArrowBlock>();
        if (arrow != null) arrow.enabled = false;

        if (gameObject.activeInHierarchy) StartCoroutine(BoomExplosionRoutine());
        else DestroySelf();
    }

    private IEnumerator BoomExplosionRoutine()
    {
        if (numberRenderer != null) numberRenderer.gameObject.SetActive(false);
        if (numberRendererTens != null) numberRendererTens.gameObject.SetActive(false);
        SpriteRenderer sr = boomOverlay != null ? boomOverlay.GetComponent<SpriteRenderer>() : null;
        Sprite boomSprite = sr != null ? sr.sprite : null;
        ColorUtility.TryParseHtmlString("#FD0505", out Color sparkColor);
        if (boomOverlay != null) boomOverlay.SetActive(false);

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
            pSr.sprite = boomSprite != null ? boomSprite : partSprite;
            pSr.color = sparkColor;
            if (sr != null) { pSr.sortingLayerID = sr.sortingLayerID; pSr.sortingOrder = sr.sortingOrder + 5; }
            particles[i] = p;
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(4f, 8.5f);
        }

        float duration = 0.45f, elapsed = 0f;
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
                    if (pSr != null) { Color c = pSr.color; c.a = Mathf.Lerp(1f, 0f, t); pSr.color = c; }
                }
            }
            yield return null;
        }
        for (int i = 0; i < particleCount; i++) if (particles[i] != null) Destroy(particles[i]);
        DestroySelf();
    }

    private Texture2D CreateSquareTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        tex.SetPixels(colors); tex.Apply();
        return tex;
    }

    private void RemoveFromSpawner()
    {
#if UNITY_2023_1_OR_NEWER
        BlockSpawner spawner = Object.FindFirstObjectByType<BlockSpawner>();
#else
        BlockSpawner spawner = Object.FindObjectOfType<BlockSpawner>();
#endif
        if (spawner != null && arrowBlock != null) spawner.RemoveBlockFromList(arrowBlock);
    }
    private void DestroySelf() { RemoveFromSpawner(); Destroy(gameObject); }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TimeCountdownBoomBlock))]
public class TimeCountdownBoomBlockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TimeCountdownBoomBlock targetBlock = (TimeCountdownBoomBlock)target;
        FreezableBlock freeze = targetBlock.GetComponent<FreezableBlock>();
        HitCountdownBoomBlock boom = targetBlock.GetComponent<HitCountdownBoomBlock>();
        if (targetBlock.hasBoom && ((freeze != null && freeze.isFrozen) || (boom != null && boom.hasBoom)))
            EditorGUILayout.HelpBox("⚠️ CẢNH BÁO: Block đang bị xung đột cơ chế!", MessageType.Error);

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck()) { targetBlock.AutoConnect(); EditorUtility.SetDirty(targetBlock); EditorApplication.delayCall -= targetBlock.SafeEditorUpdate; EditorApplication.delayCall += targetBlock.SafeEditorUpdate; }

        EditorGUILayout.Space(6);
        if (targetBlock.hasBoom) EditorGUILayout.HelpBox($"💣 TIME BOOM ĐANG CHẠY (Còn lại: {targetBlock.remainingTime:F1}s)", MessageType.Warning);
        else EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG", MessageType.Info);

        if (GUILayout.Button(targetBlock.hasBoom ? "🛑 Tắt Time Boom" : "💣 Bật Time Boom", GUILayout.Height(30)))
        {
            Undo.RecordObject(targetBlock, "Toggle Time Boom State");
            targetBlock.SetBoom(!targetBlock.hasBoom);
            EditorUtility.SetDirty(targetBlock);
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif