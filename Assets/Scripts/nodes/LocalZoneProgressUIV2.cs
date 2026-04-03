using TMPro;
using UnityEngine;

/// <summary>
/// Simple UI presenter for local-zone completion progress.
/// Shows: X/Y and optional "all done" text.
/// </summary>
public sealed class LocalZoneProgressUIV2 : MonoBehaviour
{
    [SerializeField] private LocalZoneObjectiveTrackerV2 tracker;

    [Header("UI")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text allDoneText;

    private void Awake()
    {
        if (tracker == null) tracker = LocalZoneObjectiveTrackerV2.Instance;
    }

    private void Update()
    {
        if (tracker == null) tracker = LocalZoneObjectiveTrackerV2.Instance;
        if (tracker == null) return;

        if (progressText != null)
            progressText.text = $"{tracker.CompletedCount}/{tracker.TotalCount}";

        if (allDoneText != null)
        {
            allDoneText.gameObject.SetActive(tracker.IsAllDone);
            if (tracker.IsAllDone)
                allDoneText.text = "All done. Find the exit.";
        }
    }
}
