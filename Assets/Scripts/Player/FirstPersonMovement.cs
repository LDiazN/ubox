using UnityEngine;
using UnityEngine.InputSystem;
using Settings;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonMovement : MonoBehaviour
{
    #region Inspector Properties

    [Min(0)] [SerializeField] private float movementSpeed = 5;
    [Min(0)] public float cameraSpeed = 5;
    [Min(0)] [SerializeField] private float maxCameraRotation = 20;
    [Min(0)] [SerializeField] private float gravity = 9;
    [Min(0)] [SerializeField] private float jumpHeight = 1.1f;

    #endregion

    #region Internal State

    // right, forward
    private Vector2 _input;
    private CharacterController _controller;
    private Camera _camera;
    private Vector2 _mouse;
    private float _cameraRotationX;
    private float _verticalVelocity;
    private bool _jumpRequested;
    private InputBindings _bindings;

    #endregion

    private void Awake()
    {
        _bindings = new InputBindings();
    }

    private void OnEnable()
    {
        _bindings.Player.Enable();
        _bindings.Player.Jump.started += OnJump;
    }

    private void OnDisable()
    {
        _bindings.Player.Jump.started -= OnJump;
        _bindings.Player.Disable();
    }

    private void Start()
    {
        _camera = Camera.main;
        _controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // We need this every frame anyways so ReadValue is better than an event handler
        _input = _bindings.Player.Move.ReadValue<Vector2>();
        _mouse = _bindings.Player.Look.ReadValue<Vector2>();
        _mouse *= GameManager.IsPaused ? 0 : 1;

        // Camera and body look rotation
        transform.Rotate(new Vector3(0, _mouse.x, 0) * cameraSpeed);
        _cameraRotationX = Mathf.Clamp(
            _cameraRotationX - _mouse.y * cameraSpeed,
            -maxCameraRotation,
            maxCameraRotation
            );
        if (_camera)
        {
            _camera.transform.localRotation = Quaternion.Euler(_cameraRotationX, 0, 0);
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        _jumpRequested = true;
    }

    private void FixedUpdate()
    {
        // Jump and Gravity
        if (_controller.isGrounded)
        {
            _verticalVelocity = -0.5f;
            if (_jumpRequested)
            {
                _verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
        }
        else
            _verticalVelocity -= gravity * Time.fixedDeltaTime;

        _jumpRequested = false;

        var movement = (transform.right * _input.x + transform.forward * _input.y) * movementSpeed;
        movement.y = _verticalVelocity;

        _controller.Move(movement * Time.fixedDeltaTime);
    }
}
