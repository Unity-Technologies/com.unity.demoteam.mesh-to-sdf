# Mesh-to-SDF Documentation

## Quickstart

Once the package has been imported:
1. Create an empty game object and give it an SDFTexture component.
2. This component and the game object's transform control the volume you will capture, so make sure it's place over your mesh and adjust the Size property.
3. Create a RenderTexture asset and assign it to the SDFTexture component.
4. Add a MeshToSDF component to your SkinnedMeshRenderer or MeshRenderer, and give it the previously created SDFTexture.
5. If you now select the SDFTexture object, you should see a slice of the SDF in the Scene View, dynamically updating.

Scripts can query `SDFTexture.sdf` and `SDFTexture.worldToSDFTexCoords` to get the texture and a texture coordinates transform to sample the texture.

Alternatively you can use the SDFTexture component as a way to place in the scene a static SDF 3DTexture generated elsewhere. Just assign the 3DTexture in the SDFTexture component. Scripts and shaders using the SDFTexture api won't notice the difference.