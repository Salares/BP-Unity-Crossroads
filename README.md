# BP - Unity procedural crossroads/intersection generator

## Overview

This project is a Unity tool for procedurally generating crossroads and paths. It allows users to create complex road networks with ease.

Main features include:

*   **Interactive Path Creation:** Design paths using splines directly within the Unity editor.
*   **Procedural Crossroad Generation:** Automatically generate crossroads that connect multiple paths.
*   **Customizable Parameters:** Adjust various parameters to control the appearance and geometry of paths and crossroads, such as width, material, and the number of lanes.
*   **Mesh Generation:** Generate the 3D meshes for roads and crossroads, ready to be used in a Unity scene.

## How to Use the Path Creator

This section explains how to get started with the Path Creator tool to design and manipulate paths in your Unity project.

### Adding the PathCreator Component

1.  Create an empty GameObject in your scene (GameObject -> Create Empty).
2.  Select the newly created GameObject.
3.  In the Inspector window, click "Add Component".
4.  Search for "PathCreator" and select it, or drag the `PathCreator.cs` script (located in `Assets/Scripts/Path/`) onto the GameObject.

This will add the `PathCreator` component to the GameObject. Upon adding, it automatically creates a new `Path` object, which represents the spline your GameObject will now control.

### Path Structure

The `PathCreator` component works with a `Path` object. This `Path` object is essentially a Bézier spline, a series of points that define a smooth curve. It consists of:

*   **Anchor Points:** These are the main points that the curve passes through.
*   **Control Points:** Each anchor point has two control points (one for the incoming curve segment and one for the outgoing curve segment) that determine the shape of the curve around that anchor.

### Inspector Parameters (`splineParameters`)

The `PathCreator` component exposes several parameters in the Inspector under the "Spline Parameters" foldout. These allow you to customize the visual appearance of the path editor gizmos in the Scene view:

*   `anchorColor`: Sets the color of the anchor point gizmos. Default: Red.
*   `controlPointColor`: Sets the color of the control point gizmos. Default: Blue.
*   `segmentColor`: Sets the color of the lines connecting the path segments. Default: Green.
*   `selectedSegmentColor`: Sets the color of a path segment when it's selected (e.g., by clicking on it). Default: Yellow.
*   `handlesColor`: Sets the color of the handles used to manipulate points (e.g., position handles). Default: Black.
*   `anchorDiameter`: Controls the size of the anchor point gizmos in the Scene view.
*   `controlPointDiameter`: Controls the size of the control point gizmos in the Scene view.
*   `splineWidth`: Determines the thickness of the lines drawn for the path segments in the Scene view.
*   `displayPoints`: Toggles the visibility of all path points (anchors and controls) in the Scene view.

These parameters only affect the editor visualization and do not change the geometry of the path itself.

### Manipulating the Path in the Editor

The `PathEditor.cs` script provides a custom inspector for the `PathCreator` component and enables interactive manipulation of the path directly in the Scene view.

*   **Selecting the Path:** Select the GameObject that has the `PathCreator` component. Its path will become visible and editable in the Scene view.
*   **Viewing Points:** Anchor points and their associated control points will be displayed as gizmos.
*   **Moving Points:**
    *   Click and drag an **anchor point** to move it. The connected curve segments will update.
    *   Click and drag a **control point** to change the curvature of the path around its associated anchor point.
*   **Adding Anchor Points:** To extend the path or add points in between, you can typically **Shift-click** in the Scene view at the desired position to add a new anchor point. The new point will be connected to the previously last point or the closest segment.
*   **Path Options (Inferred from typical spline editors and previous README):**
    *   **Closing the Path:** There is likely an option in the inspector (or a keyboard shortcut) to connect the last anchor point to the first, forming a closed loop ("Uzavření smyčky").
    *   **Auto-placing Control Points:** There might be a feature to automatically calculate and place control points to maintain smooth curves, potentially when new anchor points are added or existing ones are moved ("Automatické umístění ovládacích bodů").

The exact controls and additional features (like deleting points, splitting segments, etc.) are provided by the `PathEditor` and might be accessible via buttons in the Inspector or specific keyboard shortcuts when the path-enabled GameObject is selected.

## How to Use the Crossroad Creator

This section describes how to use the Crossroad Creator tool to generate intersections for your paths.

### Adding the CrossroadCreator Component

1.  Create an empty GameObject in your scene (GameObject -> Create Empty).
2.  Select the newly created GameObject.
3.  In the Inspector window, click "Add Component".
4.  Search for "CrossroadCreator" and select it, or drag the `CrossroadCreator.cs` script (located in `Assets/Scripts/Crossroads/`) onto the GameObject.

