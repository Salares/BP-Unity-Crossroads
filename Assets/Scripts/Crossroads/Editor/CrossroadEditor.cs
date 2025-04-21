using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(CrossroadCreator))]
public class CrossroadEditor : Editor
{
    CrossroadCreator creator;
    CrossroadPlacer placer;

    Crossroad Crossroad => creator.crossroad;

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
        Input();
        Draw();
    }

    void Input()
    {
        Event guiEvent = Event.current;
        Ray mouseRay = HandleUtility.GUIPointToWorldRay(guiEvent.mousePosition);
        float drawPlaneHeight = 0; // Assuming the XZ plane is at y=0
        float dstToDrawPlane = (drawPlaneHeight - mouseRay.origin.y) / mouseRay.direction.y;
        Vector3 mousePos = mouseRay.GetPoint(dstToDrawPlane);


        HandleUtility.AddDefaultControl(0);
    }

    private void Draw()
    {
        foreach (Path path in Crossroad)
        {
            Handles.color = creator.splineParameters.handlesColor;
            for (int i = 0; i < path.NumSegments; i++)
            {
                Vector3[] points = path.GetPointsInSegment(i); // Expect Vector3[]
                // ConvertToVector3(points); // No longer needed

                if (creator.splineParameters.displayPoints)
                {
                    Handles.DrawLine(points[1], points[0]);
                    Handles.DrawLine(points[3], points[2]);
                }

                Handles.DrawBezier(points[0], points[3], points[1], points[2],
                    creator.splineParameters.segmentColor, null, creator.splineParameters.splineWidth);
            }

            float diameter;
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

                    // Use path[i] directly as it's now Vector3
                    Vector3 newPos = Handles.FreeMoveHandle(path[i], Quaternion.identity, diameter, Vector3.zero, Handles.SphereHandleCap); // Changed to SphereHandleCap
                    if (path[i] != newPos)
                    {
                        Undo.RecordObject(creator, "Move point");
                        path.MovePoint(i, newPos); // Pass Vector3 directly
                        placer?.UpdateCrossroad();
                    }
                }
            }
        }
    }

    // ConvertToVector3 is no longer needed
    /*
    private Vector3[] ConvertToVector3(Vector2[] vertices)
    {
        Vector3[] vertices3D = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices3D[i] = new Vector3(vertices[i].x, 0f, vertices[i].y);
        }

        return vertices3D;
    }
    */

    void OnEnable()
    {
        creator = (CrossroadCreator)target;
        placer = creator.GetComponent<CrossroadPlacer>();
        if (creator.crossroad == null)
        {
            creator.CreateCrossroad();
        }
    }
}
