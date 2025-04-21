using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // Needed for List

[CustomEditor(typeof(PathCreator))]
public class PathEditor : Editor
{
    PathCreator creator;
    // --- MODIFIED: No single 'Path' property, access creator.paths directly ---
    // Path Path { get { return creator.path; } } // Remove this

    const float segmentSelectDistanceThreshold = .1f;
    // --- MODIFIED: Store selected path index as well ---
    int selectedPathIndex = -1;
    int selectedSegmentIndex = -1;
    // --- END MODIFIED ---

    // Add these near the top of PathEditor class with other state variables
    int activePathIndex = -1;      // Index of the path containing the active anchor
    int activeAnchorIndex = -1;    // Index of the active anchor point (must be multiple of 3)
    Color activeAnchorColor = Color.yellow; // Color to highlight the active anchor

    const float mergeDistanceThreshold = 0.15f; // Max distance to allow merging anchors
    const float mergeDistanceThresholdSqr = mergeDistanceThreshold * mergeDistanceThreshold;   

    Color intersectionAnchorColor = Color.red;
    float intersectionAnchorDiameterModifier = 1.5f;

    /// <summary>
    /// Gets the desired Y-level for path points, based on the PathCreator's transform.
    /// </summary>
    float CreatorYLevel => (creator != null) ? creator.transform.position.y : 0f;

    public override void OnInspectorGUI()
    {
        // Ensure creator exists
        if (creator == null) creator = (PathCreator)target;

        base.OnInspectorGUI();

        EditorGUI.BeginChangeCheck();

        // --- MODIFIED: Button creates a new path AND adds it to the list ---
        if (GUILayout.Button("Create New Independent Path"))
        {
            // Undo needs to record the creator because the list is changing
            Undo.RecordObject(creator, "Create New Path");
            creator.CreatePath(); // This now adds to the list
            selectedPathIndex = creator.paths.Count - 1; // Select the new path
            selectedSegmentIndex = -1;
            SceneView.RepaintAll();
        }
        // --- END MODIFIED ---


        // --- MODIFIED: Path properties need context (e.g., selected path or global?) ---
        // For simplicity, let's apply these settings globally or to a selected path later.
        // We need a way to select a *path* first. For now, let's omit these controls
        // until path selection is implemented, or apply them to *all* paths.

        // Example: Apply to ALL paths (less ideal)
        bool anyPathClosed = false;
        if (creator.paths != null && creator.paths.Count > 0)
        {
             // Check if ANY path is closed (just for toggle state)
             // A better UI would show status per-path or for selected path
             foreach(var p in creator.paths) { if(p != null && p.IsClosed) { anyPathClosed = true; break; } }

             bool closedToggle = GUILayout.Toggle(anyPathClosed, "Closed Loop (Apply All)");
             if (closedToggle != anyPathClosed) // If state changed
             {
                 Undo.RecordObject(creator, "Toggle Closed Path (All)");
                 foreach(var p in creator.paths) { if(p != null) p.IsClosed = closedToggle; }
             }

             // Similar logic for AutoSetControlPoints...
             bool anyAutoSet = false;
             foreach(var p in creator.paths) { if(p != null && p.AutoSetControlPoints) { anyAutoSet = true; break; } }
             bool autoSetToggle = GUILayout.Toggle(anyAutoSet, "Auto Set Controls (Apply All)");
              if (autoSetToggle != anyAutoSet)
              {
                  Undo.RecordObject(creator, "Toggle Auto Set Controls (All)");
                  foreach(var p in creator.paths) { if(p != null) p.AutoSetControlPoints = autoSetToggle; }
              }
        }
        // --- END MODIFIED ---


        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
    }

    void OnSceneGUI()
    {
        if (creator == null) creator = (PathCreator)target;
        // Ensure paths list exists
        if (creator.paths == null) creator.paths = new List<Path>();

        Input();
        Draw();
    }

	/// <summary>
	/// Main input processing entry point called by OnSceneGUI.
	/// Determines mouse position, checks for handle interaction, finds nearest anchor,
	/// and dispatches events to specific handlers.
	/// </summary>
	void Input()
	{
		Event guiEvent = Event.current;
		if (!mouseOverSceneView) return; // Only process if mouse is over scene view

		int controlID = GUIUtility.GetControlID(FocusType.Passive);

		Vector3 mousePos;
		if (!TryGetMouseWorldPosition(guiEvent, out mousePos)) return; // Exit if mouse position is invalid

		// Check for handle interaction first
		if (GUIUtility.hotControl != 0 && GUIUtility.hotControl != controlID)
		{
			return; // Let active handle process event
		}

		// Find nearest anchor (needed for selection, delete key, and context menu target)
		int nearestAnchorPathIdx;
		int nearestAnchorIdx;
		FindNearestAnchorToMouse(mousePos, out nearestAnchorPathIdx, out nearestAnchorIdx);

		// --- Handle KEYDOWN Events (like Delete) ---
		if (guiEvent.type == EventType.KeyDown && guiEvent.keyCode == KeyCode.X)
		{
			HandleDeleteKeyPress(nearestAnchorPathIdx, nearestAnchorIdx, guiEvent);
            // Prevent key press from triggering other actions if handled
            if (guiEvent.type == EventType.Used) return;
		}

		// --- Dispatch MOUSE event handling based on type ---
		EventType eventType = guiEvent.GetTypeForControl(controlID);
		switch (eventType)
		{
			case EventType.MouseDown:
				// --- MODIFICATION: Handle Right-Click for Context Menu ---
				if (guiEvent.button == 1) // Right Mouse Button
				{
					HandleContextMenu(guiEvent, mousePos, nearestAnchorPathIdx, nearestAnchorIdx);
				}
				else if (guiEvent.button == 0) // Left Mouse Button
				{
					// Left click logic (select, alt-branch, shift-add) remains
					HandleLeftMouseDown(guiEvent, controlID, mousePos, nearestAnchorPathIdx, nearestAnchorIdx);
				}
				break; // End MouseDown

			// --- ContextClick Event (Alternative way to detect right-click for menus) ---
            // Using MouseDown button 1 is generally more reliable across platforms/input settings
			// case EventType.ContextClick:
			//     HandleContextMenu(guiEvent, mousePos, nearestAnchorPathIdx, nearestAnchorIdx);
			//     break;

			case EventType.MouseUp:
				HandleMouseUp(guiEvent, controlID);
				break;
			case EventType.MouseMove:
				HandleMouseMove(mousePos);
				break;
			case EventType.Layout:
				HandleLayout(controlID);
				break;

            // Other events like MouseDrag, Repaint are handled implicitly or ignored here
		}
	}
 	/// <summary>
	/// Tries to calculate the mouse position in world space on the appropriate plane.
	/// </summary>
	/// <param name="guiEvent">The current GUI event.</param>
	/// <param name="mousePos">Output: The calculated world position.</param>
	/// <returns>True if a valid position was calculated, false otherwise.</returns>
	bool TryGetMouseWorldPosition(Event guiEvent, out Vector3 mousePos)
	{
		mousePos = Vector3.zero;
		// Use Plane based raycast for more robust 3D interaction
		Plane drawPlane = new Plane(Vector3.up, creator.transform.position); // Plane at creator's Y level
		Ray mouseRay = HandleUtility.GUIPointToWorldRay(guiEvent.mousePosition);
		float rayDist;

		if (drawPlane.Raycast(mouseRay, out rayDist))
		{
			mousePos = mouseRay.GetPoint(rayDist);
			return true;
		}
		else
		{
			// Fallback: Project onto Y=0 plane if view is not horizontal
			if (Mathf.Abs(mouseRay.direction.y) > Mathf.Epsilon)
			{
				float dstToDrawPlane = (0f - mouseRay.origin.y) / mouseRay.direction.y;
				if (dstToDrawPlane > 0) // Ensure intersection is in front of camera
				{
					 mousePos = mouseRay.GetPoint(dstToDrawPlane);
					 return true;
				}
			}
		}
		// Could not determine a valid mouse position
		return false;
	}

