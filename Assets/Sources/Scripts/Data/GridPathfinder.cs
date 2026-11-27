using System.Collections.Generic;
using UnityEngine;

public class GridPathfinder
{
    private struct Node
    {
        public int X;
        public int Z;
        public int GCost;
        public int HCost;
        public int FCost => GCost + HCost;
        public Vector2Int Parent;
        public bool Walkable;
        public float GroundY;
    }

    private readonly Transform _owner;
    private readonly Transform _target;
    private readonly float _cellSize;
    private readonly int _halfExtent;
    private readonly float _agentRadius;
    private readonly float _clearanceHeight;
    private readonly float _maxStepHeight;
    private readonly float _maxSlopeAngle;
    private readonly LayerMask _mask;

    private readonly Collider[] _overlapBuffer = new Collider[8];

    public GridPathfinder(
        Transform owner,
        Transform target,
        float cellSize = 1.0f,
        int halfExtent = 15,
        float agentRadius = 0.4f,
        float clearanceHeight = 1.6f,
        float maxStepHeight = 0.75f,
        float maxSlopeAngle = 45f,
        int mask = Physics.DefaultRaycastLayers)
    {
        _owner = owner;
        _target = target;
        _cellSize = Mathf.Max(0.35f, cellSize);
        _halfExtent = Mathf.Max(4, halfExtent);
        _agentRadius = Mathf.Max(0.1f, agentRadius);
        _clearanceHeight = Mathf.Max(0.4f, clearanceHeight);
        _maxStepHeight = Mathf.Max(0.1f, maxStepHeight);
        _maxSlopeAngle = Mathf.Max(1f, maxSlopeAngle);
        _mask = mask;
    }

