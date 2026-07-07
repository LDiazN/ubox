using System;
using UnityEngine;
using World;

namespace Managers
{
    public class EventsChannel : MonoBehaviour
    {
        #region Internal State

        public static EventsChannel Instance { get; private set; }

        // Arg: whether pause is active or not
        public event Action<bool> OnPause;

        public event Action<BlockType> OnPlayerBlockChanged;


        #endregion

        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public static void Pause(bool pauseActive) => Instance ?.OnPause?.Invoke(pauseActive);

        public static void ChangePlayerBlock(BlockType block) => Instance ?.OnPlayerBlockChanged?.Invoke(block);
    }
}