   	/// <summary>
	/// Finds the anchor point closest to the mouse cursor across all paths.
	/// </summary>
	/// <param name="mousePos">Current world position of the mouse.</param>
	/// <param name="nearestPathIdx">Output: Index of the path containing the nearest anchor (-1 if none found).</param>
	/// <param name="nearestAnchorIdx">Output: Index of the nearest anchor within its path (-1 if none found).</param>
	void FindNearestAnchorToMouse(Vector3 mousePos, out int nearestPathIdx, out int nearestAnchorIdx)
	{
		nearestPathIdx = -1;
		nearestAnchorIdx = -1;
		float minSqrDistToAnchor = float.MaxValue;
		// Use a consistent threshold for clicking near anchors
		float anchorClickThreshold = creator.splineParameters.anchorDiameter * 0.7f; // Adjust multiplier if needed
		float anchorClickThresholdSqr = anchorClickThreshold * anchorClickThreshold;

		// Avoid checking during drag operations controlled by other elements
		if (GUIUtility.hotControl == 0) // Only check if no handle is active
		{
			for (int pathIdx = 0; pathIdx < creator.paths.Count; pathIdx++)
			{
				Path currentPath = creator.paths[pathIdx];
				if (currentPath == null) continue;
				for (int i = 0; i < currentPath.NumPoints; i += 3) // Anchors only
				{
					float distanceSqr = (mousePos - currentPath[i]).sqrMagnitude;
					// Check threshold *and* find closest
					if (distanceSqr < anchorClickThresholdSqr && distanceSqr < minSqrDistToAnchor)
					{
						minSqrDistToAnchor = distanceSqr;
						nearestPathIdx = pathIdx;
						nearestAnchorIdx = i;
					}
				}
			}
		}
	}    

    	/// <summary>
	/// Handles all MouseDown events, dispatching to specific button/modifier handlers.
	/// </summary>
	void HandleMouseDown(Event guiEvent, int controlID, Vector3 mousePos, int nearestAnchorPathIdx, int nearestAnchorIdx)
	{
		// Check if the mouse click just started interacting with a different handle.
		// (Safety check, should ideally be caught by the initial hotControl check in Input())
		if (GUIUtility.hotControl != 0) return;

		if (guiEvent.button == 0) // Left Mouse Button
		{
			HandleLeftMouseDown(guiEvent, controlID, mousePos, nearestAnchorPathIdx, nearestAnchorIdx);
		}
		else if (guiEvent.button == 1) // Right Mouse Button
		{
			HandleRightMouseDown(guiEvent, mousePos, nearestAnchorPathIdx, nearestAnchorIdx);
		}
	}

		/// <summary>
	/// Handles Left MouseDown events, checking modifiers and dispatching actions.
	/// </summary>
	void HandleLeftMouseDown(Event guiEvent, int controlID, Vector3 mousePos, int nearestAnchorPathIdx, int nearestAnchorIdx)
	{
        // --- Double-click check is REMOVED ---

        // --- Proceed directly with modifier checks ---
		if (guiEvent.alt)
		{
			HandleAltClick(guiEvent, mousePos);
		}
		else if (guiEvent.shift)
		{
			HandleShiftClick(guiEvent, mousePos);
		}
		else // No modifiers (Single Click)
		{
			HandleSimpleLeftClick(guiEvent, controlID, mousePos, nearestAnchorPathIdx, nearestAnchorIdx);
		}
	}

    	/// <summary>
	/// Handles Alt+Left Click: Finds the nearest point on any path segment,
	/// marks/creates an intersection anchor there, and creates a new branching path.
    /// Ensures points are created on the Creator's Y-Level.
	/// </summary>
	void HandleAltClick(Event guiEvent, Vector3 mousePos)
	{
        // --- Step 1: Find the Closest Point on Any Segment ---
        Path closestPath = null;
        int closestPathIdx = -1;
        int closestSegmentIdx = -1;
        Vector3 pointOnCurve = Vector3.zero; // Raw point on curve from Bezier calc
        float tOnSegment = 0f;
        float minDistanceSqr = float.MaxValue;

        // (Loop to find closest point remains the same...)
        for (int pathIndex = 0; pathIndex < creator.paths.Count; pathIndex++) {
             Path currentPath = creator.paths[pathIndex]; if (currentPath == null || currentPath.NumSegments == 0) continue;
             for (int segmentIndex = 0; segmentIndex < currentPath.NumSegments; segmentIndex++) {
                 Vector3[] segmentPoints = currentPath.GetPointsInSegment(segmentIndex); if (segmentPoints == null || segmentPoints.Length < 4) continue;
                 float currentT; Vector3 currentClosestPoint = FindNearestPointOnBezierSegment(mousePos, segmentPoints[0], segmentPoints[1], segmentPoints[2], segmentPoints[3], out currentT, 25);
                 // Use original mousePos for distance check to find closest in 3D space first
                 float distanceSqr = (mousePos - currentClosestPoint).sqrMagnitude;
                 if (distanceSqr < minDistanceSqr) {
                     minDistanceSqr = distanceSqr; closestPath = currentPath; closestPathIdx = pathIndex; closestSegmentIdx = segmentIndex;
                     pointOnCurve = currentClosestPoint; // Store the 3D point on curve
                     tOnSegment = currentT;
                 }
             }
         }


		// --- Step 2: Check if a Close Enough Point Was Found ---
		float clickThreshold = segmentSelectDistanceThreshold * 2.0f;
		float clickThresholdSqr = clickThreshold * clickThreshold;
		if (closestPath == null || minDistanceSqr > clickThresholdSqr) {
			return; // Didn't click close enough
		}

        // --- Step 3: Determine Anchor Point (Flattened) and Mark as Intersection ---
        Undo.RecordObject(creator, "Create Branch Path");

		int intersectionAnchorIndex = -1;
        // *** FLATTEN pointOnCurve to get intersectionPosition ***
		Vector3 intersectionPosition = pointOnCurve;
        intersectionPosition.y = CreatorYLevel;
        // *** END FLATTEN ***

		int startAnchorIdx = closestSegmentIdx * 3;
		int endAnchorIdx = closestPath.LoopIndex(startAnchorIdx + 3);

        if (startAnchorIdx < 0 || startAnchorIdx >= closestPath.NumPoints || endAnchorIdx < 0 || endAnchorIdx >= closestPath.NumPoints) {
             Debug.LogError($"Alt-Click: Invalid anchor indices ({startAnchorIdx}, {endAnchorIdx})."); Undo.PerformUndo(); return;
         }

		float anchorSnapThresholdSqr = (creator.splineParameters.anchorDiameter * 0.6f) * (creator.splineParameters.anchorDiameter * 0.6f);
		// Compare using flattened intersectionPosition against potentially non-flat anchor positions
		bool nearStartAnchor = (closestPath[startAnchorIdx] - intersectionPosition).sqrMagnitude < anchorSnapThresholdSqr;
		bool nearEndAnchor = (closestPath[endAnchorIdx] - intersectionPosition).sqrMagnitude < anchorSnapThresholdSqr;

		if (nearStartAnchor)
		{
			intersectionAnchorIndex = startAnchorIdx;
			intersectionPosition = closestPath[intersectionAnchorIndex]; // Snap to exact anchor pos
            intersectionPosition.y = CreatorYLevel; // Ensure snapped position is also flat
		}
		else if (nearEndAnchor)
		{
			intersectionAnchorIndex = endAnchorIdx;
			intersectionPosition = closestPath[intersectionAnchorIndex]; // Snap to exact anchor pos
            intersectionPosition.y = CreatorYLevel; // Ensure snapped position is also flat
		}
		else
		{
            // Split the segment using the flattened position
			intersectionAnchorIndex = closestPath.SplitSegment(intersectionPosition, closestSegmentIdx);
            // intersectionPosition is already flat
		}

        if (intersectionAnchorIndex != -1)
        {
		    closestPath.MarkAsIntersection(intersectionAnchorIndex);
        }
        else { Debug.LogError($"Alt-Click: Failed to get valid intersection anchor index."); Undo.PerformUndo(); return; }

		// --- Step 4: Create the New Branching Path ---
        // Use flattened mouse position for direction calculation
        Vector3 mousePosFlat = mousePos;
        mousePosFlat.y = CreatorYLevel;
		Vector3 initialDirection = (mousePosFlat - intersectionPosition).normalized; // Direction calculated on XZ plane

		if (initialDirection.sqrMagnitude < 0.001f)
		{
			initialDirection = CalculateFallbackDirection(closestPath, intersectionAnchorIndex);
            initialDirection.y = 0; // Ensure fallback is also flat
            if (initialDirection.sqrMagnitude > 0.001f) initialDirection.Normalize(); else initialDirection = Vector3.forward; // Final fallback
		}
        // intersectionPosition is already flattened
		creator.CreatePathAtPoint(intersectionPosition, initialDirection, 1f);

		// --- Step 5: Finalize ---
		guiEvent.Use();
		SceneView.RepaintAll();
	}

