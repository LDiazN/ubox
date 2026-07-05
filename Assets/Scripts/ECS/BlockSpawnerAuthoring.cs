using Unity.Entities;
using UnityEngine;
using Utils;

namespace ECS
{
    public class BlockSpawnerAuthoring : MonoBehaviour
    {

        #region Inspector Properties

        public GameObject blockPrefab;

        [Min(0)]
        [SerializeField] private int width = 50;
        [Min(0)]
        [SerializeField] private int height = 50;
        [Min(0)]
        [SerializeField] private int depth = 50;

        #endregion

        private void Reset()
        {
            // ECSGrass.prefab.meta
            blockPrefab = AssetLoader.LoadByGuid<GameObject>("ac55cd2f7fcdc174e8aeb9669be99b1e");
        }

        class BlockSpawnerEcsBaker : Baker<BlockSpawnerAuthoring>
        {
            public override void Bake(BlockSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BlockSpawner
                {
                    blockPrefab = GetEntity(authoring.blockPrefab, TransformUsageFlags.Renderable),
                    height = authoring.height,
                    width = authoring.width,
                    depth = authoring.depth,
                });
            }
        }
    }

    public struct BlockSpawner : IComponentData
    {
        public int width;
        public int height;
        public int depth;
        public Entity blockPrefab;
    }
}
