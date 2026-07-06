using Unity.Mathematics;
using UnityEngine;
using Utils;
using World;
using WorldManagers;

namespace Player
{
    public class CubePlacer : MonoBehaviour
    {
        #region Inspector Properties

        [Tooltip("How far away can the player place blocks")]
        [Min(0)]
        [SerializeField] private float maxBlockPlaceDistance = 2;

        [Tooltip("A block without collider used for highlighting other blocks")]
        [SerializeField] private GameObject highlightBlockPrefab;


        #endregion

        #region Internal State

        private Camera _camera;
        private RaycastHit[] _hitBuffer;
        private GameObject _highlightBlock;

        #endregion

        private void Reset()
        {
            // HighlightCube.prefab.meta
            highlightBlockPrefab = AssetLoader.LoadByGuid<GameObject>("8e0fdf07c73cbca41adad1363551c4d8");
        }

        private void Awake()
        {
            _camera = GetComponentInChildren<Camera>();
            _hitBuffer = new RaycastHit[10];
            _highlightBlock = Instantiate(highlightBlockPrefab, transform.position, quaternion.identity);
            _highlightBlock.SetActive(false);
        }

        private void Update()
        {
            if (!_camera)
                return;

            var ray = GetRay();

            var nHits = Physics.RaycastNonAlloc(ray, _hitBuffer, maxBlockPlaceDistance);
            if (nHits == 0)
            {
                _highlightBlock.SetActive(false);
                return;
            }

            var closestHit = GetClosest(_hitBuffer, nHits);

            // Keep in mind that we hit the chunk itself, not just the block. We have to derive the block position
            // from the Hit location
            PlaceHighlight(closestHit);

            // Now manage addition or deletion of blocks

            if (Input.GetMouseButtonDown(0))
                PlaceBlock(closestHit);
            if (Input.GetMouseButtonDown(1))
                RemoveBlock(closestHit);
        }

        private void OnDrawGizmos()
        {
            var ray = _camera ? GetRay() : new Ray { origin = transform.position, direction = transform.forward };
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(ray.origin, ray.origin + maxBlockPlaceDistance * ray.direction);
        }

        private void PlaceHighlight(in RaycastHit hit)
        {
            var position = hit.point;
            var toHit = hit.point - _camera.transform.position;
            position += 0.1f * toHit.normalized;
            _highlightBlock.transform.position = new float3(new int3(position));
            _highlightBlock.SetActive(true);
        }

        private void PlaceBlock(in RaycastHit hit)
        {
            var worldManager = WorldManagerV2.Instance;
            if (!worldManager)
                return;

            var inside = hit.point;
            var toHit = hit.point - _camera.transform.position;
            inside += 0.1f * toHit.normalized;
            inside = new float3(new int3(inside));
            inside += 0.5f * Vector3.one;
            var nextPosition = inside + hit.normal;

            worldManager.SetBlock(new int3(nextPosition), BlockType.Grass); // TODO support other block types
        }

        private void RemoveBlock(in RaycastHit hit)
        {
            var worldManager = WorldManagerV2.Instance;
            if (!worldManager)
                return;

            var inside = hit.point;
            var toHit = hit.point - _camera.transform.position;
            inside += 0.1f * toHit.normalized;
            worldManager.SetBlock(new int3(inside), BlockType.Empty);
        }

        private RaycastHit GetClosest(RaycastHit[] buffer, int nElements)
        {
            var closest = buffer[0];
            for (var i = 1; i < nElements; i++)
            {
                if (buffer[i].distance < closest.distance)
                    closest = buffer[i];
            }
            return closest;
        }

        private Ray GetRay() => new Ray
        {
            origin = _camera.transform.position,
            direction = _camera.transform.forward
        };
    }
}
