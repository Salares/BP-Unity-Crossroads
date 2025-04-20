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
    [Range(.005f, 0.5f)] public float spacing = 1f;
    [Range(1f, 1000f)] public float tiling = 1;

    public Material roadMaterial;
    public Material crossroadMaterial;

    private List<Vector2> crossroadVerticesV2;
    private List<Vector2> guideVerticesV2;

    public void UpdateCrossroad()
    {
        Crossroad crossroad = GetComponent<CrossroadCreator>().crossroad;
        if (crossroad == null)
        {
            Debug.LogWarning("CrossroadPlacer.UpdateCrossroad: Crossroad is null. Mesh generation skipped.");
            return;
        }

        List<Vector2[]> list = new List<Vector2[]>();

        crossroadVerticesV2 = new List<Vector2>();
        guideVerticesV2 = new List<Vector2>();

        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            DestroyImmediate(transform.GetChild(j).gameObject);
        }

        int i = 0;
        foreach (Path path in crossroad)
        {
            Vector2[] points = path.CalculateEvenlySpacedPoints(spacing);
            if (points.Length >= 2)
            {
                Debug.DrawLine(new Vector3(points[0].x, 0, points[0].y), new Vector3(points[1].x, 0, points[1].y), Color.blue, 10f);

                GameObject meshObject = new GameObject("Road " + i);

                meshObject.transform.SetParent(this.transform);

                MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

                int textureRepeat = Mathf.RoundToInt(tiling * points.Length * spacing * 0.05f);
                meshRenderer.sharedMaterial = roadMaterial;
                meshRenderer.sharedMaterial.mainTextureScale = new Vector2(1, textureRepeat);

                meshFilter.mesh = CreateRoadMesh(points, false);

                i += 1;
            }
            else
            {
                Debug.LogWarning("CrossroadPlacer.UpdateCrossroad: Not enough points to create road mesh for path " + i);
            }
        }

        GameObject crossroadObject = new GameObject("Crossroad");
        crossroadObject.transform.SetParent(this.transform);
        MeshFilter crossroadMeshFilter = crossroadObject.AddComponent<MeshFilter>();
        MeshRenderer crossroadMeshRenderer = crossroadObject.AddComponent<MeshRenderer>();
        crossroadMeshFilter.mesh = CreateCrossroadMesh();
        crossroadMeshRenderer.sharedMaterial = crossroadMaterial;
    }

    private Mesh CreateCrossroadMesh()
    {
        Vector3[] verticesNoCenter = ConvertToVector3(crossroadVerticesV2.ToArray());
        Vector3[] guideVertices = ConvertToVector3(guideVerticesV2.ToArray());

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

        // Safety check: need at least 3 vertices to form a mesh
        if (vertices.Length < 3)
        {
            Debug.LogWarning("CrossroadPlacer.CreateCrossroadMesh: Not enough vertices to create crossroad mesh. Need at least 3, got " + vertices.Length);
            return new Mesh();
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(vertices.Length - 1) * 3];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 coordinate = new Vector2(Vector3.Distance(vertices[i], center), AngleBetweenVectors(vertices[i], Vector3.forward) / 360f);
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


    public Vector3[] ConvertToVector3(Vector2[] vertices)
    {
        Vector3[] vertices3D = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices3D[i] = new Vector3(vertices[i].x, 0f, vertices[i].y);
        }

        return vertices3D;
    }

    Mesh CreateRoadMesh(Vector2[] points, bool isClosed)
    {
        int numberOfTriangles = 2 * (points.Length - 1) + ((isClosed) ? 2 : 0);

        Vector3[] vertices = new Vector3[points.Length * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(numberOfTriangles) * 3];

        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int i = 0; i < points.Length; i++)
        {
            // Transform the 2D point to world space using the intersection object's transform
            Vector3 worldPoint = transform.TransformPoint(new Vector3(points[i].x, 0, points[i].y));

            Vector3 forward = Vector3.zero;
            if (i < points.Length - 1 || isClosed)
            {
                Vector3 nextWorldPoint = transform.TransformPoint(new Vector3(points[(i + 1) % points.Length].x, 0, points[(i + 1) % points.Length].y));
                forward += nextWorldPoint - worldPoint;
            }

            if (i > 0 || isClosed)
            {
                Vector3 previousWorldPoint = transform.TransformPoint(new Vector3(points[(i - 1 + points.Length) % points.Length].x, 0, points[(i - 1 + points.Length) % points.Length].y));
                forward += worldPoint - previousWorldPoint;
            }
            forward.Normalize();

            Vector3 left = new Vector3(-forward.z, 0, forward.x);

            vertices[vertexIndex] = worldPoint + left * roadWidth * .5f;
            vertices[vertexIndex + 1] = worldPoint - left * roadWidth * .5f;

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

        crossroadVerticesV2.Add(new Vector2(vertices[1].x, vertices[1].z));
        crossroadVerticesV2.Add(new Vector2(vertices[0].x, vertices[0].z));
        guideVerticesV2.Add(new Vector2(vertices[3].x, vertices[3].z));
        guideVerticesV2.Add(new Vector2(vertices[2].x, vertices[2].z));

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }

   public void BakeCrossroad()
{
    Debug.Log("BakeCrossroad called");

    // Must initialize BEFORE calling UpdateCrossroad to avoid clearing important data
    crossroadVerticesV2 = new List<Vector2>();
    guideVerticesV2 = new List<Vector2>();

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
