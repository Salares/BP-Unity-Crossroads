using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadPlacer))]
public class RoadEditor : Editor
{
    RoadPlacer placer;

    void OnSceneGUI()
    {
        if(placer.autoUpdate && Event.current.type == EventType.Repaint)
        {
            placer.UpdateRoad();
        }
    }

    void OnEnable()
    {
        placer = (RoadPlacer)target;
    }
}
