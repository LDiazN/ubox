using System;
using Unity.Mathematics;
using UnityEngine;
using Utils;
using World;

namespace WorldManagers
{
    public class WorldManagerV2 : MonoBehaviour
    {
        #region Inspector Properties

        [Tooltip("World size in chunks")]
        [SerializeField] private int3 worldSize = new(16, 1, 16);
        [SerializeField] private WorldChunkV2 chunkPrefab;

        #endregion

        private void Reset()
        {
            // WorldChunkv2.prefab.meta
            chunkPrefab = AssetLoader.LoadByGuid<WorldChunkV2>("ff9f35488e4f2dd4d88ae144fa47356d");
        }

        private void Start()
        {
            for (var x = 0; x < worldSize.x; x++)
                for (var y = 0; y < worldSize.y; y++)
                    for (var z = 0; z < worldSize.z; z++)
                        Instantiate(chunkPrefab, new Vector3(x * WorldChunkV2.CHUNK_SIZE, y * WorldChunkV2.CHUNK_SIZE, z * WorldChunkV2.CHUNK_SIZE), Quaternion.identity);
        }
    }
}
