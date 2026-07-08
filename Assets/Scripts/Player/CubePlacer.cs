using Managers;
using Settings;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Min(0)]
        [Tooltip("Cooldown time between cube placements")]
        [SerializeField] private float timeBetweenOperations = 0.1f;

        #endregion

        #region Internal State

        private Camera _camera;
        private RaycastHit[] _hitBuffer;
        private int _nHits;
        private RaycastHit _closest;
        private GameObject _highlightBlock;
        private float _timeSinceLastOpr;
        public BlockType CurrentBlockType { get; private set; }
        private InputBindings _input;
        private bool _shouldPlace;
        private bool _shouldRemove;

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
            CurrentBlockType = BlockType.Grass;
            _input = new InputBindings();
        }

        private void OnEnable()
        {
            _input.Player.Enable();
            _input.Player.PlaceBlock.started += OnPlaceBlock;
            _input.Player.PlaceBlock.canceled += OnPlaceBlock;
            _input.Player.RemoveBlock.started += OnRemoveBlock;
            _input.Player.RemoveBlock.canceled += OnRemoveBlock;
            _input.Player.Block1.performed += OnBlock1;
            _input.Player.Block2.performed += OnBlock2;
        }
        private void OnDisable()
        {
            _input.Player.PlaceBlock.started -= OnPlaceBlock;
            _input.Player.PlaceBlock.canceled -= OnPlaceBlock;
            _input.Player.RemoveBlock.started -= OnRemoveBlock;
            _input.Player.RemoveBlock.canceled -= OnRemoveBlock;
            _input.Player.Disable();
        }

        private void Update()
        {
            if (!_camera || GameManager.IsPaused)
                return;

            _timeSinceLastOpr += Time.deltaTime;

            // Update the closest block
            var ray = GetRay();
            _nHits = Physics.RaycastNonAlloc(ray, _hitBuffer, maxBlockPlaceDistance);
            if (_nHits == 0)
            {
                _highlightBlock.SetActive(false);
                return;
            }
            _closest = GetClosest(_hitBuffer, _nHits);

            // Keep in mind that we hit the chunk itself, not just the block. We have to derive the block position
            // from the Hit location
            PlaceHighlight(_closest);

            if (_timeSinceLastOpr < timeBetweenOperations)
                return;

            if (_shouldPlace)
            {
                PlaceBlock(_closest);
                _timeSinceLastOpr = 0;
            }

            if (_shouldRemove)
            {
                RemoveBlock(_closest);
                _timeSinceLastOpr = 0;
            }
        }

        private void OnDrawGizmos()
        {
            var ray = _camera ? GetRay() : new Ray { origin = transform.position, direction = transform.forward };
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(ray.origin, ray.origin + maxBlockPlaceDistance * ray.direction);
        }

        private void OnPlaceBlock(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                // Reboots the cooldown on mouse up
                _shouldPlace = false;
                _timeSinceLastOpr = timeBetweenOperations;
                return;
            }

            _shouldPlace = true;
        }

        private void OnRemoveBlock(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                // Reboots the cooldown on mouse up
                _shouldRemove = false;
                _timeSinceLastOpr = timeBetweenOperations;
                return;
            }

            _shouldRemove = true;
        }

        private void OnBlock1(InputAction.CallbackContext obj)
        {
            var old = CurrentBlockType;
            if (Input.GetKeyDown(KeyCode.Alpha1))
                CurrentBlockType = BlockType.Grass;

            if (old != CurrentBlockType)
                EventsChannel.ChangePlayerBlock(CurrentBlockType);
        }

        private void OnBlock2(InputAction.CallbackContext obj)
        {
            var old = CurrentBlockType;
            if (Input.GetKeyDown(KeyCode.Alpha2))
                CurrentBlockType = BlockType.Dirt;

            if (old != CurrentBlockType)
                EventsChannel.ChangePlayerBlock(CurrentBlockType);
        }


        private void PlaceHighlight(in RaycastHit hit)
        {
            var position = hit.point;
            var toHit = hit.point - _camera.transform.position;
            position += 0.1f * toHit.normalized;
            _highlightBlock.transform.position = math.floor((float3)position);
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
            inside = math.floor((float3)inside);
            inside += 0.5f * Vector3.one;
            var nextPosition = inside + hit.normal;

            worldManager.SetBlock(new int3(math.floor((float3)nextPosition)), CurrentBlockType); // TODO support other block types
        }

        private void RemoveBlock(in RaycastHit hit)
        {
            var worldManager = WorldManagerV2.Instance;
            if (!worldManager)
                return;

            var inside = hit.point;
            var toHit = hit.point - _camera.transform.position;
            inside += 0.1f * toHit.normalized;
            worldManager.SetBlock(new int3(math.floor((float3)inside)), BlockType.Empty);
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
