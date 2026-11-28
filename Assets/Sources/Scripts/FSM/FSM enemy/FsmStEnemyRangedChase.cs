using System.Collections.Generic;
using UnityEngine;

public class FsmStEnemyRangedChase : FsmState
{
    private readonly EnemyRanged _enemy;
    private readonly GridPathfinder _pathfinder;
    private readonly float _repathInterval;
    private readonly float _waypointReachRadius;
    private readonly float _flankRadius;
    private readonly float _directChaseDistance;

    private readonly List<Vector3> _path = new List<Vector3>(32);
    private int _waypointIndex;
    private float _stopTimer;
    private float _repathTimer;
    private Vector3 _lastGoalPos;
    private bool _hasSavedPath;

    private int _shotsSinceReposition;
    private int _targetShotsBeforeReposition;
    private bool _isRepositioning;
    private float _repositionTimer;
    private Vector3 _repositionGoal;

    public IReadOnlyList<Vector3> CurrentPath => _path;
    public int CurrentWaypointIndex => _waypointIndex;
    public Vector3 CurrentTacticalGoal { get; private set; }
    public bool HasTacticalGoal { get; private set; }
    public bool IsRepositioning => _isRepositioning;

    public FsmStEnemyRangedChase(
        Fsm fsm,
        EnemyRanged enemy,
        float cellSize = 0.6f,
        int halfExtent = 25,
        float repathInterval = 0.15f,
        float waypointReachRadius = 0.35f,
        float flankRadius = 3.0f,
        float directChaseDistance = 4.5f) : base(fsm)
    {
        _enemy = enemy;
        _repathInterval = Mathf.Max(0.05f, repathInterval);
        _waypointReachRadius = Mathf.Max(0.1f, waypointReachRadius);
        _flankRadius = Mathf.Max(1.0f, flankRadius);
        _directChaseDistance = Mathf.Max(_enemy.ViewDistance + 0.5f, directChaseDistance);

        float radius = enemy.Controller != null ? Mathf.Max(0.4f, enemy.Controller.radius * 1.2f) : 0.45f;
        float height = enemy.Controller != null ? enemy.Controller.height : 1.5f;
        float baseStepOffset = enemy.Controller != null ? Mathf.Max(0.6f, enemy.Controller.stepOffset * 1.5f) : 0.75f;
        float gridStep = WorldManager.Instance != null ? WorldManager.Instance.PointDistance * 1.25f : 1.25f;
        float effectiveStepHeight = Mathf.Max(baseStepOffset, gridStep);
        float slopeLimit = enemy.Controller != null ? enemy.Controller.slopeLimit : 45f;

        _pathfinder = new GridPathfinder(
            enemy.transform,
            enemy.Target,
            cellSize,
            halfExtent,
            radius,
            height,
            effectiveStepHeight,
            slopeLimit
        );

        RollNextShotThreshold();
    }

    public void RegisterShotFired()
    {
        if (_isRepositioning) return;

        if (_targetShotsBeforeReposition <= 0)
            RollNextShotThreshold();

        _shotsSinceReposition++;
        if (_shotsSinceReposition >= _targetShotsBeforeReposition)
            BeginReposition();
    }

    private void RollNextShotThreshold()
    {
        int minShots = Mathf.Clamp(_enemy.MinShotsBeforeReposition, 1, 5);
        int maxShots = Mathf.Clamp(_enemy.MaxShotsBeforeReposition, minShots, 5);
        _targetShotsBeforeReposition = Random.Range(minShots, maxShots + 1);
        _shotsSinceReposition = 0;
    }

    private void BeginReposition()
    {
        _isRepositioning = true;
        _repositionTimer = Mathf.Max(0.25f, _enemy.RepositionDuration);
        _stopTimer = 0f;

        Vector3 originPos = _enemy.transform.position;
        float minDist = Mathf.Max(0.5f, _enemy.MinRepositionDistance);
        float maxDist = Mathf.Max(minDist, _enemy.MaxRepositionDistance);
        float dist = Random.Range(minDist, maxDist);
        float angleDeg = Random.Range(-155f, 155f);

        Vector3 baseDir = _enemy.Target != null
            ? (_enemy.transform.position - _enemy.GetFlatTargetPosition())
            : _enemy.transform.forward;
        baseDir.y = 0f;
        if (baseDir.sqrMagnitude <= 0.001f)
            baseDir = Vector3.forward;
        baseDir.Normalize();

        Vector3 offset = Quaternion.AngleAxis(angleDeg, Vector3.up) * baseDir * dist;
        _repositionGoal = new Vector3(originPos.x + offset.x, originPos.y, originPos.z + offset.z);

        CurrentTacticalGoal = _repositionGoal;
        HasTacticalGoal = true;
        _repathTimer = 0f;
        RecalculatePath(forceUpdate: true, _repositionGoal, isEchelonPositioned: true);
    }

