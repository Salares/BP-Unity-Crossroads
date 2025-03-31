using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PathCreator))] 
[RequireComponent(typeof(MeshFilter))] 
[RequireComponent(typeof(MeshRenderer))] 
public class RoadPlacer : MonoBehaviour
{
    public float roadWidth = 1f;
    [Range(.05f, 2.5f)] public float spacing = 1f;
    
    public bool autoUpdate = false;

    public float tiling = 1;

    public void UpdateRoad()
    {
        Path path = GetComponent<PathCreator>().path;
        Vector3[] points = path.CalculateEvenlySpacedPoints(spacing);
        GetComponent<MeshFilter>().mesh = CreateRoadMesh(points, path.IsClosed);

        int textureRepeat = Mathf.RoundToInt(tiling * points.Length * spacing * 0.05f);
        GetComponent<MeshRenderer>().sharedMaterial.mainTextureScale = new Vector3(1, textureRepeat);
    }

    Mesh CreateRoadMesh(Vector3[] points, bool isClosed)
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
            if(i < points.Length - 1 || isClosed)
            {
                forward += points[(i+1) % points.Length] - points[i];
            }

            if(i > 0 || isClosed)
            {
                forward += points[i] - points[(i-1 + points.Length) % points.Length];
            }
            forward.Normalize();

            Vector3 left = new Vector3(-forward.y, 0, forward.x);

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
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }
}
