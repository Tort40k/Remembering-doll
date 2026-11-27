using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

public class Character : MonoBehaviour, IControllable
{
    [SerializeField] private CharacterController _controller;
    [SerializeField] private Transform _headTransform;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _jumpHeight = 2f;
    [SerializeField] private float _groundRadiusChecker = 0.2f;
    [SerializeField] private LayerMask _groundMask;
    [SerializeField] private Transform _groundChecker;

    [Header("Mouse & Body Rotation Settings")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _minPitch = -60f;
    [SerializeField] private float _maxPitch = 60f;
    [SerializeField] private float _bodyRotationSmoothTime = 0.15f;

    private bool isGround;
    private float _velocityY;
    private Vector3 _moveDirection;

    private float _currentYaw;
    private float _currentPitch;
    private float _bodyYaw;
    private float _bodyYawVelocity;

    [Inject]
    public void Construct()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        Debug.Log(this + " is active");
    }
    

    private void FixedUpdate()
    {
        if (_controller == null) return;

        HandleRotationAndBody();

        isGround = IsOnTheGround();
        if (isGround && _velocityY < 0)
        {
            _velocityY = -2f;
        }

        DoGravity();
        MovementLogic();
    }

    public void Move(Vector3 direction)
    {
        _moveDirection = direction;
    }

    private void HandleRotationAndBody()
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        _currentYaw += mouseDelta.x * _mouseSensitivity * 0.1f;
        _currentPitch -= mouseDelta.y * _mouseSensitivity * 0.1f;

        _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);

        _bodyYaw = Mathf.SmoothDampAngle(_bodyYaw, _currentYaw, ref _bodyYawVelocity, _bodyRotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, _bodyYaw, 0f);

        if (_headTransform != null)
        {
            float localHeadYaw = _currentYaw - _bodyYaw;
            _headTransform.localRotation = Quaternion.Euler(_currentPitch, localHeadYaw, 0f);
        }
    }

    private void MovementLogic()
    {
        if (_headTransform == null) return;

        Vector3 forward = _headTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = _headTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 worldMoveDirection = (forward * _moveDirection.z) + (right * _moveDirection.x);

        Vector3 finalMove = (worldMoveDirection * _speed) + (Vector3.up * _velocityY);
        _controller.Move(finalMove * Time.fixedDeltaTime);
    }

    public void Jump()
    {
        if (!isGround) return;
        _velocityY = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
    }

    private bool IsOnTheGround()
    {
        if (_groundChecker == null) return false;
        return Physics.CheckSphere(_groundChecker.position, _groundRadiusChecker, _groundMask);
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundChecker == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_groundChecker.position, _groundRadiusChecker);
    }

    private void DoGravity()
    {
        _velocityY += _gravity * Time.fixedDeltaTime;
    }
}