    private void FinishReposition()
    {
        _isRepositioning = false;
        _repositionTimer = 0f;
        RollNextShotThreshold();
        _repathTimer = 0f;
    }

    public override void Enter()
    {
        _stopTimer = 0f;
        _repathTimer = Random.Range(0f, _repathInterval);
        _enemy.RollEchelonParams();
        _enemy.ClearRayVisual();
        _enemy.SetTargetAcquired(false, Vector3.zero);

        if (_targetShotsBeforeReposition <= 0)
            RollNextShotThreshold();

        if (_enemy.Target == null) return;

        if (_isRepositioning)
        {
            CurrentTacticalGoal = _repositionGoal;
            HasTacticalGoal = true;
            RecalculatePath(forceUpdate: true, _repositionGoal, isEchelonPositioned: true);
            return;
        }

        Vector3 goal = ComputeEchelonGoal(out bool isEchelonPositioned);
        CurrentTacticalGoal = goal;
        HasTacticalGoal = isEchelonPositioned;

        if (!_hasSavedPath || _path.Count == 0 || _waypointIndex >= _path.Count)
        {
            _path.Clear();
            _waypointIndex = 0;
            _lastGoalPos = goal;
            RecalculatePath(forceUpdate: true, goal, isEchelonPositioned);
        }
        else if ((goal - _lastGoalPos).sqrMagnitude > 0.5f)
        {
            RecalculatePath(forceUpdate: false, goal, isEchelonPositioned);
        }
    }

    public override void Exit()
    {
        _stopTimer = 0f;
        HasTacticalGoal = false;
        _enemy.ClearPathVisual();
    }

    public override void Update()
    {
        if (_enemy.Target == null || _enemy.Controller == null) return;

        bool canSeeTarget = false;

        if (_isRepositioning)
        {
            _repositionTimer -= Time.deltaTime;
            float flatDistToRepositionGoal = Vector3.Distance(
                new Vector3(_enemy.transform.position.x, 0f, _enemy.transform.position.z),
                new Vector3(_repositionGoal.x, 0f, _repositionGoal.z)
            );

            if (_repositionTimer <= 0f || flatDistToRepositionGoal <= _enemy.RepositionArriveRadius)
                FinishReposition();
        }

        if (!_isRepositioning)
        {
            canSeeTarget = _enemy.CanSeeTargetInFOV();

            if (canSeeTarget)
            {
                _stopTimer += Time.deltaTime;
                if (_stopTimer >= _enemy.StopDelay)
                {
                    Fsm.SetState<FsmStRangedEnemyAim>();
                    return;
                }
            }
            else
            {
                _stopTimer = 0f;
            }
        }

        Vector3 tacticalGoal;
        bool isEchelonPositioned;

        if (_isRepositioning)
        {
            tacticalGoal = _repositionGoal;
            isEchelonPositioned = true;
        }
        else
        {
            tacticalGoal = ComputeEchelonGoal(out isEchelonPositioned);
        }

        CurrentTacticalGoal = tacticalGoal;
        HasTacticalGoal = isEchelonPositioned;

        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f || (!_isRepositioning && (tacticalGoal - _lastGoalPos).sqrMagnitude > 0.5f))
            RecalculatePath(forceUpdate: _isRepositioning, tacticalGoal, isEchelonPositioned);

        Vector3 moveDir = GetPathDirection(tacticalGoal);

        float speedFactor = (!_isRepositioning && canSeeTarget && _enemy.StopDelay > 0.0001f)
            ? Mathf.Clamp01(1f - (_stopTimer / _enemy.StopDelay))
            : 1f;

        float maxStep = _enemy.Speed * speedFactor * Time.deltaTime;
        float step = _path.Count == 0 ? maxStep : Mathf.Min(maxStep, GetDistanceToCurrentTarget(tacticalGoal));

        if (moveDir.sqrMagnitude > 0.0001f && _enemy.TryCastWallFanRays(moveDir, maxStep, out Vector3 pushDir, out float closestHitDist))
        {
            if (pushDir.sqrMagnitude > 0.0001f)
            {
                Vector3 blendedDir = moveDir + pushDir * _enemy.WallRepelStrength;
                blendedDir.y = 0f;
                moveDir = blendedDir.sqrMagnitude > 0.0001f ? blendedDir.normalized : pushDir;
            }

            step = Mathf.Min(step, Mathf.Max(0f, closestHitDist - _enemy.MinWallDistance));
        }

