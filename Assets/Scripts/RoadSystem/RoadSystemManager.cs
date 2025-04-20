using System.Collections.Generic;
using UnityEngine;

namespace RoadSystem
{
    /// <summary>
    /// Manages all roads and intersections created via the road system tool.
    /// Tracks open ends and provides API for adding new segments.
    /// </summary>
    public class RoadSystemManager : MonoBehaviour
    {
        public List<RoadSegment> roadSegments = new List<RoadSegment>();
        public List<Intersection> intersections = new List<Intersection>();
        public List<OpenEnd> openEnds = new List<OpenEnd>();

        /// <summary>
        /// Resets the road system to its default state.
        /// </summary>
        public void ResetSystem()
        {
            // Destroy all road and intersection GameObjects
            foreach (var segment in roadSegments)
            {
                if (segment.roadObject != null)
                {
                    DestroyImmediate(segment.roadObject);
                }
            }
            foreach (var intersection in intersections)
            {
                if (intersection.intersectionObject != null)
                {
                    DestroyImmediate(intersection.intersectionObject);
                }
            }

            // Clear the lists
            roadSegments.Clear();
            intersections.Clear();
            openEnds.Clear();
        }

        /// <summary>
        /// Adds a new road segment connected to the specified open end.
        /// </summary>
        public void AddRoad(OpenEnd connectTo)
        {
            // Instantiate a new GameObject for the road segment at the open end's position
            GameObject roadObj = new GameObject("RoadSegment");
            roadObj.transform.position = connectTo.position;
            roadObj.AddComponent<RoadSegmentMarker>();

            // For now, create a dummy segment with start and end at the same position
            var segment = new RoadSegment(connectTo.position, connectTo.position, roadObj);
            roadSegments.Add(segment);

            UpdateOpenEnds();
        }