Adding this component will automatically initialize a `Crossroad` object.

### Crossroad Structure

The `CrossroadCreator` component manages a `Crossroad` object. This object represents the intersection and is primarily composed of multiple `Path` objects (the same type used by the `PathCreator`) that are procedurally generated and positioned to form the crossroad. Each of these paths defines one of the arms of the intersection.

### Inspector Parameters

The `CrossroadCreator` component has several parameters available in the Inspector to customize the crossroad's appearance and structure:

#### Spline Parameters (`splineParameters`)

This group of settings is identical to the one found in the `PathCreator`. It controls the visual representation of the individual paths that make up the crossroad in the Unity Scene view (e.g., colors and sizes of anchors, control points, and segments). These settings do not affect the final geometry of the crossroad mesh but aid in visualizing its construction.

*   `anchorColor`: Color for anchor point gizmos.
*   `controlPointColor`: Color for control point gizmos.
*   `segmentColor`: Color for path segment lines.
*   `selectedSegmentColor`: Color for selected path segment lines.
*   `handlesColor`: Color for manipulation handles.
*   `anchorDiameter`: Size of anchor point gizmos.
*   `controlPointDiameter`: Size of control point gizmos.
*   `splineWidth`: Thickness of path segment lines.
*   `displayPoints`: Toggles visibility of all path points.

#### Crossroad Options

*   `numberOfPaths`: An integer (ranging from 2 to 16) that determines how many individual paths will meet at this crossroad. For example, a value of 4 will create a typical four-way intersection.

#### Road Positioning

These parameters control the initial shape and placement of the Bézier curve points for each path that forms the crossroad, relative to the center of the `CrossroadCreator` GameObject.

*   `startPointOffset`: Defines the distance from the crossroad's center to the start anchor point of each path. This essentially controls the size of the central open area of the intersection.
*   `endPointOffset`: Defines the distance from the crossroad's center to the end anchor point of each path. This determines the length of the arms of the intersection.
*   `controlPointOffset`: Influences the position of the control points associated with the start and end anchor points of each path. Smaller values make the paths straighter as they enter/leave the intersection, while larger values can create more pronounced curves. Specifically, it adjusts the control points along the tangent of the curve at the start/end points.

### Interaction and Visualization

The `CrossroadCreator` automatically generates the layout of the intersecting paths based on the `numberOfPaths` and the "Road Positioning" parameters. The center of the crossroad will be at the position of the GameObject containing the `CrossroadCreator` component.

*   **Automatic Generation:** When you change parameters like `numberOfPaths` or the offset values, the crossroad's paths will regenerate in the Scene view to reflect these changes.
*   **Visualization:** The individual paths forming the crossroad are drawn in the Scene view using the `splineParameters`.
*   **Editor Tools:** The `CrossroadEditor.cs` script provides the custom inspector for `CrossroadCreator`. While the primary layout is procedural, the `CrossroadEditor` offers visualization aids. Fine-tuning of the individual paths generated by `CrossroadCreator` is typically done by adjusting the `CrossroadCreator`'s parameters. Direct manipulation of sub-paths might be possible if the `CrossroadEditor` exposes such functionality or if paths are manually detached.

Typically, after setting up the `CrossroadCreator`, you would then add the `CrossroadPlacer.cs` component to the same GameObject to generate the actual 3D meshes for the intersection, as detailed in the 'Generating Geometry (Meshes)' section.

## Generating Geometry (Meshes)

The `PathCreator` and `CrossroadCreator` components are primarily responsible for defining the data and structure of your paths and intersections—where the points are, how they connect, and the overall layout. They draw gizmos in the editor to help you visualize this data, but they don't create the actual 3D meshes that you see in your game.

For generating the visible 3D geometry, you use separate "placer" scripts. This project provides examples of such scripts:

*   `Assets/Scripts/Path/Examples/RoadPlacer.cs`: This script takes the data from a `PathCreator` on the same GameObject and generates a road mesh along the defined path.
*   `Assets/Scripts/Crossroads/Examples/CrossroadPlacer.cs`: This script works with a `CrossroadCreator` on the same GameObject. It generates the complete mesh for the intersection, including the road segments forming the arms of the crossroad and the central area where they meet.

### Using Placer Scripts

1.  **For Single Paths/Roads:**
    *   Add a `PathCreator` to a GameObject and design your path.
    *   Then, add the `RoadPlacer.cs` component to the same GameObject.
    *   Adjust its parameters (like `roadWidth`, `spacing`, `tiling`) and call its `UpdateRoad()` method (often triggered by a button in the inspector or automatically if `autoUpdate` is enabled) to generate the road mesh.

