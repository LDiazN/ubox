using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace World
{
    public class WorldChunkV2 : MonoBehaviour
    {
        public const int ChunkSize = 16;

        #region Inspector Properties

        [SerializeField] private Mesh cubeMesh;

        #endregion

        #region Internal State

        private MeshFilter _meshFilter;
        private MeshCollider _collider;
        // Cache mesh data from input mesh
        private MeshDataResult _meshData;
        // When a new change comes in, enqueue it until the currently-running coroutine is finished.
        private readonly Queue<ChunkData> _changes = new();
        public bool IsBuilding {get; private set; }

        #endregion

        struct Vertex
        {
            public float3 Position;
            public float3 Normal;
            public float2 UV;
        }

        // Hardcoded direction of each face in the cube
        static readonly int3[] FaceNormals = {
            new (1,0,0), new (-1,0,0),
            new (0,1,0), new (0,-1,0),
            new (0,0,1), new (0,0,-1)
        };

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _collider = GetComponent<MeshCollider>();
        }

        private void OnEnable()
        {
            var channel = EventsChannel.Instance;
            if (channel)
                channel.OnChunkChanged += OnChunkChanged;
        }

        private void OnDisable()
        {
            var channel = EventsChannel.Instance;
            if (channel)
                channel.OnChunkChanged -= OnChunkChanged;

            if (_changes != null)
                _changes.Clear();
            IsBuilding = false;
        }

        private void OnDestroy()
        {
            _meshData.Dispose();
        }

        private void OnChunkChanged(ChunkData chunk)
        {
            var chunkCoords = chunk.Position;
            var currentCoords = new int3((int)transform.position.x, (int)transform.position.y, (int)transform.position.z);

            if (!currentCoords.Equals(chunkCoords))
                return;

            _changes.Enqueue(chunk);

            if (!IsBuilding)
            {
                StartCoroutine(BuildQueueRoutine());
            }
        }

        // This additional coroutine saves us from adding an update method
        private IEnumerator BuildQueueRoutine()
        {
            IsBuilding = true;
            while (_changes.Count > 0)
            {
                var chunk = _changes.Dequeue();
                yield return BuildChunk(chunk);
            }
            IsBuilding = false;
        }

        static bool InChunk(int x, int y, int z) =>
            x is >= 0 and < ChunkSize &&
            y is >= 0 and < ChunkSize &&
            z is >= 0 and < ChunkSize;

        void InitMeshData()
        {
            // Do nothing if already created
            if (_meshData.FaceIndexBlocks.IsCreated)
                return;

            _meshData = GetMeshData();
        }

        // Whether this cell in the chunk is occupied: There's something other than air or emptyness here
        static bool Occupied(int x, int y, int z, in ChunkData chunk)
        {
            if (!InChunk(x, y, z))
                return false;

            var type = chunk.Blocks.Get(x, y, z).Type;
            return type != BlockType.Empty && type != BlockType.None;
        }

        struct CountResult
        {
            public int TotalVerts;
            public int TotalIndices;
        }

        [BurstCompile]
        struct CountVertsJob : IJob
        {
            [ReadOnly] public ChunkData Chunk;
            [ReadOnly] public MeshDataResult MeshData;
            public NativeReference<CountResult> Result;

            public void Execute()
            {
                Result.Value = CountVerts(Chunk, MeshData);
            }
        }

        private static CountResult CountVerts(in ChunkData chunk, in MeshDataResult meshData)
        {
            // Vertex buffer layout:
            // c = cube, f = face
            // vBase1 = 0                               vBase2 = 24 * 6 = 144
            // v                                        v
            // [ c1f1[24] | c1f2[24] | ... | c1f6[24] | c2f1[24] ... ]
            // cxfy = a continuous segment of float3
            // Index buffer layout:
            // [ i_c1fx[6] | i_c1fy[6] | ...]
            // i_cxfy = a continuous segment of ushort, in particular, it's the index buffer only for the exposed face
            // Note that each face now has a copy of all vertices of the original cube. The index only chooses some of
            // these vertices.
            //
            // This approach is necessary bc otherwise we have to rebuild the index buffer of each face
            // to map to just the indices we actually need; Choosing only the useful vertices requires restructuring
            // the index buffer.

            // First pass: count total verts/indices needed
            int totalVerts = 0;
            int totalIndices = 0;
            for (var x = 0; x < ChunkSize; x++)
                for (var y = 0; y < ChunkSize; y++)
                    for (var z = 0; z < ChunkSize; z++)
                    {
                        if (!Occupied(x, y, z, chunk)) continue;
                        // TODO we could optimize the vertex buffer by storing one cube if ANY face is visible, and then
                        // only pushing the visible faces to the index buffer
                        for (var f = 0; f < 6; f++)
                        {
                            // Use FaceNormals to check neighbors: Get the normal of the current face, and check if the
                            // cell in that direction has something
                            var n = FaceNormals[f];
                            if (Occupied(x + n.x, y + n.y, z + n.z, chunk)) continue;

                            // We assume we will use all vertices and indices of this cube.
                            totalVerts += meshData.VertCount;
                            totalIndices += meshData.IndicesPerFace;
                        }
                    }

            return new CountResult
            {
                TotalVerts = totalVerts,
                TotalIndices = totalIndices
            };
        }

        [BurstCompile]
        private struct ConstructMeshJob : IJob
        {
            [ReadOnly] public ChunkData Chunk;
            [ReadOnly] public CountResult CountResult;
            [ReadOnly] public MeshDataResult MeshData;
            public Mesh.MeshDataArray MeshDataArray;

            public void Execute()
            {
                ConstructMesh(Chunk, CountResult, MeshData, MeshDataArray);
            }
        }

        static void ConstructMesh(in ChunkData chunk, in CountResult countResult, in MeshDataResult meshData, in Mesh.MeshDataArray meshDataArray)
        {
            var data = meshDataArray[0];
            var dstVerts = data.GetVertexData<Vertex>();
            var dstIndices = data.GetIndexData<uint>();

            // Creating the actual buffers.
            // vCursor: next available vertex position
            // iCursor: next available index position
            int vCursor = 0, iCursor = 0;
            for (int x = 0; x < ChunkSize; x++)
                for (int y = 0; y < ChunkSize; y++)
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (!Occupied(x, y, z, chunk)) continue;
                        float3 position = new(x, y, z);

                        for (int f = 0; f < 6; f++)
                        {
                            int3 n = FaceNormals[f];

                            // Like before, we skip if this face has neighbors
                            if (Occupied(x + n.x, y + n.y, z + n.z, chunk)) continue;

                            // vBase: where this face's vertices start
                            int vBase = vCursor;
                            // copy full vert buffer once per emitted face. Wasteful, I know, but easy to implement
                            for (int v = 0; v < meshData.VertCount; v++)
                            {
                                dstVerts[vCursor++] = new Vertex
                                {
                                    Position = (float3)meshData.Positions[v] + position,
                                    Normal = meshData.Normals[v],
                                    UV = meshData.UVs[v]
                                };
                            }

                            int startIndex = f * meshData.IndicesPerFace;
                            for (int i = 0; i < meshData.IndicesPerFace; i++)
                                dstIndices[iCursor++] = (uint)vBase + meshData.FaceIndexBlocks[startIndex + i];
                        }
                    }

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor(0, countResult.TotalIndices)
            {
                bounds = new Bounds(new Vector3(ChunkSize, ChunkSize, ChunkSize) * 0.5f,
                    new Vector3(ChunkSize, ChunkSize, ChunkSize)),
                vertexCount = countResult.TotalVerts
            }, MeshUpdateFlags.DontRecalculateBounds);
        }

        IEnumerator BuildChunk(ChunkData chunk)
        {
            InitMeshData();
            var countJob = new CountVertsJob
            {
                Chunk = chunk,
                MeshData = _meshData,
                Result = new NativeReference<CountResult>(Allocator.TempJob)
            };
            var countHandle = countJob.Schedule();

            while (!countHandle.IsCompleted)
                yield return null;

            countHandle.Complete();

            var countResult = countJob.Result.Value;
            countJob.Result.Dispose();

            // When rendering just air
            if (countResult.TotalVerts == 0)
            {
                _meshFilter.mesh = null;
                _collider.sharedMesh = null;
                yield break;
            }

            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var totalVerts = countResult.TotalVerts;
            var totalIndices = countResult.TotalIndices;

            var data = meshDataArray[0];
            var layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
            layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

            // This will allocate space for vertices and indices
            data.SetVertexBufferParams(totalVerts, layout);
            data.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);

            layout.Dispose();

            var constructJob = new ConstructMeshJob
            {
                Chunk = chunk,
                CountResult = countResult,
                MeshData = _meshData,
                MeshDataArray = meshDataArray
            };

            var constructHandle = constructJob.Schedule();

            while (!constructHandle.IsCompleted)
                yield return null;

            constructHandle.Complete();

            var mesh = new Mesh { name = "ChunkMesh" };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.RecalculateBounds();

            _meshFilter.mesh = mesh;
            _collider.sharedMesh = null;
            _collider.sharedMesh = _meshFilter.mesh;
        }

        struct MeshDataResult
        {
            public NativeArray<Vector3> Positions;
            public int VertCount;
            public NativeArray<Vector3> Normals;
            public NativeArray<Vector2> UVs;
            public NativeArray<uint> FaceIndexBlocks;
            public int IndicesPerFace;

            public void Dispose()
            {
                if (Positions.IsCreated)
                    Positions.Dispose();

                if (Normals.IsCreated)
                    Normals.Dispose();

                if (UVs.IsCreated)
                    UVs.Dispose();

                if (FaceIndexBlocks.IsCreated)
                    FaceIndexBlocks.Dispose();
            }
        }

        MeshDataResult GetMeshData()
        {
            // Read data from source cube mesh
            var srcDataArray = Mesh.AcquireReadOnlyMeshData(cubeMesh);
            var srcData = srcDataArray[0];

            int srcVertCount = srcData.vertexCount;
            var srcPositions = new NativeArray<Vector3>(srcVertCount, Allocator.Persistent);
            var srcNormals = new NativeArray<Vector3>(srcVertCount, Allocator.Persistent);
            var srcUVs = new NativeArray<Vector2>(srcVertCount, Allocator.Persistent);
            srcData.GetVertices(srcPositions);
            srcData.GetNormals(srcNormals);
            srcData.GetUVs(0, srcUVs);

            int srcIndexCount = (int)cubeMesh.GetIndexCount(0);
            NativeArray<ushort> srcIndices16 = new NativeArray<ushort>(srcIndexCount, Allocator.Temp);
            srcData.GetIndices(srcIndices16, 0);

            // Group source indices into 6 faces by triangle normal.
            // Assume indices are stored adjacent by face: the two triangles in a face are side by side

            // Holds index data per each face. A cube has 6 faces.
            int indicesPerFace = srcIndexCount / 6; // ex: 6 indices per face (2 tris)
            var faceIndexBlocks = new NativeArray<uint>(6 * indicesPerFace, Allocator.Persistent);

            for (int f = 0; f < 6; f++)
            {
                int start = f * indicesPerFace;
                for (int i = 0; i < indicesPerFace; i++)
                {
                    uint idx = srcIndices16[start + i];
                    faceIndexBlocks[start + i] = idx;
                }
            }

            srcDataArray.Dispose();
            srcIndices16.Dispose();

            return new MeshDataResult
            {
                Positions = srcPositions,
                VertCount = srcVertCount,
                Normals = srcNormals,
                UVs = srcUVs,
                FaceIndexBlocks = faceIndexBlocks,
                IndicesPerFace = indicesPerFace
            };
        }
    }
}
