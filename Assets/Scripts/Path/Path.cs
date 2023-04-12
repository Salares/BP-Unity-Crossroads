using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class Path 
{
    [SerializeField, HideInInspector] List<Vector2> points;
    [SerializeField, HideInInspector] bool isClosed;
    [SerializeField, HideInInspector] bool autoSetControlPoints;

    public Path(Vector2 centre)
    {
        points = new List<Vector2>
        {
            centre + Vector2.left,
            centre + (Vector2.left + Vector2.up),
            centre + (Vector2.right + Vector2.down),
            centre + Vector2.right
        };
        Debug.Log("Created path at " + centre);
    }

    public Path(Vector2 centre, int index, int numberOfPaths)
    {
        Vector2 startPoint = CalculatePointOnCircle(centre, index, numberOfPaths, 0.2f);
        Vector2 startPointControl = CalculatePointOnCircle(centre, index, numberOfPaths, 0.4f);
        Vector2 endPoint = CalculatePointOnCircle(centre, index, numberOfPaths);
        Vector2 endPointControl = CalculatePointOnCircle(centre, index, numberOfPaths, 0.8f);
        points = new List<Vector2>
        {
            startPoint, 
            startPointControl,
            endPointControl,
            endPoint
        };
        Debug.Log("Created path at " + startPoint + " to " + endPoint);
    }

    private Vector2 CalculatePointOnCircle(Vector2 centre, int index, int numberOfPaths, float radius = 1f)
    {
        float angle = index * 360f / numberOfPaths;

        float x = centre.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
        float y = centre.y + radius * Mathf.Sin(angle * Mathf.Deg2Rad);

        return new Vector2(x,y);
    }

    public Vector2 this[int i]
    {
        get { return points[i]; }
    }

    public bool IsClosed
    {
        get { return isClosed; }
        set
        {
            if(isClosed != value)
            {
                isClosed = value;

                if(isClosed)
                {
                    points.Add((points[points.Count - 1] * 2) - points[points.Count - 2]);
                    points.Add((points[0] * 2) - points[1]);
                    if (autoSetControlPoints)
                    {
                        AutoSetAnchorControlPoints(0);
                        AutoSetAnchorControlPoints(points.Count-3);
                    }
                }
                else
                {
                    points.RemoveRange(points.Count-2, 2);
                    if (autoSetControlPoints)
                    {
                        AutoSetStartAndEndControls();
                        
                    }
                }
            }
        }
    }
    public bool AutoSetControlPoints
    {
        get { return autoSetControlPoints; }
        set
        {
            if(autoSetControlPoints != value)
            {
                autoSetControlPoints = value;
                if(autoSetControlPoints)
                {
                    AutoSetAllControlPoints();
                }
            }
        }
    }

    public int NumPoints { get { return points.Count; } }

    public int NumSegments { get { return points.Count / 3; } }

    public void AddSegment(Vector2 anchorPos)
    {
        // P3 + (P3 - P2) = P3*2 - P2
        points.Add((points[points.Count - 1] * 2) - points[points.Count - 2]);

        // (P4 + P6) / 2
        points.Add((points[points.Count - 1] + anchorPos) * .5f);
        points.Add(anchorPos);

        if(autoSetControlPoints)
        {
            AutoSetAllAffectedControlPoints(points.Count-1);
        }
    }

    public void SplitSegment(Vector2 anchorPos, int segmentIndex)
    {
        points.InsertRange(segmentIndex * 3 + 2, new Vector2[]{Vector2.zero, anchorPos, Vector2.zero});
        if(autoSetControlPoints) { AutoSetAllAffectedControlPoints(segmentIndex*3+3); }
        else { AutoSetAnchorControlPoints(segmentIndex*3+3); }
    }

    public void DeleteSegment(int anchorIndex)
    {
        if(NumSegments > 2 || !isClosed && NumSegments > 1)
        {
            if(anchorIndex == 0)
            {
                if(isClosed) { points[points.Count - 1] = points[2]; }
                points.RemoveRange(0,3);
            }
            else if (anchorIndex == points.Count - 1 && !isClosed) { points.RemoveRange(anchorIndex - 2, 3); }
            else { points.RemoveRange(anchorIndex - 1, 3); }
        }
    }

    public Vector2[] GetPointsInSegment(int i)
    {
        if ( i > NumSegments)
        {
            return null;
        }
        return new Vector2[]{ points[i*3], points[i*3+1], points[i*3+2], points[LoopIndex(i*3+3)] };
    }

    public void MovePoint(int i, Vector2 pos)
    {
        Vector2 deltaMove = pos - points[i];
        if(i % 3 == 0 || !autoSetControlPoints)
        {
            points[i] = pos;

            if(autoSetControlPoints)
            {
                AutoSetAllAffectedControlPoints(i);
            }
            else
            {
                if ( i % 3 == 0 )
                {
                    if(i+1 < points.Count || isClosed) { points[LoopIndex(i+1)] += deltaMove; }
                    if(i-1 >= 0 || isClosed) { points[LoopIndex(i-1)] += deltaMove; }
                }
                else
                {
                    bool nextPointIsAnchor = (i+1)%3 == 0;
                    int correspondingCotrolIndex = (nextPointIsAnchor) ? i+2 : i-2;
                    int anchorIndex = (nextPointIsAnchor) ? i+1 : i-1;

                    if(correspondingCotrolIndex >= 0 && correspondingCotrolIndex < points.Count || isClosed )
                    {
                        float distance = (points[LoopIndex(anchorIndex)] - points[LoopIndex(correspondingCotrolIndex)]).magnitude;
                        Vector2 direction = (points[LoopIndex(anchorIndex)] - pos).normalized;
                        points[LoopIndex(correspondingCotrolIndex)] = points[LoopIndex(anchorIndex)] + direction * distance;
                    }
                }
            }
        }
    }

    public Vector2[] CalculateEvenlySpacedPoints(float spacing, float resolution = 1)
    {
        List<Vector2> evenlySpacedPoints = new List<Vector2>();
        evenlySpacedPoints.Add(points[0]);
        Vector2 previousPoint = points[0];
        float distanceSinceLastEvenPoint = 0;

        for (int segmentIndex = 0; segmentIndex < NumSegments; segmentIndex++)
        {
            Vector2[] pointsInSegment = GetPointsInSegment(segmentIndex);
            float controlNetLength = 
                Vector2.Distance(pointsInSegment[0],pointsInSegment[1]) + 
                Vector2.Distance(pointsInSegment[1],pointsInSegment[2]) + 
                Vector2.Distance(pointsInSegment[2],pointsInSegment[3]);

            float estimatedCurveLength =  Vector2.Distance(pointsInSegment[0],pointsInSegment[3]) + controlNetLength / 2;
            int divisions = Mathf.CeilToInt(estimatedCurveLength * resolution * 10);
            float interval = 1f/divisions;

            float time = 0;
            while(time <= 1)
            {
                time += interval;
                Vector2 pointOnCurve = Bezier.EvaluateCubic(pointsInSegment[0],pointsInSegment[1],pointsInSegment[2],pointsInSegment[3], time);
                
                while(distanceSinceLastEvenPoint >= spacing)
                {
                    float exceedence = distanceSinceLastEvenPoint - spacing;
                    Vector2 newEvenlySpacedPoint = pointOnCurve + (previousPoint - pointOnCurve).normalized * exceedence;

                    evenlySpacedPoints.Add(newEvenlySpacedPoint);

                    distanceSinceLastEvenPoint = exceedence;
                    previousPoint = newEvenlySpacedPoint;
                }

                distanceSinceLastEvenPoint += Vector2.Distance(previousPoint, pointOnCurve);
                previousPoint = pointOnCurve;
            }
        }
        return evenlySpacedPoints.ToArray();
    }


    public int LoopIndex(int i)
    {
        return( i + points.Count ) % points.Count;
    }

    void AutoSetAllAffectedControlPoints(int updatedAnchorIndex)
    {
        for(int i = updatedAnchorIndex - 3 ; i <= updatedAnchorIndex + 3; i += 3)
        {
            if(i >= 0 && i < points.Count || isClosed)
            {
                AutoSetAnchorControlPoints(LoopIndex(i));
            }
        } 
        AutoSetStartAndEndControls();
    }

    void AutoSetAllControlPoints()
    {
        for (int i = 0; i < points.Count; i+= 3)
        {
            AutoSetAnchorControlPoints(i);
        }
        AutoSetStartAndEndControls();
    }

    void AutoSetAnchorControlPoints(int anchorIndex)
    {
        Vector2 anchorPos = points[anchorIndex];
        Vector2 direction = Vector2.zero;
        float[] neighbourDistances = new float[2];

        if(anchorIndex - 3 >= 0 || isClosed)
        {
            Vector2 offset = points[LoopIndex(anchorIndex - 3)] - anchorPos;
            direction += offset.normalized;
            neighbourDistances[0] = offset.magnitude;
        }

        if(anchorIndex + 3 >= 0 || isClosed)
        {
            Vector2 offset = points[LoopIndex(anchorIndex + 3)] - anchorPos;
            direction -= offset.normalized;
            neighbourDistances[1] = -offset.magnitude;
        }

        direction.Normalize();

        for (int i = 0; i < 2; i++)
        {
            int controlIndex = anchorIndex + i * 2 - 1;
            if(controlIndex >= 0 && controlIndex < points.Count || isClosed)
            {
                points[LoopIndex(controlIndex)] = anchorPos + direction * neighbourDistances[i] * .5f;
            }
        }
    }

    void AutoSetStartAndEndControls()
    {
        if(!isClosed)
        {
            points[1] = (points[0] + points[2]) * .5f;
            points[points.Count-2] = (points[points.Count-3] + points[points.Count-1]) * .5f;
        }
    }
}