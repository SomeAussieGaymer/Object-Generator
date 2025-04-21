# Auto Object Generator

A Unity Editor tool for automatically generating game objects. This tool streamlines the creation of objects with different sizes and resource types, complete with mesh colliders and LOD configurations.

## Features

- Generate objects in three size categories: Small, Medium, and Large
- Create resource objects with metal and stump variants
- Generate tree objects with customizable trunk, leaves, and stump models
- Create bush objects with foliage effects and optional forage models
- Automatic LOD setup with customizable transitions
- Built-in navmesh generation
- Optional skybox creation
- Automatic collider setup with optimized cooking options
- Customizable culling and shadow distances based on object size
- Foliage shader integration for trees and bushes
- Automatic material generation from textures

## Installation

1. Copy the `AutoObjectGenerator.cs` script into your Unity project's `Assets/Editor/` folder
   - If the `Editor` folder doesn't exist, create it
   - The script must be in an Editor folder to function as a Unity Editor tool
2. Unity will automatically compile the script
3. The tool will appear in Unity's menu under `Tools > Object & Resource Generator`

## Usage

1. Open the tool via `Tools > Object & Resource Generator` in Unity's menu
2. Select the object type (Small/Medium/Large/Resource/Trees/Bushes)
3. Set a name for your object (optional)
4. Assign the required components based on the selected type
5. Click Generate

### Standard Objects (Small/Medium/Large)
- Assign Main Mesh and Material
- Configure LOD settings (optional)
- Add child meshes if needed
- Create skybox variant if needed

### Resource Objects
- Assign Main Mesh, Metal Mesh, and Stump Mesh
- Set Material and Physics Material
- Configure skybox options

### Tree Objects
- Assign Trunk, Leaves, and Stump meshes
- Add additional tree models if needed
- Set trunk and leaves textures
- Assign physics material

### Bush Objects
- Add bush models
- Set bush texture
- Configure forage mesh (optional)
- Assign physics material

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
└── Model_0
    ├── Metal_0
    └── Model_0

Stump
└── Model_0
    └── Model_0
```

### Tree Object
```
Debris
├── Model_0
│   └── Foliage_0
│   └── Model_0
├── Model_1
│   └── Foliage_1
│   └── Model_1

Resource
├── Model_0
│   └── Foliage_0
│   └── Model_0
├── Model_1
│   └── Foliage_1
│   └── Model_1

Stump
└── Model_0
    └── Model_0
```

### Bush Object
```
Resource
├── Model_0 (main bush mesh with collider)
├── Model_1 (additional bush model)
└── Forage (optional forage mesh)
```

## Notes

- Trees and bushes automatically get foliage effects for wind animation
- Materials are created automatically from textures for trees and bushes
- Objects are automatically organised in folders by type
- Layer and tag assignments are handled automatically
- For trees and bushes, a Custom/Foliage shader is used if available, otherwise falls back to Standard shader

## License

This tool is available under the MIT License. See the LICENSE file for details. 
