using UnityEngine;

public class FsmStMeleeEnemyWindup : FsmState
{
    private readonly EnemyMelee _enemy;
    private readonly float _windupDuration;
    private readonly float _dashDistance;
    private readonly float _dashSpeed;

    private float _timer;
    private bool _isDashing;
    private bool _hasDealtDashDamage;
    private Vector3 _dashStartPos;
    private Vector3 _dashTargetPos;
    private Vector3 _dashDir;

    public bool HasDashZone { get; private set; }
    public bool IsDashing => _isDashing;
    public float WindupProgress => _windupDuration > 0.0001f ? Mathf.Clamp01(_timer / _windupDuration) : 1f;
    public Vector3 DashStartPosition => _dashStartPos;
    public Vector3 DashTargetPosition => _dashTargetPos;
    public float DashRadius => _enemy != null ? _enemy.DashZoneRadius : 0.6f;

    public FsmStMeleeEnemyWindup(
        Fsm fsm,
        EnemyMelee enemy,
        float windupDuration = 5f,
        float dashDistance = 2.5f,
        float dashSpeed = 12f) : base(fsm)
    {
        _enemy = enemy;
        _windupDuration = Mathf.Max(0.1f, windupDuration);
        _dashDistance = Mathf.Max(0.2f, dashDistance);
        _dashSpeed = Mathf.Max(1f, dashSpeed);
    }

    public override void Enter()
    {
        _timer = 0f;
        _isDashing = false;
        _hasDealtDashDamage = false;
        _dashStartPos = _enemy.transform.position;

        Vector3 dir = _enemy.transform.forward;
        if (_enemy.Target != null)
        {
            Vector3 toTarget = _enemy.Target.position - _enemy.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                dir = toTarget.normalized;
        }
        else
        {
            dir.y = 0f;
            if (dir.sqrMagnitude <= 0.0001f) dir = Vector3.forward;
            dir.Normalize();
        }

        _dashDir = dir;
        _dashTargetPos = _dashStartPos + _dashDir * _dashDistance;
        HasDashZone = true;

        _enemy.SyncDashVisual(_dashStartPos, _dashTargetPos, isDashing: false, progress: 0f);
    }

    public override void Exit()
    {
        _timer = 0f;
        _isDashing = false;
        _hasDealtDashDamage = false;
        HasDashZone = false;
        _enemy.ClearDashVisual();
    }

    public override void Update()
    {
        if (_enemy.Target == null || _enemy.Controller == null) return;

        if (!_isDashing)
        {
            _enemy.ApplyVerticalGravityOnly();

            _timer += Time.deltaTime;
            _enemy.SyncDashVisual(_dashStartPos, _dashTargetPos, isDashing: false, progress: WindupProgress);

            if (_timer >= _windupDuration)
            {
                _isDashing = true;
                _hasDealtDashDamage = false;
                _dashStartPos = _enemy.transform.position;
                _dashTargetPos = _dashStartPos + _dashDir * _dashDistance;
                _enemy.SyncDashVisual(_dashStartPos, _dashTargetPos, isDashing: true, progress: 1f);
            }
            return;
        }

        Vector3 currentFlat = _enemy.transform.position;
        Vector3 targetFlat = _dashTargetPos;
        currentFlat.y = 0f;
        targetFlat.y = 0f;

        Vector3 toTarget = targetFlat - currentFlat;
        float remaining = toTarget.magnitude;

        if (remaining <= 0.05f || (_enemy.Controller.collisionFlags & CollisionFlags.Sides) != 0)
        {
            TryApplyDashDamageOnContact(_enemy.Controller.collisionFlags);
            _enemy.ApplyVerticalGravityOnly();
            Fsm.SetState<FsmStEnemyMeleeChase>();
            return;
        }

        float step = Mathf.Min(_dashSpeed * Time.deltaTime, remaining);
        Vector3 horizontalMove = (toTarget / remaining) * step;
        CollisionFlags flags = _enemy.Controller.Move(horizontalMove + Vector3.up * (_enemy.VelocityY * Time.deltaTime));
        _enemy.SyncDashVisual(_enemy.transform.position, _dashTargetPos, isDashing: true, progress: 1f);

        TryApplyDashDamageOnContact(flags);
    }

    private void TryApplyDashDamageOnContact(CollisionFlags moveFlags = CollisionFlags.None)
    {
        if (_hasDealtDashDamage || _enemy.Target == null) return;

        bool touchedPlayer = IsTouchingTarget(moveFlags);
        if (!touchedPlayer) return;

        IDamageable damageable = _enemy.Target.GetComponent<IDamageable>()
            ?? _enemy.Target.GetComponentInParent<IDamageable>()
            ?? _enemy.Target.GetComponentInChildren<IDamageable>();

        if (damageable != null)
        {
            int damage = Mathf.RoundToInt(_enemy.AttackDamage);
            damageable.TakeDamage(damage);
            _hasDealtDashDamage = true;
        }
    }

    private bool IsTouchingTarget(CollisionFlags moveFlags)
    {
        if (_enemy.Target == null) return false;

        float verticalDiff = Mathf.Abs(_enemy.transform.position.y - _enemy.Target.position.y);
        float maxVerticalReach = _enemy.Controller != null
            ? Mathf.Max(0.75f, _enemy.Controller.height * 0.6f)
            : 0.9f;
        if (verticalDiff > maxVerticalReach)
            return false;

        float selfRadius = _enemy.Controller != null ? _enemy.Controller.radius : 0.4f;
        CharacterController targetController = _enemy.Target.GetComponent<CharacterController>()
            ?? _enemy.Target.GetComponentInParent<CharacterController>();
        float targetRadius = targetController != null ? targetController.radius : 0.4f;

        Vector3 a = _enemy.transform.position;
        Vector3 b = _enemy.Target.position;
        a.y = 0f;
        b.y = 0f;

        float touchDistance = selfRadius + targetRadius + 0.1f;
        float distSqr = (b - a).sqrMagnitude;

        bool sideHitWhileClose = (moveFlags & CollisionFlags.Sides) != 0 && distSqr <= (touchDistance + 0.35f) * (touchDistance + 0.35f);
        bool directTouch = distSqr <= touchDistance * touchDistance;

        return directTouch || sideHitWhileClose;
    }
}