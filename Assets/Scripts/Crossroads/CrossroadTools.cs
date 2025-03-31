using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// c:\Unity Projects\BP\Assets\Scripts\Crossroads\CrossroadTools.cs
public static class CrossroadTools
{
    public static Vector3 CalculateIntersection(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        Vector2 c2 = new Vector2(c.x, c.z);
        Vector2 d2 = new Vector2(d.x, d.z);

        Vector2 ab = b2 - a2;
        Vector2 cd = d2 - c2;

        float denominator = ab.x * cd.y - ab.y * cd.x;

        if (Mathf.Abs(denominator) < 0.00001f)
        {
            Debug.LogError("Lines are parallel, intersection point cannot be calculated.");
            Vector3 fallback = (a + c) * 0.5f;
            fallback.y = a.y; // maintain height
            return fallback;
        }

        Vector2 ac = c2 - a2;
        float t = (ac.x * cd.y - ac.y * cd.x) / denominator;

        Vector2 intersection2D = a2 + t * ab;
        return new Vector3(intersection2D.x, a.y, intersection2D.y); // Keep original Y
    }
}
