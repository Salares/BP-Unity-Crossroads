using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(CrossroadCreator))]
public class CrossroadPlacer : MonoBehaviour
{
    [Header("Crossroad Mesh Options")]
    [Range(0.05f, 3f)] public float roadWidth = 1f;
    [Range(.005f, 0.5f)] public float spacing = 0.1f;
    [Range(1f, 1000f)] public float tiling = 10;

    public Material roadMaterial;
    public Material crossroadMaterial;

    private List<Vector3> crossroadVerticesV2;
    private List<Vector3> guideVerticesV2;

    public void UpdateCrossroad()
    {
        CrossroadCreator crossroadCreator = GetComponent<CrossroadCreator>();
        if (crossroadCreator == null)
        {
            Debug.LogError("CrossroadCreator component is missing on " + gameObject.name + ". Cannot update crossroad.");
            return;
        }

        Crossroad crossroad = crossroadCreator.crossroad;
        List<Vector3[]> list = new List<Vector3[]>();

        crossroadVerticesV2 = new List<Vector3>();
        guideVerticesV2 = new List<Vector3>();

        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            DestroyImmediate(transform.GetChild(j).gameObject);
        }

        int i = 0;
        foreach (Path path in crossroad)
        {
            Vector3[] points = path.CalculateEvenlySpacedPoints(spacing);
            Debug.DrawLine(points[0], points[1], Color.blue, 10f); // Directly use Vector3 points

            GameObject meshObject = new GameObject("Road " + i);

            meshObject.transform.SetParent(this.transform);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

            int textureRepeat = Mathf.RoundToInt(tiling * points.Length * spacing * 0.05f);
            if (roadMaterial != null)
            {
                meshRenderer.sharedMaterial = roadMaterial;
                meshRenderer.sharedMaterial.mainTextureScale = new Vector2(1, textureRepeat);
            }
            else
            {
                Debug.LogWarning("Road Material is not assigned in the inspector for " + gameObject.name);
            }

            meshFilter.mesh = CreateRoadMesh(points, false); // Pass Vector3[]

            i += 1;
        }

        Debug.Log("crossroadVerticesV2 count: " + crossroadVerticesV2.Count);
        Debug.Log("guideVerticesV2 count: " + guideVerticesV2.Count);

