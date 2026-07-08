using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class Coordinates : MonoBehaviour
    {
        #region Internal State

        private TextMeshProUGUI _text;
        private string _original;

        #endregion

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _original = _text.text;
        }

        private void Update()
        {
            if (!Player.Player.Instance)
            {
                SetText(0, 0, 0);
                return;
            }

            var pos = new int3(Player.Player.Instance.transform.position);
            SetText(pos.x, pos.y, pos.z);
        }

        private void SetText(int x, int y, int z)
        {
            var newText = string.Format(_original, x, y, z);
            _text.text = newText;
        }
    }
}
