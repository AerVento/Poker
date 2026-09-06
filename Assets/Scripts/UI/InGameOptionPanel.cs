using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    public sealed class InGameOptionPanel : SettingsPanel<InGameOptionPanel>
    {
        [SerializeField] private Button _leaveButton;
        [SerializeField] private TextMeshProUGUI _leaveButtonLabel;
        [SerializeField] private Button _participationButton;
        [SerializeField] private TextMeshProUGUI _participationButtonLabel;
        [SerializeField] private Button _keepSpectatingButton;

        private GamePanel _game;

        protected override void OnEnable()
        {
            base.OnEnable();
            Bind(GamePanel.Instance);
        }

        public void Bind(GamePanel game)
        {
            if (_game != null)
                _game.DetachSettingsControls();
            _game = game;
            if (_game != null)
                _game.AttachSettingsControls(_leaveButton, _leaveButtonLabel,
                    _participationButton, _participationButtonLabel, _keepSpectatingButton);
        }

        protected override void OnDisable()
        {
            Bind(null);
            base.OnDisable();
        }
    }
}
