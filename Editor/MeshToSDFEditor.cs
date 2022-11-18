using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MeshToSDF))]
public class MeshToSDFEditor : Editor
{
    SerializedProperty m_SDFTexture;
    SerializedProperty m_FloodMode;
    SerializedProperty m_FloodFillQuality;
    SerializedProperty m_FloodFillIterations;
    SerializedProperty m_DistanceMode;
    SerializedProperty m_UpdateMode;
    SerializedProperty m_Offset;

    void OnEnable()
    {
        m_SDFTexture = serializedObject.FindProperty("m_SDFTexture");
        m_FloodMode = serializedObject.FindProperty("m_FloodMode");
        m_FloodFillQuality = serializedObject.FindProperty("m_FloodFillQuality");
        m_FloodFillIterations = serializedObject.FindProperty("m_FloodFillIterations");
        m_DistanceMode = serializedObject.FindProperty("m_DistanceMode");
        m_UpdateMode = serializedObject.FindProperty("m_UpdateMode");
        m_Offset = serializedObject.FindProperty("m_Offset");
    }

    public override void OnInspectorGUI()
    {
        ValidateMesh();

        if ((MeshToSDF.UpdateMode)m_UpdateMode.enumValueIndex == MeshToSDF.UpdateMode.Explicit)
        {
            EditorGUILayout.HelpBox("Explicit update mode - SDF updates driven by a user script", MessageType.Info);
            EditorGUILayout.Space();
        }

        EditorGUILayout.PropertyField(m_SDFTexture);

        SDFTexture sdftexture = m_SDFTexture.objectReferenceValue as SDFTexture;
        if (sdftexture == null)
            EditorGUILayout.HelpBox("Assign an object with an SDFTexture component - that's where this script will write the SDF to.", MessageType.Warning);
        else if (sdftexture.mode != SDFTexture.Mode.Dynamic)
            EditorGUILayout.HelpBox("SDFTexture needs to reference a RenderTexture to be writeable.", MessageType.Error);

        EditorGUILayout.PropertyField(m_FloodMode);
        EditorGUILayout.PropertyField(m_FloodFillQuality);

        if ((MeshToSDF.FloodMode)m_FloodMode.enumValueIndex == MeshToSDF.FloodMode.Linear)
        {
            EditorGUILayout.PropertyField(m_FloodFillIterations);
            EditorGUILayout.PropertyField(m_DistanceMode);
        }
        else
        {
            GUI.enabled = false;

            int oldDistanceMode = m_DistanceMode.enumValueIndex;
            m_DistanceMode.enumValueIndex = (int)MeshToSDF.DistanceMode.Unsigned;
            EditorGUILayout.PropertyField(m_DistanceMode);
            m_DistanceMode.enumValueIndex = oldDistanceMode;
            
            GUI.enabled = true;
        }

        if ((MeshToSDF.FloodMode)m_FloodMode.enumValueIndex == MeshToSDF.FloodMode.Linear && (MeshToSDF.DistanceMode)m_DistanceMode.enumValueIndex == MeshToSDF.DistanceMode.Signed)
        {
            EditorGUILayout.PropertyField(m_Offset);
        }
        else
        {
            GUI.enabled = false;

            float oldOffset = m_Offset.floatValue;
            m_Offset.floatValue = 0;
            EditorGUILayout.PropertyField(m_Offset);
            m_Offset.floatValue = oldOffset;

            GUI.enabled = true;
        }
        
        serializedObject.ApplyModifiedProperties();
    }

    void ValidateMesh()
    {
        MeshToSDF meshToSDF = target as MeshToSDF;
        Mesh mesh = null;
        SkinnedMeshRenderer smr = meshToSDF.GetComponent<SkinnedMeshRenderer>();
        if (smr != null)
            mesh = smr.sharedMesh;
        if (mesh == null)
        {
            MeshFilter mf = meshToSDF.GetComponent<MeshFilter>();
            if (mf != null)
                mesh = mf.sharedMesh;
        }
        if (mesh == null)
        {
            EditorGUILayout.HelpBox("MeshToSDF needs a Mesh from either a SkinnedMeshRenderer or a MeshFilter component on this GameObject.", MessageType.Error);
            return;
        }

        if (mesh.subMeshCount > 1)
            EditorGUILayout.HelpBox("Multiple submeshes detected, will only use the first one.", MessageType.Warning);

        if (mesh.GetTopology(0) != MeshTopology.Triangles)
            EditorGUILayout.HelpBox("Only triangular topology meshes supported (MeshTopology.Triangles).", MessageType.Error);

        if (mesh.GetIndexCount(0) > 3 * 10000)
            EditorGUILayout.HelpBox("This looks like a large mesh. For best performance and a smoother SDF, use a proxy mesh of under 10k triangles.", MessageType.Warning);

        if (mesh.GetVertexAttributeStream(UnityEngine.Rendering.VertexAttribute.Position) < 0)
            EditorGUILayout.HelpBox("No vertex positions in the mesh.", MessageType.Error);
    }
}