	/// <summary>
	/// Handles Shift+Left Click: Adds a segment from an active endpoint.
    /// Ensures new points are created on the Creator's Y-Level.
	/// </summary>
	void HandleShiftClick(Event guiEvent, Vector3 mousePos)
	{
        // *** FLATTEN MOUSE Y for target position ***
        Vector3 targetPos = mousePos;
        targetPos.y = CreatorYLevel;
        // *** END FLATTEN Y ***

		// Scenario 1: Add segment from ACTIVE anchor
		if (activeAnchorIndex != -1 && activePathIndex != -1 && activePathIndex < creator.paths.Count)
		{
			Path selectedPath = creator.paths[activePathIndex];
			if (selectedPath != null)
			{
                bool isOpenPathEnd = !selectedPath.IsClosed && (activeAnchorIndex == 0 || activeAnchorIndex == selectedPath.NumPoints - 1);
                bool isIntersection = selectedPath.IsIntersection(activeAnchorIndex);

				if (isOpenPathEnd && !isIntersection)
				{
					Undo.RecordObject(creator, "Add Segment from Active");
					bool segmentAdded = false;

					if (activeAnchorIndex == selectedPath.NumPoints - 1) // Add from LAST point
					{
						selectedPath.AddSegment(targetPos); // Use flattened targetPos
						activeAnchorIndex = selectedPath.NumPoints - 1;
						segmentAdded = true;
					}
					else if (activeAnchorIndex == 0) // Add BEFORE first point
					{
                        try {
						    Vector3 oldStartPos = selectedPath[0]; // Y might not be flat
                            Vector3 nextControlPos = (selectedPath.NumPoints > 1) ? selectedPath[1] : oldStartPos; // Y might not be flat

                            // Calculate controls based on flattened positions if needed, ensure result is flat
						    Vector3 control2 = oldStartPos + (oldStartPos - nextControlPos).normalized * 0.5f;
                            control2.y = CreatorYLevel; // Flatten explicitly
						    Vector3 control1 = targetPos + (targetPos - control2).normalized * 0.5f;
                            control1.y = CreatorYLevel; // Flatten explicitly

                            // Insert points (targetPos and controls are now flat)
						    selectedPath.InsertPoints(0, new Vector3[] { targetPos, control1, control2 });
						    activeAnchorIndex = 0;
						    segmentAdded = true;
                        } catch (System.Exception ex) {
                            Debug.LogError($"Error prepending segment: {ex.Message}"); Undo.PerformUndo();
                        }
					}

					if(segmentAdded)
					{
						guiEvent.Use();
						SceneView.RepaintAll();
					}
				}
				else if(isOpenPathEnd && isIntersection) { /* Warning */ }
				else { /* Warning */ }
			}
		}
		else // No active anchor selected for adding
		{
			Debug.LogWarning("Shift+Click: Select a non-intersection start/end anchor first to add a segment.");
		}
	}

    /// <summary>
	/// Handles Left Click with no modifiers: Selects/Deselects the nearest anchor.
	/// </summary>
	void HandleSimpleLeftClick(Event guiEvent, int controlID, Vector3 mousePos, int nearestAnchorPathIdx, int nearestAnchorIdx)
	{
		if (nearestAnchorIdx != -1) // Clicked near an anchor
		{
			// Select the anchor visually
			if (activePathIndex != nearestAnchorPathIdx || activeAnchorIndex != nearestAnchorIdx)
			{
				activePathIndex = nearestAnchorPathIdx;
				activeAnchorIndex = nearestAnchorIdx;
				// Debug.Log($"Selected anchor {activeAnchorIndex} on path {activePathIndex}");
				HandleUtility.Repaint(); // Repaint to show selection change
			}
			// --- IMPORTANT: Do not consume event or take hotControl here ---
			// Allow the event to pass through to the FreeMoveHandle.
		}
		else // Clicked empty space
		{
			// Deselect if an anchor was active
			if (activeAnchorIndex != -1)
			{
				activePathIndex = -1;
				activeAnchorIndex = -1;
				// Debug.Log("Deselected active anchor.");
				HandleUtility.Repaint();
			}
			// Allow click to pass through for default scene interaction (e.g., deselection box)
		}
	}

    /// <summary>
	/// Handles double-clicking on an anchor point to merge it with the currently active anchor.
	/// </summary>
	void HandleDoubleClickMerge(Event guiEvent, Vector3 mousePos, int targetPathIdx, int targetAnchorIdx)
	{
		// 1. Check if an anchor is currently active
		if (activeAnchorIndex == -1 || activePathIndex == -1)
		{
			Debug.Log("Double-click merge requires an active anchor selected first.");
			return;
		}

		// 2. Check if the double-click hit a valid target anchor
		if (targetAnchorIdx == -1 || targetPathIdx == -1)
		{
			Debug.Log("Double-click did not hit a target anchor.");
			return; // Double-clicked empty space or a control point
		}

		// 3. Check if target is different from the active anchor
		if (targetPathIdx == activePathIndex && targetAnchorIdx == activeAnchorIndex)
		{
			Debug.Log("Cannot merge an anchor with itself.");
			return; // Clicked the already active anchor
		}

        // 4. Get references and positions (check indices are valid)
        if (activePathIndex >= creator.paths.Count || activeAnchorIndex >= creator.paths[activePathIndex].NumPoints ||
            targetPathIdx >= creator.paths.Count || targetAnchorIdx >= creator.paths[targetPathIdx].NumPoints)
        {
             Debug.LogError("Merge error: Invalid path or anchor indices.");
             return;
        }
        Path activePath = creator.paths[activePathIndex];
        Path targetPath = creator.paths[targetPathIdx];
        Vector3 activePos = activePath[activeAnchorIndex];
        Vector3 targetPos = targetPath[targetAnchorIdx];


		// 5. Check if the active and target anchors are close enough
		if ((activePos - targetPos).sqrMagnitude > mergeDistanceThresholdSqr)
		{
			Debug.LogWarning($"Anchors too far apart to merge. Distance sq: {(activePos - targetPos).sqrMagnitude}, Threshold sq: {mergeDistanceThresholdSqr}");
			return;
		}

        // --- All checks passed, proceed with merge ---
		Debug.Log($"Merging active anchor {activePathIndex}:{activeAnchorIndex} to target {targetPathIdx}:{targetAnchorIdx}");

        // 6. Record Undo state before any modifications
		Undo.RecordObject(creator, "Merge Anchors");

        // 7. Move the *active* anchor and any points linked to it TO the target position
        activePath.MovePoint(activeAnchorIndex, targetPos); // Move primary active point
        MoveLinkedIntersections(activePathIndex, activeAnchorIndex, activePos, targetPos); // Move others linked to active point

        // 8. Mark BOTH anchors as intersections
		activePath.MarkAsIntersection(activeAnchorIndex);
		targetPath.MarkAsIntersection(targetAnchorIdx);

        // 9. Deselect the active anchor (optional, but often makes sense after merge)
		activePathIndex = -1;
		activeAnchorIndex = -1;

		// 10. Consume the event and repaint
		guiEvent.Use();
		SceneView.RepaintAll();
	}

