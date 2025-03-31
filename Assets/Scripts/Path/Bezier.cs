using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// c:\Unity Projects\BP\Assets\Scripts\Path\Bezier.cs
public enum BezierType
{
    Cubic,
    Quadratic
}

public static class Bezier
{
    public static Vector3 EvaluateQuadratic(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        Vector3 p0 = Vector3.Lerp(a, b, t);
        Vector3 p1 = Vector3.Lerp(b, c, t);
        return Vector3.Lerp(p0, p1, t);
    }

    public static Vector3 EvaluateCubic(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        Vector3 p0 = EvaluateQuadratic(a, b, c, t);
        Vector3 p1 = EvaluateQuadratic(b, c, d, t);
        return Vector3.Lerp(p0, p1, t);
    }

    public static Vector3 Evaluate(Vector3 a, Vector3 b, Vector3 c, Vector3 d, BezierType type, float t)
    {
        switch (type)
        {
            case BezierType.Quadratic:
                return EvaluateQuadratic(a, b, c, t);
            case BezierType.Cubic:
                return EvaluateCubic(a, b, c, d, t);
            default:
                // Debug.LogError("Unsupported BezierType: " + type);
                return Vector3.zero;
        }
    }
}
