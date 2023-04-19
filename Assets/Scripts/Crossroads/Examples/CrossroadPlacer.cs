using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(CrossroadCreator))] 
public class CrossroadPlacer : MonoBehaviour
{
    [Header("Crossroad Mesh Options")]
    [Range(0.05f, 3f)]public float roadWidth = 1f;
    [Range(.005f, 0.5f)] public float spacing = 1f;

    [Range(1f, 1000f)] public float tiling = 1;

    private List<Vector2> crossroadVerticesV2;
    private List<Vector2> guideVerticesV2;

    public Material roadMaterial;
    public Material crossroadMaterial;

    public void UpdateCrossroad()
    {
        Crossroad crossroad = GetComponent<CrossroadCreator>().crossroad;
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
            Debug.DrawLine(points[0],points[1], Color.blue, 10f);

            GameObject meshObject = new GameObject("Road " + i);

            meshObject.transform.SetParent(this.transform);

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();

            int textureRepeat = Mathf.RoundToInt(tiling * points.Length * spacing * 0.05f);
            meshRenderer.sharedMaterial = roadMaterial;
            meshRenderer.sharedMaterial.mainTextureScale = new Vector2(1, textureRepeat);

            meshFilter.mesh = CreateRoadMesh(points, false);

            i+=1;
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
        
        for (int i = 1 ; i < verticesNoCenter.Length ; i+=2) 
        {
            Vector3 a = verticesNoCenter[i];
            Vector3 b = guideVertices[i];
            Vector3 c = verticesNoCenter[(i+1)%vertLength];
            Vector3 d = guideVertices[(i+1)%guideLength];

            Vector3 intersectionPoint = CrossroadTools.CalculateIntersection(b,a,d,c);

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
            Vector2 coordinate = new Vector2(Vector3.Distance(vertices[i], center), AngleBetweenVectors(vertices[i],Vector3.up)/360f);
            uvs[i] = coordinate;
            Debug.Log(coordinate);
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

        // Set the vertices and triangles to the mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        return mesh;
    }

    public Vector2[] NormalizeVector2Array(Vector2[] vectorArray)
    {
        for (int i = 0; i < vectorArray.Length; i++)
        {
            vectorArray[i] = vectorArray[i].normalized;
        }

        return vectorArray;
    }

    private float AngleBetweenVectors(Vector3 a, Vector3 b)
    {
        return Mathf.Acos(Vector3.Dot(a.normalized, b.normalized)) * Mathf.Rad2Deg;
    }

    private Vector2[] CalculateUVs(Vector3[] vertices, Vector3 center)
    {
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertexDirection = vertices[i] - center;
            float angle = AngleBetweenVectors(vertexDirection, Vector3.right);
            uvs[i] = new Vector2(angle / 360f, 0f);
        }

        return uvs;
    }

    public Vector3[] CalculateIntersectionSplines(Vector3[] points, float spacing, float resolution = 1)
    {
        List<Vector3> evenlySpacedPoints = new List<Vector3>();
        evenlySpacedPoints.Add(points[0]);
        Vector3 previousPoint = points[0];
        float distanceSinceLastEvenPoint = 0;
       
        float controlNetLength = 
            Vector3.Distance(points[0],points[1]) + 
            Vector3.Distance(points[1],points[2]);

        float estimatedCurveLength =  Vector3.Distance(points[0],points[2]) + controlNetLength / 2;
        int divisions = Mathf.CeilToInt(estimatedCurveLength * resolution * 10);
        float interval = 1f/divisions;

        float time = 0;
        while(time <= 1)
        {
            time += interval;
            Vector3 pointOnCurve = Bezier.EvaluateQuadratic(points[0],points[1],points[2], time);
            
            while(distanceSinceLastEvenPoint >= spacing)
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

        evenlySpacedPoints.Add(points[points.Length-1]);
        
        return evenlySpacedPoints.ToArray();
    }


    public Vector3[] ConvertToVector3(Vector2[] vertices)
    {
        Vector3[] vertices3D = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices3D[i] = new Vector3(vertices[i].x, vertices[i].y, 0f);
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
            Vector2 forward = Vector2.zero;
            if(i < points.Length - 1 || isClosed)
            {
                forward += points[(i+1) % points.Length] - points[i];
            }

            if(i > 0 || isClosed)
            {
                forward += points[i] - points[(i-1 + points.Length) % points.Length];
            }
            forward.Normalize();

            Vector2 left = new Vector2(-forward.y, forward.x);

            vertices[vertexIndex] = points[i] + left * roadWidth *.5f;
            vertices[vertexIndex + 1] = points[i] - left * roadWidth *.5f;

            float completionPercent = i / (float)(points.Length-1);
            float v = 1 - Mathf.Abs(2 * completionPercent - 1);
            uvs[vertexIndex] = new Vector2(0,v);
            uvs[vertexIndex + 1] = new Vector2(1, v);

            if(i < points.Length - 1 || isClosed)
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
        
        crossroadVerticesV2.Add(vertices[1]);
        crossroadVerticesV2.Add(vertices[0]);
        guideVerticesV2.Add(vertices[3]);
        guideVerticesV2.Add(vertices[2]);

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }
}
