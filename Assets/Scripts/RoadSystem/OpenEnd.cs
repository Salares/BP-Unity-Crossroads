using UnityEngine;

namespace RoadSystem
{
    /// <summary>
    /// Represents a connectable end of a road or intersection.
    /// </summary>
    public class OpenEnd
    {
        public enum EndType { Road, Intersection }
        public Vector3 position;
        public EndType type;
        public Object parent; // Reference to the parent RoadSegment or Intersection

        public OpenEnd(Vector3 position, EndType type, Object parent)
        {
            this.position = position;
            this.type = type;
            this.parent = parent;
        }
    }
}