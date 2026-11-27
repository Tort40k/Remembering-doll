using System.Collections.Generic;
using UnityEngine;

public class FsmStEnemyMeleeChase : FsmState
{
    private readonly EnemyMelee _enemy;
    private readonly GridPathfinder _pathfinder;
    private readonly float _repathInterval;
    private readonly float _waypointReachRadius;

    private readonly float _flankRadius;
    private readonly float _directChaseDistance;

    private readonly List<Vector3> _path = new List<Vector3>();
    private int _waypointIndex;
    private float _stopTimer;
    private float _repathTimer;
    private Vector3 _lastGoalPos;
    private bool _hasSavedPath;

    private static readonly float[] FlankAngles = { 0f, -75f, 75f, 165f };

    public IReadOnlyList<Vector3> CurrentPath => _path;
    public int CurrentWaypointIndex => _waypointIndex;
    public Vector3 CurrentTacticalGoal { get; private set; }
    public bool HasTacticalGoal { get; private set; }

    public FsmStEnemyMeleeChase(
        Fsm fsm,
        EnemyMelee enemy,
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
        _directChaseDistance = Mathf.Max(_enemy.AttackRange + 0.5f, directChaseDistance);

        float radius = enemy.Controller != null ? Mathf.Max(0.4f, enemy.Controller.radius * 1.2f) : 0.45f;
        float height = enemy.Controller != null ? enemy.Controller.height : 1.5f;
        float stepOffset = enemy.Controller != null ? Mathf.Max(0.6f, enemy.Controller.stepOffset * 1.5f) : 0.75f;
        float slopeLimit = enemy.Controller != null ? enemy.Controller.slopeLimit : 45f;

        _pathfinder = new GridPathfinder(
            enemy.transform,
            enemy.Target,
            cellSize,
            halfExtent,
            radius,
            height,
            stepOffset,
            slopeLimit
        );
    }

    public override void Enter()
    {
        _stopTimer = 0f;
        _repathTimer = Random.Range(0f, _repathInterval);

        float dist = _enemy.GetFlatDistanceToTarget();
        Vector3 goal = GetTacticalGoal(_enemy.GetFlatTargetPosition(), dist);
        CurrentTacticalGoal = goal;
        HasTacticalGoal = dist > _directChaseDistance;

        if (!_hasSavedPath || _path.Count == 0 || _waypointIndex >= _path.Count)
        {
            _path.Clear();
            _waypointIndex = 0;
            _lastGoalPos = goal;
            RecalculatePath(forceUpdate: true);
        }
        else if ((goal - _lastGoalPos).sqrMagnitude > 0.5f)
        {
            RecalculatePath(forceUpdate: false);
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

        float flatDistance = _enemy.GetFlatDistanceToTarget();
        bool inAttackRange = flatDistance <= _enemy.AttackRange;

        if (inAttackRange)
        {
            _stopTimer += Time.deltaTime;
            if (_stopTimer >= _enemy.StopDelay)
            {
                Fsm.SetState<FsmStMeleeEnemyAttack>();
                return;
            }
        }
        else
        {
            _stopTimer = 0f;
        }

        Vector3 flatPlayerPos = _enemy.GetFlatTargetPosition();
        Vector3 tacticalGoal = GetTacticalGoal(flatPlayerPos, flatDistance);
        CurrentTacticalGoal = tacticalGoal;
        HasTacticalGoal = flatDistance > _directChaseDistance;

        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f || (tacticalGoal - _lastGoalPos).sqrMagnitude > 0.5f)
        {
            RecalculatePath(forceUpdate: false);
        }

        Vector3 moveDir = GetPathDirection(tacticalGoal);

        float speedFactor = 1f;
        if (inAttackRange && _enemy.StopDelay > 0.0001f)
        {
            speedFactor = Mathf.Clamp01(1f - (_stopTimer / _enemy.StopDelay));
        }

        float maxStep = _enemy.Speed * speedFactor * Time.deltaTime;
        float step = _path.Count == 0
            ? maxStep
            : Mathf.Min(maxStep, GetDistanceToCurrentTarget(tacticalGoal));

        Vector3 horizontalMove = moveDir * Mathf.Max(0f, step);
        _enemy.Controller.Move(horizontalMove + Vector3.up * (_enemy.VelocityY * Time.deltaTime));

        _enemy.SyncPathVisual(_path, _waypointIndex);
        DrawDebugPath(tacticalGoal);
    }

    private Vector3 GetTacticalGoal(Vector3 playerPos, float flatDistance)
    {
        if (flatDistance <= _directChaseDistance)
            return playerPos;

        int slotIndex = Mathf.Abs(_enemy.GetInstanceID()) % FlankAngles.Length;
        float angle = FlankAngles[slotIndex];

        Vector3 fromPlayer = _enemy.transform.position - playerPos;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude <= 0.001f)
            fromPlayer = Vector3.forward;

        Vector3 flankDir = Quaternion.AngleAxis(angle, Vector3.up) * fromPlayer.normalized;
        Vector3 flankPoint = playerPos + flankDir * _flankRadius;
        flankPoint.y = playerPos.y;
        return flankPoint;
    }

    private Vector3 GetPathDirection(Vector3 flatGoal)
    {
        Vector3 current = _enemy.transform.position;
        current.y = 0f;

        if (_path.Count > 0 && _waypointIndex < _path.Count)
        {
            while (_waypointIndex < _path.Count)
            {
                Vector3 wp = _path[_waypointIndex];
                wp.y = 0f;
                if (Vector3.Distance(current, wp) <= _waypointReachRadius)
                    _waypointIndex++;
                else
                    return (wp - current).normalized;
            }
        }

        Vector3 toGoal = flatGoal - current;
        toGoal.y = 0f;
        return toGoal.sqrMagnitude > 0.0001f ? toGoal.normalized : Vector3.zero;
    }

    private float GetDistanceToCurrentTarget(Vector3 flatGoal)
    {
        Vector3 current = _enemy.transform.position;
        current.y = 0f;

        if (_path.Count > 0 && _waypointIndex < _path.Count)
        {
            Vector3 wp = _path[_waypointIndex];
            wp.y = 0f;
            return Vector3.Distance(current, wp);
        }

        return Vector3.Distance(current, flatGoal);
    }

    private void RecalculatePath(bool forceUpdate)
    {
        _repathTimer = _repathInterval;
        float dist = _enemy.GetFlatDistanceToTarget();
        Vector3 goal = GetTacticalGoal(_enemy.GetFlatTargetPosition(), dist);
        CurrentTacticalGoal = goal;
        HasTacticalGoal = dist > _directChaseDistance;

        List<Vector3> newPath = _pathfinder.FindPath(_enemy.transform.position, goal);

        if (newPath == null || newPath.Count == 0)
        {
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

        Vector3 targetPt = new Vector3(goal.x, goal.y + 0.25f, goal.z);
        Debug.DrawLine(prev, targetPt, Color.yellow);
    }
}