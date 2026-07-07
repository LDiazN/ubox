using Managers;
using TMPro;
using UnityEngine;

namespace UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private TextMeshProUGUI text;

        #endregion

        private void Reset()
        {
            text = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void OnEnable()
        {
            var events = EventsChannel.Instance;
            if (events)
            {
                events.OnPause += OnPause;
            }
        }

        private void OnDisable()
        {
            var events = EventsChannel.Instance;
            if (events) events.OnPause -= OnPause;
        }

        private void OnPause(bool pauseActive)
        {
            Debug.Log($"Paused: {pauseActive}");
            text.gameObject.SetActive(pauseActive);
        }
    }
}
