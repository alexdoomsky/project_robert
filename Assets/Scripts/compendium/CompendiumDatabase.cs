using System;
using System.Collections.Generic;
using System.Linq;

namespace Compendium
{
    public sealed class CompendiumDatabase
    {
        private readonly Dictionary<string, CompendiumArticle> _articlesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CompendiumTerm> _termsById = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, CompendiumArticle> ArticlesById => _articlesById;
        public IReadOnlyDictionary<string, CompendiumTerm> TermsById => _termsById;

        public void Load(CompendiumArticlesRoot articlesRoot, CompendiumTermsRoot termsRoot)
        {
            _articlesById.Clear();
            _termsById.Clear();

            if (articlesRoot?.articles != null)
            {
                foreach (var a in articlesRoot.articles)
                {
                    if (a == null || string.IsNullOrWhiteSpace(a.id)) continue;
                    _articlesById[a.id] = a;
                }
            }

            if (termsRoot?.terms != null)
            {
                foreach (var t in termsRoot.terms)
                {
                    if (t == null || string.IsNullOrWhiteSpace(t.id)) continue;
                    _termsById[t.id] = t;
                }
            }
        }

        public bool TryGetArticle(string articleId, out CompendiumArticle article) =>
            _articlesById.TryGetValue(articleId, out article);

        public bool TryGetTerm(string termId, out CompendiumTerm term) =>
            _termsById.TryGetValue(termId, out term);

        public IEnumerable<CompendiumArticle> GetAllArticles() => _articlesById.Values;

        public IEnumerable<CompendiumArticle> GetArticlesBySection(CompendiumSection section) =>
            _articlesById.Values.Where(a => a.SectionEnum == section);

        /// <summary>
        /// Filtered view used by UI: only articles unlocked in CompendiumStateV2.
        /// </summary>
        public IEnumerable<CompendiumArticle> GetUnlockedArticlesBySection(CompendiumStateV2 state, CompendiumSection section)
        {
            if (state == null) return Enumerable.Empty<CompendiumArticle>();
            return _articlesById.Values.Where(a => a.SectionEnum == section && state.IsUnlocked(a.id));
        }
    }
}
