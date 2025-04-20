using System.Collections.Generic;
using UnityEngine;

namespace RoadSystem
{
    /// <summary>
    /// Represents an intersection (crossroad) with multiple arms, managed by the road system.
    /// </summary>
    public class Intersection
    {
        public List<Vector3> armPositions = new List<Vector3>();
        public GameObject intersectionObject; // The GameObject representing this intersection

        public Intersection(List<Vector3> arms, GameObject obj)
        {
            armPositions = arms;
            intersectionObject = obj;
        }
    }
}