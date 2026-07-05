using System;
using System.Collections.Generic;
using TMPro.EditorUtilities;
using Unity.Mathematics;
using UnityEditor.PackageManager;
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

        #region Internal State

        public ChunkMap Map { get; private set; }
        public static WorldManagerV2 Instance { get; private set; }

        // Ids of modified chunks
        private readonly HashSet<int3> _changed = new();

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
            Map = new ChunkMap();
        }

        private void Start()
        {
            InitChunkGameobjects();
            InitMap();
        }

        private void LateUpdate()
        {
            foreach (var item in _changed)
            {
                var found = Map.GetChunk(item.x, item.y, item.z, out var data);
                Debug.Assert(found, "Trying to get data from existent chunk");
                EventsChannel.ChunkChanged(data);
            }

            _changed.Clear();
        }

        private void OnDestroy()
        {
            Map.Dispose();
        }

        private void InitChunkGameobjects()
        {
            for(var x = 0; x < worldSize.x; x++)
            for(var y = 0; y < worldSize.y; y++)
            for (var z = 0; z < worldSize.z; z++)
            {
                Instantiate(chunkPrefab,
                    new Vector3(
                        x * WorldChunkV2.ChunkSize,
                        y * WorldChunkV2.ChunkSize,
                        z * WorldChunkV2.ChunkSize
                        ),
                    Quaternion.identity);
            }
        }

        public void SetBlock(int x, int y, int z, BlockType type)
        {
            Map.SetBlock(x, y, z, new BlockData { Type = type });
            _changed.Add(ChunkMap.WorldToChunkGrid(x, y, z));
        }

        private void InitMap()
        {
            var xSize = worldSize.x * WorldChunkV2.ChunkSize;
            var ySize = worldSize.y * WorldChunkV2.ChunkSize;
            var zSize = worldSize.z * WorldChunkV2.ChunkSize;

            for(var x = 0; x < xSize; x ++)
            for(var y = 0; y < ySize; y ++)
            for (var z = 0; z < zSize; z++)
                SetBlock(x, y, z, BlockType.Grass);
        }
    }
}
