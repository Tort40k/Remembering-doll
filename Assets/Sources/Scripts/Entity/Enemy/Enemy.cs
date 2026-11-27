using UnityEngine;
using VContainer;

[RequireComponent(typeof(CharacterController))]
public abstract class Enemy : MonoBehaviour
{
    [Header("Base References")]
    [SerializeField] protected CharacterController _controller;
    [SerializeField] protected Transform _target;
    [SerializeField] protected Transform _groundChecker;
    [SerializeField] protected LayerMask _groundMask;

    [Header("Base Movement & Physics")]
    [SerializeField] protected float _speed = 3f;
    [SerializeField] protected float _gravity = -9.81f;
    [SerializeField] protected float _rotationSpeed = 720f;
    [SerializeField] protected float _groundRadiusChecker = 0.2f;

    protected Fsm _fsm;
    private bool _isGrounded;
    private float _velocityY;

    public CharacterController Controller => _controller;
    public Transform Target => _target;
    public float Speed => _speed;
    public float VelocityY => _velocityY;

    [Inject]
    public virtual void Construct()
    {
        _fsm = new Fsm();
        Debug.Log($"{name} is active");
    }

    protected virtual void Update()
    {
        if (_target == null || _controller == null) return;

        UpdateGravity();
        _fsm?.Update();
        RotationLogic();
    }

    private void UpdateGravity()
    {
        _isGrounded = _controller.isGrounded || IsOnTheGround();

        if (_isGrounded && _velocityY < 0f)
        {
            _velocityY = -2f;
        }

        _velocityY += _gravity * Time.deltaTime;
    }

    protected virtual void RotationLogic()
    {
        Vector3 direction = _target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _rotationSpeed * Time.deltaTime
        );
    }

    private bool IsOnTheGround()
    {
        if (_groundChecker == null) return false;
        return Physics.CheckSphere(_groundChecker.position, _groundRadiusChecker, _groundMask);
    }

    public float GetFlatDistanceToTarget()
    {
        if (_target == null) return float.MaxValue;
        return Vector3.Distance(transform.position, GetFlatTargetPosition());
    }

    public Vector3 GetFlatTargetPosition()
    {
        return new Vector3(_target.position.x, transform.position.y, _target.position.z);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (_groundChecker != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_groundChecker.position, _groundRadiusChecker);
        }
    }
}