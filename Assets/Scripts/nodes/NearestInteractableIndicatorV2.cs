using UnityEngine;
using UnityEngine.UI;

public sealed class NearestInteractableIndicatorV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera cam;

    [Tooltip("Optional. If provided, exit marker will only be pointed to when all objectives are completed.")]
    [SerializeField] private LocalZoneObjectiveTrackerV2 objectiveTracker;

    [Tooltip("Optional. If set, any interactables under this transform will be ignored by the indicator (service objects, UI anchors, etc.).")]
    [SerializeField] private Transform ignoreRoot;

    [Tooltip("UI arrow image (RectTransform). Pivot should be (0.5, 0.5).")]
    [SerializeField] private RectTransform arrow;

    [Header("Behavior")]
    [Tooltip("If true, arrow is hidden when the target is comfortably on-screen. If false, arrow is shown at (clamped) target screen position.")]
    [SerializeField] private bool hideWhenOnScreen = false;

    [Tooltip("Screen edge padding in pixels.")]
    [SerializeField] private float edgePadding = 40f;

    [Tooltip("Ignore interactables closer than this (world units).")]
    [SerializeField] private float minDistance = 0f;

    [Tooltip("Update rate (times per second). 0 = every frame.")]
    [SerializeField] private float updateRate = 20f;

    private float _nextUpdateTime;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (objectiveTracker == null) objectiveTracker = LocalZoneObjectiveTrackerV2.Instance;
    }

    private void Update()
    {
        if (arrow == null) return;
        if (player == null) { arrow.gameObject.SetActive(false); return; }
        if (cam == null) cam = Camera.main;
        if (cam == null) { arrow.gameObject.SetActive(false); return; }

        if (objectiveTracker == null) objectiveTracker = LocalZoneObjectiveTrackerV2.Instance;

        if (updateRate > 0f && Time.time < _nextUpdateTime)
            return;

        _nextUpdateTime = updateRate > 0f ? Time.time + 1f / updateRate : Time.time;

        if (objectiveTracker == null) objectiveTracker = LocalZoneObjectiveTrackerV2.Instance;

        var target = FindNearest(player.position);
        if (target == null)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        UpdateArrow(target.transform.position);
    }

    private WorldInteractableMarkerV2 FindNearest(Vector3 playerPos)
    {
        var items = InteractableRegistryV2.Items;
        // First pass: prefer non-exit objectives.
        bool allowExit = objectiveTracker != null && objectiveTracker.IsAllDone;

        WorldInteractableMarkerV2 best = null;
        float bestSqr = float.MaxValue;

        // 1) Find nearest non-exit active marker
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null || !it.isActiveAndEnabled || !it.IsActive) continue;
            if (ignoreRoot != null && it.transform.IsChildOf(ignoreRoot)) continue;
            if (it.Kind == InteractableKindV2.Exit) continue;

            float sqr = (it.transform.position - playerPos).sqrMagnitude;
            if (sqr < minDistance * minDistance) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = it;
            }
        }

        if (best != null)
            return best;

        // 2) If all done, point to exit.
        if (!allowExit)
            return null;

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null || !it.isActiveAndEnabled || !it.IsActive) continue;
            if (ignoreRoot != null && it.transform.IsChildOf(ignoreRoot)) continue;
            if (it.Kind != InteractableKindV2.Exit) continue;

            float sqr = (it.transform.position - playerPos).sqrMagnitude;
            if (sqr < minDistance * minDistance) continue;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = it;
            }
        }

        return best;
    }

    private void UpdateArrow(Vector3 worldPos)
    {
        // convert to screen
        Vector3 screen = cam.WorldToScreenPoint(worldPos);

        // behind camera
        if (screen.z < 0f)
        {
            // mirror to make it behave "in front"
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
            screen.z = 0.01f;
        }

        bool onScreen =
            screen.x >= edgePadding &&
            screen.x <= Screen.width - edgePadding &&
            screen.y >= edgePadding &&
            screen.y <= Screen.height - edgePadding;

        // NOTE: In practice hiding the arrow when the target is on-screen feels like "the arrow broke"
        // (especially after you interact and the nearest target changes). So we keep it visible and
        // just place it at the target screen position (clamped to padding).
        // If you really want it hidden when on-screen, do that in UI (e.g., fade out), not hard-disable.
        arrow.gameObject.SetActive(true);

        // Place arrow at clamped target screen position.
        // If the target is off-screen, this puts the arrow on the edge.
        // If the target is on-screen (and we didn't hide), this puts it near the target.
        float clampedX = Mathf.Clamp(screen.x, edgePadding, Screen.width - edgePadding);
        float clampedY = Mathf.Clamp(screen.y, edgePadding, Screen.height - edgePadding);
        arrow.position = new Vector3(clampedX, clampedY, 0f);

        // rotation: arrow should point towards target direction from screen center
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = (new Vector2(screen.x, screen.y) - screenCenter).normalized;

        // Unity UI "up" is +Y, so we rotate from up vector to dir
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrow.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }
}
