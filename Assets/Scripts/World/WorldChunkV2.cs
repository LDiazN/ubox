using System.Collections.Generic;
using Unity.Collections;
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
        private MeshRenderer _meshRenderer;

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
            _meshRenderer = GetComponent<MeshRenderer>();
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
        }

        private void OnChunkChanged(ChunkData chunk)
        {
            var chunkCoords = chunk.Position;
            var currentCoords = new int3((int)transform.position.x, (int)transform.position.y, (int)transform.position.z);

            if (!currentCoords.Equals(chunkCoords))
                return;

            _meshFilter.mesh = BuildChunk(chunk);
        }


        bool InChunk(int x, int y, int z, in ChunkData chunk) => x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize;

        Mesh BuildChunk(in ChunkData chunk)
        {
            var md = GetMeshData();

            // Group source indices into 6 faces by triangle normal.
            // Assume indices are stored adjacent by face: the two triangles in a face are side by side

            // Holds index data per each face. A cube has 6 faces.
            var faceIndexBlocks = new List<uint>[6];
            for (int f = 0; f < 6; f++)
                faceIndexBlocks[f] = new List<uint>();

            int indicesPerFace = md.IndexCount / 6; // ex: 6 indices per face (2 tris)
            for (int f = 0; f < 6; f++)
            {
                int start = f * indicesPerFace;
                for (int i = 0; i < indicesPerFace; i++)
                {
                    uint idx = md.Indices[start + i];
                    faceIndexBlocks[f].Add(idx);
                }
            }
            md.Indices.Dispose(); // not needed anymore

            // Whether this cell in the chunk is occupied (there's data in it)
            bool Occupied(int x, int y, int z, in ChunkData chunk)
            {
                if (!InChunk(x, y, z, chunk))
                    return false;

                var type = chunk.Blocks.Get(x, y, z).Type;
                return type != BlockType.Empty && type != BlockType.None;
            }

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
                    totalVerts += md.VertCount;
                    totalIndices += faceIndexBlocks[f].Count;
                }
            }


            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var data = meshDataArray[0];

            var layout = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
            layout[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            layout[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);
            layout[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
            data.SetVertexBufferParams(totalVerts, layout);
            layout.Dispose();
            data.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);

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
                float3 position = new float3(x, y, z);

                for (int f = 0; f < 6; f++)
                {
                    int3 n = FaceNormals[f];

                    // Like before, we skip if this face has neighbors
                    if (Occupied(x + n.x, y + n.y, z + n.z, chunk)) continue;

                    // vBase: where this face's vertices start
                    int vBase = vCursor;
                    // copy full vert buffer once per emitted face. Wasteful, I know, but easy to implement
                    for (int v = 0; v < md.VertCount; v++)
                    {
                        dstVerts[vCursor++] = new Vertex
                        {
                            Position = (float3)md.Positions[v] + position,
                            Normal = md.Normals[v],
                            UV = md.UVs[v]
                        };
                    }

                    var block = faceIndexBlocks[f];
                    for (int i = 0; i < block.Count; i++)
                        dstIndices[iCursor++] = (uint)vBase + block[i];
                }
            }

            md.Positions.Dispose();
            md.Normals.Dispose();
            md.UVs.Dispose();

            data.subMeshCount = 1;
            data.SetSubMesh(0, new SubMeshDescriptor(0, totalIndices)
            {
                bounds = new Bounds(new Vector3(ChunkSize, ChunkSize, ChunkSize) * 0.5f,
                    new Vector3(ChunkSize, ChunkSize, ChunkSize)),
                vertexCount = totalVerts
            }, MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh { name = "ChunkMesh" };
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            mesh.RecalculateBounds();
            return mesh;
        }

        struct MeshDataResult
        {
            public NativeArray<Vector3> Positions;
            public int VertCount;
            public NativeArray<Vector3> Normals;
            public NativeArray<Vector2> UVs;
            public NativeArray<ushort> Indices;
            public int IndexCount;
        }

        MeshDataResult GetMeshData()
        {
            // Read data from source cube mesh
            var srcDataArray = Mesh.AcquireReadOnlyMeshData(cubeMesh);
            var srcData = srcDataArray[0];

            int srcVertCount = srcData.vertexCount;
            var srcPositions = new NativeArray<Vector3>(srcVertCount, Allocator.Temp);
            var srcNormals = new NativeArray<Vector3>(srcVertCount, Allocator.Temp);
            var srcUVs = new NativeArray<Vector2>(srcVertCount, Allocator.Temp);
            srcData.GetVertices(srcPositions);
            srcData.GetNormals(srcNormals);
            srcData.GetUVs(0, srcUVs);

            int srcIndexCount = (int)cubeMesh.GetIndexCount(0);
            NativeArray<ushort> srcIndices16 = new NativeArray<ushort>(srcIndexCount, Allocator.Temp);
            srcData.GetIndices(srcIndices16, 0);
            srcDataArray.Dispose();

            return new MeshDataResult
            {
                Positions = srcPositions,
                VertCount = srcVertCount,
                Normals = srcNormals,
                UVs = srcUVs,
                Indices = srcIndices16,
                IndexCount = srcIndexCount
            };
        }
    }
}
