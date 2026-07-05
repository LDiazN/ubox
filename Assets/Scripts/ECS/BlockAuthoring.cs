using Unity.Entities;
using UnityEngine;

namespace ECS
{
    public class BlockAuthoring : MonoBehaviour
    {

        private class BlockBaker : Baker<BlockAuthoring>
        {
            public override void Bake(BlockAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Block());
            }
        }
    }

    public struct Block : IComponentData
    {

    }
}
