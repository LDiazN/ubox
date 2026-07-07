using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utils;
using World;
using PPlayer = Player.Player;
using noise = Unity.Mathematics.noise;

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
        [Tooltip("How many frames between updates")]
        [SerializeField] private int updateRateInterval = 3;
        [SerializeField] private bool showGizmos = true;

        [Header("Procedural generation")]
        [Tooltip("Minimum height such that every block below this height is a solid block")]
        [Min(1)]
        [SerializeField] private int minHeight = 1;
        [Min(1)]
        [Tooltip("Max height that a solid block can reach")]
        [SerializeField] private int maxHeight = 16;
        [Min(0)]
        [SerializeField] private float noiseScale = 1;

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

        private readonly List<(JobHandle, int3)> _pendingJobs = new();

        // Sometimes someone might try to modify a chunk while the chunk is being rendered.
        // To avoid race conditions with chunk data, we save the request in this queue and apply
        // it when the chunk renderer is finished
        private readonly List<(int3, BlockType)> _changeList = new();

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

        private void Update()
        {
            // Mark pending blocks as changed and clean completed ones
            // (run every frame for responsiveness, chunk rendering depends on this)
            for (int i = _pendingJobs.Count - 1; i >= 0; i--)
            {
                var (handle, chunkPos) = _pendingJobs[i];
                if (handle.IsCompleted)
                {
                    handle.Complete();
                    _changed.Add(chunkPos);
                    _pendingJobs.RemoveAt(i);
                }
            }

            // Use a different update rate for this function, it's expensive
            if (updateRateInterval > 0 && Time.frameCount % updateRateInterval != 0)
                return;

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
                        {
                            PopulateChunk(x, y, z);
                            continue;
                        }

                        // Ignore if it's still being populated asynchronously
                        if (InPending(x, y, z))
                            continue;

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
            // Apply queue of pending changes
            for (int i = _changeList.Count - 1; i >= 0; i--)
            {
                var (pos, type) = _changeList[i];
                if (!IsBusy(pos.x, pos.y, pos.z))
                {
                    SetBlock(pos, type);
                    _changeList.RemoveAt(i);
                }
            }


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
            if (!showGizmos || Map == null)
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

        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (!IsBusy(x, y, z))
            {
                Map.SetBlock(x, y, z, new BlockData { Type = type });
                _changed.Add(ChunkMap.WorldToChunkGrid(x, y, z));
            }
            else
                _changeList.Add((new int3(x, y, z), type));
        }

        public void SetBlock(int3 pos, BlockType type) => SetBlock(pos.x, pos.y, pos.z, type);

        private bool IsBusy(int x, int y, int z)
        {
            var chunkCoords = ChunkMap.WorldToChunkGrid(x, y, z);
            var loaded = _loadedChunks.TryGetValue(chunkCoords, out var chunk);
            if (!loaded)
                return false;

            return chunk.IsBuilding;
        }

        [BurstCompile]
        struct PopulateChunkJob : IJob
        {
            public int X, Y, Z;
            public int MaxHeight, MinHeight;
            public float NoiseScale;
            public ChunkData ChunkData;

            public void Execute()
            {
                // We will use Simplex noise as height map
                const int chunkSize = WorldChunkV2.ChunkSize;
                for (var dx = 0; dx < chunkSize; dx++)
                for (var dy = 0; dy < chunkSize; dy++)
                for (var dz = 0; dz < chunkSize; dz++)
                {
                    var sn = noise.snoise(NoiseScale * new float2(X + dx, Z + dz));
                    var height = (float) (MaxHeight - MinHeight);
                    var isSolid = sn * height + MinHeight >= (Y + dy);
                    var blockType = isSolid ? BlockType.Grass : BlockType.Empty;
                    ChunkData.Blocks.Set(dx, dy, dz, new BlockData { Type = blockType });
                }
            }
        }

        // Fills the chunk specified by its chunk position
            private void PopulateChunk(int x, int y, int z)
            {
                const int chunkSize = WorldChunkV2.ChunkSize;
                Debug.Assert(x % chunkSize == 0 &&
                             y % chunkSize == 0 &&
                             z % chunkSize == 0,
                    "Unexpected non-chunk position");


                Map.AddChunk(x, y, z);
                Map.GetChunk(x, y, z, out var data);

                // Use a job to offload this to worker threads;
                var populateJob = new PopulateChunkJob
                {
                    X = x,
                    Y = y,
                    Z = z,
                    MinHeight = minHeight,
                    MaxHeight = maxHeight,
                    NoiseScale = noiseScale,
                    ChunkData = data
                };
                var handle = populateJob.Schedule();
                _pendingJobs.Add((handle, new int3(x, y, z)));
            }

            private bool InPending(int x, int y, int z)
            {
                foreach (var (_, chunkPos) in _pendingJobs)
                    if (chunkPos.Equals(new int3(x, y, z)))
                        return true;

                return false;
            }
        }
    }
