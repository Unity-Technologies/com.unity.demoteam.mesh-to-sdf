# Package: com.unity.demoteam.mesh-to-sdf

A real-time Signed Distance Field generator. Use a Mesh or a dynamically deforming SkinnedMesh as input to generate a 3D SDF texture.

\
![mesh-to-sdf](https://user-images.githubusercontent.com/6276154/176790544-65fe89fe-bf89-425d-8597-9fc9fa1e5327.png)

## Requirements

- Unity 2021.2+ (mesh buffer access in compute shaders)

## Usage

Declare the package and its dependencies as git dependencies in `Packages/manifest.json`:

```
"dependencies": {
    "com.unity.demoteam.mesh-to-sdf": "https://github.com/Unity-Technologies/com.unity.demoteam.mesh-to-sdf.git",
    ...
}
```

Once the package has been imported:
1. Create an empty game object and give it an SDFTexture component.
2. This component and the game object's transform control the volume you will capture, so make sure it's place over your mesh and adjust the Size property.
3. Create a RenderTexture asset and assign it to the SDFTexture component.
4. Add a MeshToSDF component to your SkinnedMeshRenderer or MeshRenderer, and give it the previously created SDFTexture.
5. If you now select the SDFTexture object, you should see a slice of the SDF in the Scene View, dynamically updating.

Scripts can query `SDFTexture.sdf` and `SDFTexture.worldToSDFTexCoords` to get the texture and a texture coordinates transform to sample the texture.

Alternatively you can use the SDFTexture component as a way to place in the scene a static SDF 3DTexture generated elsewhere. Just assign the 3DTexture in the SDFTexture component. Scripts and shaders using the SDFTexture api won't notice the difference.

------

Public slack channel: [#marketing-demo-team](https://unity.slack.com/messages/C070XDMU0/) <br/>
[View this project in Backstage](https://backstage.corp.unity3d.com/catalog/default/component/mesh-to-sdf) <br/>
# Converting to public repository
Any and all Unity software of any description (including components) (1) whose source is to be made available other than under a Unity source code license or (2) in respect of which a public announcement is to be made concerning its inner workings, may be licensed and released only upon the prior approval of Legal.
The process for that is to access, complete, and submit this [FORM](https://docs.google.com/forms/d/e/1FAIpQLSe3H6PARLPIkWVjdB_zMvuIuIVtrqNiGlEt1yshkMCmCMirvA/viewform).
