using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathPlacer : MonoBehaviour
{
    public float spacing = .1f;
    public float resolution = 1f;
    void Start()
    {
        Vector3[] points = FindObjectOfType<PathCreator>().path.CalculateEvenlySpacedPoints(spacing,resolution);
        foreach(Vector3 point in points)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gameObject.transform.position = point;
            gameObject.transform.localScale = Vector3.one * spacing * .5f;
        }
    }
}
