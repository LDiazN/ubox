using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECS
{
    public partial class BlockSpawnerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<BlockSpawner>();
        }

        protected override void OnUpdate()
        {
            // Only spawn once
            // Enabled = false;
            //
            // var sp = SystemAPI.GetSingleton<BlockSpawner>();
            // var buffer = new EntityCommandBuffer(Allocator.Temp); // Use this buffer to run many commands in batch
            // for (int x = 0; x < sp.width; x++)
            // {
            //     for (int y = 0; y < sp.depth; y++)
            //     {
            //         for (int z = 0; z < sp.height; z++)
            //         {
            //             var newEntity = buffer.Instantiate(sp.blockPrefab);
            //             buffer.SetComponent(newEntity, new LocalTransform
            //             {
            //                 Position = new float3(x, -y, z),
            //                 Scale = 1,
            //                 Rotation = quaternion.identity
            //             });
            //         }
            //     }
            // }
            //
            // buffer.Playback(EntityManager);
        }
    }
}
