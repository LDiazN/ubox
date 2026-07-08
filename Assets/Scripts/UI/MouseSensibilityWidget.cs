using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class MouseSensibilityWidget : MonoBehaviour
    {
        #region Internal State

        private Slider _slider;

        #endregion

        private void Start()
        {
            _slider = GetComponent<Slider>();

            var player = Player.Player.Instance;
            if (player)
                _slider.value = player.FirstPersonMovement?.cameraSpeed ?? 0;
        }

        public void UpdateValue(float newValue)
        {
            var player = Player.Player.Instance;
            if (!player)
                return;

            player.FirstPersonMovement.cameraSpeed = newValue;
        }
    }
}
