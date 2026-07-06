using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonMovement : MonoBehaviour
{
    #region Inspector Properties

    [Min(0)]
    [SerializeField] private float movementSpeed = 5;
    [Min(0)]
    [SerializeField] private float cameraSpeed = 5;
    [Min(0)]
    [SerializeField] private float maxCameraRotation = 20;
    [Min(0)]
    [SerializeField] private float gravity = 9;
    [Min(0)]
    [SerializeField] private float jumpHeight = 1.1f;

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

    #endregion

    private void Start()
    {
        _camera = Camera.main;
        _controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        _input.x = Input.GetAxis("Horizontal");
        _input.y = Input.GetAxis("Vertical");
        _mouse.x = Input.GetAxis("Mouse X");
        _mouse.y = Input.GetAxis("Mouse Y");
        _cameraRotationX -= transform.eulerAngles.x;

        if (Input.GetButtonDown("Jump"))
        {
            _jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        transform.Rotate(new Vector3(0, _mouse.x, 0) * (Time.fixedDeltaTime * cameraSpeed));

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
        {
            _verticalVelocity -= gravity * Time.fixedDeltaTime;
        }
        _jumpRequested = false;

        var movement = (transform.right * _input.x + transform.forward * _input.y) * movementSpeed;
        movement.y = _verticalVelocity;

        _controller.Move(movement * Time.fixedDeltaTime);
        // Camera rotation
        _cameraRotationX = Mathf.Clamp(_cameraRotationX - _mouse.y * Time.fixedDeltaTime * cameraSpeed, -maxCameraRotation, maxCameraRotation);
        if (!_camera)
            return;

        var oldRotation = _camera.transform.rotation.eulerAngles;
        _camera.transform.rotation = Quaternion.Euler(new Vector3(_cameraRotationX, oldRotation.y, oldRotation.z));
    }
}
