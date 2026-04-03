using UnityEngine;

namespace Compendium
{
    /// <summary>
    /// Global runtime access point for CompendiumDatabase.
    /// Lives as DontDestroyOnLoad so gameplay scenes can read compendium without
    /// needing inspector references across scenes.
    ///
    /// NOTE: Unlock state is stored in CompendiumStateV2 (DontDestroyOnLoad), not here.
    /// </summary>
    public sealed class CompendiumRuntimeV2 : MonoBehaviour
    {
        public static CompendiumRuntimeV2 Instance { get; private set; }

        public CompendiumDatabase Database { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static CompendiumRuntimeV2 Ensure(CompendiumDatabase db)
        {
            if (Instance == null)
            {
                var go = new GameObject("CompendiumRuntimeV2");
                Instance = go.AddComponent<CompendiumRuntimeV2>();
            }

            if (Instance.Database == null && db != null)
                Instance.Database = db;

            return Instance;
        }

        public void SetIfEmpty(CompendiumDatabase db)
        {
            if (Database == null && db != null) Database = db;
        }
    }
}
