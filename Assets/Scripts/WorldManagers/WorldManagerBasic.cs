using System;
using UnityEngine;
using Utils;

namespace WorldManagers
{
    public class WorldManagerBasic : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private Transform cubePrefab;
        [Min(0)]
        [SerializeField] private int width = 50;
        [Min(0)]
        [SerializeField] private int height = 50;
        [Min(0)]
        [SerializeField] private int depth = 50;


        #endregion

        #region Internal State

        private WorldManagerBasic _instance;

        #endregion

        private void Reset()
        {
            // grass.prefab.meta
            cubePrefab = AssetLoader.LoadByGuid<Transform>("52b1b86a404b7d642af306d05dedcd96");
        }

        private void Awake()
        {
            if (_instance)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            var world = new GameObject("World");
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < depth; y++)
                {
                    for (int z = 0; z < height; z++)
                    {
                        Instantiate(cubePrefab, new Vector3(x, -y, z), Quaternion.identity, world.transform);
                    }
                }
            }
        }
    }
}
