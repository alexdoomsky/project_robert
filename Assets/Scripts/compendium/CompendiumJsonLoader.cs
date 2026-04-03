using System.IO;
using UnityEngine;

namespace Compendium
{
    public static class CompendiumJsonLoader
    {
        public static bool TryLoadFromStreamingAssets(
            string relativeArticlesPath,
            string relativeTermsPath,
            out CompendiumArticlesRoot articlesRoot,
            out CompendiumTermsRoot termsRoot)
        {
            articlesRoot = null;
            termsRoot = null;

            var articlesFull = Path.Combine(Application.streamingAssetsPath, relativeArticlesPath);
            var termsFull = Path.Combine(Application.streamingAssetsPath, relativeTermsPath);

            if (!File.Exists(articlesFull))
            {
                Debug.LogError($"[Compendium] Articles JSON not found: {articlesFull}");
                return false;
            }

            if (!File.Exists(termsFull))
            {
                Debug.LogError($"[Compendium] Terms JSON not found: {termsFull}");
                return false;
            }

            try
            {
                var articlesJson = File.ReadAllText(articlesFull);
                var termsJson = File.ReadAllText(termsFull);

                articlesRoot = JsonUtility.FromJson<CompendiumArticlesRoot>(articlesJson);
                termsRoot = JsonUtility.FromJson<CompendiumTermsRoot>(termsJson);

                if (articlesRoot == null || termsRoot == null)
                {
                    Debug.LogError("[Compendium] Failed to parse JSON (root is null). Check JSON format.");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Compendium] Exception while reading/parsing JSON: {ex}");
                return false;
            }
        }
    }
}