        /// <summary>
        /// Adds a new intersection connected to the specified open end.
        /// </summary>
        public void AddIntersection(OpenEnd connectTo)
        {
            // Instantiate a new GameObject for the intersection at the open end's position
            GameObject intersectionObj = new GameObject("Intersection");
            // Set as child of the RoadSystemManager
            intersectionObj.transform.SetParent(this.transform);

            // TEMPORARILY place at open end for initialization
            intersectionObj.transform.position = connectTo.position;

            // Add CrossroadPlacer and get CrossroadCreator
            var placer = intersectionObj.AddComponent<CrossroadPlacer>();
            var creator = intersectionObj.GetComponent<CrossroadCreator>();

            // Set default parameters (already done below)
            // creator...

            // Determine the direction of the open end
            Vector3 incomingDirection = Vector3.forward; // Default direction
            Path existingPath = null; // To store the path of the existing road/intersection arm

            if (connectTo.type == OpenEnd.EndType.Road && connectTo.parent is GameObject roadSegmentObject)
            {
                // Attempt to get the Path component from the RoadSegment object
                Path roadPath = roadSegmentObject.GetComponent<Path>(); // Assuming RoadSegment GameObject has a Path component
                if (roadPath != null)
                {
                    existingPath = roadPath;
                    // Get the direction at the end of the road segment's path
                    incomingDirection = new Vector3(roadPath.GetDirectionAtEnd().x, 0, roadPath.GetDirectionAtEnd().y);
                }
                else
                {
                    // Fallback to using start/end points if Path component is not found
                    RoadSegment roadSegment = roadSegmentObject.GetComponent<RoadSegment>();
                     if (roadSegment != null)
                    {
                        if (Vector3.Distance(connectTo.position, roadSegment.endPoint) < 0.1f && Vector3.Distance(roadSegment.endPoint, roadSegment.startPoint) > 0.01f)
                        {
                             incomingDirection = (roadSegment.endPoint - roadSegment.startPoint).normalized;
                        }
                         else if (Vector3.Distance(connectTo.position, roadSegment.startPoint) < 0.1f && Vector3.Distance(roadSegment.endPoint, roadSegment.startPoint) > 0.01f)
                        {
                             incomingDirection = (roadSegment.startPoint - roadSegment.endPoint).normalized;
                        }
                    }
                }
            }
            else if (connectTo.type == OpenEnd.EndType.Intersection && connectTo.parent is GameObject intersectionParentObject)
            {
                Debug.Log("Connecting to an existing intersection.");
                if (intersectionParentObject == null)
                {
                    Debug.LogError("intersectionParentObject is null!");
                }
                else
                {
                    CrossroadCreator parentCreator = intersectionParentObject.GetComponent<CrossroadCreator>();
                    if (parentCreator == null)
                    {
                        Debug.LogError("parentCreator is null on existing intersection object!");
                    }
                    else
                    {
                        if (parentCreator.crossroad == null)
                        {
                            Debug.LogError("parentCreator.crossroad is null on existing intersection object!");
                        }
                        else
                        {
                            Debug.Log($"Existing intersection has {parentCreator.crossroad.NumberOfPaths} paths.");
                            // Find the arm that matches the open end position
                            foreach (var path in parentCreator.crossroad)
                            {
                                float dist = Vector3.Distance(new Vector3(path[path.NumPoints - 1].x, 0, path[path.NumPoints - 1].y), connectTo.position);
                                Debug.Log($"Distance to path endpoint: {dist}");
                                if (dist < 0.5f) // Increased tolerance
                                {
                                    existingPath = path;
                                    // Get the direction of the endpoint of the intersection arm using GetDirectionAtEnd
                                    incomingDirection = new Vector3(path.GetDirectionAtEnd().x, 0, path.GetDirectionAtEnd().y);
                                    Debug.Log($"Found matching path. Incoming direction: {incomingDirection}");
                                    break;
                                }
                            }
                            if (existingPath == null)
                            {
                                Debug.LogWarning("No matching path found in existing intersection for connection point.");
                            }
                        }
                    }
                }
            }

            // Compute rotation to align the new intersection's connecting arm with the incoming direction
            Quaternion alignRot = Quaternion.identity;
            int connectingArmIndex = 0; // Default to the first arm

            if (creator.crossroad != null && creator.crossroad.NumberOfPaths > 0)
            {
                // Find the arm of the new intersection whose start point is closest to the open end
                float closestStartPointDist = float.MaxValue;
                for (int i = 0; i < creator.crossroad.NumberOfPaths; i++)
                {
                    Vector2 armStart = creator.crossroad[i][0];
                    float dist = (new Vector2(connectTo.position.x, connectTo.position.z) - armStart).sqrMagnitude;
                    if (dist < closestStartPointDist)
                    {
                        closestStartPointDist = dist;
                        connectingArmIndex = i;
                    }
                }

                // Get the direction of the connecting arm of the new intersection at its start point
                Vector2 newArmDirection2D = creator.crossroad[connectingArmIndex].GetDirectionAtEnd(); // Get direction at the "start" of the arm (which is the end of the path)
                Vector3 newArmDirection = new Vector3(newArmDirection2D.x, 0, newArmDirection2D.y);

                // Calculate the rotation needed to align newArmDirection with incomingDirection
                if (newArmDirection != Vector3.zero && incomingDirection != Vector3.zero)
                {
                     alignRot = Quaternion.FromToRotation(newArmDirection, incomingDirection);
                }
            }


            // Set intersection position and rotation
            intersectionObj.transform.position = connectTo.position;
            intersectionObj.transform.rotation = alignRot;
            intersectionObj.AddComponent<IntersectionMarker>();

            // Add CrossroadPlacer and CrossroadCreator
            // Use the placer and creator variables already declared earlier in the method
            
            // Set default parameters
            creator.numberOfPaths = 3;
            creator.startPointOffset = 0.25f;
            creator.endPointOffset = 2f;
            creator.controlPointOffset = 0.25f;

            placer.roadWidth = 0.5f;
            placer.spacing = 0.5f;
            placer.tiling = 2f;

            // Optionally assign materials if available in Resources
            placer.roadMaterial = Resources.Load<Material>("Road");
            placer.crossroadMaterial = Resources.Load<Material>("Crossroad");

            // Create and generate the crossroad mesh
            creator.CreateCrossroad();

            // Ensure each path has a minimum length for mesh generation
            if (creator.crossroad != null)
            {
                Vector2 center = new Vector2(intersectionObj.transform.position.x, intersectionObj.transform.position.z);
                float minLength = 3f; // Minimum length for each arm
                int i = 0;
                foreach (var path in creator.crossroad)
                {
                    Vector2 start = path[0];
                    Vector2 end = path[path.NumPoints - 1];
                    Vector2 dir = (end - start).normalized;
                    Vector2 newEnd = start + dir * minLength;
                    path.MovePoint(path.NumPoints - 1, newEnd);
                    i++;
                }
            }

            placer.UpdateCrossroad();

            // --- Alignment: Spline-based positioning and orientation ---
            // This section is now partially redundant with the initial alignment,
            // but keeping it for now to see the effect.
            // The primary alignment should happen before the first CreateCrossroad call.

            // 1. Find the arm whose endpoint is closest to the open end
            int bestArmIndex = 0;
            float bestDist = float.MaxValue;
            Vector2 bestArmEnd = Vector2.zero;
            for (int i = 0; i < creator.crossroad.NumberOfPaths; i++) // Use NumberOfPaths here
            {
                var path = creator.crossroad[i];
                Vector2 end = path[path.NumPoints - 1];
                float dist = (new Vector2(connectTo.position.x, connectTo.position.z) - end).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestArmIndex = i;
                    bestArmEnd = end;
                }
            }

