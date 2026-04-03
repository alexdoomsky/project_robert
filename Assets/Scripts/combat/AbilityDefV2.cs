using UnityEngine;

public enum AbilityKindV2
{
    Dash,
    Barrier,
    Heal,
    ArtilleryStrike
}

public enum AbilityTargetModeV2
{
    None,
    Self,
    Cell,
    AllyUnit
}

[CreateAssetMenu(menuName = "Abilities/AbilityV2")]
public class AbilityDefV2 : ScriptableObject
{
    [Header("Base")]
    public string abilityName;
    [TextArea] public string description;
    public AbilityKindV2 kind;
    public AbilityTargetModeV2 targetMode;

    [Header("Costs")]
    public int actionCost = 1;
    public int energyCost = 0;
    public int cooldownRounds = 0;

    [Header("Range")]
    public int minRange = 0;
    public int maxRange = 0;
    public int aoeRadius = 0;

    [Header("Dash")]
    public float dashMoveMultiplier = 2f;
    public float dashDodgeMultiplier = 1.5f;
    public int dashDurationRounds = 1;

    [Header("Barrier")]
    public int barrierMaxHP = 20;
    [Range(0f, 1f)] public float barrierAbsorbPercent = 1f;
    public int barrierDurationRounds = 2;
    public int barrierRegenPerRound = 0;

    [Header("Heal")]
    public int healHP = 10;
    public int restoreEnergy = 1;

    [Header("Artillery")]
    public int artilleryDamage = 30;
    public int artilleryDelayRounds = 1;
    public bool followCasterIfMoved = false;
    public bool scatterOnCasterDeath = true;
    public int scatterRadiusOnDeath = 1;

    [Header("Artillery Telegraph")]
    public GameObject telegraphPrefab;

    [Header("Artillery Preview (AoE)")]
    public bool showAoePreview = true;
    public Color aoePreviewColor = new Color(1f, 0.35f, 0.2f, 1f);
}
