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

    private List<Vector3> crossroadVerticesV2;
    private List<Vector3> guideVerticesV2;

    public void UpdateCrossroad()
    {
        Crossroad crossroad = GetComponent<CrossroadCreator>().crossroad;

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

            GameObject meshObject = new GameObject("Road " + i);
            meshObject.transform.SetParent(this.transform);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

            int textureRepeat = Mathf.RoundToInt(tiling * points.Length * spacing * 0.05f);
            meshRenderer.sharedMaterial = roadMaterial;
            meshRenderer.sharedMaterial.mainTextureScale = new Vector3(1, textureRepeat);

            bool collect = true;
            meshFilter.mesh = CreateRoadMesh(points, false, collect);

            i += 1;
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

        List<(Vector3 a, Vector3 b, float angle)> segments = new();

        for (int i = 0; i < verticesNoCenter.Length; i++)
        {
            Vector3 edge = verticesNoCenter[i];
            Vector3 guide = guideVertices[i];
            Vector2 dir = new Vector2(edge.x - center.x, edge.z - center.z);
            float angle = Mathf.Atan2(dir.y, dir.x);
            if (angle < 0) angle += 2 * Mathf.PI;

            segments.Add((edge, guide, angle));
        }

        // Sort all segments by angle around the center
        segments.Sort((s1, s2) => s1.angle.CompareTo(s2.angle));

        List<Vector3[]> splineList = new List<Vector3[]>();

        for (int i = 0; i < segments.Count; i++)
        {
            int next = (i + 1) % segments.Count;

            Vector3 a = segments[i].a;
            Vector3 b = segments[i].b;
            Vector3 c = segments[next].a;
            Vector3 d = segments[next].b;

            Vector3 intersectionPoint = CrossroadTools.CalculateIntersection(b, a, d, c);

            Vector3[] spline = CalculateIntersectionSplines(new Vector3[] { a, intersectionPoint, c }, 0.1f);
            splineList.Add(spline);

            Debug.DrawLine(a, b, Color.red, 10f);
            Debug.DrawLine(c, d, Color.blue, 10f);
            Debug.DrawRay(intersectionPoint, Vector3.up * 2, Color.green, 10f);
        }

        List<Vector3> verticesList = new List<Vector3> { center };
        foreach (Vector3[] spline in splineList)
            verticesList.AddRange(spline);

        Vector3[] vertices = verticesList.ToArray();
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(vertices.Length - 1) * 3];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 dir = new Vector2(vertices[i].x - center.x, vertices[i].z - center.z);
            float angle = Mathf.Atan2(dir.y, dir.x);
            if (angle < 0) angle += 2 * Mathf.PI;
            uvs[i] = new Vector2(dir.magnitude, angle / (2 * Mathf.PI));
        }

        int triangleIndex = 0;
        for (int i = 1; i < vertices.Length - 1; i++)
        {
            triangles[triangleIndex++] = i;
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i + 1;
        }
        triangles[triangleIndex++] = vertices.Length - 1;
        triangles[triangleIndex++] = 0;
        triangles[triangleIndex++] = 1;

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
        List<Vector3> evenlySpacedPoints = new List<Vector3> { points[0] };
        Vector3 previousPoint = points[0];
        float distanceSinceLastEvenPoint = 0;

        float controlNetLength = Vector3.Distance(points[0], points[1]) + Vector3.Distance(points[1], points[2]);
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

    public Vector3[] ConvertToVector3(Vector3[] vertices)
    {
        Vector3[] vertices3D = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices3D[i] = new Vector3(vertices[i].x, vertices[i].y, vertices[i].z);
        }
        return vertices3D;
    }

    Mesh CreateRoadMesh(Vector3[] points, bool isClosed, bool collectForCrossroad = false)
{
    int numberOfTriangles = 2 * (points.Length - 1) + (isClosed ? 2 : 0);

    Vector3[] vertices = new Vector3[points.Length * 2];
    Vector2[] uvs = new Vector2[vertices.Length];
    int[] triangles = new int[numberOfTriangles * 3];

    int vertexIndex = 0;
    int triangleIndex = 0;

    for (int i = 0; i < points.Length; i++)
    {
        Vector3 forward = Vector3.zero;
        if (i < points.Length - 1 || isClosed)
            forward += points[(i + 1) % points.Length] - points[i];
        if (i > 0 || isClosed)
            forward += points[i] - points[(i - 1 + points.Length) % points.Length];
        forward.Normalize();

        Vector3 left = Vector3.Cross(forward, Vector3.up).normalized;

        Vector3 leftPoint = points[i] + left * roadWidth * 0.5f;
        Vector3 rightPoint = points[i] - left * roadWidth * 0.5f;

        vertices[vertexIndex] = leftPoint;
        vertices[vertexIndex + 1] = rightPoint;

        float completionPercent = i / (float)(points.Length - 1);
        float v = 1 - Mathf.Abs(2 * completionPercent - 1);
        uvs[vertexIndex] = new Vector2(0, v);
        uvs[vertexIndex + 1] = new Vector2(1, v);

        if (i < points.Length - 1 || isClosed)
        {
            triangles[triangleIndex++] = vertexIndex;
            triangles[triangleIndex++] = (vertexIndex + 2) % vertices.Length;
            triangles[triangleIndex++] = vertexIndex + 1;

            triangles[triangleIndex++] = vertexIndex + 1;
            triangles[triangleIndex++] = (vertexIndex + 2) % vertices.Length;
            triangles[triangleIndex++] = (vertexIndex + 3) % vertices.Length;
        }

        if (collectForCrossroad && (i == 0 || i == points.Length - 1))
        {
            crossroadVerticesV2.Add(leftPoint);
            crossroadVerticesV2.Add(rightPoint);
            guideVerticesV2.Add(leftPoint + forward);
            guideVerticesV2.Add(rightPoint + forward);
        }

        vertexIndex += 2;
    }

    Mesh mesh = new Mesh();
    mesh.vertices = vertices;
    mesh.uv = uvs;
    mesh.triangles = triangles;
    return mesh;
}

}