            // 2. Move the intersection so the endpoint matches the open end
            Vector2 intersectionCenter2D = new Vector2(intersectionObj.transform.position.x, intersectionObj.transform.position.z);
            Vector2 offset2D = new Vector2(connectTo.position.x, connectTo.position.z) - bestArmEnd;
            intersectionObj.transform.position += new Vector3(offset2D.x, 0, offset2D.y);

            // 3. Adjust control points for smooth transition
            Debug.Log($"Attempting control point adjustment. existingPath is null: {existingPath == null}, existingPath.NumPoints: {(existingPath != null ? existingPath.NumPoints.ToString() : "N/A")}");
            if (creator.crossroad != null && creator.crossroad.NumberOfPaths > bestArmIndex) // Use NumberOfPaths here
            {
                 Path connectedArm = creator.crossroad[bestArmIndex];
                 if (connectedArm.NumPoints >= 4) // Ensure it's a cubic Bezier with control points
                 {
                     // Adjust the control point of the new intersection's connected arm
                     // Set the control point to be on the line defined by the connection point and the incoming direction
                     float controlPointDistance = creator.controlPointOffset; // Use the defined offset
                     Vector2 newControlPointPos = new Vector2(connectTo.position.x, connectTo.position.z) + new Vector2(incomingDirection.x, incomingDirection.z) * controlPointDistance;
                     connectedArm.MovePoint(1, newControlPointPos);

                     // Draw debug line for the new control point
                     Debug.DrawLine(connectTo.position, new Vector3(newControlPointPos.x, connectTo.position.y, newControlPointPos.y), Color.cyan, 10f);


                     // Adjust the control point of the existing road segment/intersection arm if it's an intersection
                     if (existingPath != null && existingPath.NumPoints >= 4)
                     {
                         Debug.Log("Adjusting existing path control point.");
                         // Find the index of the point in the existing path that is closest to the connectTo position
                         int closestPointIndex = -1;
                         float minDistance = float.MaxValue;
                         for (int i = 0; i < existingPath.NumPoints; i++)
                         {
                             float dist = Vector2.Distance(existingPath[i], new Vector2(connectTo.position.x, connectTo.position.z));
                             if (dist < minDistance)
                             {
                                 minDistance = dist; 
                                 closestPointIndex = i;
                             }
                         }
                         Debug.Log($"Closest point index in existing path: {closestPointIndex}");

                         if (closestPointIndex != -1)
                         {
                             // Assuming the open end is at the end of the existing path (index NumPoints - 1)
                             // The control point to adjust would be at index NumPoints - 2
                             int existingControlPointIndex = existingPath.NumPoints - 2;
                             Debug.Log($"Existing control point index: {existingControlPointIndex}");
                             if (existingControlPointIndex >= 0)
                             {
                                 // Set the control point to be on the line defined by the connection point and the *opposite* of the incoming direction
                                 Vector2 existingControlPointPos = new Vector2(connectTo.position.x, connectTo.position.z) - new Vector2(incomingDirection.x, incomingDirection.z) * controlPointDistance;
                                 existingPath.MovePoint(existingControlPointIndex, existingControlPointPos);

                                 // Draw debug line for the existing control point
                                 Debug.DrawLine(connectTo.position, new Vector3(existingControlPointPos.x, connectTo.position.y, existingControlPointPos.y), Color.magenta, 10f);
                             }
                         }
                     }
                     else
                     {
                         Debug.Log("Existing path is null or has less than 4 points. Cannot adjust control point.");
                     }
                 }
            }

            // Draw debug line at the connection point
            Debug.DrawLine(connectTo.position, connectTo.position + Vector3.up * 2f, Color.yellow, 10f); // Draw a yellow line upwards at the connection point
            Debug.DrawLine(connectTo.position, connectTo.position + incomingDirection * 2f, Color.blue, 10f); // Draw a blue line showing the incoming direction


            // 4. Regenerate mesh at new position
            placer.UpdateCrossroad();

            // (Mesh edge shifting for perfect overlap can be implemented in the mesh generation step)
            
