# Package: com.unity.demoteam.mesh-to-sdf

A real-time Signed Distance Field generator. Use a Mesh or a dynamically deforming SkinnedMesh as input to generate a 3D SDF texture.

\
![mesh-to-sdf](https://user-images.githubusercontent.com/6276154/176790544-65fe89fe-bf89-425d-8597-9fc9fa1e5327.png)

## Requirements

- Unity 2021.2+ (mesh buffer access in compute shaders)

## Installation

Use [*Add package from git URL*](https://docs.unity3d.com/Manual/upm-ui-giturl.html) (in the Package Manager): 

```https://github.com/Unity-Technologies/com.unity.demoteam.mesh-to-sdf.git```

or

Declare the package as a git dependency in `Packages/manifest.json`:

```
"dependencies": {
    "com.unity.demoteam.mesh-to-sdf": "https://github.com/Unity-Technologies/com.unity.demoteam.mesh-to-sdf.git",
    ...
}
```

## Documentation

[Quickstart](Documentation~/index.md)

