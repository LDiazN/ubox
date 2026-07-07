using UnityEngine;

namespace Player
{
    public class Player : MonoBehaviour
    {
        #region Internal State

        // This game is single player, so the player is singleton
        public static Player Instance { get; private set; }
        public CubePlacer CubePlacer { get; private set; }

        #endregion

        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CubePlacer = GetComponent<CubePlacer>();
        }
    }
}
