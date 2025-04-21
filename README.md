# Auto Object Generator

A Unity Editor tool for automatically generating game objects. This tool streamlines the creation of objects with different sizes and resource types, complete with mesh colliders and LOD configurations.

## Features

- Generate objects in three size categories: Small, Medium, and Large
- Create resource objects with metal and stump variants
- Automatic LOD setup with customizable transitions
- Built-in navmesh generation
- Optional skybox creation
- Automatic collider setup with optimized cooking options
- Customizable culling and shadow distances based on object size

## Installation

1. Copy the `AutoObjectGenerator.cs` script into your Unity project's `Assets/Editor/` folder
   - If the `Editor` folder doesn't exist, create it
   - The script must be in an Editor folder to function as a Unity Editor tool
2. Unity will automatically compile the script
3. The tool will appear in Unity's menu under `Tools > Object & Resource Generator`

## Usage

1. Open the tool via `Tools > Object & Resource Generator` in Unity's menu
2. Select the object type (Small/Medium/Large/Resource)
3. Set a name for your object (optional)
4. Assign the required meshes:
   - Main Mesh (required)
   - Metal Mesh (for resources only)
   - Stump Mesh (for resources only)
5. Assign materials and physics materials
6. Configure LOD settings (optional):
   - Use preset (1% transition by default)
   - Custom LOD levels with custom transitions
   - Adjust culling and shadow distances
7. Add additional LOD models if needed
8. Create skybox variant if needed
9. Click Generate

## Generated Structure

### Standard Object
```
Object
├── Model_0 (main mesh)
├── Model_1 (optional LOD1)
├── Model_2 (optional LOD2)
└── ... additional LOD levels

Nav (separate prefab for navigation)
└── NavMesh Collider

Skybox (optional)
└── Model_0
```

### Resource Object
```
Resource
├── Model_0
│   ├── Metal_0
│   └── Model_0 (main mesh)
└── MeshCollider

Stump (separate prefab)
├── Model_0
│   └── Model_0 (stump mesh)
└── MeshCollider
```

## Default Settings

### Culling Distances
- Small: 50 Meters
- Medium: 100 Meters
- Large: 200 Meters

### Shadow Distances
- Small: 25 Meters
- Medium: 50 Meters
- Large: 100 Meters

## Notes

- All generated objects include optimized mesh colliders
- LOD fade mode is set to None for consistent performance
- Objects are automatically organized in folders by type
- Layer and tag assignments are handled automatically
- Shadow casting is optimized based on LOD level 

## License

This tool is available under the MIT License. See the LICENSE file for details.
