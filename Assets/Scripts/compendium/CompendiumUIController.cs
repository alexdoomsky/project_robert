using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Compendium
{
    public sealed class CompendiumUIController : MonoBehaviour
    {
        [Header("JSON paths (relative to StreamingAssets)")]
        public string articlesPath = "Compendium/articles.json";
        public string termsPath = "Compendium/terms.json";

        [Header("Tabs")]
        public Button loreTabButton;
        public Button mechanicsTabButton;

        [Header("Behavior")]
        [Tooltip("If you open an article from another section via link, switch tabs automatically.")]
        public bool autoSwitchTabOnCrossSectionOpen = true;

        [Header("Left list (12 per page)")]
        public Transform entriesRoot;
        public Button entryButtonPrefab;
        public int entriesPerPage = 12;

        [Tooltip("If assigned, controller will use these buttons as fixed slots (recommended). Size should be 12.")]
        public List<Button> entryButtonSlots = new();

        [Header("Pagination")]
        public Button prevPageButton;
        public Button nextPageButton;
        public TMP_Text pageCounterText;

        [Header("Article panel (right side)")]
        public TMP_Text articleTitleText;
        public TMP_Text articleBodyText;
        public Image articleImage;
        public Sprite fallbackSprite;

        [Header("Tags & related terms")]
        public Transform tagsRoot;
        public Button tagButtonPrefab;
        public Transform relatedTermsRoot;
        public Button relatedTermButtonPrefab;

        [Header("Tooltip provider adapter")]
        public MonoBehaviour tooltipProviderBehaviour;

        [Header("Debug unlock (optional)")]
        [Tooltip("If true, will unlock all articles into CompendiumStateV2 at runtime (for testing).")]
        public bool autoUnlockAllForTesting = true;

        // Shared DB via CompendiumRuntimeV2; unlock state via CompendiumStateV2 (DontDestroyOnLoad).
        private CompendiumDatabase _db;
        private CompendiumStateV2 _state;

        private ITermTooltipProvider TooltipProvider => tooltipProviderBehaviour as ITermTooltipProvider;

        private CompendiumSection _activeSection = CompendiumSection.Lore;
        private int _pageIndex = 0;
        private string _activeTagFilter = null;

        private List<CompendiumArticle> _currentList = new();
        private string _currentArticleId = null;

        // Tooltips depend on the opened article section, not the active tab.
        private CompendiumSection _currentArticleSection = CompendiumSection.Lore;

        private readonly Stack<string> _historyBack = new();

        private void Awake()
        {
            WireButtons();

            // Ensure state exists (source of truth for unlocked IDs).
            _state = EnsureCompendiumState();

            // Reuse runtime DB if exists.
            if (CompendiumRuntimeV2.Instance != null)
                _db = CompendiumRuntimeV2.Instance.Database;

            if (_db == null) _db = new CompendiumDatabase();

            if (!CompendiumJsonLoader.TryLoadFromStreamingAssets(articlesPath, termsPath, out var aRoot, out var tRoot))
            {
                Debug.LogError("[Compendium] Failed to load JSON. Check StreamingAssets paths.");
                return;
            }

            _db.Load(aRoot, tRoot);

            // Register as global runtime so gameplay scenes can read the same DB instance.
            // IMPORTANT: your CompendiumRuntimeV2 must NOT reference CompendiumUnlockService anymore.
            CompendiumRuntimeV2.Ensure(_db);

            if (autoUnlockAllForTesting)
            {
                foreach (var a in _db.GetAllArticles())
                    _state.Unlock(a.id);
            }

            EnsureEntrySlots();
            SetTab(CompendiumSection.Lore, pushHistory: false);
        }

        private void OnDestroy()
        {
            UnwireButtons();
        }

        private static CompendiumStateV2 EnsureCompendiumState()
        {
            if (CompendiumStateV2.Instance != null)
                return CompendiumStateV2.Instance;

            var go = new GameObject("CompendiumStateV2(AutoFromUI)");
            return go.AddComponent<CompendiumStateV2>();
        }

        private void WireButtons()
        {
            if (loreTabButton != null) loreTabButton.onClick.AddListener(() => SetTab(CompendiumSection.Lore, pushHistory: false));
            if (mechanicsTabButton != null) mechanicsTabButton.onClick.AddListener(() => SetTab(CompendiumSection.Mechanics, pushHistory: false));

            if (prevPageButton != null) prevPageButton.onClick.AddListener(PrevPage);
            if (nextPageButton != null) nextPageButton.onClick.AddListener(NextPage);

            // Link-based interactor for body text
            if (articleBodyText != null)
            {
                var linkInteractor = articleBodyText.GetComponent<CompendiumLinkInteractor>();
                if (linkInteractor == null) linkInteractor = articleBodyText.gameObject.AddComponent<CompendiumLinkInteractor>();
                linkInteractor.textComponent = articleBodyText;

                linkInteractor.OnLinkHoverEnter = OnBodyLinkHoverEnter;
                linkInteractor.OnLinkHoverExit = _ => TooltipProvider?.HideTooltip();
                linkInteractor.OnLinkClick = OnBodyLinkClick;
            }
        }

        private void UnwireButtons()
        {
            if (loreTabButton != null) loreTabButton.onClick.RemoveAllListeners();
            if (mechanicsTabButton != null) mechanicsTabButton.onClick.RemoveAllListeners();
            if (prevPageButton != null) prevPageButton.onClick.RemoveAllListeners();
            if (nextPageButton != null) nextPageButton.onClick.RemoveAllListeners();
        }

        private void EnsureEntrySlots()
        {
            if (entryButtonSlots == null) entryButtonSlots = new List<Button>();

            if (entryButtonSlots.Count == 0 && entriesRoot != null)
            {
                var found = entriesRoot.GetComponentsInChildren<Button>(true);
                entryButtonSlots.AddRange(found);
            }

            if (entryButtonSlots.Count > entriesPerPage)
                entryButtonSlots = entryButtonSlots.Take(entriesPerPage).ToList();

            if (entryButtonPrefab != null && entriesRoot != null)
            {
                while (entryButtonSlots.Count < entriesPerPage)
                {
                    var btn = Instantiate(entryButtonPrefab, entriesRoot);
                    entryButtonSlots.Add(btn);
                }
            }

            for (int i = 0; i < entryButtonSlots.Count; i++)
            {
                if (entryButtonSlots[i] != null)
                    entryButtonSlots[i].gameObject.SetActive(false);
            }
        }

        private void SetTab(CompendiumSection section, bool pushHistory)
        {
            _activeSection = section;
            _pageIndex = 0;
            _activeTagFilter = null;

            RebuildList();

            if (_currentList.Count > 0)
            {
                OpenArticle(_currentList[0].id, pushHistory);
            }
            else
            {
                ClearArticlePanel();
            }
        }

        private void RebuildList()
        {
            // Only unlocked articles (unless you explicitly unlock all for testing).
            var list = _db.GetUnlockedArticlesBySection(_state, _activeSection);

            if (!string.IsNullOrWhiteSpace(_activeTagFilter))
                list = list.Where(a => a.tags != null && a.tags.Contains(_activeTagFilter));

            _currentList = list
                .OrderBy(a => a.title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ClampPage();
            RefreshPageUI();
        }

        private void ClampPage()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 0) { _pageIndex = 0; return; }
            _pageIndex = Mathf.Clamp(_pageIndex, 0, pageCount - 1);
        }

        private int GetPageCount()
        {
            if (_currentList == null || _currentList.Count == 0) return 0;
            return Mathf.CeilToInt(_currentList.Count / (float)entriesPerPage);
        }

        private void RefreshPageUI()
        {
            TooltipProvider?.HideTooltip();

            int pageCount = GetPageCount();
            int displayPage = pageCount == 0 ? 0 : (_pageIndex + 1);

            if (pageCounterText != null)
                pageCounterText.text = $"{displayPage}/{Mathf.Max(pageCount, 1)}";

            if (prevPageButton != null) prevPageButton.interactable = (_pageIndex > 0);
            if (nextPageButton != null) nextPageButton.interactable = (_pageIndex < pageCount - 1);

            int start = _pageIndex * entriesPerPage;

            for (int slot = 0; slot < entryButtonSlots.Count; slot++)
            {
                var btn = entryButtonSlots[slot];
                if (btn == null) continue;

                int idx = start + slot;
                if (idx >= 0 && idx < _currentList.Count)
                {
                    var article = _currentList[idx];
                    btn.gameObject.SetActive(true);

                    var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null) tmp.text = article.title;

                    btn.onClick.RemoveAllListeners();
                    var id = article.id;
                    btn.onClick.AddListener(() => OpenArticle(id, pushHistory: true));
                }
                else
                {
                    btn.onClick.RemoveAllListeners();
                    btn.gameObject.SetActive(false);
                }
            }
        }

        private void PrevPage()
        {
            if (_pageIndex <= 0) return;
            _pageIndex--;
            RefreshPageUI();
        }

        private void NextPage()
        {
            int pageCount = GetPageCount();
            if (_pageIndex >= pageCount - 1) return;
            _pageIndex++;
            RefreshPageUI();
        }

        private void OpenArticle(string articleId, bool pushHistory)
        {
            if (string.IsNullOrWhiteSpace(articleId)) return;
            if (_state == null || !_state.IsUnlocked(articleId)) return;
            if (!_db.TryGetArticle(articleId, out var article)) return;

            // Optional: keep left list / tab in sync with opened article section
            if (autoSwitchTabOnCrossSectionOpen && article.SectionEnum != _activeSection)
            {
                _activeSection = article.SectionEnum;
                _pageIndex = 0;
                _activeTagFilter = null;
                RebuildList();
            }

            if (pushHistory && !string.IsNullOrEmpty(_currentArticleId))
                _historyBack.Push(_currentArticleId);

            _currentArticleId = articleId;
            _currentArticleSection = article.SectionEnum;

            if (articleTitleText != null) articleTitleText.text = article.title;

            if (articleImage != null)
                articleImage.sprite = LoadArticleSprite(article.imagePath) ?? fallbackSprite;

            // Build body and convert markup -> TMP links/marks
            string raw = BuildBodyText(article);
            string tmpRich = CompendiumMarkupToTMP.ToTmpRichText(raw);

            if (articleBodyText != null)
                articleBodyText.text = tmpRich;

            RebuildTagButtons(article);
            RebuildRelatedTerms(article);

            TooltipProvider?.HideTooltip();
        }

        private string BuildBodyText(CompendiumArticle article)
        {
            if (article?.blocks == null || article.blocks.Count == 0) return string.Empty;

            var parts = new List<string>(article.blocks.Count);
            foreach (var b in article.blocks)
            {
                if (b == null) continue;
                if (string.IsNullOrWhiteSpace(b.text)) continue;
                parts.Add(b.text.Trim());
            }
            return string.Join("\n\n", parts);
        }

        private void ClearArticlePanel()
        {
            _currentArticleId = null;
            _currentArticleSection = CompendiumSection.Lore;

            if (articleTitleText != null) articleTitleText.text = "";
            if (articleBodyText != null) articleBodyText.text = "";
            if (articleImage != null) articleImage.sprite = fallbackSprite;

            ClearChildren(tagsRoot);
            ClearChildren(relatedTermsRoot);

            TooltipProvider?.HideTooltip();
        }

        private void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private void RebuildTagButtons(CompendiumArticle article)
        {
            ClearChildren(tagsRoot);
            if (tagsRoot == null || tagButtonPrefab == null) return;
            if (article?.tags == null) return;

            foreach (var tag in article.tags)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;

                var btn = Instantiate(tagButtonPrefab, tagsRoot);
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = tag;

                string captured = tag;
                btn.onClick.AddListener(() =>
                {
                    _activeTagFilter = (_activeTagFilter == captured) ? null : captured;
                    _pageIndex = 0;
                    RebuildList();
                });
            }
        }

        private void RebuildRelatedTerms(CompendiumArticle article)
        {
            ClearChildren(relatedTermsRoot);
            if (relatedTermsRoot == null || relatedTermButtonPrefab == null) return;
            if (article?.relatedTerms == null) return;

            foreach (var termId in article.relatedTerms)
            {
                if (string.IsNullOrWhiteSpace(termId)) continue;
                if (!_db.TryGetTerm(termId, out var term)) continue;

                var btn = Instantiate(relatedTermButtonPrefab, relatedTermsRoot);
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = term.displayName;

                var hover = btn.gameObject.GetComponent<UIHoverForwarder>();
                if (hover == null) hover = btn.gameObject.AddComponent<UIHoverForwarder>();

                string key = term.tooltipKey;
                hover.onEnter = () => TooltipProvider?.ShowTermTooltip(key);
                hover.onExit = () => TooltipProvider?.HideTooltip();

                btn.onClick.RemoveAllListeners();
            }
        }

        // Link ids come from CompendiumMarkupToTMP:
        // "term:status_barrier" / "article:mech_unit_astartes"
        private void OnBodyLinkHoverEnter(string linkId)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return;

            if (!linkId.StartsWith("term:", StringComparison.OrdinalIgnoreCase))
                return;

            // Terms tooltips only for mechanics articles (your spec)
            if (_currentArticleSection != CompendiumSection.Mechanics)
                return;

            var termId = linkId.Substring("term:".Length).Trim();
            if (_db.TryGetTerm(termId, out var term))
                TooltipProvider?.ShowTermTooltip(term.tooltipKey);
        }

        private void OnBodyLinkClick(string linkId)
        {
            if (string.IsNullOrWhiteSpace(linkId))
                return;

            if (!linkId.StartsWith("article:", StringComparison.OrdinalIgnoreCase))
                return;

            var articleId = linkId.Substring("article:".Length).Trim();
            if (_state == null || !_state.IsUnlocked(articleId))
                return;

            OpenArticle(articleId, pushHistory: true);
        }

        public void Back()
        {
            if (_historyBack.Count == 0) return;
            var prev = _historyBack.Pop();
            OpenArticle(prev, pushHistory: false);
        }

        private Sprite LoadArticleSprite(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            var path = imagePath.Trim();

            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[Compendium] Sprite not found in Resources at path '{path}'. " +
                                 "Remember: no file extension, path relative to Assets/Resources.");
                return null;
            }

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }

    public sealed class UIHoverForwarder : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        public Action onEnter;
        public Action onExit;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) => onEnter?.Invoke();
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => onExit?.Invoke();
    }
}
