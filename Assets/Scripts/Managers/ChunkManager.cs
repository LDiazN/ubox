using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Utils;
using World;

namespace Managers
{
    public class ChunkManager : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private ChunkRenderer chunkPrefab;

        #endregion

        #region Internal State

        public static ChunkManager Instance { get; private set; }

        private readonly List<ChunkRenderer> _pool = new();

        #endregion

        private void Reset()
        {
            // ChunkRenderer.prefab.meta
            chunkPrefab = AssetLoader.LoadByGuid<ChunkRenderer>("ff9f35488e4f2dd4d88ae144fa47356d");
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

        public static ChunkRenderer Get(Vector3 position, Quaternion rotation)
        {
            if (!Instance || Instance._pool.Count == 0)
                return Instantiate(Instance.chunkPrefab, position, rotation);

            var pool = Instance._pool;
            var chunk = pool[^1];

            chunk.transform.position = position;
            chunk.transform.rotation = rotation;
            SetChunkActive(chunk, true);

            pool.RemoveAt(pool.Count - 1);

            return chunk;
        }

        public static void Dispose(ChunkRenderer chunk)
        {
            if (!Instance)
                Destroy(chunk.gameObject);

            var pool = Instance._pool;
            SetChunkActive(chunk, false);
            chunk.Clear();
            pool.Add(chunk);
        }

        private static void SetChunkActive(ChunkRenderer chunk, bool active)
        {
            // We were doing chunk.SetActive(active) before but according to the profiler it was generating stutters.
            // This seems to be less expensive and achieves the same result
            chunk.Collider.enabled = active;
            chunk.MeshRenderer.enabled = active;
            chunk.enabled = active;
        }
    }
}
