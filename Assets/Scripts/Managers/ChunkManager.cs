using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using World;

namespace Managers
{
    public class ChunkManager : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private WorldChunkV2 chunkPrefab;

        #endregion

        #region Internal State

        public static ChunkManager Instance { get; private set; }

        private readonly List<WorldChunkV2> _pool = new();

        #endregion

        private void Reset()
        {
            // WorldChunkv2.prefab.meta
            chunkPrefab = AssetLoader.LoadByGuid<WorldChunkV2>("ff9f35488e4f2dd4d88ae144fa47356d");
        }

        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public static WorldChunkV2 Get(Vector3 position, Quaternion rotation)
        {
            if (!Instance || Instance._pool.Count == 0)
                return Instantiate(Instance.chunkPrefab, position, rotation);

            var pool = Instance._pool;
            var chunk = pool[^1];

            chunk.transform.position = position;
            chunk.transform.rotation = rotation;
            chunk.gameObject.SetActive(true);

            pool.RemoveAt(pool.Count - 1);

            return chunk;
        }

        public static void Dispose(WorldChunkV2 chunk)
        {
            if (!Instance)
                Destroy(chunk.gameObject);

            var pool = Instance._pool;
            chunk.gameObject.SetActive(false);
            chunk.Clear();
            pool.Add(chunk);
        }

    }
}