    public List<Vector3> FindPath(Vector3 startWorld, Vector3 goalWorld)
    {
        int size = _halfExtent * 2 + 1;
        Vector3 gridCenter = startWorld;
        float refY = Mathf.Max(startWorld.y, goalWorld.y);

        Node[,] grid = new Node[size, size];
        bool[,] closed = new bool[size, size];

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                Vector3 cellXZ = GridToWorld(gridCenter, x, z, refY);
                bool walkable = EvaluateCell(cellXZ, refY, out float groundY);

                grid[x, z] = new Node
                {
                    X = x,
                    Z = z,
                    GCost = int.MaxValue,
                    Parent = new Vector2Int(-1, -1),
                    Walkable = walkable,
                    GroundY = groundY
                };
            }
        }

        Vector2Int startCell = WorldToGrid(gridCenter, startWorld);
        Vector2Int goalCell = ClampToGrid(WorldToGrid(gridCenter, goalWorld), size);

        grid[startCell.x, startCell.y].Walkable = true;
        if (Mathf.Abs(grid[startCell.x, startCell.y].GroundY - startWorld.y) > _clearanceHeight)
            grid[startCell.x, startCell.y].GroundY = startWorld.y;

        if (!grid[goalCell.x, goalCell.y].Walkable)
            goalCell = FindNearestWalkable(grid, goalCell, size);

        if (!grid[goalCell.x, goalCell.y].Walkable)
            return new List<Vector3>();

        grid[startCell.x, startCell.y].GCost = 0;
        grid[startCell.x, startCell.y].HCost = Heuristic(startCell, goalCell);

        List<Vector2Int> open = new List<Vector2Int>(64) { startCell };

        while (open.Count > 0)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                Node a = grid[open[i].x, open[i].y];
                Node b = grid[open[bestIndex].x, open[bestIndex].y];
                if (a.FCost < b.FCost || (a.FCost == b.FCost && a.HCost < b.HCost))
                    bestIndex = i;
            }

            Vector2Int current = open[bestIndex];
            open.RemoveAt(bestIndex);
            closed[current.x, current.y] = true;

            if (current == goalCell)
                return ReconstructPath(grid, startCell, goalCell, gridCenter);

            Node currNode = grid[current.x, current.y];

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    int nx = current.x + dx;
                    int nz = current.y + dz;
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    if (closed[nx, nz]) continue;

                    Node nextNode = grid[nx, nz];
                    if (!nextNode.Walkable) continue;

                    float heightDiff = Mathf.Abs(nextNode.GroundY - currNode.GroundY);
                    if (heightDiff > _maxStepHeight) continue;

                    if (dx != 0 && dz != 0)
                    {
                        Node sideX = grid[current.x + dx, current.y];
                        Node sideZ = grid[current.x, current.y + dz];
                        if (!sideX.Walkable || !sideZ.Walkable) continue;
                        if (Mathf.Abs(sideX.GroundY - currNode.GroundY) > _maxStepHeight) continue;
                        if (Mathf.Abs(sideZ.GroundY - currNode.GroundY) > _maxStepHeight) continue;
                    }

                    int stepCost = (dx != 0 && dz != 0) ? 14 : 10;
                    int tentativeG = currNode.GCost + stepCost + Mathf.RoundToInt(heightDiff * 10f);

                    if (tentativeG < nextNode.GCost)
                    {
                        grid[nx, nz].Parent = current;
                        grid[nx, nz].GCost = tentativeG;
                        grid[nx, nz].HCost = Heuristic(new Vector2Int(nx, nz), goalCell);

                        Vector2Int nextPos = new Vector2Int(nx, nz);
                        if (!open.Contains(nextPos))
                            open.Add(nextPos);
                    }
                }
            }
        }

        return new List<Vector3>();
    }

    private int Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dz = Mathf.Abs(a.y - b.y);
        return Mathf.Min(dx, dz) * 14 + Mathf.Abs(dx - dz) * 10;
    }

    private List<Vector3> ReconstructPath(Node[,] grid, Vector2Int start, Vector2Int goal, Vector3 gridCenter)
    {
        List<Vector3> path = new List<Vector3>();
        Vector2Int curr = goal;

        while (curr.x >= 0 && curr.y >= 0)
        {
            Node n = grid[curr.x, curr.y];
            path.Add(GridToWorld(gridCenter, n.X, n.Z, n.GroundY));
            if (curr == start) break;
            curr = n.Parent;
        }

        path.Reverse();
        if (path.Count > 0) path.RemoveAt(0);
        return path;
    }

    private bool EvaluateCell(Vector3 worldXZ, float refY, out float groundY)
    {
        groundY = refY;
        Collider floorCollider = null;

        float rayStartY = refY + _clearanceHeight * 2f;
        float rayDist = _clearanceHeight * 4f;

        if (Physics.Raycast(
            new Vector3(worldXZ.x, rayStartY, worldXZ.z),
            Vector3.down,
            out RaycastHit hit,
            rayDist,
            _mask,
            QueryTriggerInteraction.Ignore))
        {
            if (!IsIgnored(hit.transform))
            {
                groundY = hit.point.y;
                floorCollider = hit.collider;

                if (Vector3.Angle(hit.normal, Vector3.up) > _maxSlopeAngle)
                    return false;
            }
        }

        float checkRadius = Mathf.Max(_agentRadius, _cellSize * 0.4f);
        float halfH = _clearanceHeight * 0.4f;
        Vector3 boxCenter = new Vector3(worldXZ.x, groundY + 0.15f + halfH, worldXZ.z);
        Vector3 halfExtents = new Vector3(checkRadius, halfH, checkRadius);

        int count = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            _overlapBuffer,
            Quaternion.identity,
            _mask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == floorCollider || IsIgnored(col.transform)) continue;
            return false;
        }

        return true;
    }

    private bool IsIgnored(Transform t)
    {
        if (t == null) return true;
        Transform root = t.root;
        if (root == _owner.root) return true;
        if (_target != null && root == _target.root) return true;
        return false;
    }

    private Vector2Int WorldToGrid(Vector3 center, Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - center.x) / _cellSize) + _halfExtent;
        int z = Mathf.RoundToInt((worldPos.z - center.z) / _cellSize) + _halfExtent;
        return new Vector2Int(Mathf.Clamp(x, 0, _halfExtent * 2), Mathf.Clamp(z, 0, _halfExtent * 2));
    }

    private Vector3 GridToWorld(Vector3 center, int x, int z, float y)
    {
        return new Vector3(
            center.x + (x - _halfExtent) * _cellSize,
            y,
            center.z + (z - _halfExtent) * _cellSize
        );
    }

    private Vector2Int ClampToGrid(Vector2Int cell, int size)
    {
        return new Vector2Int(Mathf.Clamp(cell.x, 0, size - 1), Mathf.Clamp(cell.y, 0, size - 1));
    }

    private Vector2Int FindNearestWalkable(Node[,] grid, Vector2Int origin, int size)
    {
        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int x = origin.x + dx;
                    int z = origin.y + dz;
                    if (x >= 0 && z >= 0 && x < size && z < size && grid[x, z].Walkable)
                        return new Vector2Int(x, z);
                }
            }
        }
        return origin;
    }
}