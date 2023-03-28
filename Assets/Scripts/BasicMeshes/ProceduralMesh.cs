using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class ProceduralMesh : MonoBehaviour
{
    Mesh mesh;
    Vector3[] vertices;
    int[] triangles;

    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;

    }

    void Update()
    {
        CreateMeshData();
        CreateMesh();
    }

    private void CreateMeshData()
    {
        vertices = new Vector3[] {new Vector3(0,YValue.ins.yValue,0),new Vector3(0,0,1),new Vector3(1,0,0),new Vector3(1,0,1)};
        triangles = new int[] {0,1,2,2,1,3};
    }

        private void CreateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
