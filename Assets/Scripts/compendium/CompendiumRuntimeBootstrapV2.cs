using UnityEngine;

namespace Compendium
{
    /// <summary>
    /// Optional helper for scenes where you might start directly (Map/LocalZone) in the Editor.
    /// If CompendiumRuntimeV2 doesn't exist, loads JSON and creates it.
    ///
    /// In normal flow (MainMenu has CompendiumUIController), you don't need this.
    /// </summary>
    public sealed class CompendiumRuntimeBootstrapV2 : MonoBehaviour
    {
        [Header("JSON paths (relative to StreamingAssets)")]
        public string articlesPath = "Compendium/articles.json";
        public string termsPath = "Compendium/terms.json";

        [Header("Create CompendiumStateV2 if missing")]
        public bool ensureCompendiumState = true;

        private void Awake()
        {
            if (CompendiumRuntimeV2.Instance != null && CompendiumRuntimeV2.Instance.Database != null)
            {
                if (ensureCompendiumState) EnsureCompendiumState();
                return;
            }

            if (!CompendiumJsonLoader.TryLoadFromStreamingAssets(articlesPath, termsPath, out var articlesRoot, out var termsRoot))
            {
                Debug.LogError($"CompendiumRuntimeBootstrapV2: failed to load JSON from StreamingAssets: {articlesPath}, {termsPath}");
                return;
            }

            var db = new CompendiumDatabase();
            db.Load(articlesRoot, termsRoot);

            CompendiumRuntimeV2.Ensure(db);

            if (ensureCompendiumState) EnsureCompendiumState();
        }

        private static CompendiumStateV2 EnsureCompendiumState()
        {
            if (CompendiumStateV2.Instance != null) return CompendiumStateV2.Instance;
            var go = new GameObject("CompendiumStateV2(Auto)");
            return go.AddComponent<CompendiumStateV2>();
        }
    }
}
