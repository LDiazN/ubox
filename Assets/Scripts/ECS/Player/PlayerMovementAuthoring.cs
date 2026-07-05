using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class PlayerMovementAuthoring : MonoBehaviour
{
    #region Inspector Properties

    [Min(0)]
    [SerializeField] private float movementSpeed = 5;

    #endregion
    private class PlayerMovementBaker : Baker<PlayerMovementAuthoring>
    {
        public override void Bake(PlayerMovementAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerMovement
            {
                MovementSpeed = authoring.movementSpeed,
                Gravity = authoring.movementSpeed
            });
        }
    }
}

public struct PlayerMovement : IComponentData
{
    public float MovementSpeed;
    public float Gravity;
}

public partial class PlayerMovementSystem : SystemBase
{
    private float2 _input;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<PlayerMovement>();
    }

    protected override void OnUpdate()
    {
        _input.x = Input.GetAxis("Horizontal");
        _input.y = Input.GetAxis("Vertical");

        // TODO finish ECS version of character controller. Needs to implement a new one for the camera as well
        foreach (var (playerMovement, transform) in SystemAPI.Query<RefRO<PlayerMovement>, RefRW<LocalTransform>>())
        {

            var movement = (
                               transform.ValueRW.Right() * _input.x +
                               transform.ValueRW.Forward() * _input.y
                           ) *
                           (SystemAPI.Time.fixedDeltaTime * playerMovement.ValueRO.MovementSpeed);

            var gravity = playerMovement.ValueRO.Gravity * SystemAPI.Time.fixedDeltaTime * new float3(0, -1, 0);

            transform.ValueRW.Position += movement + gravity;
        }

    }
}
