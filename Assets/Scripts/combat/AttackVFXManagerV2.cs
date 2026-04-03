using UnityEngine;

/// <summary>
/// Scene-level manager that spawns simple attack VFX (tracers).
/// Keep one instance in the combat scene and assign a tracer prefab.
/// </summary>
public sealed class AttackVFXManagerV2 : MonoBehaviour
{
    public static AttackVFXManagerV2 Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("Prefab with AttackTracerVFX + LineRenderer.")]
    [SerializeField] private AttackTracerVFX tracerPrefab;

    [Header("Colors")]
    [SerializeField] private Color playerColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Placement")]
    [SerializeField] private float yOffset = 0.35f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayTracer(UnitV2 attacker, HexCellV2 targetCell)
    {
        if (attacker == null || targetCell == null) return;
        var color = (attacker.Team == UnitV2.Faction.Player) ? playerColor : enemyColor;
        PlayTracer(attacker.transform.position, targetCell.transform.position, color);
    }

    public void PlayTracerFromTurret(TurretV2 turret, HexCellV2 targetCell)
    {
        if (turret == null || targetCell == null) return;
        // Turrets only fire when controlled (player), but keep safe default.
        var color = playerColor;
        PlayTracer(turret.transform.position, targetCell.transform.position, color);
    }

    public void PlayTracer(Vector3 fromWorld, Vector3 toWorld, Color color)
    {
        if (tracerPrefab == null) return;

        fromWorld.y += yOffset;
        toWorld.y += yOffset;

        var vfx = Instantiate(tracerPrefab, Vector3.zero, Quaternion.identity);
        vfx.Init(fromWorld, toWorld, color);
    }
}
