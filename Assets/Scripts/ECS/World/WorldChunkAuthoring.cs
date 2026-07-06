using Unity.Entities;
using UnityEngine;
using Unity.Rendering;
using UnityEngine.Rendering;
using Utils;
using Unity.Collections;
using Unity.VisualScripting;

namespace ECS.World
{
    public class WorldChunkAuthoring : MonoBehaviour
    {
        #region Inspector Properties

        [Min(1)]
        [SerializeField] private int height;
        [Min(1)]
        [SerializeField] private int width;
        [Min(1)]
        [SerializeField] private int depth;
        [SerializeField] private Mesh cubeMesh;

        #endregion

        private void Reset()
        {
            cubeMesh = AssetLoader.LoadByGuid<Mesh>("e342d1903b0f84348a42f482f6fe3c32");
        }

        private class WorldChunkBaker : Baker<WorldChunkAuthoring>
        {
            public override void Bake(WorldChunkAuthoring authoring)
            {
                WorldChunkSystem.VertexCount = authoring.cubeMesh.vertexCount;
                WorldChunkSystem.IndexCount = authoring.cubeMesh.GetIndexCount(0);

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WorldChunk
                {
                    Dirty = true,
                });
            }
        }
    }

    public struct WorldChunk : IComponentData
    {
        // If the chunk needs to be rebuilt
        public bool Dirty;
    }

    public partial class WorldChunkSystem : SystemBase
    {
        // Size of vertex buffer
        public static int VertexCount;
        // Size of index buffer
        public static uint IndexCount;

        private static int Height = 16;
        private static int Width = 16;
        private static int Depth = 16;

        private Mesh.MeshDataArray meshDataArray;


        protected override void OnCreate()
        {
            // RequireForUpdate<WorldChunk>();
            // var config = SystemAPI.ManagedAPI.GetSingleton<WorldChunkConfig>();
            // meshDataArray = Mesh.AcquireReadOnlyMeshData(config.Mesh);
        }

        protected override void OnUpdate()
        {
            // foreach (var chunk in SystemAPI.Query<RefRW<WorldChunk>>())
            // {
            //     if (chunk.ValueRW.Dirty)
            //         continue;
            //
            //     RebuildChunk(meshDataArray[0]);
            // }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

        }

        private void RebuildChunk(Mesh.MeshData meshData)
        {
            var dataArray = Mesh.AllocateWritableMeshData(1);
            var data = dataArray[0];
            int totalCubes = Height * Width * Depth;
            // TODO copy the attributes from the source cube mesh
            for (var i = 0; i <= (int)VertexAttribute.BlendIndices; i++)
            {
                VertexAttributeDescriptor d = new()
                {
                    attribute = VertexAttribute.Position,
                    dimension = meshData.GetVertexAttributeDimension(VertexAttribute.Position)
                };
            }
        }
    }
}
