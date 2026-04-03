using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks local-zone completion: how many interactables were spawned and how many were interacted with.
/// State is restored from RunStateV2 so progress survives combat scene loads.
///
/// Exit is NOT counted in totals.
/// </summary>
public sealed class LocalZoneObjectiveTrackerV2 : MonoBehaviour
{
    public static LocalZoneObjectiveTrackerV2 Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private int totalCount;
    [SerializeField] private int completedCount;

    private readonly HashSet<WorldInteractableMarkerV2> _registered = new();
    private readonly HashSet<WorldInteractableMarkerV2> _completed = new();

    public int TotalCount => totalCount;
    public int CompletedCount => completedCount;
    public bool IsAllDone => totalCount > 0 && completedCount >= totalCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        totalCount = 0;
        completedCount = 0;
        _registered.Clear();
        _completed.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Registers an interactable as part of the "things to do" list.
    /// Exit markers should NOT be registered.
    /// </summary>
    public void RegisterObjective(WorldInteractableMarkerV2 marker)
    {
        if (marker == null) return;
        if (_registered.Contains(marker)) return;

        _registered.Add(marker);
        totalCount++;

        // restore completion if it already happened before scene reload
        if (RunStateV2.Instance != null &&
            RunStateV2.Instance.IsLocalObjectiveCompleted(marker))
        {
            _completed.Add(marker);
            completedCount++;
        }
    }

    /// <summary>
    /// Marks an objective as completed. Safe to call multiple times.
    /// </summary>
    public void MarkCompleted(WorldInteractableMarkerV2 marker)
    {
        if (marker == null) return;
        if (!_registered.Contains(marker)) return;
        if (_completed.Contains(marker)) return;

        _completed.Add(marker);
        completedCount++;

        // persist to RunState so combat reload doesn't reset progress
        if (RunStateV2.Instance != null)
            RunStateV2.Instance.MarkLocalObjectiveCompleted(marker);
    }
}
