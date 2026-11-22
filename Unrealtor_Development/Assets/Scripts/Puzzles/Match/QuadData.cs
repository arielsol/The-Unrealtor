using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuadData", menuName = "Scriptable Objects/QuadData")]
public class QuadData : ScriptableObject
{
    // Stores the vertex points as Vector3

    public List<Vector3> vertices = new List<Vector3>(4);

    public Vector3 topPoint;
    public Vector3 bottomPoint;
    public Vector3 midpoint;
    public Vector3 keyPoint;

    public Vector3 spawnPoint;
    public Vector3 size;
}
