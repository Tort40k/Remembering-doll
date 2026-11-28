using UnityEngine;

public class FsmStRangedEnemyAim : FsmState
{
    private readonly EnemyRanged _enemy;
    private readonly float _raycastInterval;
    private readonly float _lostSightGraceTime;

    private float _rayTimer;
    private float _lockTimer;
    private float _postFireTimer;
    private float _lostSightTimer;
    private bool _hasLockedPoint;
    private Vector3 _lockedTargetPoint;

    public FsmStRangedEnemyAim(
        Fsm fsm,
        EnemyRanged enemy,
        float raycastInterval = 0.08f,
        float lostSightGraceTime = 0.25f
    ) : base(fsm)
    {
        _enemy = enemy;
        _raycastInterval = raycastInterval;
        _lostSightGraceTime = lostSightGraceTime;
    }

    public override void Enter()
    {
        ResetAimCycle();
        _enemy.CastTargetAcquisitionRay(out _);
    }

    public override void Update()
    {
        _enemy.ApplyGravityOnly();

        if (_enemy.Target == null)
            return;

        bool canSee = _enemy.CanSeeTargetInFOV();
        if (!canSee)
        {
            _hasLockedPoint = false;
            _lockTimer = 0f;
            _enemy.SetTargetAcquired(false, Vector3.zero);
            _lostSightTimer += Time.deltaTime;
            if (_lostSightTimer >= _lostSightGraceTime)
            {
                Fsm.SetState<FsmStEnemyRangedChase>();
                return;
            }
        }
        else
        {
            _lostSightTimer = 0f;
        }

        if (_hasLockedPoint)
        {
            _enemy.RotateCannonTowardsPoint(_lockedTargetPoint);
            bool clearLine = _enemy.HasClearLineOfSightToPoint(_lockedTargetPoint, out Vector3 blockedPoint);
            bool stillClear = canSee && clearLine;

            _enemy.UpdateRayVisual(
                _enemy.GetRayOriginPosition(),
                stillClear ? _lockedTargetPoint : blockedPoint,
                stillClear
            );

            _postFireTimer -= Time.deltaTime;
            if (_postFireTimer <= 0f)
                ResetAimCycle();

            return;
        }

        _enemy.RotateCannonTowardsTarget();

        _rayTimer -= Time.deltaTime;
        if (_rayTimer <= 0f)
        {
            _rayTimer = _raycastInterval;
            _enemy.CastTargetAcquisitionRay(out _);
        }

        if (canSee && _enemy.HasAcquiredTarget)
        {
            _lockTimer += Time.deltaTime;
            if (_lockTimer >= _enemy.FireLockDelay)
            {
                Vector3 candidatePoint = _enemy.GetAimPointOnTarget();
                if (_enemy.HasClearLineOfSightToPoint(candidatePoint, out Vector3 blockedPoint))
                {
                    _lockedTargetPoint = candidatePoint;
                    _hasLockedPoint = true;
                    _postFireTimer = Mathf.Max(0.05f, _enemy.PostFireCooldown);

                    _enemy.RotateCannonTowardsPoint(_lockedTargetPoint);
                    _enemy.UpdateRayVisual(_enemy.GetRayOriginPosition(), _lockedTargetPoint, true);
                    _enemy.SpawnBulletTowards(_lockedTargetPoint);
                }
                else
                {
                    _lockTimer = 0f;
                    _enemy.UpdateRayVisual(_enemy.GetRayOriginPosition(), blockedPoint, false);
                }
            }
        }
        else
        {
            _lockTimer = 0f;
        }
    }

    public override void Exit()
    {
        ResetAimCycle();
        _enemy.ClearRayVisual();
    }

    private void ResetAimCycle()
    {
        _rayTimer = 0f;
        _lockTimer = 0f;
        _postFireTimer = 0f;
        _lostSightTimer = 0f;
        _hasLockedPoint = false;
        _lockedTargetPoint = Vector3.zero;
        _enemy.SetTargetAcquired(false, Vector3.zero);
    }
}