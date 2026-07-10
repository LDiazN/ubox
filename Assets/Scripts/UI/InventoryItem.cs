using Character;
using Managers;
using UnityEngine;
using UnityEngine.UI;
using World;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public class InventoryItem : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private Color activeColor;
        [SerializeField] private Color inactiveColor;
        [SerializeField] private CubeType type;

        #endregion

        #region Internal State

        private Image _image;

        #endregion

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void Start()
        {
            if (Player.Instance)
                UpdateState(Player.Instance.CubePlacer.CurrentCubeType);
        }

        private void OnEnable()
        {
            var channel = EventsChannel.Instance;
            if (!channel)
                return;

            channel.OnPlayerBlockChanged += UpdateState;
        }

        private void OnDisable()
        {
            var channel = EventsChannel.Instance;
            if (!channel)
                return;

            channel.OnPlayerBlockChanged -= UpdateState;
        }


        private void UpdateState(CubeType newType) => _image.color = newType == type ? activeColor : inactiveColor;
    }
}