2.  **For Crossroads:**
    *   Add a `CrossroadCreator` to a GameObject and configure your intersection.
    *   Then, add the `CrossroadPlacer.cs` component to the same GameObject.
    *   This script will use the path data from `CrossroadCreator` to generate the meshes for each connecting road segment and the central intersection area.
    *   The method `UpdateCrossroad()` in `CrossroadPlacer.cs` is responsible for generating/updating the crossroad geometry. This might be triggered by a button in the inspector.

### `CrossroadPlacer.cs` Parameters

The `CrossroadPlacer.cs` script has several key parameters in its "Crossroad Mesh Options" to control the generated geometry:

*   `roadWidth`: Defines the width of the generated road meshes for the arms of the crossroad.
*   `spacing`: Controls the distance between vertices along the length of the road segments. Smaller values create a denser, smoother mesh.
*   `tiling`: Affects the UV texture coordinates, controlling how textures repeat along the length of the road segments.
*   `roadMaterial`: The material assigned to the generated road segment meshes.
*   `crossroadMaterial`: The material assigned to the generated mesh for the central part of the intersection.

The `RoadPlacer.cs` script has similar parameters (`roadWidth`, `spacing`, `tiling`) for customizing individual road meshes.

### Customization

The provided `RoadPlacer.cs` and `CrossroadPlacer.cs` are example implementations. They demonstrate how to take the path and crossroad data and convert it into renderable meshes.

You might need to:

*   **Modify these scripts:** Adapt their mesh generation logic, UV unwrapping, or material handling to better suit your project's specific visual or performance requirements.
*   **Create your own placer scripts:** If you need different types of road profiles, more complex intersection geometry, level-of-detail (LOD) systems, or integration with other tools, you can write custom scripts that consume the data from `PathCreator` and `CrossroadCreator`.

The separation of data definition (Creators) and mesh generation (Placers) provides flexibility in how you visualize and use the procedural path and crossroad system.

## Required Assets

To achieve visual results for the generated road and crossroad meshes, you need to provide certain assets, primarily **Materials** and **Textures**. The scripts that generate meshes, such as `RoadPlacer.cs` and `CrossroadPlacer.cs`, typically have public fields in the Inspector where you can assign these assets.

### Materials

The mesh generation scripts require Unity [Material](https://docs.unity3d.com/Manual/Materials.html) assets to render the roads and crossroads.

*   **Assignment:** For example, `CrossroadPlacer.cs` has `roadMaterial` and `crossroadMaterial` parameters. You will need to create your own materials in your Unity project (or use existing ones) and drag them onto these slots in the Inspector for the GameObject that has the `CrossroadPlacer` component. Similarly, `RoadPlacer.cs` will require a material for the road mesh.
*   **Shader:** The material will determine how the road looks, including its color, smoothness, and how it reacts to light. This is controlled by the shader selected in the material (e.g., Standard Shader, or a custom road shader).

### Textures

Materials, in turn, typically use [Texture](https://docs.unity3d.com/Manual/Textures.html) assets to define the visual details of the surfaces.

*   **Purpose:** For roads, textures are used for the asphalt or concrete surface, lane markings, cracks, and other details.
*   **Examples in Project:**
    *   The project includes an example road texture: `Assets/Scripts/Path/Examples/road_road_0021_02_tiled.jpg`.
    *   A generic checker pattern `Assets/checker-map_tho.png` is also provided, which can be useful for UV mapping checks.
*   **Sources:** You can create your own textures or find them from various sources. The "Zdroje" (Sources) section of this README links to [Texturelib.com](http://texturelib.com/), which is one possible source for textures.

### Example Assets Provided

The project includes some example materials that you can examine:

*   `Assets/Scripts/Path/Examples/Road.mat`: An example material intended for road segments, likely configured to use the `Assets/Scripts/Path/Examples/road_road_0021_02_tiled.jpg` texture.
*   `Assets/Scripts/Path/Examples/DefaultCrossroad.mat`: An example material intended for the central part of crossroads.
*   `Assets/UV_CHECK.mat`: This material likely uses the `Assets/checker-map_tho.png` texture and can be useful for visualizing UV coordinates on generated meshes to ensure they are mapped correctly.

You can select these `.mat` files in your Unity Project window to see how they are configured in the Inspector (e.g., which textures they use, shader properties).

### Customization

While the example assets can help you get started and test the mesh generation, you are generally expected to **create and use your own materials and textures** to match the specific visual style and requirements of your game or application.

## Autor

- Koláček Daniel - [github](https://github.com/Salares)

## Zdroje

[Textury](http://texturelib.com/)