    	/// <summary>
	/// Handles Right MouseDown events: Deletes the nearest anchor point.
	/// </summary>
	void HandleRightMouseDown(Event guiEvent, Vector3 mousePos, int nearestAnchorPathIdx, int nearestAnchorIdx)
	{
		if (nearestAnchorIdx != -1) // Clicked near an anchor to delete
		{
            // Check path index validity before accessing
            if (nearestAnchorPathIdx < 0 || nearestAnchorPathIdx >= creator.paths.Count) return;

			Path pathToDeleteFrom = creator.paths[nearestAnchorPathIdx];
			// Debug.Log($"Right-Click: Trying to delete anchor {nearestAnchorIdx} on path {nearestAnchorPathIdx}.");

			// Check if deletion is allowed
			if (pathToDeleteFrom != null && (pathToDeleteFrom.NumSegments > 1 || (pathToDeleteFrom.IsClosed && pathToDeleteFrom.NumSegments > 0)))
			{
				// Deselect if deleting the active anchor
				if (nearestAnchorPathIdx == activePathIndex && nearestAnchorIdx == activeAnchorIndex)
				{
					activePathIndex = -1; activeAnchorIndex = -1;
				}

				Undo.RecordObject(creator, "Delete Segment");
				pathToDeleteFrom.DeleteSegment(nearestAnchorIdx);

				// Optional: Remove path if it becomes empty
                // Check *after* deletion, path index might be invalid if path was removed.
                // Use helper which checks bounds again.
				CleanupEmptyPath(nearestAnchorPathIdx);

				guiEvent.Use(); // Consume the right-click
				SceneView.RepaintAll();
			}
			else
			{
				Debug.LogWarning("Cannot delete the last segment of an open path, or path is invalid.");
			}
		}
		else
		{
			 // Debug.Log("Right-Click: No anchor found nearby.");
		}
	}

    	/// <summary>
	/// Handles MouseMove events: Updates segment highlighting based on proximity.
	/// </summary>
	void HandleMouseMove(Vector3 mousePos)
	{
		int newSelectedPathIndex = -1;
		int newSelectedSegmentIndex = -1;
		float minDistanceToSegment = segmentSelectDistanceThreshold;

		// Iterate through all paths and segments to find the closest
		for (int pathIdx = 0; pathIdx < creator.paths.Count; pathIdx++)
		{
			Path currentPath = creator.paths[pathIdx];
            // Check if path exists and has enough points for at least one segment
            // path.NumPoints gives us the count directly from the internal list
			if (currentPath == null || currentPath.NumPoints < 4) continue;

			for (int i = 0; i < currentPath.NumSegments; i++)
			{
                // GetPointsInSegment uses the public indexer internally, so it's safe
				Vector3[] points = currentPath.GetPointsInSegment(i);
				if (points == null || points.Length < 4) continue;

				Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.z);
				Vector2 p0_2D = new Vector2(points[0].x, points[0].z);
				Vector2 p1_2D = new Vector2(points[1].x, points[1].z);
				Vector2 p2_2D = new Vector2(points[2].x, points[2].z);
				Vector2 p3_2D = new Vector2(points[3].x, points[3].z);
				float distanceToSegment = HandleUtility.DistancePointBezier(mousePos2D, p0_2D, p3_2D, p1_2D, p2_2D);

				if (distanceToSegment < minDistanceToSegment)
				{
					minDistanceToSegment = distanceToSegment;
					newSelectedPathIndex = pathIdx;
					newSelectedSegmentIndex = i;
				}
			}
		}

		// Update selection state and repaint if changed
		if (newSelectedPathIndex != selectedPathIndex || newSelectedSegmentIndex != selectedSegmentIndex)
		{
			selectedPathIndex = newSelectedPathIndex;
			selectedSegmentIndex = newSelectedSegmentIndex;
			HandleUtility.Repaint();
		}
	}
    
    /// <summary>
	/// Handles MouseUp events. Currently just releases hotControl if taken by this script.
	/// </summary>
	void HandleMouseUp(Event guiEvent, int controlID)
	{
		// Release hot control ONLY if we specifically took it (currently not taking it)
		// if (GUIUtility.hotControl == controlID)
		// {
		//     GUIUtility.hotControl = 0;
		// }
		// FreeMoveHandles manage their own MouseUp and hotControl release.
	}

    	/// <summary>
	/// Handles Layout events, associating the control ID with the scene view area.
	/// </summary>
	void HandleLayout(int controlID)
	{
		// Associate this control with the scene view area for proper event routing.
		HandleUtility.AddControl(controlID, HandleUtility.DistanceToRectangle(creator.transform.position, Quaternion.identity, 0f));
	}

    	/// <summary>
	/// Helper to remove a path if it becomes empty after a deletion and adjust selection indices.
	/// </summary>
	/// <param name="deletedPathIdx">The index of the path from which a segment was potentially deleted.</param>
	void CleanupEmptyPath(int deletedPathIdx)
	{
		 // Check bounds before accessing, as path might already be removed if called multiple times rapidly
		 if (deletedPathIdx < 0 || deletedPathIdx >= creator.paths.Count) return;

		 Path pathToCheck = creator.paths[deletedPathIdx];
         // Added null check for pathToCheck
		 if (pathToCheck != null && pathToCheck.NumPoints < 4 && pathToCheck.NumSegments < 1)
		 {
			 bool isLastPath = creator.paths.Count == 1;
			 creator.paths.RemoveAt(deletedPathIdx); // Remove the path object
			 // Debug.Log($"Removed path {deletedPathIdx} as it became empty.");

			 // Reset selection if the deleted path was selected
			 if(selectedPathIndex == deletedPathIdx) {
				selectedPathIndex = -1; selectedSegmentIndex = -1;
				activePathIndex = -1; activeAnchorIndex = -1; // Also clear active anchor
			 } else if (selectedPathIndex > deletedPathIdx) {
				 selectedPathIndex--; // Adjust index if it was after the deleted one
			 }
			 // Adjust active index if it referred to a path after the deleted one
			 if (activePathIndex > deletedPathIdx) {
				 activePathIndex--;
			 }
		 }
	}

    	/// <summary>
	/// Calculates a fallback direction for branching if the click is exactly on an anchor.
	/// Tries based on neighbors, defaults to Vector3.forward.
	/// </summary>
	Vector3 CalculateFallbackDirection(Path path, int anchorIndex)
	{
		// Ensure path is not null and indices are valid before accessing points via the indexer
		if (path == null || path.NumPoints < 1) return Vector3.forward; // Check if path exists and has points

		// Try direction away from previous anchor
        // Use path.NumPoints for upper bound check
		if (anchorIndex >= 3 && anchorIndex < path.NumPoints)
			return (path[anchorIndex] - path[anchorIndex - 3]).normalized;

		// Try direction towards next anchor
        // Use path.NumPoints for upper bound check
		if (anchorIndex >= 0 && anchorIndex + 3 < path.NumPoints)
			return (path[anchorIndex + 3] - path[anchorIndex]).normalized;

		// Absolute fallback
		return Vector3.forward;
	}

    	// --- Helper property to check if mouse is over the scene view ---
	// Requires the MouseUtility class defined below
	bool mouseOverSceneView
	{
		get { return MouseUtility.mouseOverWindow == SceneView.currentDrawingSceneView; }
	}

// --- Make sure the MouseUtility class is also included in your editor script ---
#if UNITY_EDITOR
public static class MouseUtility
{
	public static EditorWindow mouseOverWindow;

	static MouseUtility()
	{
		EditorApplication.update -= UpdateMouseOverWindow; // Prevent duplicates if recompiled
		EditorApplication.update += UpdateMouseOverWindow;
	}

	// No need for [InitializeOnLoadMethod] if using static constructor

	public static void UpdateMouseOverWindow()
	{
		// This doesn't need focus check, just tracks the window the mouse is over
		if (EditorWindow.mouseOverWindow != null)
		{
			mouseOverWindow = EditorWindow.mouseOverWindow;
		}
	}
}
#endif

#region Drawing Methods

/// <summary>
/// Main drawing entry point called by OnSceneGUI.
/// Iterates through all paths and calls helper methods to draw segments and handles.
/// </summary>
void Draw()
{
    if (creator == null || creator.splineParameters == null) return;

    // Loop through all paths managed by the PathCreator
    for (int pathIdx = 0; pathIdx < creator.paths.Count; pathIdx++)
    {
        Path currentPath = creator.paths[pathIdx];
        if (currentPath == null) continue; // Skip null paths

        // Draw the visual representation of the path segments and control lines
        DrawPathSegments(currentPath, pathIdx);

        // Draw the interactive handles for anchors and control points
        if (creator.splineParameters.displayPoints)
        {
            DrawPathHandles(currentPath, pathIdx);
        }
    }
}

