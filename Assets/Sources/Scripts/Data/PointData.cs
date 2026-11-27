using UnityEngine;

public class PointData
{
    public float GScore;
    public float FScore;
    public Vector3Int CameFrom;
    public Vector3Int Coords;

    public PointData(Point point)
    {
        GScore = Mathf.Infinity;
        FScore = Mathf.Infinity;
        CameFrom = new Vector3Int(-1, -1, -1);
        Coords = point.Coords;
    }
}