            // Store arm positions for open ends (use last point of each path)
            var arms = new List<Vector3>();
            if (creator.crossroad != null)
            {
                foreach (var path in creator.crossroad)
                {
                    var points = path.CalculateEvenlySpacedPoints(placer.spacing);
                    if (points.Length > 0)
                    {
                        arms.Add(new Vector3(points[points.Length - 1].x, 0, points[points.Length - 1].y));
                    }
                }
            }
            var intersection = new Intersection(arms, intersectionObj);
            intersections.Add(intersection);

            UpdateOpenEnds();
        }

        /// <summary>
        /// Updates the list of open ends based on current roads and intersections.
        /// </summary>
        public void UpdateOpenEnds()
        {
            // Implementation will scan all managed objects and update openEnds.
            // For now, just clear and add ends at all created objects' positions

            openEnds.Clear();

            foreach (var segment in roadSegments)
            {
                openEnds.Add(new OpenEnd(segment.startPoint, OpenEnd.EndType.Road, segment.roadObject));
                openEnds.Add(new OpenEnd(segment.endPoint, OpenEnd.EndType.Road, segment.roadObject));
            }

            foreach (var intersection in intersections)
            {
                foreach (var arm in intersection.armPositions)
                {
                    openEnds.Add(new OpenEnd(arm, OpenEnd.EndType.Intersection, intersection.intersectionObject));
                }
            }
        }

        /// <summary>
        /// Adds the first road segment at the manager's position.
        /// </summary>
        public void AddFirstRoad()
        {
            Vector3 pos = transform.position;
            GameObject roadObj = new GameObject("RoadSegment");
            roadObj.transform.position = pos;
            roadObj.AddComponent<RoadSegmentMarker>();

            var segment = new RoadSegment(pos, pos, roadObj);
            roadSegments.Add(segment);

            UpdateOpenEnds();
        }

        /// <summary>
        /// Adds the first intersection at the manager's position.
        /// </summary>
        public void AddFirstIntersection()
        {
            Vector3 pos = transform.position;
            GameObject intersectionObj = new GameObject("Intersection");
            intersectionObj.transform.position = pos;
            // Set as child of the RoadSystemManager
            intersectionObj.transform.SetParent(this.transform);
            intersectionObj.AddComponent<IntersectionMarker>();

            Debug.Log("Creating intersection at: " + pos);

            // Add CrossroadPlacer and CrossroadCreator
            var placer = intersectionObj.AddComponent<CrossroadPlacer>();
            var creator = intersectionObj.GetComponent<CrossroadCreator>();

            Debug.Log("Added CrossroadPlacer and CrossroadCreator");

            // Set default parameters
            creator.numberOfPaths = 3;
            creator.startPointOffset = 0.25f;
            creator.endPointOffset = 2f;
            creator.controlPointOffset = 0.25f;

            placer.roadWidth = 0.5f;
            placer.spacing = 0.5f;
            placer.tiling = 2f;

            // Optionally assign materials if available in Resources
            placer.roadMaterial = Resources.Load<Material>("Road");
            placer.crossroadMaterial = Resources.Load<Material>("Crossroad");

            Debug.Log("Set parameters and materials");

            // Create and generate the crossroad mesh
            creator.CreateCrossroad();
            Debug.Log("Called CreateCrossroad. crossroad is null? " + (creator.crossroad == null));
            // Ensure each path has a minimum length for mesh generation
            if (creator.crossroad != null)
            {
                Vector2 center = new Vector2(intersectionObj.transform.position.x, intersectionObj.transform.position.z);
                float minLength = 3f; // Minimum length for each arm
                int i = 0;
                foreach (var path in creator.crossroad)
                {
                    // Move the end point outward from the center
                    Vector2 start = path[0];
                    Vector2 end = path[path.NumPoints - 1];
                    Vector2 dir = (end - start).normalized;
                    Vector2 newEnd = start + dir * minLength;
                    path.MovePoint(path.NumPoints - 1, newEnd);
                    i++;
                }
            }

            placer.UpdateCrossroad();
            Debug.Log("Called UpdateCrossroad");

            // Store arm positions for open ends (use last point of each path)
            var arms = new List<Vector3>();
            if (creator.crossroad != null)
            {
                foreach (var path in creator.crossroad)
                {
                    var points = path.CalculateEvenlySpacedPoints(placer.spacing);
                    if (points.Length > 0)
                    {
                        arms.Add(new Vector3(points[points.Length - 1].x, 0, points[points.Length - 1].y));
                    }
                }
            }
            else
            {
                Debug.LogWarning("Crossroad was null after CreateCrossroad!");
            }
            var intersection = new Intersection(arms, intersectionObj);
            intersections.Add(intersection);

            UpdateOpenEnds();
        }
    }
}