        _enemy.Controller.Move(moveDir * Mathf.Max(0f, step) + Vector3.up * (_enemy.VelocityY * Time.deltaTime));
        _enemy.SyncPathVisual(_path, _waypointIndex);
        DrawDebugPath(tacticalGoal);
    }

    private Vector3 ComputeEchelonGoal(out bool isEchelonPositioned)
    {
        float flatDistance = _enemy.GetFlatDistanceToTarget();
        Vector3 playerPos = _enemy.GetFlatTargetPosition();

        if (flatDistance <= Mathf.Min(_directChaseDistance, _enemy.VanguardDistance))
        {
            isEchelonPositioned = false;
            return playerPos;
        }

        EnemyRanged.TacticalEchelonRole role = _enemy.GetEffectiveEchelonRole();
        float radius = role == EnemyRanged.TacticalEchelonRole.LeftFlank || role == EnemyRanged.TacticalEchelonRole.RightFlank
            ? Mathf.Max(_enemy.GetEchelonRadius(role), _flankRadius)
            : _enemy.GetEchelonRadius(role);
        float angle = _enemy.GetEchelonAngleDeg(role);

        isEchelonPositioned = true;
        Vector3 fromPlayer = _enemy.transform.position - playerPos;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude <= 0.001f)
            fromPlayer = Vector3.forward;

        Vector3 echelonPoint = playerPos + Quaternion.AngleAxis(angle, Vector3.up) * fromPlayer.normalized * radius;
        echelonPoint.y = playerPos.y;
        return echelonPoint;
    }

    private Vector3 GetPathDirection(Vector3 flatGoal)
    {
        Vector3 pos = _enemy.transform.position;
        Vector3 current = new Vector3(pos.x, 0f, pos.z);

        while (_path.Count > 0 && _waypointIndex < _path.Count)
        {
            Vector3 rawWp = _path[_waypointIndex];
            Vector3 wp = new Vector3(rawWp.x, 0f, rawWp.z);
            float dy = Mathf.Abs(pos.y - rawWp.y);

            if (Vector3.Distance(current, wp) <= _waypointReachRadius && dy <= _waypointReachRadius * 2f)
                _waypointIndex++;
            else
                return (wp - current).normalized;
        }

        Vector3 toGoal = flatGoal - current;
        toGoal.y = 0f;
        return toGoal.sqrMagnitude > 0.0001f ? toGoal.normalized : Vector3.zero;
    }

    private float GetDistanceToCurrentTarget(Vector3 flatGoal)
    {
        Vector3 current = new Vector3(_enemy.transform.position.x, 0f, _enemy.transform.position.z);
        if (_path.Count > 0 && _waypointIndex < _path.Count)
        {
            Vector3 wp = new Vector3(_path[_waypointIndex].x, 0f, _path[_waypointIndex].z);
            return Vector3.Distance(current, wp);
        }

        return Vector3.Distance(current, flatGoal);
    }

    private void RecalculatePath(bool forceUpdate, Vector3 goal, bool isEchelonPositioned)
    {
        _repathTimer = _repathInterval;
        CurrentTacticalGoal = goal;
        HasTacticalGoal = isEchelonPositioned;

        List<Vector3> newPath = _pathfinder.FindPath(_enemy.transform.position, goal);
        if (newPath == null || newPath.Count == 0)
        {
            if (isEchelonPositioned && !_isRepositioning)
            {
                Vector3 directPlayerGoal = _enemy.GetFlatTargetPosition();
                List<Vector3> fallbackPath = _pathfinder.FindPath(_enemy.transform.position, directPlayerGoal);
                if (fallbackPath != null && fallbackPath.Count > 0)
                {
                    CurrentTacticalGoal = directPlayerGoal;
                    HasTacticalGoal = false;
                    _lastGoalPos = directPlayerGoal;
                    _path.Clear();
                    _path.AddRange(fallbackPath);
                    _waypointIndex = 0;
                    _hasSavedPath = true;
                    _enemy.SyncPathVisual(_path, _waypointIndex);
                    return;
                }
            }

            if (_path.Count > 0 && _waypointIndex < _path.Count)
                _hasSavedPath = true;
            return;
        }

        if (!forceUpdate && _hasSavedPath && _waypointIndex < _path.Count && (goal - _lastGoalPos).sqrMagnitude <= 0.5f)
            return;

        _lastGoalPos = goal;
        _path.Clear();
        _path.AddRange(newPath);
        _waypointIndex = 0;
        _hasSavedPath = true;
        _enemy.SyncPathVisual(_path, _waypointIndex);
    }

    private void DrawDebugPath(Vector3 goal)
    {
        Vector3 prev = _enemy.transform.position + Vector3.up * 0.25f;
        for (int i = _waypointIndex; i < _path.Count; i++)
        {
            Vector3 next = new Vector3(_path[i].x, _path[i].y + 0.25f, _path[i].z);
            Debug.DrawLine(prev, next, i == _waypointIndex ? Color.green : Color.cyan);
            prev = next;
        }

        Debug.DrawLine(prev, new Vector3(goal.x, goal.y + 0.25f, goal.z), _isRepositioning ? Color.magenta : Color.yellow);
    }
}