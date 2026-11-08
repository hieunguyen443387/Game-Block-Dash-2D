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

        // XUNG ĐỘT FIX: Nếu bật Đóng Băng thì tự động tắt Boom để tránh xung đột
        if (isFrozen)
        {
            HitCountdownBoomBlock boom = GetComponent<HitCountdownBoomBlock>();
            if (boom != null && boom.hasBoom)
            {
                boom.hasBoom = false;
                boom.UpdateBoomState();
#if UNITY_EDITOR
                EditorUtility.SetDirty(boom);
#endif
            }
        }

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
        isFrozen = false; // Mặc định ban đầu là tắt
        freezeCount = 1;
        AutoConnect();
        CleanupLegacyText();
        EnsureNumberDisplay();
        UpdateFreezeState();
    }

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

    public void EnsureNumberDisplay()
    {
        Transform parentTr = (iceOverlay != null) ? iceOverlay.transform : transform;

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
                    numberRenderer.transform.localPosition = new Vector3(0, 0, -0.1f);
                    numberRenderer.sprite = PixelNumberSprites.GetSprite(freezeCount);

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

    public void SetFrozen(bool frozen, int count = -1)
    {
        isFrozen = frozen;
        
        // Nếu bật đóng băng, tự động tắt Boom trên cùng object
        if (isFrozen)
        {
            HitCountdownBoomBlock boom = GetComponent<HitCountdownBoomBlock>();
            if (boom != null)
            {
                boom.SetBoom(false);
            }
        }

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

    public void BreakIce()
    {
        isFrozen = false;
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

    private IEnumerator MeltIceRoutine()
    {
        isMeltAnimating = true;

        if (numberRenderer != null) numberRenderer.gameObject.SetActive(false);
        if (numberRendererTens != null) numberRendererTens.gameObject.SetActive(false);

        if (iceOverlay != null)
        {
            SpriteRenderer sr = iceOverlay.GetComponent<SpriteRenderer>();
            Sprite iceSprite = (sr != null) ? sr.sprite : null;
            Color iceColor = (sr != null) ? sr.color : new Color(0.7f, 0.9f, 1f, 1f);

            iceOverlay.SetActive(false);

            int pixelCount = 24;
            GameObject[] shardObjects = new GameObject[pixelCount];
            Vector2[] shardVelocities = new Vector2[pixelCount];

            Texture2D squareTex = CreateSquareTexture(4);
            Sprite shardSprite = Sprite.Create(squareTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);

            Vector3 spawnPos = iceOverlay.transform.position;

            for (int i = 0; i < pixelCount; i++)
            {
                GameObject shard = new GameObject($"IceShard_{i}");
                shard.transform.position = spawnPos;

                float scale = Random.Range(0.12f, 0.28f);
                shard.transform.localScale = new Vector3(scale, scale, 1f);

                SpriteRenderer shardSr = shard.AddComponent<SpriteRenderer>();
                shardSr.sprite = iceSprite != null ? iceSprite : shardSprite;
                shardSr.color = iceColor;
                
                if (sr != null)
                {
                    shardSr.sortingLayerID = sr.sortingLayerID;
                    shardSr.sortingOrder = sr.sortingOrder + 5;
                }

                shardObjects[i] = shard;

                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(3f, 7f);
                shardVelocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }

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
                        shardObjects[i].transform.position += (Vector3)(shardVelocities[i] * dt);
                        shardVelocities[i] *= (1f - dt * 3f);

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

            for (int i = 0; i < pixelCount; i++)
            {
                if (shardObjects[i] != null)
                {
                    Destroy(shardObjects[i]);
                }
            }
        }

        isMeltAnimating = false;
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

    public static void OnBlockEnteredGate()
    {
#if UNITY_2023_1_OR_NEWER
        FreezableBlock[] blocks = Object.FindObjectsByType<FreezableBlock>(FindObjectsSortMode.None);
#else
        FreezableBlock[] blocks = Object.FindObjectsOfType<FreezableBlock>();
#endif
        if (blocks == null || blocks.Length == 0) return;

        foreach (var b in blocks)
        {
            if (b != null && b.gameObject.activeInHierarchy && b.isFrozen && b.freezeCount > 0)
            {
                b.DecreaseFreezeCount(1);
            }
        }
    }

    public void Unfreeze()
    {
        freezeCount = 0;
        BreakIce();
    }

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

        // Cảnh báo nếu đang bật cả hai (đề phòng trường hợp dữ liệu cũ)
        HitCountdownBoomBlock boom = targetBlock.GetComponent<HitCountdownBoomBlock>();
        if (boom != null && boom.hasBoom && targetBlock.isFrozen)
        {
            EditorGUILayout.HelpBox("⚠️ CẢNH BÁO: Block đang bị xung đột (vừa bật Đóng Băng vừa bật Boom). Hãy tắt 1 trong 2!", MessageType.Error);
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
        if (targetBlock.isFrozen)
        {
            EditorGUILayout.HelpBox($"🧊 Block ĐANG BỊ ĐÓNG BĂNG (Số Pixel = {targetBlock.freezeCount})", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Block BÌNH THƯỜNG (Không đóng băng)", MessageType.Info);
        }

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