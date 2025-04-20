using UnityEngine;

namespace RoadSystem
{
    /// <summary>
    /// Represents a road segment between two points, managed by the road system.
    /// </summary>
    public class RoadSegment
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public GameObject roadObject; // The GameObject representing this road segment

        public RoadSegment(Vector3 start, Vector3 end, GameObject obj)
        {
            startPoint = start;
            endPoint = end;
            roadObject = obj;
        }
    }
}