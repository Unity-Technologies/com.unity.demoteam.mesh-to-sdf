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

    void OnEnable()
    {
        m_SDFTexture = serializedObject.FindProperty("m_SDFTexture");
        m_FloodMode = serializedObject.FindProperty("m_FloodMode");
        m_FloodFillQuality = serializedObject.FindProperty("m_FloodFillQuality");
        m_FloodFillIterations = serializedObject.FindProperty("m_FloodFillIterations");
        m_DistanceMode = serializedObject.FindProperty("m_DistanceMode");
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.PropertyField(m_SDFTexture);

        SDFTexture sdftexture = m_SDFTexture.objectReferenceValue as SDFTexture;
        if (sdftexture == null)
            EditorGUILayout.HelpBox("Assign an object with an SDFTexture component - that's where this script will write the SDF to.", MessageType.Warning);

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
            int oldValue = m_DistanceMode.enumValueIndex;
            m_DistanceMode.enumValueIndex = (int)MeshToSDF.DistanceMode.Unsigned;
            EditorGUILayout.PropertyField(m_DistanceMode);
            m_DistanceMode.enumValueIndex = oldValue;
            GUI.enabled = true;
        }

        serializedObject.ApplyModifiedProperties();
    }
}