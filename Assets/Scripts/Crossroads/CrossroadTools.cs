using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CrossroadTools
{
    public static Vector3 CalculateIntersection(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector3 ab = b - a;
        Vector3 cd = d - c;
        Vector3 ac = c - a;

        float crossProduct = Vector3.Cross(ab, cd).magnitude;

        if (crossProduct < 0.00001f)
        {
            Debug.LogError("Lines are parallel, intersection point cannot be calculated.");
            return Vector3.zero;
        }

        float s = Vector3.Cross(ac, cd).magnitude / crossProduct;
        Vector3 intersection = a + s * ab;

        return intersection;
    }
}
