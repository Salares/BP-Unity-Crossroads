using UnityEngine;
using System.Collections.Generic; // Required for List

// Add ISerializationCallbackReceiver interface
public class PathCreator : MonoBehaviour, ISerializationCallbackReceiver
{
    // --- MODIFIED: Store a list of paths ---
    [SerializeField, HideInInspector]
    public List<Path> paths = new List<Path>(); // Initialize the list
    // --- END MODIFIED ---

    [SerializeField]
    public SplineParameters splineParameters;

    [System.Serializable]
    public class SplineParameters
    {
        // ... (parameters remain the same) ...
        public Color anchorColor = Color.white;
        public Color controlPointColor = Color.grey;
        public Color segmentColor = Color.green;
        public Color selectedSegmentColor = Color.yellow;
        public Color handlesColor = Color.black;
        public float anchorDiameter = .1f;
        public float controlPointDiameter = .075f;
        public float splineWidth = 3f;
        public bool displayPoints = true;
    }

    // --- ISerializationCallbackReceiver Implementation ---
    public void OnBeforeSerialize()
    {
        // No specific action needed here if Path handles its own list updates
    }

    public void OnAfterDeserialize()
    {
        // --- MODIFIED: Restore intersection data for ALL paths in the list ---
        if (paths != null)
        {
            foreach (var p in paths)
            {
                if (p != null)
                {
                    // Ensure the path's internal list is ready before restoring
                    // (Path constructor should handle initialization)
                    p.RestoreIntersectionIndicesFromSerializedData();
                }
            }
        }
        else
        {
            paths = new List<Path>(); // Ensure list exists if loaded as null
        }
        // --- END MODIFIED ---
    }
    // --- End ISerializationCallbackReceiver ---

    void Awake()
    {
        // --- MODIFIED: Ensure at least one path exists ---
        CreatePathIfNeeded(); // Now checks the list

        // Restore data for all paths
        if (paths != null)
        {
            foreach (var p in paths)
            {
                if (p != null)
                {
                    p.RestoreIntersectionIndicesFromSerializedData();
                }
            }
        }
        // --- END MODIFIED ---
    }

    // --- MODIFIED: CreatePath now adds to the list or creates the first ---
    public Path CreatePath() // Changed return type to Path
    {
        // Creates a new Path object centered at the GameObject's position
        Path newPath = new Path(transform.position);
        paths.Add(newPath); // Add the new path to the list
        Debug.Log("New path created and added to list.");
        #if UNITY_EDITOR
        UnityEditor.Undo.RegisterCreatedObjectUndo(this, "Create Path"); // ??? Check if this works for list modification
        // Recording the creator object itself might be better for list changes
        UnityEditor.Undo.RecordObject(this, "Create Path");
        #endif
        return newPath; // Return the newly created path
    }
    // --- END MODIFIED ---

     // --- NEW: Create a path starting at a specific point ---
     public Path CreatePathAtPoint(Vector3 position, Vector3 initialDirection, float initialLength = 1f)
     {
         Path newPath = new Path(position); // Creates default 4 points around position

         // Reposition the default points to form an initial segment
         Vector3 control1 = position + initialDirection * initialLength * 0.33f;
         Vector3 endAnchor = position + initialDirection * initialLength;
         Vector3 control2 = endAnchor - initialDirection * initialLength * 0.33f;

         // Modify the points list directly (assuming default constructor creates 4 points)
         if (newPath.NumPoints >= 4)
         {
            newPath.MovePoint(0, position); // Start Anchor
            newPath.MovePoint(1, control1); // Control 1
            newPath.MovePoint(2, control2); // Control 2
            newPath.MovePoint(3, endAnchor);  // End Anchor

            // Clean up extra points if the default constructor added more
            while (newPath.NumPoints > 4)
            {
                 newPath.DeleteSegment(newPath.NumPoints - 1); // Delete last segments until only one remains
            }
             // Ensure AutoSet is off initially for manual placement
             bool wasAutoSet = newPath.AutoSetControlPoints;
             newPath.AutoSetControlPoints = false;
             // Re-move points to ensure they stick after potential auto-set cleanup
             newPath.MovePoint(0, position);
             newPath.MovePoint(1, control1);
             newPath.MovePoint(2, control2);
             newPath.MovePoint(3, endAnchor);
             // Restore auto-set if needed, though often better left off initially
             // newPath.AutoSetControlPoints = wasAutoSet;

             // Mark the start anchor (index 0) as an intersection
             newPath.MarkAsIntersection(0);

         } else {
            Debug.LogError("Path constructor did not create enough points for initial setup!");
            // Fallback: just add the segment (less precise control points)
            newPath.MovePoint(0, position); // Ensure start is correct
            newPath.AddSegment(endAnchor);
            newPath.MarkAsIntersection(0);
         }


         paths.Add(newPath);
         Debug.Log("New branch path created at point.");
         #if UNITY_EDITOR
         UnityEditor.Undo.RecordObject(this, "Create Branch Path");
         #endif
         return newPath;
     }
     // --- END NEW ---


    // --- MODIFIED: Checks if the list is empty ---
    public void CreatePathIfNeeded()
    {
        if (paths == null || paths.Count == 0) // Check if list is null or empty
        {
            #if UNITY_EDITOR
            Debug.LogWarning("Path list was empty in PathCreator. Creating a new default path.", this);
            CreatePath(); // Creates and adds the first path
            UnityEditor.EditorUtility.SetDirty(this);
            #else
            Debug.LogError("Path list is null or empty in PathCreator component!", this);
            #endif
        }
    }
    // --- END MODIFIED ---

    // --- MODIFIED: Reset clears the list and adds one ---
    void Reset()
    {
        if (splineParameters == null)
        {
             splineParameters = new SplineParameters();
        }

        // Clear existing paths and create a fresh one
        paths = new List<Path>(); // Initialize new list
        CreatePath(); // Creates and adds the first path

        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
    // --- END MODIFIED ---

    #if UNITY_EDITOR
    void OnValidate()
    {
         if (splineParameters == null)
         {
            splineParameters = new SplineParameters();
         }
         // Ensure list exists
         if (paths == null) {
            paths = new List<Path>();
         }
         // Optional: Restore data on validate, though Awake/Deserialize usually cover it
         // foreach (var p in paths) { if (p != null) p.RestoreIntersectionIndicesFromSerializedData(); }
    }
    #endif
}