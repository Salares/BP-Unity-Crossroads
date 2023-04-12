using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class ProceduralCube : MonoBehaviour
{
    Mesh mesh;
    List<Vector3> vertices;
    List<int> triangles;

    [Header("Cube Settings")]
    public float scale = 1f;
    public int posX, posY, posZ;
    float adjScale;


    void Awake() 
    {
        mesh = GetComponent<MeshFilter>().mesh;
        adjScale = scale * 0.5f;
    }

    void Start()
    {
        CreateCube(adjScale, new Vector3(posX,posY,posZ));
        UpdateMesh();
    }

    void OnValidate()
    {
        if(mesh != null)
        {
            adjScale = scale * 0.5f;
            CreateCube(adjScale, new Vector3((float) posX * scale, (float) posY * scale, (float) posZ * scale ));
            UpdateMesh();
        }
    }

    private void CreateCube(float cubeScale, Vector3 cubePosition)
    {
        vertices = new List<Vector3>();
        triangles = new List<int>();

        for (int i = 0; i < 6; i++)
        {
            CreateFace(i, cubeScale, cubePosition);
        }
    }

    private void CreateFace(int dir, float faceScale, Vector3 facePosition)
    {
        vertices.AddRange(CubeMeshData.faceVertices(dir, faceScale, facePosition));
        int vCount = vertices.Count;

        triangles.Add(vCount - 4);
        triangles.Add(vCount - 3);
        triangles.Add(vCount - 2);
        triangles.Add(vCount - 4);
        triangles.Add(vCount - 2);
        triangles.Add(vCount - 1);
    }

    private void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
    }
}