/// <summary>
/// Draws the Bezier curve segments and control point connector lines for a single path.
/// </summary>
/// <param name="currentPath">The Path object to draw segments for.</param>
/// <param name="pathIdx">The index of this path in the creator's list.</param>
void DrawPathSegments(Path currentPath, int pathIdx)
{
    for (int i = 0; i < currentPath.NumSegments; i++)
    {
        // Get points for this specific segment
        Vector3[] points = currentPath.GetPointsInSegment(i);
        if (points == null || points.Length < 4) continue; // Skip invalid segments

        // Draw control point connector lines (if enabled)
        if (creator.splineParameters.displayPoints)
        {
            DrawControlPointLines(points);
        }

        // Determine segment color based on selection state
        bool isSelectedSegment = (pathIdx == selectedPathIndex && i == selectedSegmentIndex);
        Color segmentCol = (isSelectedSegment && Event.current.shift) ? creator.splineParameters.selectedSegmentColor : creator.splineParameters.segmentColor;

        // Draw the actual Bezier curve
        Handles.DrawBezier(points[0], points[3], points[1], points[2], segmentCol, null, creator.splineParameters.splineWidth);
    }
}

/// <summary>
/// Draws the connector lines between anchors and their control points.
/// </summary>
/// <param name="segmentPoints">The four points defining the Bezier segment.</param>
void DrawControlPointLines(Vector3[] segmentPoints)
{
    Handles.color = creator.splineParameters.handlesColor;
    // Only draw if points are distinct enough
    if (Vector3.Distance(segmentPoints[1], segmentPoints[0]) > 0.001f) Handles.DrawLine(segmentPoints[1], segmentPoints[0]);
    if (Vector3.Distance(segmentPoints[2], segmentPoints[3]) > 0.001f) Handles.DrawLine(segmentPoints[2], segmentPoints[3]);
}

/// <summary>
/// Draws all the interactive handles (anchors and controls) for a single path.
/// </summary>
/// <param name="currentPath">The Path object to draw handles for.</param>
/// <param name="pathIdx">The index of this path in the creator's list.</param>
void DrawPathHandles(Path currentPath, int pathIdx)
{
    for (int i = 0; i < currentPath.NumPoints; i++)
    {
        DrawPointHandle(currentPath, pathIdx, i);
    }
}

	/// <summary>
	/// Draws a single interactive handle for a specific point (anchor or control).
	/// Handles appearance, interaction, movement (constrained to Y-Level), and linked intersection updates.
	/// </summary>
	void DrawPointHandle(Path currentPath, int pathIdx, int pointIdx)
	{
		// Determine appearance
		Color pointColor;
		float diameter;
		DetermineHandleAppearance(currentPath, pathIdx, pointIdx, out pointColor, out diameter);

		// Draw exit line if it's an intersection anchor
		bool isAnchor = pointIdx % 3 == 0;
		if (isAnchor && currentPath.IsIntersection(pointIdx))
		{
			DrawIntersectionExitLine(currentPath, pointIdx);
		}

		// Set color and get current position
		Handles.color = pointColor;
		Vector3 currentPos = currentPath[pointIdx]; // Position before potential move
		float handleSize = Mathf.Max(0.01f, diameter * 0.5f);

		// Draw the handle and check for changes
		EditorGUI.BeginChangeCheck();
		Vector3 newPos = Handles.FreeMoveHandle(currentPos, Quaternion.identity, handleSize, Vector3.zero, Handles.SphereHandleCap);

		if (EditorGUI.EndChangeCheck()) // Handle was moved
		{
            // *** FLATTEN Y coordinate ***
            newPos.y = CreatorYLevel;
            // *** END FLATTEN Y ***

            // Check if position actually changed AFTER flattening
            // Use a small threshold to avoid tiny floating point changes triggering logic
            if (Vector3.SqrMagnitude(newPos - currentPos) > 0.00001f)
            {
                Undo.RecordObject(creator, "Move Point"); // Record before modifying

                // Move the primary point using the Path class's method
                currentPath.MovePoint(pointIdx, newPos);

                // If the moved point was an intersection anchor, move linked anchors
                if (isAnchor && currentPath.IsIntersection(pointIdx))
                {
                    // Pass original 3D position and new flattened position
                    MoveLinkedIntersections(pathIdx, pointIdx, currentPos, newPos);
                }
                // Repaint is implicitly handled by handle interaction
            }
		}
	}

/// <summary>
/// Determines the color and diameter for a handle based on its type and state.
/// </summary>
/// <param name="currentPath">The path the point belongs to.</param>
/// <param name="pathIdx">The index of the path.</param>
/// <param name="pointIdx">The index of the point.</param>
/// <param name="color">Output: The determined color for the handle.</param>
/// <param name="diameter">Output: The determined diameter for the handle.</param>
void DetermineHandleAppearance(Path currentPath, int pathIdx, int pointIdx, out Color color, out float diameter)
{
    bool isAnchor = pointIdx % 3 == 0;
    bool isActive = (pathIdx == activePathIndex && pointIdx == activeAnchorIndex);

    if (isAnchor) // Anchor point
    {
        if (isActive)
        {
            color = activeAnchorColor; // Use the dedicated active color
            diameter = creator.splineParameters.anchorDiameter * intersectionAnchorDiameterModifier;
        }
        else if (currentPath.IsIntersection(pointIdx))
        {
            color = intersectionAnchorColor; // Intersection color
            diameter = creator.splineParameters.anchorDiameter * intersectionAnchorDiameterModifier;
        }
        else // Regular anchor
        {
            color = creator.splineParameters.anchorColor;
            diameter = creator.splineParameters.anchorDiameter;
        }
    }
    else // Control point
    {
        color = creator.splineParameters.controlPointColor;
        diameter = creator.splineParameters.controlPointDiameter;
    }
}

/// <summary>
/// Draws the visual indicator line pointing out from an intersection anchor.
/// </summary>
/// <param name="currentPath">The path the intersection point belongs to.</param>
/// <param name="intersectionAnchorIdx">The index of the intersection anchor point.</param>
void DrawIntersectionExitLine(Path currentPath, int intersectionAnchorIdx)
{
    int nextPointIndex = intersectionAnchorIdx + 1;
    // Check bounds within this path
    if (nextPointIndex < currentPath.NumPoints)
    {
        Vector3 startPoint = currentPath[intersectionAnchorIdx];
        Vector3 endPoint = currentPath[nextPointIndex]; // The first control point *after*
        // Only draw if distinct
        if (Vector3.Distance(startPoint, endPoint) > 0.001f)
        {
            Color exitLineColor = Color.cyan;
            Handles.color = exitLineColor;
            Handles.DrawLine(startPoint, endPoint);
            // Reset color potentially? Handled by next handle setting its color.
        }
    }
}

/// <summary>
/// Finds and moves other intersection anchors that were at the original position of a moved anchor.
/// (This function remains the same as before)
/// </summary>
void MoveLinkedIntersections(int movedPathIdx, int movedAnchorIdx, Vector3 originalPos, Vector3 newPos)
{
    const float linkingThresholdSqr = 0.001f * 0.001f; // Squared threshold

    for (int searchPathIdx = 0; searchPathIdx < creator.paths.Count; searchPathIdx++)
    {
        Path searchPath = creator.paths[searchPathIdx];
        if (searchPath == null) continue;

        for (int searchAnchorIdx = 0; searchAnchorIdx < searchPath.NumPoints; searchAnchorIdx += 3)
        {
            if (searchPathIdx == movedPathIdx && searchAnchorIdx == movedAnchorIdx) continue; // Skip self

            if (searchPath.IsIntersection(searchAnchorIdx))
            {
                if ((searchPath[searchAnchorIdx] - originalPos).sqrMagnitude < linkingThresholdSqr)
                {
                    searchPath.MovePoint(searchAnchorIdx, newPos);
                }
            }
        }
    }
}

