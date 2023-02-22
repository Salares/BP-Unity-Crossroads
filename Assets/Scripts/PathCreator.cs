using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathCreator : MonoBehaviour
{
    [HideInInspector] public Path path;

    [System.Serializable] public class SplineParameters
    {
        public Color anchorColor = Color.red;
        public Color controlPointColor = Color.blue;
        public Color segmentColor = Color.green;
        public Color selectedSegmentColor = Color.yellow;
        public Color handlesColor = Color.black;
        public float anchorDiameter = .1f;
        public float controlPointDiameter = .066f;
        public float splineWidth = 2f;
        public bool displayPoints = true;

    }

    public SplineParameters splineParameters;
    
    public void CreatePath()
    {
        path = new Path(transform.position);
        
    }

    void Reset()
    {
        CreatePath();
    }

}
