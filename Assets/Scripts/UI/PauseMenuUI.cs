using Managers;
using UnityEngine;

namespace UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        #region Internal State

        private CanvasGroup _canvasGroup;

        #endregion

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            SetVisible(false);
        }

        private void OnEnable()
        {
            var events = EventsChannel.Instance;
            if (events)
                events.OnPause += SetVisible;
        }

        private void OnDisable()
        {
            var events = EventsChannel.Instance;
            if (events) events.OnPause -= SetVisible;
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1 : 0;
            _canvasGroup.interactable = visible;
            // _canvasGroup.blocksRaycasts = visible;
        }
    }
}
