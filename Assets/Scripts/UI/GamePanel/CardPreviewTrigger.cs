using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    // Separate from the visual: PokerElement disables its fallback Image after artwork loads.
    public sealed class CardPreviewTrigger : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private PokerElement _source;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging ||
                _source == null || !UIManager.Available)
                return;
            CardPreviewPanel.Open(_source);
        }

        private void OnDisable()
        {
            if (CardPreviewPanel.Instance != null)
                CardPreviewPanel.Instance.CloseFor(_source);
        }
    }
}
