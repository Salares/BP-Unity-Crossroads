using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrossroadCreator : MonoBehaviour
{
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

    [HideInInspector] public Crossroad crossroad;

    [Header("Crossroads Options")]
    [Range(2, 16)]
    public int numberOfPaths = 2;

    public void CreateCrossroad()
    {
        crossroad = new Crossroad(transform.position, numberOfPaths);
    }

    void Start()
    {
        CreateCrossroad();
    }
}
