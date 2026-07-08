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
        [SerializeField] private BlockType type;

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
            if (Player.Player.Instance)
                UpdateState(Player.Player.Instance.CubePlacer.CurrentBlockType);
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


        private void UpdateState(BlockType newType) => _image.color = newType == type ? activeColor : inactiveColor;
    }
}
