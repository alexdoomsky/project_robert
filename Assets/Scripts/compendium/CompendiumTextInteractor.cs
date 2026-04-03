using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Compendium
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CompendiumTextInteractor : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
    {
        public TMP_Text textComponent;

        private readonly List<MarkupSpan> _spans = new();
        private MarkupSpan? _hoverSpan;

        public IReadOnlyList<MarkupSpan> Spans => _spans;

        public System.Action<MarkupSpan> OnSpanHoverEnter;
        public System.Action<MarkupSpan> OnSpanHoverExit;
        public System.Action<MarkupSpan> OnSpanClick;

        private void Reset()
        {
            textComponent = GetComponent<TMP_Text>();
        }

        public void SetParsedText(string visibleText, List<MarkupSpan> spans)
        {
            if (textComponent == null) textComponent = GetComponent<TMP_Text>();

            textComponent.text = visibleText;

            _spans.Clear();
            _spans.AddRange(spans);

            _hoverSpan = null;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (textComponent == null || _spans.Count == 0)
            {
                ClearHover();
                return;
            }

            int charIndex = TMP_TextUtilities.FindIntersectingCharacter(textComponent, eventData.position, eventData.enterEventCamera, true);
            if (charIndex < 0)
            {
                ClearHover();
                return;
            }

            var span = FindSpanByCharIndex(charIndex);
            if (span.HasValue)
            {
                if (!_hoverSpan.HasValue || !SpanEquals(_hoverSpan.Value, span.Value))
                {
                    if (_hoverSpan.HasValue) OnSpanHoverExit?.Invoke(_hoverSpan.Value);
                    _hoverSpan = span;
                    OnSpanHoverEnter?.Invoke(span.Value);
                }
            }
            else
            {
                ClearHover();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearHover();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_hoverSpan.HasValue) return;
            OnSpanClick?.Invoke(_hoverSpan.Value);
        }

        private void ClearHover()
        {
            if (_hoverSpan.HasValue)
            {
                OnSpanHoverExit?.Invoke(_hoverSpan.Value);
                _hoverSpan = null;
            }
        }

        private MarkupSpan? FindSpanByCharIndex(int charIndex)
        {
            for (int i = 0; i < _spans.Count; i++)
            {
                if (_spans[i].ContainsIndex(charIndex))
                    return _spans[i];
            }
            return null;
        }

        private static bool SpanEquals(MarkupSpan a, MarkupSpan b)
        {
            return a.start == b.start &&
                   a.length == b.length &&
                   a.type == b.type &&
                   a.targetId == b.targetId;
        }
    }
}
