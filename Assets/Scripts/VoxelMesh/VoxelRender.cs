using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class VoxelRender : MonoBehaviour
{
     Mesh mesh;
    List<Vector3> vertices;
    List<int> triangles;

    [Header("Voxel Settings")]
    public float scale = 1f;
    float adjScale;


    void Awake() 
    {
        mesh = GetComponent<MeshFilter>().mesh;
        adjScale = scale * 0.5f;
    }

    void Start()
    {
        GenerateVoxelMesh(new VoxelData());
        UpdateMesh();
    }

    private void GenerateVoxelMesh(VoxelData data)
    {
        vertices = new List<Vector3>();
        triangles = new List<int>();

        for (int z = 0; z < data.Depth; z++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                if(data.GetCell(x,z) == 0) { continue; }

                CreateCube(adjScale, new Vector3((float) x * scale, 0, (float) z * scale ), x, z, data);
            }
        }
    }

    void OnValidate()
    {
        if(mesh != null)
        {
            adjScale = scale * 0.5f;
            GenerateVoxelMesh(new VoxelData());
            UpdateMesh();
        }
    }

    private void CreateCube(float cubeScale, Vector3 cubePosition, int x, int z, VoxelData data)
    {
        for (int i = 0; i < 6; i++)
        {
            Direction dir = (Direction)i;
            if(data.GetNeighbor(x, z, dir) == 0)
            {
                CreateFace(dir, cubeScale, cubePosition);
            }
        }
    }

    private void CreateFace(Direction dir, float faceScale, Vector3 facePosition)
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

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public override bool Equals(object other)
    {
        return base.Equals(other);
    }
}
