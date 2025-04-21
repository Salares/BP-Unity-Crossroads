using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
// using TriangleNet.Geometry;
// using TriangleNet.Meshing;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PathCreator))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class RoadPlacer : MonoBehaviour
{
    //=================================================
    // Public Inspector Settings
    //=================================================
    [Header("Road Settings")]
    [Tooltip("Width of the generated road mesh.")]
    public float roadWidth = 1f;
    [Tooltip("Target distance between points generated along the spline.")]
    [Range(0.05f, 2f)] public float spacing = 0.15f;
    [Tooltip("Controls detail level along curves (higher = more points).")]
    public float resolution = 1;
    [Tooltip("Multiplier for the road texture's V tiling (length).")]
    public float roadMaterialTiling = 1;

    [Header("Intersection Settings")]
    [Tooltip("Generate fill meshes for intersection areas.")]
    public bool generateIntersections = true;
    [Tooltip("Distance back along the path from an intersection anchor where the road mesh stops.")]
    [Range(0f, 2f)] public float intersectionCutback = 0.5f;
    [Tooltip("Material for the main road segments.")]
    public Material roadMaterial;
    [Tooltip("Material for the intersection fill meshes.")]
    public Material intersectionMaterial;
    [Tooltip("Multiplier for the intersection texture tiling (applied based on intersection bounds).")]
    public float intersectionMaterialTiling = 1;

    [Header("Debug")]
    [Tooltip("Automatically update the mesh in the editor when paths or settings change.")]
    public bool autoUpdate = false;
    [Tooltip("Draw debug lines in the Scene view for intersection boundaries and fans.")]
    public bool drawDebugLines = true;
    public Color roadEndpointColor = Color.yellow;
    public Color intersectionEdgeColor = Color.magenta;

    //=================================================
    // Private Member Variables
    //=================================================
    private PathCreator creator;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // Mesh Data Lists
    private List<Vector3> roadVertices = new List<Vector3>();
    private List<int> roadTriangles = new List<int>();
    private List<Vector2> roadUVs = new List<Vector2>();

    private List<Vector3> intersectionVertices = new List<Vector3>();
    private List<int> intersectionTriangles = new List<int>();
    private List<Vector2> intersectionUVs = new List<Vector2>();

    // Cached Endpoint Data (Calculated during road generation pass)
    // Key: (Path Index, Anchor Index)
    private Dictionary<(int pathIdx, int anchorIdx), List<RoadEndPointInfo>> roadEndPointsCache;



    //=================================================
    // Nested Helper Classes & Structs
    //=================================================
    /// <summary>
    /// Stores the precise boundary information where a road segment connects to an intersection.
    /// Calculated based on the *actual* first/last vertices used after cutback.
    /// </summary>
    public struct RoadEndPointInfo
{
    public int PathIndex;
    public int AnchorIndex;         // The anchor index this endpoint relates to
    public int SegmentIndex;        // The segment index this endpoint belongs to/comes from
    public Vector3 LeftVertex;      // Precise world position of the left vertex at the boundary
    public Vector3 RightVertex;     // Precise world position of the right vertex at the boundary
    public Vector3 CenterPoint;     // Precise world position of the center point at the boundary
    public Vector3 DirectionIntoRoad; // Normalized direction pointing AWAY from intersection INTO the road segment
    public float VCoordinate;       // The V texture coordinate at this endpoint boundary
}

    /// <summary>
    /// Groups intersection anchors by their world position.
    /// </summary>
    public class IntersectionGroupInfo
    {
        public Vector3 CenterPosition; // Approx center, used for grouping key
        public List<(int pathIdx, int anchorIdx)> Connections = new List<(int, int)>();
    }

    /// <summary>
    /// Custom comparer for Vector3 dictionary keys (handles floating point inaccuracies).
    /// </summary>
    public class Vector3EqualityComparer : IEqualityComparer<Vector3>
    {
        private readonly float toleranceSq;
        public Vector3EqualityComparer(float tolerance) { toleranceSq = tolerance * tolerance; }
        public bool Equals(Vector3 v1, Vector3 v2) { return (v1 - v2).sqrMagnitude < toleranceSq; }
        public int GetHashCode(Vector3 v)
        {
            float tol = Mathf.Max(0.001f, Mathf.Sqrt(toleranceSq));
            int hx = Mathf.RoundToInt(v.x / tol).GetHashCode();
            int hy = Mathf.RoundToInt(v.y / tol).GetHashCode();
            int hz = Mathf.RoundToInt(v.z / tol).GetHashCode();
            return hx ^ (hy << 8) ^ (hz << 16);
        }
    }

    //=================================================
    // Initialization and Update Triggering
    //=================================================

    void Awake()
    {
        creator = GetComponent<PathCreator>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        roadEndPointsCache = new Dictionary<(int, int), List<RoadEndPointInfo>>();
        if (meshFilter.sharedMesh == null) { meshFilter.sharedMesh = new Mesh { name = "Road Network Mesh" }; }
        SetupMaterials();
        UpdateRoad(); // Initial generation
    }

    /// <summary>
    /// Main entry point to regenerate the road and intersection meshes.
    /// </summary>
    public void UpdateRoad()
    {
        if (!EnsureComponents()) return; // Check required components

        // --- Reset Data ---
        roadVertices.Clear(); roadTriangles.Clear(); roadUVs.Clear();
        intersectionVertices.Clear(); intersectionTriangles.Clear(); intersectionUVs.Clear();
        roadEndPointsCache = new Dictionary<(int, int), List<RoadEndPointInfo>>();
        int roadVertexCount = 0; // Use local count for road vertices
        int intersectionVertexCount = 0; // Use local count for intersection vertices

        // --- Step 1: Find Intersection Groups ---
        var intersectionGroups = FindIntersectionAnchorGroups();

        // --- Step 2: Generate Road Mesh Segments (Stores Endpoints Inline) ---
        for (int pathIndex = 0; pathIndex < creator.paths.Count; pathIndex++)
        {
            Path path = creator.paths[pathIndex];
            if (path == null || path.NumPoints < 4) continue;
            // Pass reference to local vertex count, which will be updated inside the function
            BuildRoadMeshForPath(pathIndex, path, ref roadVertexCount);
        }

        // --- Step 3: Generate Intersection Fill Meshes ---
        if (generateIntersections)
        {
            // Use the cached endpoint data which should now be correctly populated
            foreach (var kvp in intersectionGroups)
            {
                IntersectionGroupInfo group = kvp.Value;
                if (group.Connections.Count >= 2)
                {
                    // Pass reference to local vertex count, which will be updated inside the function
                    BuildIntersectionMesh(group, ref intersectionVertexCount);
                }
            }
        }

        // --- Step 4: Finalize and Assign Combined Mesh ---
        AssignCombinedMesh(roadVertexCount, intersectionVertexCount); // Pass final counts

        // --- Post-Processing ---
        SetupMaterials(); // Ensure materials are correct after mesh update

        #if UNITY_EDITOR
        CacheDebugData(intersectionGroups); // Cache data for gizmos
        SceneView.RepaintAll(); // Update scene view to show changes
        #endif
    }

    /// <summary>
    /// Checks if required components are present. Initializes MeshFilter.sharedMesh if null.
    /// </summary>
    bool EnsureComponents() {
        if (creator == null) creator = GetComponent<PathCreator>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        if (creator == null || creator.paths == null || meshFilter == null || meshRenderer == null) {
             // Ensure mesh exists even if components are missing, prevents errors later
             if (meshFilter != null && meshFilter.sharedMesh == null) {
                 meshFilter.sharedMesh = new Mesh { name = "Road Network Mesh (Empty)" };
             }
             // Consider logging a warning only once or less frequently if needed
             // Debug.LogWarning("RoadPlacer: Missing required components (PathCreator, MeshFilter, or MeshRenderer).", this);
             return false; // Indicate failure
         }

         // Ensure mesh exists if components are present but mesh isn't assigned
         if (meshFilter.sharedMesh == null) {
             meshFilter.sharedMesh = new Mesh { name = "Road Network Mesh" };
         }
         return true; // All good
    }

    /// <summary>
    /// Ensures the MeshRenderer has the correct number and assignment of materials.
    /// Uses assigned materials or falls back appropriately. Handles null cases.
    /// </summary>
    void SetupMaterials() {
         if (meshRenderer == null) {
              Debug.LogError("RoadPlacer: MeshRenderer component not found during SetupMaterials!", this);
              return; // Cannot proceed without a renderer
         }

         var currentMaterials = meshRenderer.sharedMaterials;
         Material mat1 = roadMaterial; // Can be null
         Material mat2 = null; // Start as null

         // Determine material 2 based on settings and assigned materials
         if (generateIntersections) {
             mat2 = intersectionMaterial; // Use assigned intersection material (can be null)
         }
         // Fallback logic: If mat2 is null (either because intersections are off, or intersectionMaterial wasn't assigned),
         // use mat1 (roadMaterial) instead, *if* mat1 is not null.
         if (mat2 == null && mat1 != null) {
             mat2 = mat1;
         }
         // At this point:
         // - mat1 is roadMaterial (or null)
         // - mat2 is intersectionMaterial (if assigned & generateIntersections is true)
         // - OR mat2 is roadMaterial (if intersectionMaterial was null/unused AND roadMaterial is not null)
         // - OR mat2 is null (if both intersectionMaterial was null/unused AND roadMaterial is null)

         // Create the desired material array
         Material[] newMaterials = new Material[2]; // Always aim for 2 slots for submeshes
         newMaterials[0] = mat1; // Assign (could be null)
         newMaterials[1] = mat2; // Assign (could be null)

         // Check if an update is actually needed to avoid unnecessary reassignments
         bool updateNeeded = false;
         if (currentMaterials == null || currentMaterials.Length != 2) {
             updateNeeded = true; // Array size is wrong
         } else if (currentMaterials[0] != newMaterials[0] || currentMaterials[1] != newMaterials[1]) {
             updateNeeded = true; // Material references have changed
         }

         if (updateNeeded) {
              meshRenderer.sharedMaterials = newMaterials; // Assign the potentially null-containing array
              // Optional: Log warning if assigning nulls, but only if it wasn't null before
              if ( (mat1 == null || mat2 == null) && !(currentMaterials != null && currentMaterials.Length == 2 && currentMaterials[0] == null && currentMaterials[1] == null) )
              {
                  // Only warn if we are transitioning *to* a state with missing materials
                 // Debug.LogWarning("RoadPlacer: Assign Road Material and/or Intersection Material in the Inspector. One or both materials are missing.", this);
              }
         }
    }


    //=================================================
    // Step 1: Find Intersection Groups
    //=================================================
    /// <summary>
    /// Finds all intersection anchors across all paths and groups them by world position.
    /// </summary>
    Dictionary<Vector3, IntersectionGroupInfo> FindIntersectionAnchorGroups()
    {
        // Initialize with a small tolerance for comparing anchor positions
        var groups = new Dictionary<Vector3, IntersectionGroupInfo>(new Vector3EqualityComparer(0.01f));
        if (creator == null || creator.paths == null) return groups; // Safety check

        for (int pathIdx = 0; pathIdx < creator.paths.Count; pathIdx++)
        {
            Path path = creator.paths[pathIdx];
            if (path == null || path.NumPoints < 1) continue; // Skip invalid paths

            // Iterate through anchor points only (multiples of 3)
            for (int anchorIdx = 0; anchorIdx < path.NumPoints; anchorIdx += 3)
            {
                if (path.IsIntersection(anchorIdx)) // Check if this anchor is marked as an intersection
                {
                    Vector3 anchorPos = path[anchorIdx]; // Get the world position
                    // Use transform.TransformPoint if paths store local points
                    // Vector3 anchorPos = transform.TransformPoint(path[anchorIdx]);

                    // Try to find an existing group at this position
                    if (!groups.TryGetValue(anchorPos, out var groupInfo))
                    {
                        // If no group exists, create a new one
                        groupInfo = new IntersectionGroupInfo() { CenterPosition = anchorPos };
                        groups.Add(anchorPos, groupInfo); // Add using the precise position as key
                    }
                    // Add the reference (path index, anchor index) to the group
                    groupInfo.Connections.Add((pathIdx, anchorIdx));
                }
            }
        }
        return groups; // Return the dictionary of grouped intersections
    }


        //=================================================
    // Step 2: Build Road Mesh (REVISED with List Cache)
    //=================================================
    /// <summary>
    /// Generates the mesh for a single path segment by segment.
    /// Applies intersection cutback and calculates/stores precise endpoint info in lists.
    /// </summary>
    void BuildRoadMeshForPath(int pathIndex, Path path, ref int vertexCount)
    {
        float cumulativeV = 0f; // Track V coordinate along the entire path being processed

        for (int segmentIndex = 0; segmentIndex < path.NumSegments; segmentIndex++)
        {
            // Get segment data
            int startAnchorIndex = segmentIndex * 3;
            int endAnchorIndex = path.LoopIndex(startAnchorIndex + 3); // Handles closed paths
            bool startsAtIntersection = generateIntersections && path.IsIntersection(startAnchorIndex);
            bool endsAtIntersection = generateIntersections && path.IsIntersection(endAnchorIndex);

            Vector3[] segmentControlPoints = path.GetPointsInSegment(segmentIndex);
            if (segmentControlPoints == null || segmentControlPoints.Length < 4) continue;

            // Evaluate points along the Bezier curve for this segment
            List<Vector3> segmentPoints = EvaluateSegmentPoints(segmentControlPoints, spacing, resolution * 2f);
            if (segmentPoints.Count < 2) continue; // Need at least two points

            // --- Determine Start and End Indices after Cutback ---
            int firstPointIndex = 0;
            int lastPointIndex = segmentPoints.Count - 1;
            float distanceIntoSegmentStart = 0f;
            float distanceIntoSegmentEnd = 0f;

            // Calculate cumulative distances
            List<float> cumulativeDistances = new List<float>(segmentPoints.Count);
            cumulativeDistances.Add(0f);
            for (int i = 1; i < segmentPoints.Count; i++) {
                cumulativeDistances.Add(cumulativeDistances[i-1] + Vector3.Distance(segmentPoints[i-1], segmentPoints[i]));
            }
            float totalSegmentLength = cumulativeDistances.LastOrDefault(); // Use LastOrDefault for safety

            // Apply start cutback
            if (startsAtIntersection && intersectionCutback > 0)
            {
                firstPointIndex = segmentPoints.Count; // Default to skipping if cutback is too long
                for (int i = 0; i < segmentPoints.Count; i++) {
                    if (cumulativeDistances[i] >= intersectionCutback) {
                        firstPointIndex = i;
                        distanceIntoSegmentStart = cumulativeDistances[i];
                        break;
                    }
                }
            }

            // Apply end cutback
            if (endsAtIntersection && intersectionCutback > 0)
            {
                 float targetEndDistance = totalSegmentLength - intersectionCutback;
                 lastPointIndex = -1; // Default to skipping if cutback is too long
                 for (int i = segmentPoints.Count - 1; i >= 0; i--) { // Iterate backwards
                     if (cumulativeDistances[i] <= targetEndDistance) {
                         lastPointIndex = i;
                         distanceIntoSegmentEnd = cumulativeDistances[i];
                         break;
                     }
                 }
            }

            // Check if segment is valid after cutback
            if (firstPointIndex >= lastPointIndex || firstPointIndex >= segmentPoints.Count || lastPointIndex < 0) {
                 continue; // Skip this segment
            }

            // --- Add Vertices, Triangles, UVs for the valid range ---
            int segmentVertexStartOffset = roadVertices.Count;
            int pointsInSegmentProcessed = 0;
            float currentVInSegment = 0f; // V coord relative to start of processed part

            for (int i = firstPointIndex; i <= lastPointIndex; i++)
            {
                Vector3 currentPoint = segmentPoints[i];
                Vector3 forward = CalculateForward(segmentPoints, i);
                Vector3 localRight = new Vector3(forward.z, 0, -forward.x).normalized;

                Vector3 vertexL = currentPoint - localRight * roadWidth * 0.5f;
                Vector3 vertexR = currentPoint + localRight * roadWidth * 0.5f;

                roadVertices.Add(vertexL);
                roadVertices.Add(vertexR);

                // V Coordinate Calculation
                if (pointsInSegmentProcessed > 0) {
                    currentVInSegment += Vector3.Distance(segmentPoints[i-1], segmentPoints[i]);
                } // else currentVInSegment remains 0 for the first point
                float finalV = cumulativeV + currentVInSegment;

                roadUVs.Add(new Vector2(0, finalV * roadMaterialTiling));
                roadUVs.Add(new Vector2(1, finalV * roadMaterialTiling));

                // --- Store Endpoint Info (Add to List in Cache) ---
                if (i == firstPointIndex && startsAtIntersection)
                {
                    var endpointInfo = new RoadEndPointInfo {
                        PathIndex = pathIndex,
                        AnchorIndex = startAnchorIndex,
                        SegmentIndex = segmentIndex, // Store segment index
                        LeftVertex = vertexL,
                        RightVertex = vertexR,
                        CenterPoint = currentPoint,
                        DirectionIntoRoad = forward,
                        VCoordinate = finalV
                    };
                    // Get or create the list and add the info
                    if (!roadEndPointsCache.TryGetValue((pathIndex, startAnchorIndex), out var list)) {
                        list = new List<RoadEndPointInfo>();
                        roadEndPointsCache[(pathIndex, startAnchorIndex)] = list;
                    }
                    list.Add(endpointInfo);
                    // Debug.Log($"[RoadPlacer] Cached START Endpoint - Path: {pathIndex}, Anchor: {startAnchorIndex}, Segment: {segmentIndex}"); // Optional log
                }
                if (i == lastPointIndex && endsAtIntersection)
                {
                     Vector3 directionIntoRoadAtEnd = -CalculateForward(segmentPoints, i);
                     var endpointInfo = new RoadEndPointInfo {
                        PathIndex = pathIndex,
                        AnchorIndex = endAnchorIndex,
                        SegmentIndex = segmentIndex, // Store segment index
                        LeftVertex = vertexL,
                        RightVertex = vertexR,
                        CenterPoint = currentPoint,
                        DirectionIntoRoad = directionIntoRoadAtEnd,
                        VCoordinate = finalV
                    };
                    // Get or create the list and add the info
                    if (!roadEndPointsCache.TryGetValue((pathIndex, endAnchorIndex), out var list)) {
                        list = new List<RoadEndPointInfo>();
                        roadEndPointsCache[(pathIndex, endAnchorIndex)] = list;
                    }
                    list.Add(endpointInfo);
                    // Debug.Log($"[RoadPlacer] Cached END Endpoint - Path: {pathIndex}, Anchor: {endAnchorIndex}, Segment: {segmentIndex}"); // Optional log
                }
                // --- End Endpoint Storage ---

                // Add Triangles
                if (pointsInSegmentProcessed > 0) {
                    int currentBaseIndex = segmentVertexStartOffset + pointsInSegmentProcessed * 2;
                    int prevBaseIndex = currentBaseIndex - 2;
                    roadTriangles.Add(prevBaseIndex); roadTriangles.Add(currentBaseIndex); roadTriangles.Add(prevBaseIndex + 1);
                    roadTriangles.Add(prevBaseIndex + 1); roadTriangles.Add(currentBaseIndex); roadTriangles.Add(currentBaseIndex + 1);
                }

                pointsInSegmentProcessed++;
            }

            // Update cumulative V only if points were processed
            if (pointsInSegmentProcessed > 0) {
                cumulativeV += currentVInSegment;
            }
            vertexCount += pointsInSegmentProcessed * 2;
        }
    }
    //=================================================
    // Step 3: Build Intersection Mesh (Using Triangle.NET for Triangulation)
    //=================================================
    /// <summary>
    /// Generates the mesh for a single intersection area using stored road endpoints retrieved from lists.
    /// Uses Triangle.NET for robust 2D constrained Delaunay triangulation.
    /// </summary>
        //=================================================
    // Step 3: Build Intersection Mesh (Reverted to Pre-Triangle.NET - Two Triangles Per Endpoint)
    //=================================================
    /// <summary>
    /// Generates the mesh for a single intersection area using stored road endpoints retrieved from lists.
    /// Sorts endpoints based on their position relative to the intersection center.
    /// Creates two triangles per endpoint: (Center, Right, Left) and (Center, NextLeft, CurrentRight).
    /// Calculates absolute vertex indices correctly for the combined mesh.
    /// </summary>
        //=================================================
    // Step 3: Build Intersection Mesh (Sort Perimeter Vertices Directly)
    //=================================================
    /// <summary>
    /// Generates the mesh for a single intersection area using stored road endpoints retrieved from lists.
    /// Sorts all Left/Right perimeter vertices directly by angle around the center.
    /// Uses a standard triangle fan for triangulation.
    /// </summary>
    void BuildIntersectionMesh(IntersectionGroupInfo group, ref int vertexCount)
    {
        List<RoadEndPointInfo> endPoints = new List<RoadEndPointInfo>();
        // --- Gather endpoints ---
        foreach (var connectionTuple in group.Connections) {
             if (roadEndPointsCache.TryGetValue(connectionTuple, out var endpointInfoList)) {
                 endPoints.AddRange(endpointInfoList);
             } else {
                  Debug.LogWarning($"[RoadPlacer] Intersection Mesh: No endpoint data list found in cache for connection Path {connectionTuple.pathIdx}, Anchor {connectionTuple.anchorIdx}. Segments might be fully cut back.", this);
             }
        }
        // Need at least 2 endpoints to potentially form 3+ perimeter points
        if (endPoints.Count < 2) {
            // Debug.Log($"[RoadPlacer] Skipping intersection generation for group at {group.CenterPosition}: Found only {endPoints.Count} valid individual endpoints in cache.");
            return;
        }

        // --- Calculate center ---
        Vector3 intersectionCenter = Vector3.zero;
        if (endPoints.Count > 0) {
             foreach(var ep in endPoints) { intersectionCenter += ep.CenterPoint; }
             intersectionCenter /= endPoints.Count;
        } else {
            // Should be caught above, but safety first
             return;
        }

        // --- Create List of ALL Perimeter Vertices (from all endpoints) ---
        List<Vector3> allPerimeterVertices = new List<Vector3>();
        foreach (var ep in endPoints) {
            allPerimeterVertices.Add(ep.LeftVertex);
            allPerimeterVertices.Add(ep.RightVertex);
        }
        // Need at least 3 perimeter vertices for any triangulation
        if (allPerimeterVertices.Count < 3) {
             Debug.LogWarning($"[RoadPlacer] Skipping intersection mesh for group at {group.CenterPosition}: Not enough perimeter vertices ({allPerimeterVertices.Count}) gathered.", this);
             return;
        }

        // --- Sort the Combined Perimeter Vertices by Angle ---
        try {
            // Sort the actual Vector3 points based on their angle around the center
             allPerimeterVertices = allPerimeterVertices.OrderBy<Vector3, float>(p => {
                 Vector3 relativePos = p - intersectionCenter; // Position relative to center
                 // Handle case where a perimeter point might be exactly at the center (unlikely but possible)
                 // Assigning a consistent angle (like -PI) ensures it's handled predictably.
                 if (relativePos.sqrMagnitude < 0.00001f) return -Mathf.PI;
                 return Mathf.Atan2(relativePos.z, relativePos.x); // Angle on XZ plane
             }).ToList();
        } catch (System.Exception ex) {
             Debug.LogError($"[RoadPlacer] Error during PERIMETER vertex sorting for group at {group.CenterPosition}: {ex.Message}", this);
             return; // Cannot proceed if sorting failed
        }


        // --- Add Vertices & Calculate UVs for Unity Mesh ---
        int roadVertexOffset = roadVertices.Count; // Offset needed for absolute triangle indices
        int baseIntersectionVertexIndex_relative = intersectionVertices.Count; // Starting index within intersectionVertices list

        // Add Center Vertex
        intersectionVertices.Add(intersectionCenter);
        intersectionUVs.Add(new Vector2(0.5f, 0.5f)); // Center UV is fixed at 0.5, 0.5

        // Calculate Bounds using the now SORTED perimeter vertices
        float minX=float.MaxValue, maxX=float.MinValue, minZ=float.MaxValue, maxZ=float.MinValue;
        foreach(var p in allPerimeterVertices) { // Use sorted list for bounds/UVs
             minX=Mathf.Min(minX, p.x); maxX=Mathf.Max(maxX, p.x);
             minZ=Mathf.Min(minZ, p.z); maxZ=Mathf.Max(maxZ, p.z);
        }
        float rangeX = Mathf.Max(0.1f, maxX - minX); // Avoid division by zero
        float rangeZ = Mathf.Max(0.1f, maxZ - minZ); // Avoid division by zero

        // Add SORTED Perimeter Vertices & Calculate UVs
        for (int i = 0; i < allPerimeterVertices.Count; i++) {
            intersectionVertices.Add(allPerimeterVertices[i]); // Add vertex in sorted angular order
            // Calculate UV based on normalized position within the intersection bounds
            float u = (allPerimeterVertices[i].x - minX) / rangeX;
            float v = (allPerimeterVertices[i].z - minZ) / rangeZ;
            intersectionUVs.Add(new Vector2(u * intersectionMaterialTiling, v * intersectionMaterialTiling));
        }


        // --- STANDARD TRIANGLE FAN TRIANGULATION (Using the sorted perimeter vertices) ---
        // Absolute index of the center vertex IN THE FINAL COMBINED MESH
        int centerVertIndexAbsolute_Final = roadVertexOffset + baseIntersectionVertexIndex_relative;
        int numPerimeterVerts = allPerimeterVertices.Count;

        // Loop through the sorted perimeter vertices to create the fan triangles
        for (int i = 0; i < numPerimeterVerts; i++)
        {
            // Index relative to the start of the *intersection* vertex block (Center=0, Perim0=1, Perim1=2...)
            int currentPerimIndex_relative = 1 + i;
            int nextPerimIndex_relative = 1 + ((i + 1) % numPerimeterVerts); // Wrap around for the last triangle

            // Calculate Absolute indices IN THE FINAL COMBINED MESH by adding offsets
            int absCurrentPerimIndex_Final = roadVertexOffset + baseIntersectionVertexIndex_relative + currentPerimIndex_relative;
            int absNextPerimIndex_Final = roadVertexOffset + baseIntersectionVertexIndex_relative + nextPerimIndex_relative;

            // Add triangle (Center, Next Perimeter, Current Perimeter)
            // This order assumes the perimeter vertices are sorted Counter-Clockwise (CCW)
            // which Atan2 typically provides, resulting in CCW triangles for Unity.
            intersectionTriangles.Add(centerVertIndexAbsolute_Final);
            intersectionTriangles.Add(absNextPerimIndex_Final);
            intersectionTriangles.Add(absCurrentPerimIndex_Final);
        }
        // --- END STANDARD TRIANGLE FAN TRIANGULATION ---

        // Update the total intersection vertex count passed by ref
        vertexCount += allPerimeterVertices.Count + 1; // +1 for the center vertex
    }


    //=================================================
    // Step 4: Final Mesh Assignment
    //=================================================
    /// <summary>
    /// Combines road and intersection mesh data into a single mesh with submeshes.
    /// Assigns vertices, UVs, and triangles to the MeshFilter's shared mesh.
    /// </summary>
        //=================================================
    // Step 4: Final Mesh Assignment (with Logging)
    //=================================================
    /// <summary>
    /// Combines road and intersection mesh data into a single mesh with submeshes.
    /// Assigns vertices, UVs, and triangles to the MeshFilter's shared mesh.
    /// Includes logging for debugging purposes.
    /// </summary>
    /// <param name="roadVertexCount">Calculated total road vertices (passed by value, informational).</param>
    /// <param name="intersectionVertexCount">Calculated total intersection vertices (passed by value, informational).</param>
    void AssignCombinedMesh(int roadVertexCount, int intersectionVertexCount)
    {
         // --- Log Entry and Initial Data Sizes ---
         Debug.Log($"[AssignCombinedMesh] ENTERED. Road Vertices List Size: {roadVertices.Count}, Intersection Vertices List Size: {intersectionVertices.Count}");
         Debug.Log($"[AssignCombinedMesh] Road Triangles List Size: {roadTriangles.Count}, Intersection Triangles List Size: {intersectionTriangles.Count}");
         // Note: The parameters roadVertexCount/intersectionVertexCount were passed by ref earlier and incremented.
         // Here they are passed by value, showing the final calculated count from the generation phase.
         // Let's use the actual List.Count for mesh assignment logic as it's more direct.
         // Debug.Log($"[AssignCombinedMesh] Passed roadVertCount (calculated): {roadVertexCount}, Passed intersectionVertCount (calculated): {intersectionVertexCount}");

         // --- Mesh Preparation ---
         Mesh combinedMesh = meshFilter.sharedMesh;
         if (combinedMesh == null) {
             combinedMesh = new Mesh();
             combinedMesh.name = "Road Network Mesh";
             meshFilter.sharedMesh = combinedMesh;
             Debug.Log("[AssignCombinedMesh] Created new Mesh object.");
         }
         else {
             combinedMesh.Clear(); // Clear existing data before assigning new data
             // Debug.Log("[AssignCombinedMesh] Cleared existing Mesh data.");
         }

         // --- Vertex and Triangle Count Check ---
         int totalVertices = roadVertices.Count + intersectionVertices.Count; // Use current list sizes
         if (totalVertices == 0) {
             Debug.LogWarning("[AssignCombinedMesh] EXITING: No vertices generated (Road + Intersection). Skipping mesh assignment.");
             // Ensure mesh is empty if no vertices
             combinedMesh.Clear(); // Make sure it's visibly empty
             combinedMesh.subMeshCount = 0; // Reset submesh count
             return;
         }

         // --- Index Format ---
         combinedMesh.indexFormat = (totalVertices > 65534) ?
             UnityEngine.Rendering.IndexFormat.UInt32 :
             UnityEngine.Rendering.IndexFormat.UInt16;
         // Debug.Log($"[AssignCombinedMesh] Total Vertices: {totalVertices}. Index Format set to: {combinedMesh.indexFormat}");


         // --- Combine Vertex and UV Data ---
         List<Vector3> finalVertices = new List<Vector3>(totalVertices);
         List<Vector2> finalUVs = new List<Vector2>(totalVertices);

         finalVertices.AddRange(roadVertices); // Add road verts first
         finalUVs.AddRange(roadUVs);           // Add road UVs first

         // Add intersection data only if generated and present
         if (generateIntersections && intersectionVertices.Count > 0) {
             finalVertices.AddRange(intersectionVertices);
             finalUVs.AddRange(intersectionUVs);
         }
         // Debug.Log($"[AssignCombinedMesh] Combined Vertices: {finalVertices.Count}, Combined UVs: {finalUVs.Count}");


         // --- Assign Combined Data to Mesh ---
         combinedMesh.vertices = finalVertices.ToArray();
         combinedMesh.uv = finalUVs.ToArray();


         // --- Assign Triangles to Submeshes ---
         combinedMesh.subMeshCount = 2; // Submesh 0 for Road, Submesh 1 for Intersections

         // Submesh 0: Road Triangles (No index offset needed)
         combinedMesh.SetTriangles(roadTriangles.ToArray(), 0, true);
         Debug.Log($"[AssignCombinedMesh] Assigned {roadTriangles.Count} road triangles ({roadTriangles.Count/3} faces) to submesh 0.");

         // Submesh 1: Intersection Triangles
         if (generateIntersections && intersectionTriangles.Count > 0) {
             int[] finalIntersectionTris = intersectionTriangles.ToArray();
             // Important: The indices in intersectionTriangles MUST already be absolute indices
             // relative to the start of the combined vertex list (i.e., offset by roadVertices.Count
             // when they were calculated in BuildIntersectionMesh). If they are relative to the start
             // of intersectionVertices list, you MUST offset them here:
             // int roadVertCountActual = roadVertices.Count;
             // for (int i = 0; i < finalIntersectionTris.Length; i++) {
             //     finalIntersectionTris[i] += roadVertCountActual;
             // }
             // Our current BuildIntersectionMesh *does* calculate absolute indices using baseIntersectionVertexIndex.
             combinedMesh.SetTriangles(finalIntersectionTris, 1, true);
             Debug.Log($"[AssignCombinedMesh] Assigned {finalIntersectionTris.Length} intersection triangles ({finalIntersectionTris.Length/3} faces) to submesh 1.");
         } else {
             // If no intersections generated or no triangles, assign an empty array
             combinedMesh.SetTriangles(new int[0], 1, true);
             Debug.LogWarning($"[AssignCombinedMesh] Assigned NO intersection triangles to submesh 1. (GenerateIntersections={generateIntersections}, TriangleCount={intersectionTriangles.Count})");
         }

         // --- Final Calculations ---
         try {
             // Debug.Log("[AssignCombinedMesh] Recalculating Normals and Bounds...");
             combinedMesh.RecalculateNormals(); // Calculate vertex normals based on triangle geometry
             combinedMesh.RecalculateBounds();  // Calculate the bounding box of the mesh
         }
         catch (System.Exception ex) {
             Debug.LogError($"[AssignCombinedMesh] Mesh finalization error during RecalculateNormals/Bounds: {ex.Message}\nCheck for invalid mesh data (e.g., degenerate triangles).", this);
             // Add more specific checks if needed
             if (combinedMesh.vertexCount == 0) Debug.LogError("Mesh has zero vertices after assignment.");
             if (combinedMesh.GetTriangles(0).Length % 3 != 0) Debug.LogError("Submesh 0 triangle count not multiple of 3.");
             if (combinedMesh.subMeshCount > 1 && combinedMesh.GetTriangles(1).Length % 3 != 0) Debug.LogError("Submesh 1 triangle count not multiple of 3.");
         }
         Debug.Log("[AssignCombinedMesh] FINISHED.");
    }


    /// <summary>
    /// Calculates evenly spaced points along a single Bezier segment.
    /// Includes both the start (p[0]) and end (p[3]) points.
    /// Uses an adaptive step approach for better spacing accuracy.
    /// </summary>
    List<Vector3> EvaluateSegmentPoints(Vector3[] p, float spacing, float resolution)
    {
        spacing = Mathf.Max(0.01f, spacing); // Ensure positive spacing
        resolution = Mathf.Max(0.1f, resolution); // Ensure positive resolution

         List<Vector3> points = new List<Vector3>();
         if (p == null || p.Length < 4) {
              Debug.LogWarning("EvaluateSegmentPoints received invalid control point array.");
              return points;
         }

         points.Add(p[0]); // Start with the first point
         Vector3 previousPoint = p[0];
         float distanceSinceLastEvenPoint = 0;

         // Estimate curve length more accurately
         float chordLength = Vector3.Distance(p[0], p[3]);
         float controlNetLength = Vector3.Distance(p[0],p[1]) + Vector3.Distance(p[1],p[2]) + Vector3.Distance(p[2],p[3]);
         // Approximation: Paul Bourke's method or similar refinements could be used here.
         // Using a simple average for estimation.
         float estimatedCurveLength = (chordLength + controlNetLength) / 2f;

        // Calculate initial divisions based on spacing and resolution
        // More divisions generally lead to better accuracy but higher cost.
         int divisions = Mathf.Max(3, Mathf.CeilToInt(estimatedCurveLength * resolution / spacing));
         float dt = 1.0f / divisions; // Initial step size in parameter 't'

         float currentT = 0; // Start parameter at 0

         while (currentT < 1.0f)
         {
             // Calculate point slightly ahead to estimate distance
             float nextT = Mathf.Min(1.0f, currentT + dt);
             Vector3 pointOnCurve = Path.EvaluateCubicBezier(p[0], p[1], p[2], p[3], nextT);
             float segmentLength = Vector3.Distance(previousPoint, pointOnCurve);

            // If the segment is too short, simply advance t and the point
            if (segmentLength < spacing * 0.1f && nextT < 1.0f) // Avoid tiny steps except at the end
            {
                 // Adjust dt if we are taking very small steps repeatedly (optional optimization)
                 currentT = nextT;
                 previousPoint = pointOnCurve; // Update previous point even if no even point was added
                 continue; // Skip adding points for this tiny step
            }


             // Add evenly spaced points along this curve segment
             while (distanceSinceLastEvenPoint + segmentLength >= spacing)
             {
                 float overshootDist = (distanceSinceLastEvenPoint + segmentLength) - spacing;
                 float ratio = (segmentLength - overshootDist) / segmentLength; // Ratio along the segment where the new point lies
                 // Handle potential division by zero if segmentLength is tiny
                 if (segmentLength < Mathf.Epsilon) {
                     // Can't interpolate, break the inner loop for this step
                     distanceSinceLastEvenPoint += segmentLength; // Accumulate and move on
                     break;
                 }

                 Vector3 newEvenlySpacedPoint = Vector3.Lerp(previousPoint, pointOnCurve, ratio);

                 // Add point if distinct enough from the last added one
                 if (points.Count == 0 || Vector3.SqrMagnitude(newEvenlySpacedPoint - points.Last()) > 0.00001f)
                 {
                     points.Add(newEvenlySpacedPoint);
                 }
                 // Reset tracking for the next evenly spaced point
                 distanceSinceLastEvenPoint = 0;
                 previousPoint = newEvenlySpacedPoint; // New point becomes the reference
                 segmentLength = overshootDist; // Remaining length in this segment
             }

             // Accumulate remaining distance and update t and previousPoint
             distanceSinceLastEvenPoint += segmentLength;
             currentT = nextT;
             previousPoint = pointOnCurve; // Update overall previous point on curve

             // Adaptive step size adjustment (optional, can help with highly curved areas)
             // If segmentLength was large, we might need smaller dt next time. If small, larger dt.
             // float desiredSteps = segmentLength / spacing;
             // dt = Mathf.Clamp(dt * (desiredSteps > 0 ? (1.0f / desiredSteps) : 1.0f), 1.0f / (divisions * 10), 1.0f / 5); // Clamp dt bounds
         }


         // Final Check: Ensure the exact end point p[3] is included if it wasn't added
         // due to spacing, and if it's distinct enough.
         if (points.Count == 0 || Vector3.SqrMagnitude(points.Last() - p[3]) > 0.00001f) {
             points.Add(p[3]);
         } else if (points.Count > 0) {
             // If the last point is very close, replace it with p[3] for perfect match
             points[points.Count-1] = p[3];
         }

         return points;
    }


    /// <summary>
    /// Helper to find the index of the point in a list closest to a target position.
    /// Returns -1 if the list is null or empty.
    /// </summary>
    int FindClosestPointIndex(List<Vector3> points, Vector3 targetPos)
    {
        if (points == null || points.Count == 0) {
             // Debug.LogWarning("FindClosestPointIndex received an empty or null list.");
             return -1;
        }

        float minDistSqr = float.MaxValue;
        int closestIndex = 0; // Default to 0 if list has only one element

        for(int i=0; i<points.Count; i++) {
            float distSqr = (points[i] - targetPos).sqrMagnitude;
            if (distSqr < minDistSqr) {
                minDistSqr = distSqr;
                closestIndex = i;
            }
        }
        return closestIndex;
    }


    /// <summary>
    /// Calculates the forward direction at a point within a list of points by averaging neighbor directions.
    /// Handles endpoints and coincident points. Result is normalized.
    /// </summary>
    Vector3 CalculateForward(List<Vector3> points, int i) {
         if (points == null || points.Count < 1 || i < 0 || i >= points.Count) {
              // Return a default value, logging can be excessive here
              return Vector3.forward;
         }
         if (points.Count == 1) {
             return Vector3.forward; // Cannot determine direction from a single point
         }

         Vector3 forward = Vector3.zero;
         const float thresholdSqr = 0.00001f; // Use squared magnitude for efficiency

         // Direction FROM previous point (if exists and distinct)
         if (i > 0) {
              Vector3 dirToPrev = points[i] - points[i-1];
              if (dirToPrev.sqrMagnitude > thresholdSqr) {
                  forward += dirToPrev.normalized;
              }
         }
         // Direction TO next point (if exists and distinct)
         if (i < points.Count - 1) {
              Vector3 dirToNext = points[i+1] - points[i];
               if (dirToNext.sqrMagnitude > thresholdSqr) {
                  forward += dirToNext.normalized;
              }
         }

         // If calculated forward is near zero (e.g., start/end point, or coincident neighbors)
         if (forward.sqrMagnitude < thresholdSqr) {
             // Fallback 1: Use ONLY previous point direction if available
             if (i > 0) {
                  Vector3 dirToPrev = points[i] - points[i-1];
                  if (dirToPrev.sqrMagnitude > thresholdSqr) forward = dirToPrev.normalized;
             }
             // Fallback 2: Use ONLY next point direction if previous didn't work/exist
             else if (i < points.Count - 1) {
                 Vector3 dirToNext = points[i+1] - points[i];
                 if (dirToNext.sqrMagnitude > thresholdSqr) forward = dirToNext.normalized;
             }
             // Fallback 3: Use overall segment direction (if more than one point exists)
             if (forward.sqrMagnitude < thresholdSqr && points.Count >= 2) {
                 Vector3 overallDir = points.Last() - points.First();
                  if (overallDir.sqrMagnitude > thresholdSqr) forward = overallDir.normalized;
             }
              // Ultimate Fallback: Default to world forward
              if (forward.sqrMagnitude < thresholdSqr) {
                  forward = Vector3.forward;
              }
         } else {
            // If forward was calculated from neighbors, normalize the sum
            forward.Normalize();
         }

         return forward;
    }

    // UpdateTextureTiling is effectively obsolete as tiling is handled by UVs directly now.
    // void UpdateTextureTiling() { /* No longer needed */ }

    //=================================================
    // Editor Specific (Auto-Update & Debugging)
    //=================================================
    #if UNITY_EDITOR // Start of the EDITOR block

    // --- Member Variables for Debugging (Using consistent names) ---
    [System.NonSerialized] public Dictionary<Vector3, IntersectionGroupInfo> lastIntersectionGroupsForGizmo;
    [System.NonSerialized] private Dictionary<(int, int), List<RoadEndPointInfo>> lastRoadEndPointsForGizmo;
    // --- End Member Variables ---

    /// <summary>
    /// Called in the editor when inspector values change. Triggers auto-update if enabled.
    /// </summary>
     void OnValidate() {
         // Clamp values to prevent issues
         spacing = Mathf.Max(0.01f, spacing);
         intersectionCutback = Mathf.Max(0f, intersectionCutback);
         resolution = Mathf.Max(0.1f, resolution);

         if (autoUpdate && Application.isEditor && !Application.isPlaying) {
              // Use delayCall for safety, preventing updates during rapid changes or drags
              UnityEditor.EditorApplication.delayCall -= TriggerRoadUpdate; // Remove previous pending call
              UnityEditor.EditorApplication.delayCall += TriggerRoadUpdate; // Schedule a new call
         }
     }

    /// <summary>
    /// Helper function called by delayCall to execute UpdateRoad safely in the editor context.
    /// </summary>
     void TriggerRoadUpdate() {
         // Extra checks to ensure the component and its scene context are valid
         if (this != null && this.enabled && !Application.isPlaying &&
             gameObject.scene.isLoaded && // Check if the scene is loaded
             UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle() == UnityEditor.SceneManagement.StageUtility.GetMainStageHandle()) // Check if in main stage
             {
             // Ensure creator is valid (might be briefly null after component add/reset)
             if (creator == null) creator = GetComponent<PathCreator>();

             if (creator != null && creator.paths != null) { // Also check paths list
                 // Restore intersection data before update, as OnValidate might change serialized data
                 // Although usually PathEditor handles this, belt-and-suspenders.
                 foreach (var p in creator.paths) { if (p != null) p.RestoreIntersectionIndicesFromSerializedData(); }
                 UpdateRoad();
             }
         }
     }

    /// <summary>
    /// Stores the calculated intersection and endpoint data for use by OnDrawGizmos.
    /// </summary>
    void CacheDebugData(Dictionary<Vector3, IntersectionGroupInfo> groups) {
     lastIntersectionGroupsForGizmo = groups ?? new Dictionary<Vector3, IntersectionGroupInfo>();
     // Cache a *copy* of the endpoint cache list dictionary
     lastRoadEndPointsForGizmo = roadEndPointsCache != null
         // Create new dictionary, copying keys and creating new lists with copies of endpoint infos
         ? roadEndPointsCache.ToDictionary(kvp => kvp.Key, kvp => new List<RoadEndPointInfo>(kvp.Value))
         : new Dictionary<(int, int), List<RoadEndPointInfo>>();
 }
    /// <summary>
    /// Draws debug visualization for intersection boundaries and fans using Gizmos.
    /// Uses the cached data passed as parameters.
    /// </summary>

    // ... inside RoadPlacer class ...

      void OnDrawGizmos() {
       // Only draw gizmos if the component is active, flag is on, not playing, and creator exists
       if (drawDebugLines && this.enabled && !Application.isPlaying && creator != null) {
           // Check if cached data exists before drawing
           if(lastIntersectionGroupsForGizmo != null && lastRoadEndPointsForGizmo != null) {
                // Call the detailed drawing function with the cached data
                DrawIntersectionDebugGizmo(lastIntersectionGroupsForGizmo, lastRoadEndPointsForGizmo);
           } else {
                // Optional warning if data is missing when expected
                // Debug.LogWarning("[OnDrawGizmos] Cached Gizmo data is null, skipping detailed draw.");
           }
            // Optional: Draw path lines themselves if PathCreator doesn't
            // foreach (var path in creator.paths) { /* Draw path lines */ }
       }
    }

    /// <summary>
    /// Draws debug visualization for intersection boundaries, fan lines, and numbered perimeter points.
    /// Sorts endpoints relative to center. Matches the two-triangle-per-endpoint triangulation visually.
    /// </summary>
    void DrawIntersectionDebugGizmo(Dictionary<Vector3, IntersectionGroupInfo> intersectionGroups, Dictionary<(int, int), List<RoadEndPointInfo>> endpoints)
    {
        // --- Draw Endpoint Boundary Lines (Original L/R lines) ---
        Gizmos.color = roadEndpointColor;
        if (endpoints != null) {
             foreach (var endpointInfoList in endpoints.Values) {
                 foreach(var endpointInfo in endpointInfoList) {
                      Gizmos.DrawLine(endpointInfo.LeftVertex, endpointInfo.RightVertex);
                      Gizmos.DrawSphere(endpointInfo.LeftVertex, 0.05f);
                      Gizmos.DrawSphere(endpointInfo.RightVertex, 0.05f);
                 }
             }
        }

        // --- Prepare for Labels ---
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontSize = 12;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        // --- Draw Intersection Fan Lines & Labels based on sorted perimeter ---
        Handles.color = intersectionEdgeColor; // Color for fan lines and labels

        if (intersectionGroups != null)
        {
             foreach (var group in intersectionGroups.Values) {
                 // --- Gather endpoints & Calculate Center ---
                 List<RoadEndPointInfo> currentEndPoints = new List<RoadEndPointInfo>();
                 Vector3 centerSum = Vector3.zero; int validEndpointsFound = 0;
                 foreach(var connTuple in group.Connections) {
                      if(endpoints != null && endpoints.TryGetValue(connTuple, out var epList)) {
                           currentEndPoints.AddRange(epList);
                           foreach(var ep in epList) { centerSum += ep.CenterPoint; validEndpointsFound++; }
                      }
                 }
                 if(validEndpointsFound < 2) continue; // Not enough data for an intersection
                 Vector3 center = centerSum / validEndpointsFound;

                 // --- Create List of ALL Perimeter Vertices ---
                 List<Vector3> allPerimeterVertices = new List<Vector3>();
                 foreach (var ep in currentEndPoints) { // Use the unsorted endpoints list just to gather points
                     allPerimeterVertices.Add(ep.LeftVertex);
                     allPerimeterVertices.Add(ep.RightVertex);
                 }
                 if (allPerimeterVertices.Count < 3) continue; // Need 3+ for gizmo lines

                 // --- Sort the Combined Perimeter Vertices by Angle ---
                 // This list now dictates the drawing order
                  try {
                     allPerimeterVertices = allPerimeterVertices.OrderBy<Vector3, float>(p => {
                          Vector3 relativePos = p - center;
                          if (relativePos.sqrMagnitude < 0.00001f) return -Mathf.PI; // Handle point at center
                          return Mathf.Atan2(relativePos.z, relativePos.x); // Sort by angle
                      }).ToList();
                 } catch (System.Exception ex) {
                      Debug.LogError($"[RoadPlacer Gizmo] Error during PERIMETER vertex sorting for group at {group.CenterPosition}: {ex.Message}", this);
                      continue; // Skip drawing if sort fails
                 }

                 // --- Draw Gizmos using the SORTED perimeter vertices ---
                 Handles.SphereHandleCap(0, center, Quaternion.identity, 0.07f, EventType.Repaint); // Draw center

                 // Draw fan lines and labels based on the sorted vertex list
                 for(int i = 0; i < allPerimeterVertices.Count; i++)
                 {
                     Vector3 currentPerimPoint = allPerimeterVertices[i]; // Use the sorted list
                     Vector3 nextPerimPoint = allPerimeterVertices[(i + 1) % allPerimeterVertices.Count]; // Wrap around using sorted list

                     // Draw line from center to current perimeter point
                     Handles.DrawLine(center, currentPerimPoint);
                     // Draw line connecting current perimeter point to next along the sorted perimeter
                     Handles.DrawLine(currentPerimPoint, nextPerimPoint);

                     // --- Draw Label with Number ---
                     // Offset label for visibility
                     Vector3 labelOffset = Vector3.up * 0.1f + (currentPerimPoint - center).normalized * 0.15f;
                     Handles.Label(currentPerimPoint + labelOffset, $"{i}", labelStyle); // Draw index 'i' reflecting the sorted order
                 }
                 // --- End Draw Gizmos ---

             } // End foreach group
        } // End if intersectionGroups != null
    } // End DrawIntersectionDebugGizmo
#endif // End Editor block
    // --- END Editor Specific ---

} // End of RoadPlacer class