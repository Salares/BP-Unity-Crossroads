using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
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

    [Header("Crossroad Options")]
    [Range(2, 16)]public int numberOfPaths = 2;

    [Header("Road positioning")]
    [Range(0f, 2f)] public float startPointOffset = 0.2f;
    [Range(2f, 10f)] public float endPointOffset = 1f;
    [Range(0.05f, 1f)] public float controlPointOffset = 0.05f;


    public void CreateCrossroad()
    {
        crossroad = new Crossroad(new Vector2(transform.position.x, transform.position.z), numberOfPaths, startPointOffset, endPointOffset, controlPointOffset);

    }

    void Start()
    {
        CreateCrossroad();
    }
}
