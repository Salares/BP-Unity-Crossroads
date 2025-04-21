using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathPlacer : MonoBehaviour
{
    public float spacing = .1f;
    public float resolution = 1f;

    // Optional: Assign in Inspector if you have multiple PathCreators
    // and want this placer tied to a specific one.
    public PathCreator targetCreator;

    // Keep track of spawned objects to clean up if needed
    private List<GameObject> spawnedObjects = new List<GameObject>();

    void Start()
    {
        GenerateSpheres();
    }

    // Public method to allow triggering generation manually or from editor
    public void GenerateSpheres()
    {
        // --- Cleanup previous spheres ---
        ClearSpheres();
        // --- End Cleanup ---

        // Find the PathCreator if not assigned
        if (targetCreator == null)
        {
            targetCreator = FindObjectOfType<PathCreator>();
        }

        // Validate PathCreator and its paths list
        if (targetCreator == null)
        {
            Debug.LogError("PathPlacer: No PathCreator found or assigned.", this);
            return;
        }
        if (targetCreator.paths == null)
        {
            Debug.LogWarning("PathPlacer: PathCreator has a null paths list.", this);
            return; // Nothing to place
        }
        if (targetCreator.paths.Count == 0)
        {
            Debug.Log("PathPlacer: PathCreator has no paths in its list.", this);
            return; // Nothing to place
        }

        // --- Iterate through all paths ---
        foreach (Path path in targetCreator.paths)
        {
            // Skip null paths or paths with insufficient points
            if (path == null || path.NumPoints < 2)
            {
                continue;
            }

            // Calculate points along the *current* path
            // This uses the existing method which calculates across the entire path length
            Vector3[] points = path.CalculateEvenlySpacedPoints(spacing, resolution);

            // Place spheres at the calculated points for this path
            foreach (Vector3 point in points)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = point;
                // Scale spheres based on spacing for visibility
                sphere.transform.localScale = Vector3.one * spacing * 0.5f;
                // Parent the spheres to this object for scene organization
                sphere.transform.parent = this.transform;
                // Add to list for potential cleanup
                spawnedObjects.Add(sphere);

                // Optional: Remove collider for performance if many spheres
                Collider col = sphere.GetComponent<Collider>();
                if (col != null)
                {
                   Destroy(col); // Remove collider if not needed
                }
            }
        }
        // --- End Path Iteration ---
    }

    // Helper method to destroy previously spawned spheres
    public void ClearSpheres()
    {
        // Iterate backwards to safely remove from list while destroying
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
                // Use DestroyImmediate in editor contexts if needed, otherwise Destroy
                if (Application.isPlaying)
                {
                    Destroy(spawnedObjects[i]);
                }
                else
                {
                    DestroyImmediate(spawnedObjects[i]);
                }
            }
        }
        spawnedObjects.Clear(); // Clear the list
    }

    // Example of how to trigger updates from other scripts or editor buttons
    [ContextMenu("Regenerate Spheres")]
    void Regenerate()
    {
        GenerateSpheres();
    }

     // Cleanup when the component is destroyed
     void OnDestroy()
     {
         // Optionally clear spheres when this component is destroyed
         // ClearSpheres(); // Uncomment if you want automatic cleanup
     }
}