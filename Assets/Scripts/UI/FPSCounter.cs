using System;
using TMPro;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class FPSCounter : MonoBehaviour
    {
        #region Internal State

        private TextMeshProUGUI _text;

        #endregion

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            var fps = 1f / Time.unscaledDeltaTime;
            _text.text = $"{Mathf.RoundToInt(fps)} FPS";
        }
    }
}
