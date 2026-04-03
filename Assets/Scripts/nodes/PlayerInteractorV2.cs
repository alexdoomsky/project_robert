using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractorV2 : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("UI")]
    [SerializeField] private InteractPromptUIV2 promptUI;

    [Header("Selection")]
    [SerializeField] private bool preferNearest = true;

    private readonly List<IInteractableV2> _inRange = new();
    private IInteractableV2 _current;

    private void Awake()
    {
        if (promptUI == null)
            promptUI = FindFirstObjectByType<InteractPromptUIV2>();
    }

    private void Update()
    {
        PickCurrent();

        if (_current != null && _current.CanInteract(this))
        {
            promptUI?.Show($"[{interactKey}] {_current.DisplayName}");
            if (Input.GetKeyDown(interactKey))
            {
                _current.Interact(this);

                // After interaction the object may deactivate itself. Refresh selection and
                // ensure the prompt does not linger in that frame.
                PickCurrent();
                if (_current == null || !_current.CanInteract(this))
                    promptUI?.Hide();
            }
        }
        else
        {
            promptUI?.Hide();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var it = other.GetComponentInParent<IInteractableV2>();
        if (it == null) return;

        if (!_inRange.Contains(it))
            _inRange.Add(it);
    }

    private void OnTriggerExit(Collider other)
    {
        var it = other.GetComponentInParent<IInteractableV2>();
        if (it == null) return;

        _inRange.Remove(it);

        if (_current == it)
            _current = null;
    }

    private void PickCurrent()
    {
        // Cleanup stale entries.
        for (int i = _inRange.Count - 1; i >= 0; i--)
        {
            var it = _inRange[i];
            if (it == null || it.Transform == null || !it.Transform.gameObject.activeInHierarchy)
                _inRange.RemoveAt(i);
        }

        if (_inRange.Count == 0)
        {
            _current = null;
            return;
        }

        IInteractableV2 best = null;

        if (preferNearest)
        {
            float bestSqr = float.MaxValue;
            Vector3 p = transform.position;
            for (int i = 0; i < _inRange.Count; i++)
            {
                var it = _inRange[i];
                if (it == null) continue;
                float sqr = (it.Transform.position - p).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = it;
                }
            }
        }
        else
        {
            int bestPri = int.MinValue;
            for (int i = 0; i < _inRange.Count; i++)
            {
                var it = _inRange[i];
                if (it == null) continue;
                if (it.Priority > bestPri)
                {
                    bestPri = it.Priority;
                    best = it;
                }
            }
        }

        _current = best;
    }
}
