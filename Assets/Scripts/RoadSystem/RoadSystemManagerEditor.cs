using UnityEditor;
using UnityEngine;

namespace RoadSystem
{
    [CustomEditor(typeof(RoadSystemManager))]
    public class RoadSystemManagerEditor : Editor
    {
        private void OnSceneGUI()
        {
            RoadSystemManager manager = (RoadSystemManager)target;

            // Draw "+" handles at each open end
            foreach (var openEnd in manager.openEnds)
            {
                Handles.color = Color.green;
                float handleSize = HandleUtility.GetHandleSize(openEnd.position) * 0.2f;
                if (Handles.Button(openEnd.position, Quaternion.identity, handleSize, handleSize, Handles.CubeHandleCap))
                {
                    // Show popup menu to choose between adding a road or intersection
                    ShowAddMenu(manager, openEnd);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Use the scene view '+' handles to add roads or intersections.", MessageType.Info);

            RoadSystemManager manager = (RoadSystemManager)target;

            // Add a button to reset the system and add the first intersection
            EditorGUILayout.Space();
            if (GUILayout.Button("Reset and Add First Intersection"))
            {
                manager.ResetSystem();
                manager.AddFirstIntersection();
            }

            if (manager.roadSegments.Count == 0 && manager.intersections.Count == 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Initialize Road System:", EditorStyles.boldLabel);

                if (GUILayout.Button("Add First Road"))
                {
                    manager.AddFirstRoad();
                }
                if (GUILayout.Button("Add First Intersection"))
                {
                    manager.AddFirstIntersection();
                }
            }
        }
        private void ShowAddMenu(RoadSystemManager manager, OpenEnd openEnd)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Road"), false, () => manager.AddRoad(openEnd));
            menu.AddItem(new GUIContent("Add Intersection"), false, () => manager.AddIntersection(openEnd));
            menu.ShowAsContext();
        }
    }
}