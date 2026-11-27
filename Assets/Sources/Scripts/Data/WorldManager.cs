using System.Collections.Generic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    public static WorldManager Instance { get; private set; }

    [Header("Grid Dimensions")]
    [SerializeField] private int _gridWidth = 30;
    [SerializeField] private int _gridHeight = 10;
    [SerializeField] private int _gridLength = 30;
    [SerializeField] private float _pointDistance = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool _drawGridBounds = true;

    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public int GridLength => _gridLength;
    public float PointDistance => _pointDistance;

    public Point[][][] Grid { get; private set; }
    private Vector3 _startPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeGrid();
    }

    public void InitializeGrid()
    {
        _pointDistance = Mathf.Max(0.2f, _pointDistance);
        _gridWidth = Mathf.Max(1, _gridWidth);
        _gridHeight = Mathf.Max(1, _gridHeight);
        _gridLength = Mathf.Max(1, _gridLength);

        _startPoint = transform.position - new Vector3(_gridWidth - 1, _gridHeight - 1, _gridLength - 1) * 0.5f * _pointDistance;
        Grid = new Point[_gridWidth][][];

        for (int x = 0; x < _gridWidth; x++)
        {
            Grid[x] = new Point[_gridHeight][];
            for (int y = 0; y < _gridHeight; y++)
            {
                Grid[x][y] = new Point[_gridLength];
                for (int z = 0; z < _gridLength; z++)
                {
                    Vector3 worldPos = _startPoint + new Vector3(x, y, z) * _pointDistance;

                    Point point = new Point
                    {
                        Coords = new Vector3Int(x, y, z),
                        WorldPosition = worldPos,
                        Invalid = false
                    };

                    PopulateNeighbours(point, x, y, z);
                    Grid[x][y][z] = point;
                }
            }
        }
    }

    private void PopulateNeighbours(Point point, int x, int y, int z)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    int nz = z + dz;

                    if (nx >= 0 && nx < _gridWidth &&
                        ny >= 0 && ny < _gridHeight &&
                        nz >= 0 && nz < _gridLength)
                    {
                        point.Neighbours.Add(new Vector3Int(nx, ny, nz));
                    }
                }
            }
        }
    }

    public Point GetClosestPointWorldSpace(Vector3 position)
    {
        if (Grid == null)
            InitializeGrid();

        Vector3 local = position - _startPoint;
        int x = Mathf.Clamp(Mathf.RoundToInt(local.x / _pointDistance), 0, _gridWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(local.y / _pointDistance), 0, _gridHeight - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(local.z / _pointDistance), 0, _gridLength - 1);

        return Grid[x][y][z];
    }

    public List<Point> GetFreePoints()
    {
        if (Grid == null)
            InitializeGrid();

        List<Point> points = new List<Point>(_gridWidth * _gridHeight * _gridLength);
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                for (int z = 0; z < _gridLength; z++)
                {
                    points.Add(Grid[x][y][z]);
                }
            }
        }
        return points;
    }

    private void OnDrawGizmos()
    {
        if (!_drawGridBounds) return;

        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(_gridWidth, _gridHeight, _gridLength) * _pointDistance
        );
    }
}