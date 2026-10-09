using System.Collections.Generic;
using UnityEngine;

// Serialized point references are from the exact 0.7.92 playtest prefab.
public sealed class PathPointTool : MonoBehaviour
{
    [SerializeField] List<Transform> points = new List<Transform>();
    public IReadOnlyList<Transform> Points => points;
}
