using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class EnemyMelee : Enemy
{
    [Header("Melee Settings")]
    [SerializeField] private float _attack = 10f;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _stopDelay = 0.3f;

    [Header("A* Pathfinding")]
    [SerializeField] private float _cellSize = 0.6f;
    [SerializeField] private int _halfExtent = 25;
    [SerializeField] private float _repathInterval = 0.15f;
    [SerializeField] private float _waypointReachRadius = 0.35f;

    [Header("Tactical Flanking")]
    [SerializeField] private float _flankRadius = 3.0f;
    [SerializeField] private float _directChaseDistance = 4.5f;

    [Header("Path Visualization")]
    [SerializeField] private bool _drawPathInGameView = false;
    [SerializeField] private float _pathHeightOffset = 0.25f;
    [SerializeField] private float _waypointSphereRadius = 0.14f;

    private FsmStEnemyMeleeChase _chaseState;
    private LineRenderer _pathLineRenderer;

    public float AttackDamage => _attack;
    public float AttackRange => _attackRange;
    public float StopDelay => _stopDelay;

    [Inject]
    public override void Construct()
    {
        base.Construct();
        SetupLineRendererIfNeeded();

        _chaseState = new FsmStEnemyMeleeChase(
            _fsm,
            this,
            _cellSize,
            _halfExtent,
            _repathInterval,
            _waypointReachRadius,
            _flankRadius,
            _directChaseDistance
        );

        _fsm.AddState(_chaseState);
        _fsm.AddState(new FsmStMeleeEnemyAttack(_fsm, this));

        _fsm.SetState<FsmStEnemyMeleeChase>();
    }

    private void SetupLineRendererIfNeeded()
    {
        if (!_drawPathInGameView) return;

        _pathLineRenderer = GetComponent<LineRenderer>();
        if (_pathLineRenderer == null)
            _pathLineRenderer = gameObject.AddComponent<LineRenderer>();

        _pathLineRenderer.useWorldSpace = true;
        _pathLineRenderer.startWidth = 0.08f;
        _pathLineRenderer.endWidth = 0.08f;
        _pathLineRenderer.numCapVertices = 4;
    }

    public void SyncPathVisual(IReadOnlyList<Vector3> path, int waypointIndex)
    {
        if (_pathLineRenderer == null) return;

        if (!_drawPathInGameView || path == null || waypointIndex >= path.Count)
        {
            _pathLineRenderer.positionCount = 0;
            return;
        }

        int count = path.Count - waypointIndex + 1;
        _pathLineRenderer.positionCount = count;
        _pathLineRenderer.SetPosition(0, transform.position + Vector3.up * _pathHeightOffset);

        for (int i = waypointIndex; i < path.Count; i++)
        {
            Vector3 pt = path[i] + Vector3.up * _pathHeightOffset;
            _pathLineRenderer.SetPosition(i - waypointIndex + 1, pt);
        }
    }

    public void ClearPathVisual()
    {
        if (_pathLineRenderer != null)
            _pathLineRenderer.positionCount = 0;
    }

    public void MoveToFlatTarget()
    {
        if (_target == null || _controller == null) return;

        Vector3 flatTargetPosition = GetFlatTargetPosition();
        Vector3 nextHorizontalPosition = Vector3.MoveTowards(
            transform.position,
            flatTargetPosition,
            _speed * Time.deltaTime
        );

        Vector3 horizontalDisplacement = nextHorizontalPosition - transform.position;
        Vector3 totalDisplacement = horizontalDisplacement + Vector3.up * (VelocityY * Time.deltaTime);

        _controller.Move(totalDisplacement);
    }

    public void ApplyVerticalGravityOnly()
    {
        if (_controller == null) return;
        _controller.Move(Vector3.up * (VelocityY * Time.deltaTime));
    }

    public bool IsTargetInAttackRange()
    {
        if (_target == null) return false;
        return Vector3.Distance(transform.position, _target.position) <= _attackRange;
    }

    private void OnDrawGizmos()
    {
        DrawTacticalGoalGizmo();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = IsTargetInAttackRange() ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        DrawPathWaypointsGizmo();
    }

    private void DrawPathWaypointsGizmo()
    {
        if (_chaseState == null || _chaseState.CurrentPath == null) return;

        IReadOnlyList<Vector3> path = _chaseState.CurrentPath;
        int activeIdx = _chaseState.CurrentWaypointIndex;

        for (int i = activeIdx; i < path.Count; i++)
        {
            Vector3 pt = path[i] + Vector3.up * _pathHeightOffset;
            Gizmos.color = i == activeIdx ? Color.green : Color.cyan;
            Gizmos.DrawSphere(pt, _waypointSphereRadius);
        }
    }

    private void DrawTacticalGoalGizmo()
    {
        Vector3 goal = Vector3.zero;
        bool showGoal = false;

        if (_chaseState != null && _chaseState.HasTacticalGoal)
        {
            goal = _chaseState.CurrentTacticalGoal;
            showGoal = true;
        }
        else if (_target != null)
        {
            Vector3 playerPos = _target.position;
            Vector3 flatPlayer = new Vector3(playerPos.x, transform.position.y, playerPos.z);
            Vector3 flatPos = new Vector3(transform.position.x, transform.position.y, transform.position.z);

            float dist = Vector3.Distance(flatPos, flatPlayer);
            if (dist > _directChaseDistance)
            {
                float[] flankAngles = { 0f, -75f, 75f, 165f };
                int slotIndex = Mathf.Abs(GetInstanceID()) % flankAngles.Length;

                Vector3 fromPlayer = flatPos - flatPlayer;
                if (fromPlayer.sqrMagnitude <= 0.001f)
                    fromPlayer = Vector3.forward;

                Vector3 flankDir = Quaternion.AngleAxis(flankAngles[slotIndex], Vector3.up) * fromPlayer.normalized;
                goal = flatPlayer + flankDir * _flankRadius;
                goal.y = playerPos.y;
                showGoal = true;
            }
        }

        if (!showGoal) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(goal, 0.35f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.25f, goal + Vector3.up * 0.25f);
    }
}