#endregion // End Drawing Methods
     static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
         t = Mathf.Clamp01(t);
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        Vector3 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;
        return p;
    }

    Vector3 FindNearestPointOnBezierSegment(Vector3 point, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, out float tValue, int steps = 25)
    {
        float minSqrDistance = float.MaxValue;
        Vector3 nearestPoint = p0;
        float bestT = 0f;
        float stepSize = 1f / Mathf.Max(1, steps);

        for (int i = 0; i <= steps; i++) { /* ... find coarse point ... */
             float t = i * stepSize;
             Vector3 pointOnCurve = EvaluateCubicBezier(p0, p1, p2, p3, t);
             float sqrDistance = (point - pointOnCurve).sqrMagnitude;
             if (sqrDistance < minSqrDistance)
             {
                 minSqrDistance = sqrDistance;
                 nearestPoint = pointOnCurve;
                 bestT = t;
             }
        }
        // Simple refinement (optional but good)
        for (int r = 0; r < 3; r++) { /* ... refine ... */
            float finerStep = stepSize * 0.1f;
            float t_minus = Mathf.Clamp01(bestT - finerStep);
            float t_plus = Mathf.Clamp01(bestT + finerStep);
            Vector3 p_minus = EvaluateCubicBezier(p0, p1, p2, p3, t_minus);
            Vector3 p_plus = EvaluateCubicBezier(p0, p1, p2, p3, t_plus);
            float sqrDist_minus = (point - p_minus).sqrMagnitude;
            float sqrDist_plus = (point - p_plus).sqrMagnitude;
            if (sqrDist_minus < minSqrDistance) { minSqrDistance = sqrDist_minus; nearestPoint = p_minus; bestT = t_minus; stepSize = finerStep; }
            else if (sqrDist_plus < minSqrDistance) { minSqrDistance = sqrDist_plus; nearestPoint = p_plus; bestT = t_plus; stepSize = finerStep; }
            else { break; }
        }
        tValue = bestT;
        return nearestPoint;
    }

    void OnEnable()
    {
        creator = (PathCreator)target;
        // Ensure paths list exists
        if (creator.paths == null) creator.paths = new List<Path>();
        // Restore intersection data for all paths when editor enables
        foreach (var p in creator.paths) { if (p != null) p.RestoreIntersectionIndicesFromSerializedData(); }

        Undo.undoRedoPerformed += OnUndoRedo;
    }

     void OnDisable()
     {
         Undo.undoRedoPerformed -= OnUndoRedo;
     }

      void OnUndoRedo()
    {
        // Need to restore intersection data for ALL paths after undo/redo
        if (creator != null && creator.paths != null)
        {
            foreach (var p in creator.paths)
            {
                if (p != null) p.RestoreIntersectionIndicesFromSerializedData();
            }
        }
        // Reset selection indices as they might be invalid after undo/redo
        selectedPathIndex = -1;
        selectedSegmentIndex = -1;

        // *** Reset active anchor selection ***
        activePathIndex = -1;
        activeAnchorIndex = -1;

        SceneView.RepaintAll();
        Repaint(); // Repaint inspector
    }

    /// <summary>
	/// Handles the 'X' key press to delete the anchor nearest to the mouse cursor.
	/// </summary>
	void HandleDeleteKeyPress(int nearestAnchorPathIdx, int nearestAnchorIdx, Event guiEvent)
	{
		if (nearestAnchorIdx != -1) // Found an anchor near the mouse
		{
            // Check path index validity before accessing
            if (nearestAnchorPathIdx < 0 || nearestAnchorPathIdx >= creator.paths.Count) return;

			Path pathToDeleteFrom = creator.paths[nearestAnchorPathIdx];
			// Debug.Log($"X Key: Trying to delete anchor {nearestAnchorIdx} on path {nearestAnchorPathIdx}.");

			// Check if deletion is allowed (same logic as right-click delete before)
			if (pathToDeleteFrom != null && (pathToDeleteFrom.NumSegments > 1 || (pathToDeleteFrom.IsClosed && pathToDeleteFrom.NumSegments > 0)))
			{
				// Deselect if deleting the active anchor
				if (nearestAnchorPathIdx == activePathIndex && nearestAnchorIdx == activeAnchorIndex)
				{
					activePathIndex = -1; activeAnchorIndex = -1;
				}

				Undo.RecordObject(creator, "Delete Segment (Key)");
				pathToDeleteFrom.DeleteSegment(nearestAnchorIdx);

				// Cleanup empty path (uses existing helper)
				CleanupEmptyPath(nearestAnchorPathIdx);

				guiEvent.Use(); // Consume the key press event
				SceneView.RepaintAll();
			}
			else
			{
				Debug.LogWarning("Cannot delete the last segment of an open path, or path is invalid.");
			}
		}
		// else: No anchor nearby, key press does nothing in this context
	}

    // Add this function to PathEditor.cs

	/// <summary>
	/// Handles right-clicks to display the appropriate context menu.
	/// </summary>
	void HandleContextMenu(Event guiEvent, Vector3 mousePos, int nearestAnchorPathIdx, int nearestAnchorIdx)
	{
		GenericMenu menu = new GenericMenu();

		// --- Determine Context: On Anchor or In Space ---
		bool onAnchor = nearestAnchorIdx != -1;

		if (onAnchor)
		{
            // --- Build Menu for Right-Clicking ON an Anchor ---
            AddAnchorContextMenuItems(menu, nearestAnchorPathIdx, nearestAnchorIdx);
		}
		else
		{
			// --- Build Menu for Right-Clicking IN SPACE ---
            AddSpaceContextMenuItems(menu, mousePos);
		}

        // Display the menu at the mouse position
		menu.ShowAsContext();
		guiEvent.Use(); // Consume the right-click so it doesn't trigger other actions
	}

    /// <summary>
    /// Adds menu items specific to right-clicking ON an anchor point.
    /// </summary>
    void AddAnchorContextMenuItems(GenericMenu menu, int targetPathIdx, int targetAnchorIdx)
    {
        // Data needed for menu actions
        Path targetPath = (targetPathIdx >= 0 && targetPathIdx < creator.paths.Count) ? creator.paths[targetPathIdx] : null;
        if (targetPath == null) return; // Safety check

        bool isTargetAnIntersection = targetPath.IsIntersection(targetAnchorIdx);
        bool isTargetTheActive = (targetPathIdx == activePathIndex && targetAnchorIdx == activeAnchorIndex);
        bool isActiveAnchorValid = activeAnchorIndex != -1 && activePathIndex != -1;

        // --- Menu Items ---

        // Make Intersection
        if (!isTargetAnIntersection) {
            menu.AddItem(new GUIContent("Make Intersection"), false, () => { DoMakeIntersection(targetPathIdx, targetAnchorIdx); });
        } else {
            // Optional: Add "Remove Intersection Mark"? Or just let Merge/Delete handle it? For now, disable if already one.
            menu.AddDisabledItem(new GUIContent("Make Intersection"));
        }

        // Merge Active Here
        if (isActiveAnchorValid && !isTargetTheActive) { // Can merge if an *other* anchor is active
             menu.AddItem(new GUIContent("Merge Active Here"), false, () => { DoMergeActiveHere(targetPathIdx, targetAnchorIdx); });
        } else {
             menu.AddDisabledItem(new GUIContent("Merge Active Here"));
        }

        // Delete
        // Check if deletable (same rules as X key)
        bool canDelete = (targetPath.NumSegments > 1 || (targetPath.IsClosed && targetPath.NumSegments > 0));
        if(canDelete) {
            menu.AddItem(new GUIContent("Delete Anchor"), false, () => { DoDeleteAnchor(targetPathIdx, targetAnchorIdx); });
        } else {
            menu.AddDisabledItem(new GUIContent("Delete Anchor"));
        }

        menu.AddSeparator("");

        // Select / Deselect
        if (!isTargetTheActive) {
             menu.AddItem(new GUIContent("Select This Anchor"), false, () => { DoSelectAnchor(targetPathIdx, targetAnchorIdx); });
        }
        if (isTargetTheActive) { // Only show deselect if right-clicking the active one
             menu.AddItem(new GUIContent("Deselect Active Anchor"), false, DoDeselectActiveAnchor);
        }

    }

     /// <summary>
    /// Adds menu items specific to right-clicking IN EMPTY SPACE.
    /// </summary>
    void AddSpaceContextMenuItems(GenericMenu menu, Vector3 mousePos)
    {
        bool isActiveAnchorValid = activeAnchorIndex != -1 && activePathIndex != -1;
        bool canExtendFromActive = false;

        if (isActiveAnchorValid) {
            Path activePath = creator.paths[activePathIndex];
            // Check if active anchor is a valid non-intersection start/end point
            canExtendFromActive = !activePath.IsClosed &&
                                  (activeAnchorIndex == 0 || activeAnchorIndex == activePath.NumPoints - 1) &&
                                  !activePath.IsIntersection(activeAnchorIndex);
        }

        // Add Segment Here
        if (isActiveAnchorValid && canExtendFromActive) {
             menu.AddItem(new GUIContent("Add Segment Here"), false, () => { DoAddSegment(mousePos); });
        } else {
             menu.AddDisabledItem(new GUIContent("Add Segment Here"));
        }

        // Add Segment & Mark Origin Intersection
        if (isActiveAnchorValid) { // Can always mark origin if adding from active
            menu.AddItem(new GUIContent("Add Segment & Mark Origin Intersection"), false, () => { DoAddSegmentMarkIntersection(mousePos); });
        } else {
             menu.AddDisabledItem(new GUIContent("Add Segment & Mark Origin Intersection"));
        }

         // Split Nearest Segment Here
         menu.AddItem(new GUIContent("Split Nearest Segment Here"), false, () => { DoSplitNearestSegment(mousePos); });


        menu.AddSeparator("");

        // Create Independent Path
        menu.AddItem(new GUIContent("Create Independent Path"), false, DoCreateIndependentPath);

        // Deselect Active Anchor
        if (isActiveAnchorValid) {
            menu.AddItem(new GUIContent("Deselect Active Anchor"), false, DoDeselectActiveAnchor);
        }
    }

    // Add these functions to PathEditor.cs

    // --- Context Menu Action Implementations ---

    // Add/Replace these functions inside the PathEditor class

    // --- Context Menu Action Implementations ---

    /// <summary>
    /// Action: Marks the specified anchor as an intersection point.
    /// </summary>
    void DoMakeIntersection(int pathIdx, int anchorIdx)
    {
        // Basic validation
        if (pathIdx < 0 || pathIdx >= creator.paths.Count) return;
        Path path = creator.paths[pathIdx];
        if (path == null || anchorIdx < 0 || anchorIdx >= path.NumPoints || anchorIdx % 3 != 0) return;

        // Debug.Log($"Action: Make Intersection {pathIdx}:{anchorIdx}");
        Undo.RecordObject(creator, "Make Intersection"); // Record the change
        path.MarkAsIntersection(anchorIdx); // Mark the point
        SceneView.RepaintAll(); // Update the view
    }

    /// <summary>
    /// Action: Merges the currently active anchor onto the target anchor.
    /// </summary>
    void DoMergeActiveHere(int targetPathIdx, int targetAnchorIdx)
    {
        // Validate active and target anchors
        if (activeAnchorIndex == -1 || activePathIndex == -1 ||
            targetAnchorIdx == -1 || targetPathIdx == -1 ||
            activePathIndex >= creator.paths.Count || targetPathIdx >= creator.paths.Count)
        {
            Debug.LogWarning("Merge requires a valid active anchor and a valid target anchor.");
            return;
        }
        Path activePath = creator.paths[activePathIndex];
        Path targetPath = creator.paths[targetPathIdx];
        if (activePath == null || targetPath == null ||
            activeAnchorIndex >= activePath.NumPoints || targetAnchorIdx >= targetPath.NumPoints)
        {
             Debug.LogWarning("Merge target paths or indices are invalid.");
             return;
        }

        // Don't merge onto self
        if (activePathIndex == targetPathIdx && activeAnchorIndex == targetAnchorIdx) return;

        Vector3 activePos = activePath[activeAnchorIndex];
        Vector3 targetPos = targetPath[targetAnchorIdx];

        // Optional distance check again for safety
        if ((activePos - targetPos).sqrMagnitude > mergeDistanceThresholdSqr) {
            Debug.LogWarning("Anchors too far apart to merge via menu.");
            return;
        }

        // Debug.Log($"Action: Merge Active ({activePathIndex}:{activeAnchorIndex}) onto Target ({targetPathIdx}:{targetAnchorIdx})");
        Undo.RecordObject(creator, "Merge Active Anchor"); // Record state BEFORE changes

        // Move the active anchor and its linked points to the target position
        activePath.MovePoint(activeAnchorIndex, targetPos);
        MoveLinkedIntersections(activePathIndex, activeAnchorIndex, activePos, targetPos);

        // Mark both anchors as intersections
        activePath.MarkAsIntersection(activeAnchorIndex); // Mark the one that moved
        targetPath.MarkAsIntersection(targetAnchorIdx); // Mark the destination

        // Deselect after merge
        activePathIndex = -1;
        activeAnchorIndex = -1;
        SceneView.RepaintAll(); // Update the view
    }

    /// <summary>
    /// Action: Deletes the specified anchor point and related segment data.
    /// </summary>
    void DoDeleteAnchor(int pathIdx, int anchorIdx)
    {
         // Basic validation
        if (pathIdx < 0 || pathIdx >= creator.paths.Count) return;
        Path pathToDeleteFrom = creator.paths[pathIdx];
        if (pathToDeleteFrom == null || anchorIdx < 0 || anchorIdx >= pathToDeleteFrom.NumPoints || anchorIdx % 3 != 0) return;

        // Check if deletion is allowed
        bool canDelete = (pathToDeleteFrom.NumSegments > 1 || (pathToDeleteFrom.IsClosed && pathToDeleteFrom.NumSegments > 0));
        if (!canDelete) {
            Debug.LogWarning("Deletion not allowed (cannot delete last segment of open path).");
            return;
        }

        // Debug.Log($"Action: Delete Anchor {pathIdx}:{anchorIdx}");

        // Deselect if deleting the active anchor
        if (pathIdx == activePathIndex && anchorIdx == activeAnchorIndex)
        {
            DoDeselectActiveAnchor(); // Use the deselect function
        }

        Undo.RecordObject(creator, "Delete Anchor (Menu)"); // Record BEFORE delete
        pathToDeleteFrom.DeleteSegment(anchorIdx);
        CleanupEmptyPath(pathIdx); // Check if path needs removal AFTER delete
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Action: Sets the specified anchor as the active one.
    /// </summary>
     void DoSelectAnchor(int pathIdx, int anchorIdx)
     {
         // Basic validation
        if (pathIdx < 0 || pathIdx >= creator.paths.Count) return;
        Path path = creator.paths[pathIdx];
        if (path == null || anchorIdx < 0 || anchorIdx >= path.NumPoints || anchorIdx % 3 != 0) return;

        // Debug.Log($"Action: Select Anchor {pathIdx}:{anchorIdx}");
        activePathIndex = pathIdx;
        activeAnchorIndex = anchorIdx;
        SceneView.RepaintAll(); // Repaint needed to show selection change
     }

    /// <summary>
    /// Action: Clears the active anchor selection.
    /// </summary>
     void DoDeselectActiveAnchor()
     {
        if (activeAnchorIndex != -1) // Only log/repaint if something *was* selected
        {
             // Debug.Log("Action: Deselect Active Anchor");
             activePathIndex = -1;
             activeAnchorIndex = -1;
             SceneView.RepaintAll(); // Repaint needed to show selection change
        }
     }

    /// <summary>
    /// Action: Adds a new segment from the active anchor (if valid) to the specified position.
    /// </summary>
    void DoAddSegment(Vector3 mousePos)
    {

        Vector3 targetPos = mousePos;
        targetPos.y = CreatorYLevel;

        // Check if an active anchor is selected and valid
        if (activeAnchorIndex == -1 || activePathIndex == -1 || activePathIndex >= creator.paths.Count) {
            Debug.LogWarning("Cannot Add Segment: No active anchor selected.");
            return;
        }
        Path selectedPath = creator.paths[activePathIndex];
        if (selectedPath == null || activeAnchorIndex >= selectedPath.NumPoints) {
             Debug.LogWarning("Cannot Add Segment: Active path or anchor index invalid.");
             return;
        }

        // Check if active anchor is a valid non-intersection start/end point
        bool isOpenPathEnd = !selectedPath.IsClosed && (activeAnchorIndex == 0 || activeAnchorIndex == selectedPath.NumPoints - 1);
        bool isIntersection = selectedPath.IsIntersection(activeAnchorIndex);

        if (isOpenPathEnd && !isIntersection) {
            // Debug.Log($"Action: Add Segment Here (from {activePathIndex}:{activeAnchorIndex})");
            Undo.RecordObject(creator, "Add Segment (Menu)");
            bool segmentAdded = false;

            if (activeAnchorIndex == selectedPath.NumPoints - 1) { // Add last
                selectedPath.AddSegment(targetPos);
                activeAnchorIndex = selectedPath.NumPoints - 1; // Update active anchor
                segmentAdded = true;
            } else if (activeAnchorIndex == 0) { // Prepend first
                // Requires InsertPoints method in Path.cs
                try {
                    Vector3 oldStartPos = selectedPath[0];
                    Vector3 nextControlPos = (selectedPath.NumPoints > 1) ? selectedPath[1] : oldStartPos;
                    Vector3 control2 = oldStartPos + (oldStartPos - nextControlPos).normalized * 0.5f;
                    Vector3 control1 = targetPos + (targetPos - control2).normalized * 0.5f;
                    selectedPath.InsertPoints(0, new Vector3[] { targetPos, control1, control2 });
                    activeAnchorIndex = 0; // New start is active
                    segmentAdded = true;
                } catch (System.Exception ex) {
                    Debug.LogError($"Error prepending segment via menu. Ensure Path.InsertPoints exists and works. Error: {ex.Message}");
                    Undo.PerformUndo(); // Revert recording
                }
            }

            if(segmentAdded) SceneView.RepaintAll();

        } else {
            Debug.LogWarning("Cannot add segment from this active anchor (must be non-intersection start/end of an open path).");
        }
    }

    /// <summary>
    /// Action: Adds a segment from the active anchor and marks the active anchor as an intersection.
    /// Handles adding from start/end or splitting from middle anchors.
    /// </summary>
    void DoAddSegmentMarkIntersection(Vector3 mousePos)
    {

        Vector3 targetPos = mousePos;
        targetPos.y = CreatorYLevel;
        // Check if an active anchor is selected and valid
        if (activeAnchorIndex == -1 || activePathIndex == -1 || activePathIndex >= creator.paths.Count) {
            Debug.LogWarning("Cannot Add Segment: No active anchor selected.");
            return;
        }
        Path selectedPath = creator.paths[activePathIndex];
        if (selectedPath == null || activeAnchorIndex >= selectedPath.NumPoints) {
            Debug.LogWarning("Cannot Add Segment: Active path or anchor index invalid.");
            return;
        }

        // Debug.Log($"Action: Add Segment & Mark Origin Intersection (from {activePathIndex}:{activeAnchorIndex})");
        Undo.RecordObject(creator, "Add Segment & Mark Intersection (Menu)");
        bool operationDone = false;

        int originalActiveAnchor = activeAnchorIndex; // Store original index before potential changes

        // --- Add/Prepend Logic (similar to DoAddSegment but marks intersection) ---
        if (activeAnchorIndex == selectedPath.NumPoints - 1 || (selectedPath.IsClosed && activeAnchorIndex % 3 == 0) ) { // Add last (or anywhere on closed path)
            selectedPath.AddSegment(targetPos);
            // Mark the ORIGINAL anchor as intersection
            selectedPath.MarkAsIntersection(originalActiveAnchor);
            activeAnchorIndex = selectedPath.NumPoints - 1; // New end is active
            operationDone = true;
        }
        else if (activeAnchorIndex == 0 && !selectedPath.IsClosed) { // Prepend first
             try {
                 Vector3 oldStartPos = selectedPath[0]; Vector3 nextControlPos = (selectedPath.NumPoints > 1) ? selectedPath[1] : oldStartPos; Vector3 control2 = oldStartPos + (oldStartPos - nextControlPos).normalized * 0.5f; Vector3 control1 = targetPos + (targetPos - control2).normalized * 0.5f;
                 selectedPath.InsertPoints(0, new Vector3[] { targetPos, control1, control2 });
                 // Mark the *original* start anchor (which is now at index 3)
                 selectedPath.MarkAsIntersection(3);
                 activeAnchorIndex = 0; // New start is active
                 operationDone = true;
             } catch (System.Exception ex) {
                  Debug.LogError($"Error prepending segment via menu. Ensure Path.InsertPoints exists. Error: {ex.Message}");
                  Undo.PerformUndo();
             }
        }
        // --- Handle adding from MIDDLE anchor (requires split) ---
        else if (!selectedPath.IsClosed && activeAnchorIndex > 0 && activeAnchorIndex < selectedPath.NumPoints - 1)
        {
            // Split the segment *following* the active anchor, using targetPos as the new anchor position
            int segmentIdxToSplit = originalActiveAnchor / 3;

            if (segmentIdxToSplit < selectedPath.NumSegments)
            {
                 // Split the segment. SplitSegment inserts points and returns the index of the new anchor.
                 int newAnchorIndex = selectedPath.SplitSegment(targetPos, segmentIdxToSplit);

                 // Mark the ORIGINAL active anchor as an intersection
                 selectedPath.MarkAsIntersection(originalActiveAnchor);

                 // Update active anchor to the newly created one from the split
                 activeAnchorIndex = newAnchorIndex;
                 // activePathIndex remains the same

                 // Debug.Log($"Split segment {segmentIdxToSplit} at {targetPos}, marked original anchor {originalActiveAnchor} as intersection. New active: {activeAnchorIndex}");
                 operationDone = true;
            } else {
                Debug.LogError("Cannot Add/Mark Intersection: Failed to determine segment index for split.");
                 Undo.PerformUndo();
            }
        } else {
             Debug.LogWarning("Add Segment & Mark Intersection: Case not handled (potentially closed path issue?).");
              Undo.PerformUndo();
        }


        if (operationDone) SceneView.RepaintAll();
    }

    /// <summary>
    /// Action: Finds the nearest segment/point and splits the segment there.
    /// </summary>
    void DoSplitNearestSegment(Vector3 mousePos)
    {
        Vector3 targetPos = mousePos;
        targetPos.y = CreatorYLevel;
        // Debug.Log("Action: Split Nearest Segment Here");

        // --- Find Closest Point (Similar to HandleAltClick Step 1) ---
        Path closestPath = null; int closestPathIdx = -1; int closestSegmentIdx = -1;
        Vector3 pointOnCurve = Vector3.zero; float tOnSegment = 0f; float minDistanceSqr = float.MaxValue;

        for (int pathIndex = 0; pathIndex < creator.paths.Count; pathIndex++) {
             Path currentPath = creator.paths[pathIndex]; if (currentPath == null || currentPath.NumSegments == 0) continue;
             for (int segmentIndex = 0; segmentIndex < currentPath.NumSegments; segmentIndex++) {
                 Vector3[] segmentPoints = currentPath.GetPointsInSegment(segmentIndex); if (segmentPoints == null || segmentPoints.Length < 4) continue;
                 float currentT; Vector3 currentClosestPoint = FindNearestPointOnBezierSegment(targetPos, segmentPoints[0], segmentPoints[1], segmentPoints[2], segmentPoints[3], out currentT, 25);
                 float distanceSqr = (targetPos - currentClosestPoint).sqrMagnitude;
                 if (distanceSqr < minDistanceSqr) {
                     minDistanceSqr = distanceSqr; closestPath = currentPath; closestPathIdx = pathIndex; closestSegmentIdx = segmentIndex; pointOnCurve = currentClosestPoint; tOnSegment = currentT;
                 }
             }
         }

        // --- Check Threshold and Split ---
        // Use a standard selection threshold, maybe slightly larger than hover threshold
        float splitThreshold = segmentSelectDistanceThreshold * 1.5f;
        float splitThresholdSqr = splitThreshold * splitThreshold;

         if (closestPath != null && minDistanceSqr <= splitThresholdSqr) {
            Undo.RecordObject(creator, "Split Segment (Menu)");
            // Split at the calculated closest point on the curve for precision
            closestPath.SplitSegment(pointOnCurve, closestSegmentIdx);
            DoDeselectActiveAnchor(); // Deselect active anchor after splitting
            SceneView.RepaintAll();
         } else {
            Debug.Log("No segment close enough to split.");
         }
    }

    /// <summary>
    /// Action: Creates a new, separate path object.
    /// </summary>
    void DoCreateIndependentPath() {
         // Debug.Log("Action: Create Independent Path");
         Undo.RecordObject(creator, "Create Independent Path (Menu)");
         creator.CreatePath(); // PathCreator.CreatePath now adds to its list
         // Select the new path and deselect any active anchor/segment
         selectedPathIndex = creator.paths.Count - 1;
         selectedSegmentIndex = -1;
         DoDeselectActiveAnchor(); // Deselect active anchor
         SceneView.RepaintAll();
    }

    // --- End Context Menu Actions ---
}