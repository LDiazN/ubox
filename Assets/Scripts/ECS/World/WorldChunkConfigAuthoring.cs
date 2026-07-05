using Unity.Entities;
using UnityEngine;
using Utils;

namespace ECS.World
{
    public class WorldChunkConfigAuthoring : MonoBehaviour
    {
        #region Inspector Properties

        [SerializeField] private Mesh cubeMesh;

        #endregion

        private void Reset()
        {
            cubeMesh = AssetLoader.LoadByGuid<Mesh>("e342d1903b0f84348a42f482f6fe3c32");

        }

        private class WorldChunkConfigBaker : Baker<WorldChunkConfigAuthoring>
        {
            public override void Bake(WorldChunkConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, new WorldChunkConfig
                {
                    Mesh = authoring.cubeMesh
                });
            }
        }
    }

    public class WorldChunkConfig : IComponentData
    {
        public Mesh Mesh;
    }
}
