using System.Collections;
using UnityEngine;

public sealed class AttackTracerVFX : MonoBehaviour
{
    [SerializeField] private LineRenderer lr;
    [SerializeField] private float lifetime = 0.12f;
    [SerializeField] private float yOffset = 0.35f;

    private Color _base;
    private Material _matInstance;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

    private void Awake()
    {
        if (lr == null) lr = GetComponent<LineRenderer>();
    }

    public void Init(Vector3 from, Vector3 to, Color color, float? overrideLifetime = null, float? overrideYOffset = null)
    {
        if (overrideLifetime.HasValue) lifetime = overrideLifetime.Value;
        if (overrideYOffset.HasValue) yOffset = overrideYOffset.Value;

        _base = color;

        from.y += yOffset;
        to.y += yOffset;

        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        EnsureMaterialInstance();
        ApplyMaterialColor(_base, 1f);

        StopAllCoroutines();
        StartCoroutine(LifeRoutine());
    }

    private void EnsureMaterialInstance()
    {
        // Важно: sharedMaterial менять нельзя (покрасишь всем трассерам сразу).
        // material создаёт инстанс, но лучше контролировать явно.
        if (_matInstance != null) return;

        if (lr.sharedMaterial == null)
        {
            Debug.LogWarning("AttackTracerVFX: LineRenderer has no material. Assign an Unlit material.");
            return;
        }

        _matInstance = new Material(lr.sharedMaterial);
        lr.material = _matInstance;
    }

    private void ApplyMaterialColor(Color c, float alphaMul)
    {
        c.a *= alphaMul;

        if (_matInstance == null) return;

        if (_matInstance.HasProperty(BaseColorId))
            _matInstance.SetColor(BaseColorId, c);
        else if (_matInstance.HasProperty(ColorId))
            _matInstance.SetColor(ColorId, c);

        // На всякий: для некоторых шейдеров vertex color всё же нужен
        lr.startColor = c;
        lr.endColor = c;
    }

    private IEnumerator LifeRoutine()
    {
        yield return null; // дать кадр на рендер

        float t = 0f;
        float dur = Mathf.Max(0.02f, lifetime);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(1f - (t / dur));
            ApplyMaterialColor(_base, k);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_matInstance != null)
            Destroy(_matInstance);
    }
}
