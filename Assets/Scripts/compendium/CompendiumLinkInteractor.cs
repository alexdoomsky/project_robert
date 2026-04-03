using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Compendium
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CompendiumLinkInteractor : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler
    {
        public TMP_Text textComponent;

        public Action<string> OnLinkHoverEnter; // linkId, e.g. "term:status_barrier"
        public Action<string> OnLinkHoverExit;  // linkId
        public Action<string> OnLinkClick;      // linkId

        private int _hoveredLinkIndex = -1;
        private string _hoveredLinkId = null;

        private void Reset()
        {
            textComponent = GetComponent<TMP_Text>();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (textComponent == null)
                return;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textComponent, eventData.position, eventData.enterEventCamera);

            if (linkIndex < 0)
            {
                ClearHover();
                return;
            }

            var linkInfo = textComponent.textInfo.linkInfo[linkIndex];
            var linkId = linkInfo.GetLinkID();

            if (_hoveredLinkIndex != linkIndex || !string.Equals(_hoveredLinkId, linkId, StringComparison.Ordinal))
            {
                // exit previous
                if (_hoveredLinkIndex >= 0 && !string.IsNullOrEmpty(_hoveredLinkId))
                    OnLinkHoverExit?.Invoke(_hoveredLinkId);

                _hoveredLinkIndex = linkIndex;
                _hoveredLinkId = linkId;

                OnLinkHoverEnter?.Invoke(linkId);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearHover();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_hoveredLinkIndex < 0 || string.IsNullOrEmpty(_hoveredLinkId))
                return;

            OnLinkClick?.Invoke(_hoveredLinkId);
        }

        private void ClearHover()
        {
            if (_hoveredLinkIndex >= 0 && !string.IsNullOrEmpty(_hoveredLinkId))
                OnLinkHoverExit?.Invoke(_hoveredLinkId);

            _hoveredLinkIndex = -1;
            _hoveredLinkId = null;
        }
    }
}
