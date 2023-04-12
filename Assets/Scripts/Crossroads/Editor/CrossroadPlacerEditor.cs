using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CrossroadPlacer))]
public class CrossroadPlacerEditor : Editor
{
    CrossroadPlacer placer;

    

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        

        EditorGUI.BeginChangeCheck();
        if(GUILayout.Button("Generate Crossroad Mesh"))
        {
            Undo.RecordObject(placer, "Generate Crossroad Mesh");
            placer.UpdateCrossroad();
        }
        
        if(EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
    }

    void OnEnable()
    {
        placer = (CrossroadPlacer)target;
    }
}
