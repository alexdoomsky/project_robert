using System;
using System.Collections.Generic;
using UnityEngine;

namespace Compendium
{
    public enum CompendiumSection
    {
        Lore,
        Mechanics
    }

    [Serializable]
    public class CompendiumArticlesRoot
    {
        public int version = 1;
        public List<CompendiumArticle> articles = new();
    }

    [Serializable]
    public class CompendiumTermsRoot
    {
        public int version = 1;
        public List<CompendiumTerm> terms = new();
    }

    [Serializable]
    public class CompendiumArticle
    {
        public string id;
        public string section;   // "Lore" / "Mechanics"
        public string type;      // "Unit" / "Faction" / etc
        public string title;
        public string imagePath; // user-defined, optional

        public List<string> tags = new();
        public List<string> relatedTerms = new(); // term ids (e.g. status_barrier)

        public List<CompendiumBlock> blocks = new();

        public CompendiumSection SectionEnum =>
            string.Equals(section, "Lore", StringComparison.OrdinalIgnoreCase) ? CompendiumSection.Lore : CompendiumSection.Mechanics;
    }

    [Serializable]
    public class CompendiumBlock
    {
        public string kind; // "p", "h1", "h2" (на будущее)
        [TextArea(1, 10)]
        public string text;
    }

    [Serializable]
    public class CompendiumTerm
    {
        public string id;          // "status_barrier"
        public string displayName; // "Barrier"
        public string tooltipKey;  // key inside your existing tooltip system
    }
}
