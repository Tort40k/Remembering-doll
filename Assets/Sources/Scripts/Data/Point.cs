using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Point
{
    public Vector3Int Coords;
    public Vector3 WorldPosition;
    public List<Vector3Int> Neighbours;
    public bool Invalid;

    public Point()
    {
        Neighbours = new List<Vector3Int>();
    }
}