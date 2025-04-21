using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class Path
{
    [SerializeField, HideInInspector] List<Vector3> points =  new List<Vector3>();
    [SerializeField, HideInInspector] bool isClosed;
    [SerializeField, HideInInspector] bool autoSetControlPoints;

    // --- NEW: Store indices of anchor points that are intersections ---
    // Note: HashSet itself isn't serialized by Unity. We'll handle this
    // via the PathCreator using ISerializationCallbackReceiver later if needed.
    [SerializeField, HideInInspector]
    List<int> serializedIntersectionIndices = new List<int>(); // For serialization
    HashSet<int> intersectionAnchorIndices = new HashSet<int>(); // For runtime use
    // --- END NEW ---

    // Constructor 1 (Unchanged)
    public Path(Vector3 centre)
    {
        points = new List<Vector3>
        {
            centre + Vector3.left,
            centre + (Vector3.left + Vector3.forward),
            centre + (Vector3.right + Vector3.back),
            centre + Vector3.right
        };
        intersectionAnchorIndices = new HashSet<int>(); // Initialize
        serializedIntersectionIndices = new List<int>(); // Initialize
        Debug.Log("Created path at " + centre);
    }

    // Constructor 2 (Unchanged, assumes no intersections initially)
    public Path(Vector3 centre, int index, int numberOfPaths, float startPointOffset, float endPointOffset, float controlPointOffset)
    {
        Vector3 startPoint = CalculatePointOnCircle(centre, index, numberOfPaths, startPointOffset);
        Vector3 startPointControl = CalculatePointOnCircle(centre, index, numberOfPaths, startPointOffset + controlPointOffset);
        Vector3 endPoint = CalculatePointOnCircle(centre, index, numberOfPaths, endPointOffset);
        Vector3 endPointControl = CalculatePointOnCircle(centre, index, numberOfPaths, endPointOffset - controlPointOffset);
        points = new List<Vector3>
        {
            startPoint,
            startPointControl,
            endPointControl,
            endPoint
        };
        intersectionAnchorIndices = new HashSet<int>(); // Initialize
        serializedIntersectionIndices = new List<int>(); // Initialize
        Debug.Log("Created path at " + startPoint + " to " + endPoint);
    }

    // --- NEW: Method to get/set intersection status ---
    public bool IsIntersection(int anchorIndex)
    {
        // Ensure it's actually an anchor index being queried
        if (anchorIndex < 0 || anchorIndex >= points.Count || anchorIndex % 3 != 0)
        {
            // Optional: Log warning or return false silently
            // Debug.LogWarning($"IsIntersection called with invalid index: {anchorIndex}");
            return false;
        }
        return intersectionAnchorIndices.Contains(anchorIndex);
    }

    public void MarkAsIntersection(int anchorIndex)
    {
        if (anchorIndex >= 0 && anchorIndex < points.Count && anchorIndex % 3 == 0)
        {
            intersectionAnchorIndices.Add(anchorIndex);
            UpdateSerializedIntersectionIndices(); // Keep serialized list updated
        }
        else
        {
            Debug.LogWarning($"Attempted to mark invalid index as intersection: {anchorIndex}");
        }
    }

    public void UnmarkAsIntersection(int anchorIndex)
    {
        if (anchorIndex >= 0 && anchorIndex < points.Count && anchorIndex % 3 == 0)
        {
            intersectionAnchorIndices.Remove(anchorIndex);
            UpdateSerializedIntersectionIndices(); // Keep serialized list updated
        }
        // No warning if removing non-existent, that's fine
    }

    // Helper to keep the serialized list in sync with the runtime HashSet
    private void UpdateSerializedIntersectionIndices()
    {
        serializedIntersectionIndices.Clear();
        foreach (int index in intersectionAnchorIndices)
        {
            serializedIntersectionIndices.Add(index);
        }
        // It's good practice to sort if order doesn't matter, helps comparing serialized data
        serializedIntersectionIndices.Sort();
    }

     // Call this method when the Path object is loaded/deserialized
    public void RestoreIntersectionIndicesFromSerializedData()
    {
        intersectionAnchorIndices.Clear();
        if (serializedIntersectionIndices != null)
        {
            foreach (int index in serializedIntersectionIndices)
            {
                // Basic validation on load
                if (index >= 0 && index < points.Count && index % 3 == 0)
                {
                     intersectionAnchorIndices.Add(index);
                }
                 else
                {
                    Debug.LogWarning($"Found invalid intersection index ({index}) during deserialization. Ignoring.");
                }
            }
        }
        else
        {
            serializedIntersectionIndices = new List<int>(); // Ensure it's not null
        }
        // Optional: Immediately call UpdateSerializedIntersectionIndices()
        // to ensure the list is clean and sorted after potential discards.
        UpdateSerializedIntersectionIndices();
    }

    // --- END NEW ---


    // CalculatePointOnCircle (Unchanged)
    private Vector3 CalculatePointOnCircle(Vector3 centre, int index, int numberOfPaths, float radius = 1f)
    {
        float angle = index * 360f / numberOfPaths;
        float x = centre.x + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
        float z = centre.z + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
        return new Vector3(x, centre.y, z);
    }

    // Indexer (Unchanged)
    public Vector3 this[int i]
    {
        get { return points[i]; }
    }

    // IsClosed Property (Unchanged)
    public bool IsClosed
    {
        get { return isClosed; }
        set
        {
            if(isClosed != value)
            {
                isClosed = value;
                // ... (rest of the IsClosed logic is unchanged) ...
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
                    // When un-closing, check if the points being removed were intersections
                    if (IsIntersection(points.Count - 1)) UnmarkAsIntersection(points.Count-1);
                    if (IsIntersection(0)) UnmarkAsIntersection(0); // Check the original point 0
                    // The actual points being removed are control points, but we check the related anchors.
                    // Need to be careful here if the logic intended removing anchors. Let's re-read.
                    // The logic removes the last two CONTROL points added when closing.
                    // The anchors (0 and Count-3) remain. So no need to unmark here based on this code.

                    points.RemoveRange(points.Count-2, 2);
                    if (autoSetControlPoints)
                    {
                        AutoSetStartAndEndControls();

                    }
                }
            }
        }
    }

    // AutoSetControlPoints Property (Unchanged)
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

    // NumPoints (Unchanged)
    public int NumPoints { get { return points.Count; } }

    // NumSegments (Unchanged)
    public int NumSegments { get { return points.Count / 3; } }

    // AddSegment (Unchanged)
    public void AddSegment(Vector3 anchorPos)
    {
        points.Add((points[points.Count - 1] * 2) - points[points.Count - 2]);
        points.Add((points[points.Count - 1] + anchorPos) * .5f);
        points.Add(anchorPos);

        if(autoSetControlPoints)
        {
            AutoSetAllAffectedControlPoints(points.Count-1);
        }
    }

        // Add this method inside your Path class in Path.cs

    /// <summary>
    /// Inserts an anchor point and its associated control points into the path.
    /// Primarily used for adding a segment before the start of the path.
    /// </summary>
    /// <param name="insertionAnchorIndex">The index where the new anchor should be inserted (must be a multiple of 3).</param>
    /// <param name="pointsToInsert">An array containing the points to insert, typically [newAnchor, newControl1, newControl2]. Should have a length that's a multiple of 3.</param>
    public void InsertPoints(int insertionAnchorIndex, Vector3[] pointsToInsert)
    {
        // --- Input Validation ---
        if (pointsToInsert == null || pointsToInsert.Length == 0)
        {
            Debug.LogWarning("InsertPoints called with no points to insert.");
            return;
        }
        if (insertionAnchorIndex % 3 != 0)
        {
            Debug.LogError($"InsertPoints must happen at an anchor index (multiple of 3). Invalid index: {insertionAnchorIndex}");
            return;
        }
        if (pointsToInsert.Length % 3 != 0)
        {
             Debug.LogError($"InsertPoints requires the number of points to insert to be a multiple of 3. Invalid count: {pointsToInsert.Length}");
             return;
        }
         // Bounds check for insertion index
         if (insertionAnchorIndex < 0 || insertionAnchorIndex > points.Count) // Allow insertion at the very end (points.Count)
         {
             Debug.LogError($"InsertPoints index {insertionAnchorIndex} is out of bounds for path with {points.Count} points.");
             return;
         }
        // --- End Validation ---


        // --- Insert Points ---
        // Use InsertRange for efficiency
        points.InsertRange(insertionAnchorIndex, pointsToInsert);
        // --- End Insert Points ---


        // --- Adjust Intersection Indices ---
        int shiftAmount = pointsToInsert.Length;
        List<int> updatedIndices = new List<int>();
        foreach (int index in intersectionAnchorIndices)
        {
            if (index >= insertionAnchorIndex) // Shift indices at or after the insertion point
            {
                updatedIndices.Add(index + shiftAmount);
            }
            else
            {
                updatedIndices.Add(index); // Keep indices before
            }
        }
        // Update the runtime HashSet and the serialized List
        intersectionAnchorIndices = new HashSet<int>(updatedIndices);
        UpdateSerializedIntersectionIndices();
        // --- End Adjust Intersection Indices ---


        // --- Adjust Auto Handles if Enabled ---
        if (autoSetControlPoints)
        {
            // Update control points around the newly inserted anchor(s)
            // The main new anchor is at insertionAnchorIndex.
            // The anchor just *after* the inserted block is at insertionAnchorIndex + shiftAmount.
            // The anchor just *before* the inserted block is at insertionAnchorIndex - 3.

            // Update controls for the anchor *before* the insertion (if it exists)
             AutoSetAllAffectedControlPoints(insertionAnchorIndex - 3);

            // Update controls for the newly inserted anchor(s)
            // Loop in case multiple anchors were inserted (though typically only one)
             for(int i = 0; i < shiftAmount; i += 3) {
                AutoSetAllAffectedControlPoints(insertionAnchorIndex + i);
             }

             // Update controls for the anchor *after* the insertion (if it exists)
             AutoSetAllAffectedControlPoints(insertionAnchorIndex + shiftAmount);

             // Also ensure start/end controls are correct if path isn't closed
             AutoSetStartAndEndControls();
        }
        // --- End Adjust Auto Handles ---
    }

    // --- MODIFIED: SplitSegment returns the index of the new anchor ---
    public int SplitSegment(Vector3 anchorPos, int segmentIndex)
    {
        // Calculate insertion point index (it's after the first control point of the segment)
        int insertionIndex = segmentIndex * 3 + 2;
        points.InsertRange(insertionIndex, new Vector3[]{Vector3.zero, anchorPos, Vector3.zero});

        // --- Adjust intersection indices ---
        // Indices >= insertionIndex + 1 need to be shifted up by 3
        List<int> updatedIndices = new List<int>();
        foreach (int index in intersectionAnchorIndices)
        {
            if (index > insertionIndex) // Only shift indices strictly *after* the insertion point
            {
                updatedIndices.Add(index + 3);
            }
            else
            {
                updatedIndices.Add(index); // Keep indices before or at the insertion point
            }
        }
        intersectionAnchorIndices = new HashSet<int>(updatedIndices);
        UpdateSerializedIntersectionIndices(); // Update serialized list
        // --- End Adjustment ---


        int newAnchorIndex = insertionIndex + 1; // The index of the newly added anchorPos

        if(autoSetControlPoints)
        {
            AutoSetAllAffectedControlPoints(newAnchorIndex);
        }
        else
        {
            AutoSetAnchorControlPoints(newAnchorIndex);
        }
        return newAnchorIndex; // Return the index
    }
    // --- END MODIFIED ---

    // --- NEW: Method specifically for creating an intersection point ---
    public int CreateIntersectionPoint(Vector3 anchorPos, int segmentIndex)
    {
        // Reuse SplitSegment to create the anchor and controls
        int newAnchorIndex = SplitSegment(anchorPos, segmentIndex);

        // Mark the newly created anchor as an intersection
        MarkAsIntersection(newAnchorIndex);

        return newAnchorIndex; // Return the index of the intersection anchor
    }
    // --- END NEW ---


    // --- MODIFIED: DeleteSegment needs to handle intersection points ---
    public void DeleteSegment(int anchorIndex)
    {
        if (anchorIndex % 3 != 0) {
             Debug.LogError("DeleteSegment called with non-anchor index!");
             return;
        }

        // If this anchor is an intersection, unmark it before deleting
        if (IsIntersection(anchorIndex))
        {
            UnmarkAsIntersection(anchorIndex);
        }

        if(NumSegments > 2 || !isClosed && NumSegments > 1)
        {
            int startIndexToRemove = -1;
            int countToRemove = 0;
            bool updateClosingAnchor = false;
            int closingAnchorOriginalIndex = -1;

            if(anchorIndex == 0)
            {
                if(isClosed)
                {
                     // The last control point (points.Count-1) should point towards the *new* start anchor (which was originally points[3])
                    points[points.Count - 1] = points[2]; // Point towards the control before the deleted anchor
                    closingAnchorOriginalIndex = points.Count - 3; // The anchor before the control points added by closing
                    updateClosingAnchor = true;
                }
                startIndexToRemove = 0;
                countToRemove = 3;
            }
            else if (anchorIndex == points.Count - 1 && !isClosed)
            {
                startIndexToRemove = anchorIndex - 2;
                countToRemove = 3;
            }
            else
            {
                 startIndexToRemove = anchorIndex - 1;
                 countToRemove = 3;
            }

             if (startIndexToRemove != -1)
             {
                points.RemoveRange(startIndexToRemove, countToRemove);

                // --- Adjust intersection indices ---
                // Indices >= startIndexToRemove + countToRemove need to be shifted down by countToRemove (3)
                // Indices within the removed range are handled by the UnmarkAsIntersection call above.
                 List<int> updatedIndices = new List<int>();
                 foreach (int index in intersectionAnchorIndices)
                 {
                     if (index > startIndexToRemove) // Shift indices strictly *after* the removed section
                     {
                         updatedIndices.Add(index - countToRemove);
                     }
                     else
                     {
                         updatedIndices.Add(index); // Keep indices before the removed section
                     }
                 }
                 intersectionAnchorIndices = new HashSet<int>(updatedIndices);

                 // If we deleted the first anchor of a closed loop, the old closing anchor
                 // might need its intersection status re-evaluated based on its *new* index.
                 if (updateClosingAnchor && IsIntersection(closingAnchorOriginalIndex))
                 {
                     // Find its new index (it should be points.Count - 3 now)
                     int newClosingAnchorIndex = points.Count - 3;
                     // Remove the old index entry and add the new one
                     intersectionAnchorIndices.Remove(closingAnchorOriginalIndex);
                     intersectionAnchorIndices.Add(newClosingAnchorIndex);

                 }
                 UpdateSerializedIntersectionIndices(); // Update serialized list
                 // --- End Adjustment ---


                // If autoSet is on, fix the control points around the deletion
                 if (autoSetControlPoints && points.Count > 0)
                 {
                    // Need to figure out which points might have been affected
                    // It's likely the anchor *before* the deleted segment (if one exists)
                    // and the anchor *after* (if one exists)
                    int prevAnchor = LoopIndex(startIndexToRemove - 1); // Index before the first removed point
                    if (prevAnchor % 3 == 0) AutoSetAnchorControlPoints(prevAnchor);

                    // The point *after* the removed range now sits at startIndexToRemove
                    int nextAnchor = LoopIndex(startIndexToRemove);
                     if (nextAnchor % 3 == 0) AutoSetAnchorControlPoints(nextAnchor);

                    // Also handle ends if not closed
                    AutoSetStartAndEndControls();
                 }
            }
        }
    }
    // --- END MODIFIED ---


    // GetPointsInSegment (Unchanged)
    public Vector3[] GetPointsInSegment(int i)
    {
         // Note: This calculation relies on points.Count, which changes when segments are added/deleted.
        // Need to ensure i is valid *before* calling this.
        if (i < 0 || i >= NumSegments) // More robust check
        {
             Debug.LogError($"GetPointsInSegment called with invalid index: {i} when NumSegments is {NumSegments}");
             // Return a default/empty array or handle appropriately
            return new Vector3[4]; // e.g., return array of zeros to avoid null ref later
        }
        // The original logic for calculating indices seems correct for cubic Bezier segments
        return new Vector3[]{ points[i*3], points[i*3+1], points[i*3+2], points[LoopIndex(i*3+3)] };
    }


    // MovePoint (Unchanged, intersection status moves with the point)
    public void MovePoint(int i, Vector3 pos)
    {
        Vector3 deltaMove = pos - points[i];

        // Check if the point being moved is a marked intersection anchor
        // bool movingIntersection = (i % 3 == 0 && IsIntersection(i)); // Store if needed

        points[i] = pos; // Update position regardless

        if (autoSetControlPoints)
        {
            AutoSetAllAffectedControlPoints(i);
        }
        else
        {
            // Keep manual control point logic as is
            if ( i % 3 == 0 ) // Moved an anchor point
            {
                // Move adjacent control points
                if(i+1 < points.Count || isClosed) { points[LoopIndex(i+1)] += deltaMove; }
                if(i-1 >= 0 || isClosed) { points[LoopIndex(i-1)] += deltaMove; }
            }
            else // Moved a control point
            {
                // Maintain alignment with the opposite control point
                bool nextPointIsAnchor = (i+1)%3 == 0;
                int correspondingControlIndex = (nextPointIsAnchor) ? i+2 : i-2;
                int anchorIndex = (nextPointIsAnchor) ? i+1 : i-1;

                // Check bounds before accessing indices
                if (LoopIndex(anchorIndex) < points.Count && LoopIndex(correspondingControlIndex) < points.Count ) // Check validity after looping
                {
                     // Ensure indices are valid before accessing points array
                    if (correspondingControlIndex >= 0 && correspondingControlIndex < points.Count || isClosed)
                    {
                        // Existing logic for aligned control points seems okay
                        float distance = (points[LoopIndex(anchorIndex)] - points[LoopIndex(correspondingControlIndex)]).magnitude;
                        Vector3 direction = (points[LoopIndex(anchorIndex)] - pos).normalized;
                        points[LoopIndex(correspondingControlIndex)] = points[LoopIndex(anchorIndex)] + direction * distance;
                    }
                }
                 else
                 {
                    // This case might happen briefly during complex operations or if indices are wrong.
                    // Add logging if needed.
                    // Debug.LogWarning($"MovePoint: Invalid indices calculated ({anchorIndex}, {correspondingControlIndex}) for point {i}");
                 }
            }
        }
         // No need to explicitly update intersection status, as it's tied to the index 'i' which doesn't change here.
    }

    // CalculateEvenlySpacedPoints (Unchanged)
    public Vector3[] CalculateEvenlySpacedPoints(float spacing, float resolution = 1)
    {
        List<Vector3> evenlySpacedPoints = new List<Vector3>();
        if (NumPoints == 0) return evenlySpacedPoints.ToArray(); // Handle empty path

        evenlySpacedPoints.Add(points[0]);
        Vector3 previousPoint = points[0];
        float distanceSinceLastEvenPoint = 0;

        for (int segmentIndex = 0; segmentIndex < NumSegments; segmentIndex++)
        {
            Vector3[] pointsInSegment = GetPointsInSegment(segmentIndex);
             if (pointsInSegment == null || pointsInSegment.Length < 4) continue; // Skip if segment data is invalid

            // Estimate curve length (existing logic seems reasonable)
            float controlNetLength =
                Vector3.Distance(pointsInSegment[0],pointsInSegment[1]) +
                Vector3.Distance(pointsInSegment[1],pointsInSegment[2]) +
                Vector3.Distance(pointsInSegment[2],pointsInSegment[3]);

            float estimatedCurveLength =  Vector3.Distance(pointsInSegment[0],pointsInSegment[3]) + controlNetLength / 2;
            int divisions = Mathf.Max(1, Mathf.CeilToInt(estimatedCurveLength * resolution * 10)); // Ensure at least 1 division
            float interval = 1f/divisions;

            // Use the EvaluateCubicBezier implementation if Bezier class isn't available
             // Ensure you have the Bezier class or replace Bezier.Evaluate with the local version below.
            // Example replacement: Vector3 pointOnCurve = EvaluateCubicBezier(pointsInSegment[0], ..., t);

            float t = 0; // Use float t = 0 for the loop start
            while(t < 1f) // Iterate up to, but not including 1 (the endpoint of the segment is the start of the next)
            {
                t = Mathf.Min(1f, t + interval); // Ensure t doesn't exceed 1 due to float precision
                // Assuming Bezier.Evaluate exists and works like EvaluateCubicBezier
                Vector3 pointOnCurve = Bezier.Evaluate(pointsInSegment[0],pointsInSegment[1],pointsInSegment[2],pointsInSegment[3],BezierType.Cubic, t);

                float distToPoint = Vector3.Distance(previousPoint, pointOnCurve);

                // While loop to place points along the segment chunk
                while (distanceSinceLastEvenPoint + distToPoint >= spacing)
                {
                     float overshootDist = (distanceSinceLastEvenPoint + distToPoint) - spacing;
                     // Calculate the position of the new evenly spaced point
                     Vector3 direction = (pointOnCurve - previousPoint).normalized;
                     Vector3 newEvenlySpacedPoint = pointOnCurve - direction * overshootDist;

                     // Check if the new point is distinct enough from the last added point
                     if (evenlySpacedPoints.Count == 0 || Vector3.SqrMagnitude(newEvenlySpacedPoint - evenlySpacedPoints[evenlySpacedPoints.Count - 1]) > 0.0001f)
                     {
                        evenlySpacedPoints.Add(newEvenlySpacedPoint);
                     }

                     // Update for the next iteration within the while loop
                     distanceSinceLastEvenPoint = 0; // Reset distance since last point was just added
                     previousPoint = newEvenlySpacedPoint; // The new point becomes the reference
                     distToPoint = overshootDist; // The remaining distance to the original pointOnCurve
                }

                // If no points were added in the while loop, update the distance and previous point
                distanceSinceLastEvenPoint += distToPoint;
                previousPoint = pointOnCurve;
            }
        }
         // Optional: Add the very last point of the path if it's not closed,
         // or if the loop didn't quite reach it due to spacing.
         if (!isClosed && NumPoints > 0 && (evenlySpacedPoints.Count == 0 || Vector3.SqrMagnitude(points[NumPoints - 1] - evenlySpacedPoints[evenlySpacedPoints.Count - 1]) > 0.0001f))
         {
            // Could potentially add the last point if needed, depending on desired behavior
            // evenlySpacedPoints.Add(points[NumPoints - 1]);
         }


        return evenlySpacedPoints.ToArray();
    }

     // --- Local Bezier Evaluation (if Bezier class is not available) ---
    public static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t); // Ensure t is between 0 and 1
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0; // (1-t)^3 * P0
        p += 3 * uu * t * p1; // 3 * (1-t)^2 * t * P1
        p += 3 * u * tt * p2; // 3 * (1-t) * t^2 * P2
        p += ttt * p3; // t^3 * P3

        return p;
    }
    // --- End Local Bezier ---


    // LoopIndex (Unchanged)
    public int LoopIndex(int i)
    {
        return( i + points.Count ) % points.Count;
    }

    // AutoSetAllAffectedControlPoints (Unchanged)
    void AutoSetAllAffectedControlPoints(int updatedAnchorIndex)
    {
        // Ensure the index is actually an anchor index
        if (updatedAnchorIndex % 3 != 0)
        {
            // Find the nearest anchor index if a control point index was passed
            if ((updatedAnchorIndex + 1) % 3 == 0) updatedAnchorIndex++;
            else if ((updatedAnchorIndex - 1) % 3 == 0) updatedAnchorIndex--;
            else return; // Should not happen with correct usage
        }


        for(int i = updatedAnchorIndex - 3 ; i <= updatedAnchorIndex + 3; i += 3)
        {
            if(i >= 0 && i < points.Count || isClosed)
            {
                AutoSetAnchorControlPoints(LoopIndex(i));
            }
        }
        AutoSetStartAndEndControls();
    }

    // AutoSetAllControlPoints (Unchanged)
    void AutoSetAllControlPoints()
    {
        if (NumPoints < 3) return; // Avoid errors on very small paths

        for (int i = 0; i < points.Count; i+= 3)
        {
           AutoSetAnchorControlPoints(i);
        }
        AutoSetStartAndEndControls();
    }

    // AutoSetAnchorControlPoints (Unchanged)
    void AutoSetAnchorControlPoints(int anchorIndex)
    {
        // Basic check
        if (anchorIndex < 0 || anchorIndex >= NumPoints || anchorIndex % 3 != 0) return;

        Vector3 anchorPos = points[anchorIndex];
        Vector3 direction = Vector3.zero;
        float[] neighbourDistances = new float[2]; // Distances to previous and next anchors

        // Previous Anchor Info
        int prevAnchorIndex = anchorIndex - 3;
        if (prevAnchorIndex >= 0 || isClosed)
        {
            Vector3 offset = points[LoopIndex(prevAnchorIndex)] - anchorPos;
            direction += offset.normalized; // Direction towards previous anchor
            neighbourDistances[0] = offset.magnitude;
        }

        // Next Anchor Info
        int nextAnchorIndex = anchorIndex + 3;
        if (nextAnchorIndex < points.Count || isClosed) // Use '< points.Count' for open paths
        {
             Vector3 offset = points[LoopIndex(nextAnchorIndex)] - anchorPos;
             direction -= offset.normalized; // Direction away from next anchor (adds to the overall direction vector correctly)
             neighbourDistances[1] = -offset.magnitude; // Negative magnitude used later
        }


        // Normalize the combined direction vector
        if (direction.sqrMagnitude > 0.001f) // Avoid normalizing zero vector
        {
            direction.Normalize();
        }
        else if (!isClosed && (anchorIndex == 0 || anchorIndex == NumPoints - 1))
        {
             // Handle start/end points specifically if no neighbor on one side
             if (anchorIndex == 0 && (anchorIndex + 3 < NumPoints)) // Start point
             {
                direction = (anchorPos - points[anchorIndex + 3]).normalized; // Point away from next
             }
             else if (anchorIndex == NumPoints - 1 && (anchorIndex - 3 >= 0)) // End point
             {
                direction = (points[anchorIndex - 3] - anchorPos).normalized; // Point away from previous
             }
             // If it's a single segment path, direction remains zero, control points will be halfway.
        }


        // Set control points
        for (int i = 0; i < 2; i++) // i=0 -> prev control (index-1), i=1 -> next control (index+1)
        {
            int controlIndex = anchorIndex + i * 2 - 1; // Calculates anchorIndex-1 and anchorIndex+1

            // Check if control point exists
            if (controlIndex >= 0 && controlIndex < points.Count || isClosed)
            {
                // Check if the neighbor distance was actually set (i.e., neighbor exists)
                if (neighbourDistances[i] != 0)
                {
                     points[LoopIndex(controlIndex)] = anchorPos + direction * neighbourDistances[i] * 0.5f;
                }
                 else if (!isClosed)
                 {
                     // If a neighbor doesn't exist (start/end of path), place control point halfway to the *other* control point
                     // Find the other control point index relative to the anchor
                     int otherControlIndex = anchorIndex + (1 - i) * 2 - 1; // Calculates anchorIndex+1 or anchorIndex-1
                     if (otherControlIndex >= 0 && otherControlIndex < points.Count || isClosed)
                     {
                          points[LoopIndex(controlIndex)] = (anchorPos + points[LoopIndex(otherControlIndex)]) * 0.5f;
                     }
                 }
                 // If it's closed and neighbour distance is 0, something is wrong (e.g., duplicate points)
            }
        }
    }


    // AutoSetStartAndEndControls (Unchanged)
    void AutoSetStartAndEndControls()
    {
        if(!isClosed && NumPoints >= 3) // Need at least one segment
        {
            // Set first control point halfway between first anchor and its control point
            points[1] = (points[0] + points[2]) * .5f;
            // Set last control point halfway between its anchor and the last anchor
            points[points.Count-2] = (points[points.Count-3] + points[points.Count-1]) * .5f;
        }
    }

     // --- BezierType Enum (if needed for Bezier.Evaluate) ---
    public enum BezierType { Linear, Quadratic, Cubic }
     // --- Static Bezier Class (if you don't have one elsewhere) ---
    public static class Bezier
    {
        public static Vector3 Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, BezierType type, float t)
        {
            switch (type)
            {
                case BezierType.Linear:
                    return Vector3.Lerp(p0, p1, t);
                case BezierType.Quadratic:
                     return EvaluateQuadraticBezier(p0, p1, p2, t);
                case BezierType.Cubic:
                     return EvaluateCubicBezier(p0, p1, p2, p3, t);
                default:
                     return Vector3.zero; // Or throw exception
            }
        }
         private static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
         {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return u * u * p0 + 2 * u * t * p1 + t * t * p2;
         }
         private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
         {
             // Use the existing implementation
             return Path.EvaluateCubicBezier(p0, p1, p2, p3, t);
         }
    }
    // --- End Static Bezier Class ---

} // End of Path class