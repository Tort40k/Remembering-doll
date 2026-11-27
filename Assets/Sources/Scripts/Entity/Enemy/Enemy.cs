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

    [Header("Contact Damage Box")]
    [SerializeField] protected bool _dealContactDamage = true;
    [SerializeField] protected int _contactDamage = 10;
    [SerializeField] protected float _contactDamageCooldown = 0.75f;
    [SerializeField] protected Vector3 _contactBoxSize = new Vector3(1.2f, 1.8f, 1.2f);
    [SerializeField] protected Vector3 _contactBoxOffset = new Vector3(0f, 0.9f, 0.25f);

    protected Fsm _fsm;
    private bool _isGrounded;
    private float _velocityY;
    private float _contactCooldownTimer;

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

        if (_contactCooldownTimer > 0f)
            _contactCooldownTimer -= Time.deltaTime;

        UpdateGravity();
        _fsm?.Update();
        RotationLogic();

        if (_dealContactDamage)
            TryDealContactDamage();
    }

    protected virtual void TryDealContactDamage()
    {
        if (_contactCooldownTimer > 0f || _contactDamage <= 0 || _target == null)
            return;

        if (!IsTargetInsideContactBox())
            return;

        IDamageable damageable = _target.GetComponent<IDamageable>()
            ?? _target.GetComponentInParent<IDamageable>()
            ?? _target.GetComponentInChildren<IDamageable>();

        if (damageable == null)
            return;

        damageable.TakeDamage(_contactDamage);
        _contactCooldownTimer = Mathf.Max(0.05f, _contactDamageCooldown);
    }

    protected virtual bool IsTargetInsideContactBox()
    {
        Vector3 boxCenter = transform.TransformPoint(_contactBoxOffset);
        Quaternion boxRot = transform.rotation;
        Vector3 halfExtents = GetSafeBoxHalfExtents(_contactBoxSize);

        return IsPointInsideOrientedBox(_target.position, boxCenter, boxRot, halfExtents)
            || IsPointInsideOrientedBox(_target.position + Vector3.up * 0.5f, boxCenter, boxRot, halfExtents);
    }

    protected static Vector3 GetSafeBoxHalfExtents(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(0.05f, size.x * 0.5f),
            Mathf.Max(0.05f, size.y * 0.5f),
            Mathf.Max(0.05f, size.z * 0.5f)
        );
    }

    protected static bool IsPointInsideOrientedBox(Vector3 point, Vector3 center, Quaternion rotation, Vector3 halfExtents)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (point - center);
        return Mathf.Abs(local.x) <= halfExtents.x
            && Mathf.Abs(local.y) <= halfExtents.y
            && Mathf.Abs(local.z) <= halfExtents.z;
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

        if (_dealContactDamage)
        {
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.TransformPoint(_contactBoxOffset),
                transform.rotation,
                Vector3.one
            );
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.85f);
            Gizmos.DrawWireCube(Vector3.zero, _contactBoxSize);
            Gizmos.matrix = prevMatrix;
        }
    }
}