using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;
using World;
using PPlayer = Player.Player;

namespace WorldManagers
{
    public class WorldManagerV2 : MonoBehaviour
    {
        #region Inspector Properties

        [Tooltip("Initial world size in chunks")]
        [SerializeField] private int3 worldSize = new(16, 1, 16);
        [SerializeField] private WorldChunkV2 chunkPrefab;
        [Tooltip("How far away, measured in chunks, the player can see")]
        [SerializeField] private int chunkRenderDistance;

        #endregion

        #region Internal State

        public ChunkMap Map { get; private set; }
        public static WorldManagerV2 Instance { get; private set; }

        // Ids of modified chunks
        private readonly HashSet<int3> _changed = new();

        // Chunk renderers currently active
        private readonly Dictionary<int3, WorldChunkV2> _loadedChunks = new();

        // List of chunks to onload at the end of the frame
        private readonly List<int3> _chunksToUnload = new();

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
            // InitChunkGameobjects();
        }

        private void Update()
        {
            if (!PPlayer.Instance)
                return;

            var playerPosition = new int3(PPlayer.Instance.transform.position);
            var playerChunk = ChunkMap.WorldToChunkGrid(playerPosition.x, playerPosition.y, playerPosition.z);
            var chunkSize = WorldChunkV2.ChunkSize;

            // 1. Make sure that all needed chunks are internally created and rendered
            var minChunk = playerChunk - chunkSize * new int3(chunkRenderDistance, chunkRenderDistance, chunkRenderDistance);
            var maxChunk = playerChunk + chunkSize * new int3(chunkRenderDistance, chunkRenderDistance, chunkRenderDistance);

            for (var x = minChunk.x; x < maxChunk.x; x += chunkSize)
                for (var y = minChunk.y; y < maxChunk.y; y += chunkSize)
                    for (var z = minChunk.z; z < maxChunk.z; z += chunkSize)
                    {
                        // Is this chunk internally created?
                        if (!Map.GetChunk(x, y, z, out _))
                            PopulateChunk(x, y, z);

                        // Is this chunk rendered?
                        var pos = new int3(x, y, z);
                        if (!_loadedChunks.ContainsKey(pos))
                            SpawnChunk(x, y, z);
                    }

            // 2. Unload all chunks that are too far to be visible
            foreach (var cp in _loadedChunks.Keys)
            {
                var visible = minChunk.x <= cp.x && cp.x < maxChunk.x &&
                              minChunk.y <= cp.y && cp.y < maxChunk.y &&
                              minChunk.z <= cp.z && cp.z < maxChunk.z;
                if (!visible)
                    _chunksToUnload.Add(cp);
            }
        }

        private void LateUpdate()
        {
            // Update meshes of chunks that changed recently
            foreach (var item in _changed)
            {
                var found = Map.GetChunk(item.x, item.y, item.z, out var data);
                Debug.Assert(found, "Trying to get data from existent chunk");
                EventsChannel.ChunkChanged(data);
            }

            _changed.Clear();

            // Unload pending chunks
            foreach (var chunk in _chunksToUnload)
            {
                Destroy(_loadedChunks[chunk].gameObject);
                _loadedChunks.Remove(chunk);
            }

            _chunksToUnload.Clear();
        }

        private void OnDestroy()
        {
            Map.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (Map == null)
                return;

            Gizmos.color = Color.red;
            var chunkSize = WorldChunkV2.ChunkSize;
            foreach (var entry in Map.Map)
                Gizmos.DrawWireCube(new float3(entry.Value.Position) + 0.5f * new float3(chunkSize), new float3(chunkSize));
        }

        private void SpawnChunk(int x, int y, int z)
        {
            Debug.Assert(ChunkMap.IsChunkCoords(x, y, z), "Invalid Non-chunk coordinates");
            var instance = Instantiate(chunkPrefab, new float3(x, y, z), Quaternion.identity);
            _loadedChunks[new(x, y, z)] = instance;

            // Trigger a rebuild of this chunk in late update:
            _changed.Add(new(x, y, z));
        }

        private void InitChunkGameobjects()
        {
            for (var x = 0; x < worldSize.x; x++)
                for (var y = 0; y < worldSize.y; y++)
                    for (var z = 0; z < worldSize.z; z++)
                    {
                        var position = new int3(
                            x * WorldChunkV2.ChunkSize,
                            y * WorldChunkV2.ChunkSize,
                            z * WorldChunkV2.ChunkSize
                        );

                        var newChunk = Instantiate(chunkPrefab,
                            new float3(position),
                            Quaternion.identity);

                        _loadedChunks[position] = newChunk;
                    }
        }

        public void SetBlock(int x, int y, int z, BlockType type)
        {
            Map.SetBlock(x, y, z, new BlockData { Type = type });
            _changed.Add(ChunkMap.WorldToChunkGrid(x, y, z));
        }

        // Fills the chunk specified by its chunk position
        private void PopulateChunk(int x, int y, int z)
        {
            const int chunkSize = WorldChunkV2.ChunkSize;
            Debug.Assert(x % chunkSize == 0 &&
                         y % chunkSize == 0 &&
                         z % chunkSize == 0,
                "Unexpected non-chunk position");

            // TODO Populate this properly with ProcGen
            // Is this the sky?
            var blockType = y > worldSize.y * chunkSize ? BlockType.Empty : BlockType.Grass;
            for (var dx = 0; dx < chunkSize; dx++)
                for (var dy = 0; dy < chunkSize; dy++)
                    for (var dz = 0; dz < chunkSize; dz++)
                        SetBlock(x + dx, y + dy, z + dz, blockType);
        }
    }
}
