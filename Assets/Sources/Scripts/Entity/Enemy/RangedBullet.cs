using UnityEngine;

public class RangedBullet : MonoBehaviour
{
    [SerializeField] private float _speed = 24f;
    [SerializeField] private float _lifeTime = 4f;
    [SerializeField] private float _hitRadius = 0.15f;
    [SerializeField] private float _spawnForwardOffset = 0.25f;
    [SerializeField] private float _armTime = 0.08f;
    [SerializeField] private bool _destroyOnObstacleBeforeArm = true;
    [SerializeField] private float _maxFrameDelta = 0.05f;

    private int _damage;
    private Transform _ownerRoot;
    private Vector3 _moveDirection;
    private float _ageTimer;
    private bool _hasDealtDamage;

    public void Initialize(Vector3 targetPoint, int damage, Transform ownerRoot, float speed = -1f)
    {
        _damage = Mathf.Max(0, damage);
        _ownerRoot = ownerRoot;
        _ageTimer = 0f;
        _hasDealtDamage = false;

        if (speed > 0.01f)
            _speed = speed;

        Vector3 toTarget = targetPoint - transform.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            _moveDirection = toTarget.normalized;
            transform.position += _moveDirection * _spawnForwardOffset;
            transform.rotation = Quaternion.LookRotation(_moveDirection);
        }
        else
        {
            _moveDirection = transform.forward;
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, _maxFrameDelta);
        _ageTimer += dt;

        if (_ageTimer >= _lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        float step = _speed * dt;
        if (step <= 0f) return;

        bool armed = _ageTimer >= _armTime;

        if (CheckContactAlongStep(step, armed, out bool hitDamageable))
        {
            if (hitDamageable || armed || _destroyOnObstacleBeforeArm)
            {
                Destroy(gameObject);
                return;
            }
        }

        transform.position += _moveDirection * step;
    }

    private bool CheckContactAlongStep(float step, bool armed, out bool hitDamageable)
    {
        hitDamageable = false;

        // 1. Точный луч по траектории шага (хорошо ловит тонкие и наклонные коллайдеры)
        if (Physics.Raycast(
            transform.position,
            _moveDirection,
            out RaycastHit rayHit,
            step + _hitRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore))
        {
            if (!IsIgnoredOwner(rayHit.transform))
            {
                hitDamageable = TryApplyDamage(rayHit.transform);
                return hitDamageable || armed || _destroyOnObstacleBeforeArm;
            }
        }

        // 2. Сферический каст по шагу (ловит касание краем радиуса пули)
        if (Physics.SphereCast(
            transform.position,
            _hitRadius,
            _moveDirection,
            out RaycastHit sphereHit,
            step,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore))
        {
            if (!IsIgnoredOwner(sphereHit.transform))
            {
                hitDamageable = TryApplyDamage(sphereHit.transform);
                return hitDamageable || armed || _destroyOnObstacleBeforeArm;
            }
        }

        // 3. Проверка перекрытия в следующей точке (ловит старт/попадание внутри коллайдера)
        Vector3 nextPos = transform.position + _moveDirection * step;
        Collider[] overlaps = Physics.OverlapSphere(
            nextPos,
            _hitRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (IsIgnoredOwner(overlaps[i].transform))
                continue;

            hitDamageable = TryApplyDamage(overlaps[i].transform);
            if (hitDamageable || armed || _destroyOnObstacleBeforeArm)
                return true;
        }

        return false;
    }

    private bool IsIgnoredOwner(Transform hitTransform)
    {
        if (_ownerRoot == null || hitTransform == null) return false;
        return hitTransform == _ownerRoot || hitTransform.IsChildOf(_ownerRoot);
    }

    private bool TryApplyDamage(Transform hitTransform)
    {
        if (_hasDealtDamage || hitTransform == null) return false;

        IDamageable damageable = hitTransform.GetComponent<IDamageable>()
            ?? hitTransform.GetComponentInParent<IDamageable>()
            ?? hitTransform.GetComponentInChildren<IDamageable>();

        if (damageable != null)
        {
            if (_damage > 0)
                damageable.TakeDamage(_damage);

            _hasDealtDamage = true;
            return true;
        }

        return false;
    }
}