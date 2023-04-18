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

    private List<Vector2> crossroadVertices;

    public Material roadMaterial;

    public void UpdateCrossroad()
    {
        Crossroad crossroad = GetComponent<CrossroadCreator>().crossroad;
        List<Vector2[]> list = new List<Vector2[]>();

        crossroadVertices = new List<Vector2>();

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
    }

    private Mesh CreateCrossroadMesh()
    {
        Vector3[] verticesNoCenter = ConvertToVector3(crossroadVertices.ToArray());
        Vector3[] vertices = new Vector3[1 + verticesNoCenter.Length];
        vertices[0] = this.transform.position;
        verticesNoCenter.CopyTo(vertices, 1);

        //vert 9
        //tri 8 * 3

        int[] triangles = new int[(vertices.Length - 1) * 3];

        Mesh mesh = new Mesh();

        // Generate triangles
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

        Debug.Log(triangles.Length);
        Debug.Log(vertices.Length);

        // triangles[triangleIndex] = vertices.Length;
        // triangles[triangleIndex + 1] = 0;
        // triangles[triangleIndex + 2] = 1;

        // Debug.DrawLine(vertices[0],vertices[1], Color.cyan, 10f);
        // Debug.DrawLine(vertices[1],vertices[2], Color.magenta, 10f);
        // Debug.DrawLine(vertices[2],vertices[3], Color.yellow, 10f);
        // Debug.DrawLine(vertices[3],vertices[4], Color.black, 10f);
        // Debug.DrawLine(vertices[4],vertices[5], Color.red, 10f);
        // Debug.DrawLine(vertices[5],vertices[6], Color.green, 10f);
        // Debug.DrawLine(vertices[6],vertices[7], Color.blue, 10f);
        // Debug.DrawLine(vertices[7],vertices[0], Color.white, 10f);

        // Set the vertices and triangles to the mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;

        return mesh;
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
        
        crossroadVertices.Add(vertices[1]);
        crossroadVertices.Add(vertices[0]);
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }
}
