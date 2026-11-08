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

    [Tooltip("Số lượt chịu lực (N). Mỗi khi bị ArrowBlock khác húc trúng sẽ giảm đi 1. Về 0 sẽ phát nổ.")]
    [Range(0, 99)]
    public int hitCount = 3;

    [Header("2. Đối tượng con & Giao diện")]
    [Tooltip("Object hiển thị hình ảnh/viền Boom phủ lên block")]
    public GameObject boomOverlay;

    [Tooltip("Object chứa hiệu ứng tàn lửa (Gắn script BoomSparkEffect vào đây)")]
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

    private bool isExploding = false;

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
                    numberRenderer.transform.localPosition = new Vector3(0, 0, -0.1f);
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

                        numberRendererTens.transform.localPosition = new Vector3(-0.16f, 0, -0.1f);
                        numberRendererTens.sprite = PixelNumberSprites.GetSprite(hitCount / 10);
                    }

                    numberRenderer.transform.localPosition = new Vector3(0.16f, 0, -0.1f);
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
        Debug.Log($"<color=red>[HitCountdownBoomBlock]</color> Khối '{name}' đã về 0 và PHÁT NỔ!", this);

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

    /// <summary>
    /// Hiệu ứng nổ bung các mảnh tàn lửa màu #FD0505 tản ra xung quanh (giống hiệu ứng băng vỡ của FreezableBlock)
    /// </summary>
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
            EditorGUILayout.HelpBox($"💣 Block ĐANG GẮN BOOM (Số đếm = {targetBlock.hitCount}):\n- Hiệu ứng nổ bung tàn lửa màu #FD0505 sẵn sàng.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG:\n- Không kích hoạt Boom.", MessageType.Info);
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