        GameObject crossroadObject = new GameObject("Crossroad");
        crossroadObject.transform.SetParent(this.transform);
        MeshFilter crossroadMeshFilter = crossroadObject.AddComponent<MeshFilter>();
        MeshRenderer crossroadMeshRenderer = crossroadObject.AddComponent<MeshRenderer>();
        crossroadMeshFilter.mesh = CreateCrossroadMesh();
        crossroadMeshRenderer.sharedMaterial = crossroadMaterial;
    }

    private Mesh CreateCrossroadMesh()
    {
        Vector3[] verticesNoCenter = crossroadVerticesV2.ToArray(); // No need for ConvertToVector3
        Vector3[] guideVertices = guideVerticesV2.ToArray(); // No need for ConvertToVector3

        Debug.Log("verticesNoCenter length: " + verticesNoCenter.Length);
        Debug.Log("guideVertices length: " + guideVertices.Length);

        if (verticesNoCenter.Length == 0 || verticesNoCenter.Length % 2 != 0)
        {
            Debug.LogError("Crossroad vertices are not valid. Cannot create crossroad mesh.");
            return new Mesh(); // Return an empty mesh to prevent crash
        }

        Vector3 center = this.transform.position;

        int vertLength = verticesNoCenter.Length;
        int guideLength = guideVertices.Length;

        List<Vector3[]> splineList = new List<Vector3[]>();

        for (int i = 1; i < verticesNoCenter.Length; i += 2)
        {
            Vector3 a = verticesNoCenter[i];
            Vector3 b = guideVertices[i];
            Vector3 c = verticesNoCenter[(i + 1) % vertLength];
            Vector3 d = guideVertices[(i + 1) % guideLength];

            Vector3 intersectionPoint = CrossroadTools.CalculateIntersection(b, a, d, c);

            List<Vector3> pointsList = new List<Vector3>
            {
                a,
                intersectionPoint,
                c
            };

            Vector3[] points = pointsList.ToArray();
            Vector3[] spline = CalculateIntersectionSplines(points, 0.1f);

            splineList.Add(spline);
        }

        List<Vector3> verticesList = new List<Vector3>();
        verticesList.Add(center);

        foreach (Vector3[] spline in splineList)
        {
            foreach (Vector3 point in spline)
            {
                verticesList.Add(point);
            }
        }

        Vector3[] vertices = verticesList.ToArray();
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(vertices.Length - 1) * 3];

        for (int i = 0; i < vertices.Length; i++)
        {
            // Assuming UV mapping is based on distance from center and angle in XZ plane
            Vector3 flatVertex = new Vector3(vertices[i].x, 0, vertices[i].z);
            Vector3 flatCenter = new Vector3(center.x, 0, center.z);
            Vector3 direction = (flatVertex - flatCenter).normalized;
            float angle = Vector3.SignedAngle(Vector3.forward, direction, Vector3.up);
            if (angle < 0) angle += 360;

            Vector2 coordinate = new Vector2(Vector3.Distance(flatVertex, flatCenter), angle / 360f);
            uvs[i] = coordinate;
        }
        int triangleIndex = 0;
        for (int i = 1; i < vertices.Length - 1; i++)
        {
            triangles[triangleIndex] = i;
            triangles[triangleIndex + 1] = 0;
            triangles[triangleIndex + 2] = i + 1;
            triangleIndex += 3;
        }

        triangles[triangleIndex] = vertices.Length - 1;
        triangles[triangleIndex + 1] = 0;
        triangles[triangleIndex + 2] = 1;

        Mesh mesh = new Mesh();

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        return mesh;
    }

    private float AngleBetweenVectors(Vector3 a, Vector3 b)
    {
        return Mathf.Acos(Vector3.Dot(a.normalized, b.normalized)) * Mathf.Rad2Deg;
    }

    public Vector3[] CalculateIntersectionSplines(Vector3[] points, float spacing, float resolution = 1)
    {
        List<Vector3> evenlySpacedPoints = new List<Vector3>();
        evenlySpacedPoints.Add(points[0]);
        Vector3 previousPoint = points[0];
        float distanceSinceLastEvenPoint = 0;

        float controlNetLength =
            Vector3.Distance(points[0], points[1]) +
            Vector3.Distance(points[1], points[2]);

        float estimatedCurveLength = Vector3.Distance(points[0], points[2]) + controlNetLength / 2;
        int divisions = Mathf.CeilToInt(estimatedCurveLength * resolution * 10);
        float interval = 1f / divisions;

        float time = 0;
        while (time <= 1)
        {
            time += interval;
            Vector3 pointOnCurve = Bezier.EvaluateQuadratic(points[0], points[1], points[2], time);

            while (distanceSinceLastEvenPoint >= spacing)
            {
                float exceedence = distanceSinceLastEvenPoint - spacing;
                Vector3 newEvenlySpacedPoint = pointOnCurve + (previousPoint - pointOnCurve).normalized * exceedence;

                evenlySpacedPoints.Add(newEvenlySpacedPoint);

                distanceSinceLastEvenPoint = exceedence;
                previousPoint = newEvenlySpacedPoint;
            }

            distanceSinceLastEvenPoint += Vector3.Distance(previousPoint, pointOnCurve);
            previousPoint = pointOnCurve;
        }

        evenlySpacedPoints.Add(points[points.Length - 1]);

        return evenlySpacedPoints.ToArray();
    }


    // ConvertToVector3 is no longer needed as points are already Vector3
    /*
    public Vector3[] ConvertToVector3(Vector2[] vertices)
    {
        Vector3[] vertices3D = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices3D[i] = new Vector3(vertices[i].x, 0f, vertices[i].y);
        }

        return vertices3D;
    }
    */

    Mesh CreateRoadMesh(Vector3[] points, bool isClosed) // Accept Vector3[]
    {
        int numberOfTriangles = 2 * (points.Length - 1) + ((isClosed) ? 2 : 0);

        Vector3[] vertices = new Vector3[points.Length * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(numberOfTriangles) * 3];

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 forward = Vector3.zero;
            if (i < points.Length - 1 || isClosed)
            {
                forward += points[(i + 1) % points.Length] - points[i]; // Use Vector3
            }

            if (i > 0 || isClosed)
            {
                forward += points[i] - points[(i - 1 + points.Length) % points.Length]; // Use Vector3
            }
            forward.Normalize();

            Vector3 left = new Vector3(-forward.z, 0, forward.x); // Calculate left for XZ plane

            vertices[vertexIndex] = points[i] + left * roadWidth * .5f; // Use Vector3
            vertices[vertexIndex + 1] = points[i] - left * roadWidth * .5f; // Use Vector3

            float completionPercent = i / (float)(points.Length - 1);
            float v = 1 - Mathf.Abs(2 * completionPercent - 1);
            uvs[vertexIndex] = new Vector2(0, v);
            uvs[vertexIndex + 1] = new Vector2(1, v);

            if (i < points.Length - 1 || isClosed)
            {
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = (vertexIndex + 2) % vertices.Length;
                triangles[triangleIndex + 2] = vertexIndex + 1;

                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = (vertexIndex + 2) % vertices.Length;
                triangles[triangleIndex + 5] = (vertexIndex + 3) % vertices.Length;
            }

            vertexIndex += 2;
            triangleIndex += 6;
        }
        Mesh mesh = new Mesh();

        crossroadVerticesV2.Add(vertices[1]); // Add Vector3 directly
        crossroadVerticesV2.Add(vertices[0]); // Add Vector3 directly
        guideVerticesV2.Add(vertices[3]); // Add Vector3 directly
        guideVerticesV2.Add(vertices[2]); // Add Vector3 directly

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }

   public void BakeCrossroad()
{
    Debug.Log("BakeCrossroad called");

    // Must initialize BEFORE calling UpdateCrossroad to avoid clearing important data
    crossroadVerticesV2 = new List<Vector3>(); // Changed to Vector3
    guideVerticesV2 = new List<Vector3>(); // Changed to Vector3

    UpdateCrossroad(); // Regenerate procedural mesh

    MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
    Debug.Log("Found " + meshFilters.Length + " MeshFilters in children.");
    List<CombineInstance> combine = new List<CombineInstance>();
    List<Material> materials = new List<Material>();

    Matrix4x4 parentWorldToLocal = transform.worldToLocalMatrix;

    foreach (MeshFilter meshFilter in meshFilters)
    {
        if (meshFilter.sharedMesh == null) continue;

        Debug.Log("Checking MeshFilter: " + meshFilter.gameObject.name);
        if (meshFilter.gameObject.name.StartsWith("Road"))
        {
            CombineInstance combineInstance = new CombineInstance();
            combineInstance.mesh = meshFilter.sharedMesh;
            combineInstance.transform = parentWorldToLocal * meshFilter.transform.localToWorldMatrix;
            combine.Add(combineInstance);

            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                materials.Add(meshRenderer.sharedMaterial);
            }
        }
    }

    Mesh combinedRoadMesh = new Mesh();
    try
    {
        combinedRoadMesh.CombineMeshes(combine.ToArray(), false, true); // keep submeshes
        Debug.Log("Combined road meshes successfully.");
    }
    catch (Exception e)
    {
        Debug.LogError("Error combining road meshes: " + e.Message);
        return;
    }

    // Find crossroad mesh
    Transform crossroadObject = null;
    foreach (Transform child in transform)
    {
        if (child.name == "Crossroad")
        {
            crossroadObject = child;
            break;
        }
    }

    if (crossroadObject == null)
    {
        Debug.LogError("No Crossroad object found.");
        return;
    }

    MeshFilter crossroadMeshFilter = crossroadObject.GetComponent<MeshFilter>();
    MeshRenderer crossroadRenderer = crossroadObject.GetComponent<MeshRenderer>();

    if (crossroadMeshFilter == null || crossroadMeshFilter.sharedMesh == null)
    {
        Debug.LogError("Crossroad MeshFilter missing or empty.");
        return;
    }

    // Add the crossroad mesh
    CombineInstance[] combineAll = new CombineInstance[combine.Count + 1];
    for (int i = 0; i < combine.Count; i++)
    {
        combineAll[i] = combine[i];
    }

    combineAll[combine.Count] = new CombineInstance
    {
        mesh = crossroadMeshFilter.sharedMesh,
        transform = parentWorldToLocal * crossroadMeshFilter.transform.localToWorldMatrix
    };

    if (crossroadRenderer != null)
    {
        materials.Add(crossroadRenderer.sharedMaterial);
    }

    Mesh combinedMesh = new Mesh();
    try
    {
        combinedMesh.CombineMeshes(combineAll, false, true); // keep submeshes/materials
        Debug.Log("Combined road + crossroad mesh.");
    }
    catch (Exception e)
    {
        Debug.LogError("Final combine failed: " + e.Message);
        return;
    }

    // Create the baked object
    GameObject bakedObject = new GameObject("BakedCrossroad");
    bakedObject.transform.position = transform.position;
    bakedObject.transform.rotation = transform.rotation;
    bakedObject.transform.localScale = transform.localScale;

    MeshFilter bakedMeshFilter = bakedObject.AddComponent<MeshFilter>();
    MeshRenderer bakedMeshRenderer = bakedObject.AddComponent<MeshRenderer>();

    bakedMeshFilter.mesh = combinedMesh;
    bakedMeshRenderer.sharedMaterials = materials.ToArray(); // support multiple materials

    // Clean up
    for (int j = transform.childCount - 1; j >= 0; j--)
    {
        DestroyImmediate(transform.GetChild(j).gameObject);
    }

    Debug.Log("BakeCrossroad finished.");
}


}
