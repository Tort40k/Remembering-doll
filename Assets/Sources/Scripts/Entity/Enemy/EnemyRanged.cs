using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class EnemyRanged : Enemy
{
    public enum TacticalEchelonRole
    {
        Vanguard,
        LeftFlank,
        RightFlank,
        Rearguard
    }

    [Header("Ranged Aim & Raycast")]
    [SerializeField] private Transform _cannonTransform;
    [SerializeField] private Transform _rayOrigin;
    [SerializeField] private float _stopDelay = 0.25f;
    [SerializeField] private float _fireLockDelay = 0.6f;
    [SerializeField] private float _postFireCooldown = 0.8f;
    [SerializeField] private float _maxRaycastDistance = 20f;
    [SerializeField] private float _cannonRotateSpeed = 10f;

    [Header("Chase Reposition After Shots")]
    [SerializeField] [Range(1, 5)] private int _minShotsBeforeReposition = 1;
    [SerializeField] [Range(1, 5)] private int _maxShotsBeforeReposition = 5;
    [SerializeField] private float _repositionDuration = 1.6f;
    [SerializeField] private float _minRepositionDistance = 2.0f;
    [SerializeField] private float _maxRepositionDistance = 4.5f;
    [SerializeField] private float _repositionArriveRadius = 0.65f;

    [Header("FOV")]
    [SerializeField] [Range(10f, 180f)] private float _horizontalViewAngle = 90f;
    [SerializeField] [Range(5f, 120f)] private float _verticalViewAngle = 55f;
    [SerializeField] private float _viewDistance = 14f;
    [SerializeField] private float _visionHeightOffset = 1.1f;
    [SerializeField] [Range(6, 48)] private int _visionRayCount = 24;
    [SerializeField] private Color _visionColor = new Color(0.2f, 1f, 0.55f, 0.9f);

    [Header("Wall Limiter (Fan Rays)")]
    [SerializeField] private float _minWallDistance = 0.75f;
    [SerializeField] private float _wallRayHeightOffset = 0.75f;
    [SerializeField] [Range(3, 15)] private int _wallFanRayCount = 5;
    [SerializeField] [Range(15f, 150f)] private float _wallFanAngle = 70f;
    [SerializeField] private float _wallRepelStrength = 1.25f;
    [SerializeField] private Color _wallRayClearColor = Color.cyan;
    [SerializeField] private Color _wallRayHitColor = Color.magenta;

    [Header("Bullet & Damage")]
    [SerializeField] private RangedBullet _bulletPrefab;
    [SerializeField] private float _bulletSpeed = 24f;
    [SerializeField] private int _bulletDamage = 15;

    [Header("Ray Debug")]
    [SerializeField] private bool _drawRayInGameView = true;
    [SerializeField] private float _rayLineWidth = 0.05f;
    [SerializeField] private Color _aimAcquiredColor = Color.green;
    [SerializeField] private Color _aimBlockedColor = Color.red;

    [Header("A* Pathfinding")]
    [SerializeField] private float _cellSize = 0.6f;
    [SerializeField] private int _halfExtent = 25;
    [SerializeField] private float _repathInterval = 0.15f;
    [SerializeField] private float _waypointReachRadius = 0.35f;

    [Header("Echelon Flanking (Random)")]
    [SerializeField] private bool _randomizeRole = true;
    [SerializeField] private bool _rerollRoleOnChaseEnter = false;
    [SerializeField] private TacticalEchelonRole _echelonRole = TacticalEchelonRole.LeftFlank;
    [SerializeField] private float _vanguardDistance = 2.5f;
    [SerializeField] private float _flankDistance = 3.5f;
    [SerializeField] private float _rearguardDistance = 5.5f;
    [SerializeField] private float _radiusRandomJitter = 0.9f;
    [SerializeField] private float _angleRandomJitterDeg = 22f;
    [SerializeField] private float _directChaseDistance = 4.5f;

    [Header("Path Visualization")]
    [SerializeField] private bool _drawPathInGameView = false;
    [SerializeField] private float _pathHeightOffset = 0.25f;
    [SerializeField] private float _waypointSphereRadius = 0.14f;

    private FsmStEnemyRangedChase _chaseState;
    private FsmStRangedEnemyAim _aimState;
    private LineRenderer _pathLineRenderer;
    private LineRenderer _rayLineRenderer;
    private Material _lineMaterial;
    private Vector3 _lastWallRayDir = Vector3.forward;

    private TacticalEchelonRole _activeRole;
    private bool _hasRolledRole;
    private float _currentRadiusJitter;
    private float _currentAngleJitter;

    public Transform CannonTransform => _cannonTransform;
    public float ViewDistance => _viewDistance;
    public float StopDelay => _stopDelay;
    public float FireLockDelay => _fireLockDelay;
    public float PostFireCooldown => _postFireCooldown;
    public float MaxRaycastDistance => _maxRaycastDistance;
    public int MinShotsBeforeReposition => _minShotsBeforeReposition;
    public int MaxShotsBeforeReposition => _maxShotsBeforeReposition;
    public float RepositionDuration => _repositionDuration;
    public float MinRepositionDistance => _minRepositionDistance;
    public float MaxRepositionDistance => _maxRepositionDistance;
    public float RepositionArriveRadius => _repositionArriveRadius;
    public float FlankDistance => _flankDistance;
    public float DirectChaseDistance => _directChaseDistance;
    public float VanguardDistance => _vanguardDistance;
    public float MinWallDistance => _minWallDistance;
    public float WallRepelStrength => _wallRepelStrength;
    public bool HasAcquiredTarget { get; private set; }
    public Vector3 LastHitPoint { get; private set; }

    [Inject]
    public override void Construct()
    {
        base.Construct();
        SetupLineRenderers();
        RollEchelonParams(force: true);

        _chaseState = new FsmStEnemyRangedChase(
            _fsm,
            this,
            _cellSize,
            _halfExtent,
            _repathInterval,
            _waypointReachRadius,
            _flankDistance,
            _directChaseDistance
        );

        _aimState = new FsmStRangedEnemyAim(_fsm, this);

        _fsm.AddState(_chaseState);
        _fsm.AddState(_aimState);
        _fsm.SetState<FsmStEnemyRangedChase>();
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null) Destroy(_lineMaterial);
    }

    public void NotifyShotFired()
    {
        _chaseState?.RegisterShotFired();
    }

    public void RollEchelonParams(bool force = false)
    {
        if (!_hasRolledRole || force || (_randomizeRole && _rerollRoleOnChaseEnter))
        {
            _activeRole = _randomizeRole
                ? (TacticalEchelonRole)Random.Range(0, 4)
                : _echelonRole;
            _hasRolledRole = true;
        }

        float rJitter = Mathf.Max(0f, _radiusRandomJitter);
        float aJitter = Mathf.Max(0f, _angleRandomJitterDeg);
        _currentRadiusJitter = Random.Range(-rJitter, rJitter);
        _currentAngleJitter = Random.Range(-aJitter, aJitter);
    }

    public TacticalEchelonRole GetEffectiveEchelonRole()
    {
        if (!_hasRolledRole)
            RollEchelonParams(force: true);
        return _activeRole;
    }

    public float GetEchelonRadius(TacticalEchelonRole role)
    {
        float baseDist = role switch
        {
            TacticalEchelonRole.Vanguard => Mathf.Max(1.0f, _vanguardDistance),
            TacticalEchelonRole.LeftFlank => Mathf.Max(1.0f, _flankDistance),
            TacticalEchelonRole.RightFlank => Mathf.Max(1.0f, _flankDistance),
            TacticalEchelonRole.Rearguard => Mathf.Max(1.5f, _rearguardDistance),
            _ => Mathf.Max(1.0f, _flankDistance)
        };

        return Mathf.Max(1.0f, baseDist + _currentRadiusJitter);
    }

    public float GetEchelonAngleDeg(TacticalEchelonRole role)
    {
        float baseAngle = role switch
        {
            TacticalEchelonRole.Vanguard => 0f,
            TacticalEchelonRole.LeftFlank => -75f,
            TacticalEchelonRole.RightFlank => 75f,
            TacticalEchelonRole.Rearguard => 165f,
            _ => 0f
        };

        return baseAngle + _currentAngleJitter;
    }

    public bool IsTargetTransform(Transform hitTransform)
    {
        if (_target == null || hitTransform == null) return false;
        return hitTransform == _target || hitTransform.IsChildOf(_target);
    }

    private bool IsSelfOrTarget(Transform candidate)
    {
        if (candidate == null) return false;
        if (candidate == transform || candidate.IsChildOf(transform)) return true;
        return IsTargetTransform(candidate);
    }

    public bool TryCastWallFanRays(Vector3 flatMoveDir, float lookAhead, out Vector3 pushDir, out float closestHitDist)
    {
        pushDir = Vector3.zero;
        closestHitDist = float.MaxValue;

        Vector3 centerDir = new Vector3(flatMoveDir.x, 0f, flatMoveDir.z);
        if (centerDir.sqrMagnitude <= 0.0001f)
            centerDir = GetFallbackWallRayDir();
        else
            centerDir.Normalize();

        _lastWallRayDir = centerDir;

        Vector3 origin = transform.position + Vector3.up * _wallRayHeightOffset;
        float rayLen = Mathf.Max(_minWallDistance, _minWallDistance + Mathf.Max(0f, lookAhead));

        int count = Mathf.Max(1, _wallFanRayCount);
        bool anyHit = false;

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float yaw = Mathf.Lerp(-_wallFanAngle * 0.5f, _wallFanAngle * 0.5f, t);
            Vector3 rayDir = Quaternion.AngleAxis(yaw, Vector3.up) * centerDir;

            if (Physics.Raycast(origin, rayDir, out RaycastHit hit, rayLen, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                && !IsSelfOrTarget(hit.transform))
            {
                anyHit = true;
                if (hit.distance < closestHitDist)
                    closestHitDist = hit.distance;

                float proximity = Mathf.Clamp01(1f - (hit.distance / rayLen));
                Vector3 normalXZ = new Vector3(hit.normal.x, 0f, hit.normal.z);
                if (normalXZ.sqrMagnitude > 0.0001f)
                    pushDir += normalXZ.normalized * proximity;

                Vector3 awayFromHit = origin - hit.point;
                awayFromHit.y = 0f;
                if (awayFromHit.sqrMagnitude > 0.0001f)
                    pushDir += awayFromHit.normalized * (proximity * 0.5f);
            }
        }

        if (anyHit && pushDir.sqrMagnitude > 0.0001f)
            pushDir.Normalize();

        return anyHit;
    }

    private Vector3 GetFallbackWallRayDir()
    {
        if (Application.isPlaying && _lastWallRayDir.sqrMagnitude > 0.0001f)
            return _lastWallRayDir.normalized;

        Vector3 fwd = transform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }

    public bool IsTargetInsideFOV()
    {
        if (_target == null) return false;

        Vector3 origin = transform.position + Vector3.up * _visionHeightOffset;
        Vector3 toTarget = GetAimPointOnTarget() - origin;
        float dist = toTarget.magnitude;
        if (dist <= 0.001f || dist > _viewDistance) return false;

        Vector3 dir = toTarget / dist;
        Vector3 flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatFwd.sqrMagnitude <= 0.0001f || flatDir.sqrMagnitude <= 0.0001f) return false;

        if (Vector3.Angle(flatFwd.normalized, flatDir.normalized) > _horizontalViewAngle * 0.5f)
            return false;

        float vAngle = Mathf.Abs(Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg);
        return vAngle <= _verticalViewAngle * 0.5f;
    }

    public bool CanSeeTargetInFOV()
    {
        if (!IsTargetInsideFOV()) return false;

        Vector3 origin = GetRayOriginPosition();
        Vector3 toAim = GetAimPointOnTarget() - origin;
        float dist = toAim.magnitude;
        if (dist <= 0.001f) return true;

        if (Physics.Raycast(origin, toAim / dist, out RaycastHit hit, dist, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            return IsTargetTransform(hit.transform);

        return true;
    }

    public bool HasClearLineOfSightToPoint(Vector3 worldPoint, out Vector3 blockedPoint)
    {
        blockedPoint = worldPoint;
        Vector3 origin = GetRayOriginPosition();
        Vector3 toPoint = worldPoint - origin;
        float dist = toPoint.magnitude;
        if (dist <= 0.001f) return true;

        if (Physics.Raycast(origin, toPoint / dist, out RaycastHit hit, dist, Physics.AllLayers, QueryTriggerInteraction.Ignore)
            && !IsTargetTransform(hit.transform))
        {
            blockedPoint = hit.point;
            return false;
        }

        return true;
    }

    public void SetTargetAcquired(bool acquired, Vector3 hitPoint)
    {
        HasAcquiredTarget = acquired;
        LastHitPoint = hitPoint;
    }

    public void UpdateRayVisual(Vector3 origin, Vector3 endPoint, bool acquired)
    {
        HasAcquiredTarget = acquired;
        LastHitPoint = endPoint;

        if (_rayLineRenderer == null || !_drawRayInGameView)
        {
            if (_rayLineRenderer != null) _rayLineRenderer.positionCount = 0;
            return;
        }

        Color color = acquired ? _aimAcquiredColor : _aimBlockedColor;
        _rayLineRenderer.startColor = color;
        _rayLineRenderer.endColor = color;
        _rayLineRenderer.startWidth = _rayLineWidth;
        _rayLineRenderer.endWidth = _rayLineWidth;
        _rayLineRenderer.positionCount = 2;
        _rayLineRenderer.SetPosition(0, origin);
        _rayLineRenderer.SetPosition(1, endPoint);
    }

    public void ClearRayVisual()
    {
        if (_rayLineRenderer != null) _rayLineRenderer.positionCount = 0;
    }

    public Vector3 GetRayOriginPosition()
    {
        if (_rayOrigin != null) return _rayOrigin.position;
        if (_cannonTransform != null) return _cannonTransform.position;
        return transform.position + Vector3.up * 1.2f;
    }

    public Vector3 GetAimPointOnTarget()
    {
        if (_target == null)
            return GetRayOriginPosition() + (_cannonTransform != null ? _cannonTransform.forward : transform.forward) * _maxRaycastDistance;

        return _target.position + Vector3.up * 1.0f;
    }

    public Vector3 GetRayDirectionToTarget()
    {
        Vector3 dir = GetAimPointOnTarget() - GetRayOriginPosition();
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
    }

    public void RotateCannonTowardsPoint(Vector3 worldPoint)
    {
        if (_cannonTransform == null) return;
        Vector3 dir = worldPoint - _cannonTransform.position;
        if (dir.sqrMagnitude <= 0.0001f) return;

        _cannonTransform.rotation = Quaternion.Slerp(
            _cannonTransform.rotation,
            Quaternion.LookRotation(dir.normalized),
            _cannonRotateSpeed * Time.deltaTime
        );
    }

    public void RotateCannonTowardsTarget()
    {
        if (_target != null) RotateCannonTowardsPoint(GetAimPointOnTarget());
    }

    public bool CastTargetAcquisitionRay(out RaycastHit hitInfo)
    {
        hitInfo = default;
        if (_target == null)
        {
            SetTargetAcquired(false, Vector3.zero);
            ClearRayVisual();
            return false;
        }

        Vector3 origin = GetRayOriginPosition();
        Vector3 direction = GetRayDirectionToTarget();

        if (!IsTargetInsideFOV())
        {
            SetTargetAcquired(false, Vector3.zero);
            UpdateRayVisual(origin, origin + direction * _maxRaycastDistance, false);
            return false;
        }

        bool hitSomething = Physics.Raycast(origin, direction, out hitInfo, _maxRaycastDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        bool hitPlayer = hitSomething && IsTargetTransform(hitInfo.transform);
        Vector3 endPoint = hitSomething ? hitInfo.point : origin + direction * _maxRaycastDistance;

        UpdateRayVisual(origin, endPoint, hitPlayer);
        return hitPlayer;
    }

    public void SpawnBulletTowards(Vector3 lockedTargetPoint)
    {
        if (_bulletPrefab == null || !CanSeeTargetInFOV()) return;
        if (!HasClearLineOfSightToPoint(lockedTargetPoint, out _)) return;

        RangedBullet bullet = Instantiate(_bulletPrefab, GetRayOriginPosition(), Quaternion.identity);
        bullet.Initialize(lockedTargetPoint, _bulletDamage, transform, _bulletSpeed);
        NotifyShotFired();
    }

    public void ApplyGravityOnly()
    {
        if (_controller != null)
            _controller.Move(Vector3.up * (VelocityY * Time.deltaTime));
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
            _pathLineRenderer.SetPosition(i - waypointIndex + 1, path[i] + Vector3.up * _pathHeightOffset);
    }

    public void ClearPathVisual()
    {
        if (_pathLineRenderer != null) _pathLineRenderer.positionCount = 0;
    }

    private void SetupLineRenderers()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) _lineMaterial = new Material(shader);

        if (_drawPathInGameView)
        {
            GameObject pathObj = new GameObject("RangedPathLineRenderer");
            pathObj.transform.SetParent(transform, false);
            _pathLineRenderer = pathObj.AddComponent<LineRenderer>();
            _pathLineRenderer.useWorldSpace = true;
            _pathLineRenderer.startWidth = 0.08f;
            _pathLineRenderer.endWidth = 0.08f;
            if (_lineMaterial != null) _pathLineRenderer.material = _lineMaterial;
            _pathLineRenderer.positionCount = 0;
        }

        if (_drawRayInGameView)
        {
            GameObject rayObj = new GameObject("RangedRayLineRenderer");
            rayObj.transform.SetParent(transform, false);
            _rayLineRenderer = rayObj.AddComponent<LineRenderer>();
            _rayLineRenderer.useWorldSpace = true;
            _rayLineRenderer.startWidth = _rayLineWidth;
            _rayLineRenderer.endWidth = _rayLineWidth;
            if (_lineMaterial != null) _rayLineRenderer.material = _lineMaterial;
            _rayLineRenderer.positionCount = 0;
        }
    }

    private void DrawFovGizmo()
    {
        Vector3 origin = transform.position + Vector3.up * _visionHeightOffset;
        Vector3 flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatFwd.sqrMagnitude <= 0.0001f) flatFwd = Vector3.forward;
        flatFwd.Normalize();

        float halfH = _horizontalViewAngle * 0.5f;
        float halfV = _verticalViewAngle * 0.5f;

        Vector3 leftDir = Quaternion.AngleAxis(-halfH, Vector3.up) * flatFwd;
        Vector3 rightDir = Quaternion.AngleAxis(halfH, Vector3.up) * flatFwd;
        Vector3 upDir = Quaternion.AngleAxis(-halfV, transform.right) * flatFwd;
        Vector3 downDir = Quaternion.AngleAxis(halfV, transform.right) * flatFwd;

        Gizmos.color = _visionColor;
        Gizmos.DrawLine(origin, origin + leftDir * _viewDistance);
        Gizmos.DrawLine(origin, origin + rightDir * _viewDistance);
        Gizmos.DrawLine(origin, origin + upDir * _viewDistance);
        Gizmos.DrawLine(origin, origin + downDir * _viewDistance);

        Vector3 prev = origin + leftDir * _viewDistance;
        for (int i = 1; i <= _visionRayCount; i++)
        {
            float angle = Mathf.Lerp(-halfH, halfH, (float)i / _visionRayCount);
            Vector3 next = origin + (Quaternion.AngleAxis(angle, Vector3.up) * flatFwd) * _viewDistance;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    private void DrawWallFanGizmo()
    {
        Vector3 origin = transform.position + Vector3.up * _wallRayHeightOffset;
        Vector3 centerDir = GetFallbackWallRayDir();
        float rayLen = Mathf.Max(_minWallDistance, _minWallDistance + 0.4f);
        int count = Mathf.Max(1, _wallFanRayCount);

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float yaw = Mathf.Lerp(-_wallFanAngle * 0.5f, _wallFanAngle * 0.5f, t);
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * centerDir;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, rayLen, Physics.AllLayers, QueryTriggerInteraction.Ignore)
                && !IsSelfOrTarget(hit.transform))
            {
                Gizmos.color = _wallRayHitColor;
                Gizmos.DrawLine(origin, hit.point);
                Gizmos.DrawWireSphere(hit.point, 0.06f);
            }
            else
            {
                Gizmos.color = _wallRayClearColor;
                Gizmos.DrawLine(origin, origin + dir * rayLen);
            }
        }
    }

    private void DrawEchelonGoalGizmo()
    {
        if (_target == null) return;

        TacticalEchelonRole role = GetEffectiveEchelonRole();
        float radius = GetEchelonRadius(role);
        float angle = GetEchelonAngleDeg(role);

        Vector3 playerPos = _target.position;
        Vector3 flatPlayer = new Vector3(playerPos.x, transform.position.y, playerPos.z);
        Vector3 flatPos = new Vector3(transform.position.x, transform.position.y, transform.position.z);

        Vector3 fromPlayer = flatPos - flatPlayer;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude <= 0.001f) fromPlayer = Vector3.forward;

        Vector3 goal = flatPlayer + Quaternion.AngleAxis(angle, Vector3.up) * fromPlayer.normalized * radius;
        goal.y = playerPos.y;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(goal, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.25f, goal + Vector3.up * 0.25f);
    }

    private void OnDrawGizmos()
    {
        DrawFovGizmo();
        DrawWallFanGizmo();
        DrawEchelonGoalGizmo();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        DrawFovGizmo();
        DrawWallFanGizmo();
        DrawEchelonGoalGizmo();

        if (_chaseState?.CurrentPath == null) return;
        IReadOnlyList<Vector3> path = _chaseState.CurrentPath;
        for (int i = _chaseState.CurrentWaypointIndex; i < path.Count; i++)
        {
            Gizmos.color = i == _chaseState.CurrentWaypointIndex ? Color.green : Color.cyan;
            Gizmos.DrawSphere(path[i] + Vector3.up * _pathHeightOffset, _waypointSphereRadius);
        }
    }
}