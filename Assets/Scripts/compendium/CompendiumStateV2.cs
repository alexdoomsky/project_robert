using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CompendiumStateV2 : MonoBehaviour
{
    public static CompendiumStateV2 Instance { get; private set; }

    public event Action OnChanged;

    [SerializeField] private List<string> unlockedIds = new List<string>();
    private readonly HashSet<string> _unlocked = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _unlocked.Clear();
        for (int i = 0; i < unlockedIds.Count; i++)
        {
            var id = unlockedIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                _unlocked.Add(id);
        }
    }

    public bool IsUnlocked(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return _unlocked.Contains(id);
    }

    public bool Unlock(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (_unlocked.Add(id))
        {
            unlockedIds.Add(id);
            OnChanged?.Invoke();
            return true;
        }
        return false;
    }
}
