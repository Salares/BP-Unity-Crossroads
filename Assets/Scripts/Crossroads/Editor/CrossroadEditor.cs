using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(CrossroadCreator))] public class CrossroadEditor : Editor
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
        if(GUILayout.Button("Create New Crossroad"))
        {
            Undo.RecordObject(creator, "Create New Crossroad");
            creator.CreateCrossroad();
        }
        
        if(EditorGUI.EndChangeCheck())
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
                Vector3[] points = path.GetPointsInSegment(i);

                if(creator.splineParameters.displayPoints)
                {
                    Handles.DrawLine(points[1], points[0]);
                    Handles.DrawLine(points[3], points[2]);
                }

                Color segmentCol = creator.splineParameters.segmentColor;
                Handles.DrawBezier(points[0], points[3], points[1], points[2], segmentCol, null, creator.splineParameters.splineWidth);
            }
            float diameter = creator.splineParameters.controlPointDiameter;
            if(creator.splineParameters.displayPoints)
            {
                for (int i = 0; i < path.NumPoints; i++)
                {
                    if(i % 3 == 0 ) 
                    { 
                        Handles.color = creator.splineParameters.anchorColor;
                        diameter = creator.splineParameters.anchorDiameter; 
                    }
                    else 
                    {
                        Handles.color = creator.splineParameters.controlPointColor;
                        diameter = creator.splineParameters.controlPointDiameter; 
                    }

                    Vector3 newPos = Handles.FreeMoveHandle(path[i], Quaternion.identity, diameter, Vector2.zero, Handles.CylinderHandleCap);
                    if(path[i] != newPos)
                    {
                        Undo.RecordObject(creator, "Move point");
                        path.MovePoint(i, newPos);
                    }
                }
            }
        }

    }

    void OnEnable()
    {
        creator = (CrossroadCreator)target;
        if(creator.crossroad == null)
        {
            creator.CreateCrossroad();
        }
    }
}
