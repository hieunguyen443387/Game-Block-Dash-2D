using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoomSparkEffect : MonoBehaviour
{
    [Header("Cấu hình Tàn lửa (Idle Sparks)")]
    [Tooltip("Bật/tắt hiệu ứng tàn lửa")]
    public bool enableSparks = true;
    [Tooltip("Số lượng hạt tối đa hiển thị cùng lúc (Object Pooling)")]
    [Range(2, 10)] public int maxSparks = 5;
    [Tooltip("Kích thước hạt tàn lửa")]
    public float sparkScale = 0.5f;

    private List<SparkParticle> sparkPool = new List<SparkParticle>();
    private Coroutine sparkUpdateRoutine;
    private static Sprite cachedSparkSprite;
    private bool isRunning = false;

    private class SparkParticle
    {
        public GameObject obj;
        public SpriteRenderer sr;
        public Vector3 localPos;
        public Vector2 velocity;
        public float lifeTime;
        public float maxLife;
        public bool active;
    }

    private void Awake()
    {
        InitSparkPool();
    }

    private void OnEnable()
    {
        if (enableSparks)
        {
            StartSparks();
        }
    }

    private void OnDisable()
    {
        StopSparks();
    }

    private void InitSparkPool()
    {
        if (cachedSparkSprite == null)
        {
            Texture2D tex = new Texture2D(8, 8);
            Color[] cols = new Color[64];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            tex.SetPixels(cols);
            tex.Apply();
            cachedSparkSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);
        }

        if (sparkPool.Count > 0) return;

        SpriteRenderer parentSr = GetComponentInParent<SpriteRenderer>();

        for (int i = 0; i < maxSparks; i++)
        {
            GameObject sparkObj = new GameObject($"SparkParticle_{i}");
            sparkObj.transform.SetParent(transform, false);
            sparkObj.SetActive(false);

            SpriteRenderer sparkSr = sparkObj.AddComponent<SpriteRenderer>();
            sparkSr.sprite = cachedSparkSprite;

            if (parentSr != null)
            {
                sparkSr.sortingLayerID = parentSr.sortingLayerID;
                sparkSr.sortingOrder = parentSr.sortingOrder + 3;
            }

            sparkPool.Add(new SparkParticle
            {
                obj = sparkObj,
                sr = sparkSr,
                active = false
            });
        }
    }

    public void StartSparks()
    {
        if (!enableSparks) return;
        isRunning = true;
        if (sparkUpdateRoutine == null && gameObject.activeInHierarchy)
        {
            sparkUpdateRoutine = StartCoroutine(UpdateSparksRoutine());
        }
    }

    public void StopSparks()
    {
        isRunning = false;
        if (sparkUpdateRoutine != null)
        {
            if (Application.isPlaying) StopCoroutine(sparkUpdateRoutine);
            sparkUpdateRoutine = null;
        }

        foreach (var spark in sparkPool)
        {
            if (spark.obj != null) spark.obj.SetActive(false);
            spark.active = false;
        }
    }

    private IEnumerator UpdateSparksRoutine()
    {
        while (isRunning)
        {
            foreach (var spark in sparkPool)
            {
                if (!spark.active)
                {
                    ActivateSpark(spark);
                    break;
                }
            }

            foreach (var spark in sparkPool)
            {
                if (spark.active)
                {
                    spark.lifeTime += Time.deltaTime;
                    if (spark.lifeTime >= spark.maxLife)
                    {
                        spark.active = false;
                        spark.obj.SetActive(false);
                    }
                    else
                    {
                        spark.localPos += (Vector3)(spark.velocity * Time.deltaTime);
                        spark.obj.transform.localPosition = spark.localPos;

                        float t = spark.lifeTime / spark.maxLife;
                        // Sử dụng đúng chuẩn mã màu #FD0505
                        Color targetColor;
                        ColorUtility.TryParseHtmlString("#FD0505", out targetColor);
                        targetColor.a = Mathf.Lerp(1f, 0f, t);
                        spark.sr.color = targetColor;
                    }
                }
            }
            yield return null;
        }
    }

    private void ActivateSpark(SparkParticle spark)
    {
        spark.active = true;
        spark.obj.SetActive(true);

        // Xuất phát chuẩn từ vị trí của object chứa script (đầu ngòi nổ) có lệch nhẹ ngẫu nhiên
        spark.localPos = new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.05f, 0.05f), 0f);
        spark.obj.transform.localPosition = spark.localPos;
        spark.obj.transform.localScale = Vector3.one * sparkScale;

        spark.velocity = new Vector2(Random.Range(-1.0f, 1.0f), Random.Range(1.0f, 2.5f));
        spark.lifeTime = 0f;
        spark.maxLife = Random.Range(0.4f, 0.7f);
    }
}