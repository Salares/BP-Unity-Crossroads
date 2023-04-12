using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(CrossroadCreator))] 
public class CrossroadPlacer : MonoBehaviour
{
    public float roadWidth = 1f;
    [Range(.05f, 2.5f)] public float spacing = 1f;
    
    public bool autoUpdate = false;

    public float tiling = 1;

    public Material roadMaterial;

    public void UpdateCrossroad()
    {
        Crossroad crossroad = GetComponent<CrossroadCreator>().crossroad;
        List<Vector2[]> list = new List<Vector2[]>();

        for (int j = transform.childCount - 1; j >= 0; j--)
        {
            DestroyImmediate(transform.GetChild(j).gameObject);
        }

        int i = 0;
        foreach (Path path in crossroad) 
        {
            Vector2[] points = path.CalculateEvenlySpacedPoints(spacing);

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

        Debug.Log(triangles);
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        return mesh;
    }
}
