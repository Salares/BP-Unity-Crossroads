using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(CrossroadCreator))]
public class CrossroadEditor : Editor
{
    CrossroadCreator creator;

    Crossroad Crossroad
    {
        get { return creator.crossroad; }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();


        EditorGUI.BeginChangeCheck();
        if (GUILayout.Button("Create New Crossroad"))
        {
            Undo.RecordObject(creator, "Create New Crossroad");
            creator.CreateCrossroad();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
    }

    void OnSceneGUI()
    {
        // Input();
        Draw();
    }

    private void Draw()
    {


        foreach (Path path in Crossroad)
        {
            Handles.color = creator.splineParameters.handlesColor;
            for (int i = 0; i < path.NumSegments; i++)
            {
                Vector2[] points = path.GetPointsInSegment(i);

                // Convert points to Vector3 for XZ plane
                Vector3[] points3D = ConvertToVector3(points);

                if (creator.splineParameters.displayPoints)
                {
                    Handles.DrawLine(points3D[1], points3D[0]);
                    Handles.DrawLine(points3D[3], points3D[2]);
                }

                Color segmentCol = creator.splineParameters.segmentColor;
                Handles.DrawBezier(points3D[0], points3D[3], points3D[1], points3D[2], segmentCol, null, creator.splineParameters.splineWidth);
            }
            float diameter = creator.splineParameters.controlPointDiameter;
            if (creator.splineParameters.displayPoints)
            {
                for (int i = 0; i < path.NumPoints; i++)
                {
                    if (i % 3 == 0)
                    {
                        Handles.color = creator.splineParameters.anchorColor;
                        diameter = creator.splineParameters.anchorDiameter;
                    }
                    else
                    {
                        Handles.color = creator.splineParameters.controlPointColor;
                        diameter = creator.splineParameters.controlPointDiameter;
                    }

                    // Convert point to Vector3 for XZ plane
                    Vector3 point3D = new Vector3(path[i].x, 0, path[i].y);

                    Vector3 newPos3D = Handles.FreeMoveHandle(point3D, Quaternion.identity, diameter, Vector3.zero, Handles.CylinderHandleCap);
                    if (point3D != newPos3D)
                    {
                        Undo.RecordObject(creator, "Move point");
                        // Convert back to Vector2 for path.MovePoint
                        path.MovePoint(i, new Vector2(newPos3D.x, newPos3D.z));
                    }
                }
            }
        }

    }

    // Helper function to convert Vector2 array to Vector3 array for XZ plane
    private Vector3[] ConvertToVector3(Vector2[] vertices)
    {
        Vector3[] vertices3D = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices3D[i] = new Vector3(vertices[i].x, 0f, vertices[i].y);
        }

        return vertices3D;
    }

    void OnEnable()
    {
        creator = (CrossroadCreator)target;
        if (creator.crossroad == null)
        {
            creator.CreateCrossroad();
        }
    }
}
