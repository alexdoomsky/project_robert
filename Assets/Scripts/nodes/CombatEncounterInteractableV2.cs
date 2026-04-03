using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Interactable that ALWAYS starts combat during exploration.
/// Uses ZoneExitEncounterConfigV2 only to PICK a preset (no chance roll),
/// based on current zone/threat (RunStateV2).
/// Returns back to the current Local Zone scene after combat.
/// </summary>
public sealed class CombatEncounterInteractableV2 : InteractableEventV2
{
    [Header("Encounter")]
    [Tooltip("If null, will try LocalZoneControllerV2.exitEncounterConfig.")]
    [SerializeField] private ZoneExitEncounterConfigV2 encounterConfig;

    protected override void Awake()
    {
        base.Awake();

        if (encounterConfig == null)
        {
            var lz = FindFirstObjectByType<LocalZoneControllerV2>();
            if (lz != null)
                encounterConfig = lz.exitEncounterConfig;
        }
    }

    public override void Interact(PlayerInteractorV2 interactor)
    {
        var run = RunStateV2.Instance;
        var router = SceneRouterV2.Instance;

        if (run == null || router == null)
        {
            Debug.LogError("CombatEncounterInteractableV2: RunStateV2 or SceneRouterV2 missing.");
            return;
        }

        // Count interaction as completed so it disappears from objective/arrow systems.
        base.Interact(interactor);

        if (encounterConfig == null)
        {
            Debug.LogError("CombatEncounterInteractableV2: encounterConfig is null. Assign it on the prefab or via LocalZoneControllerV2.exitEncounterConfig.");
            return;
        }

        // ALWAYS start combat here: no roll chance, only preset pick.
        run.pendingEncounterPreset = encounterConfig.PickPreset(run.currentZoneIndex, run.currentThreat);

        // After combat return to this local-zone scene (NOT the map).
        string returnScene = SceneManager.GetActiveScene().name;
        router.LoadCombat(returnScene);
    }
}
