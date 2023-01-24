using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SDFTexture))]
public class SDFTextureEditor : Editor
{
    enum Axis { X, Y, Z }

    static Mesh s_Quad;
    static SDFTexture s_SDFTexture;
    static Material s_Material;
    static float s_Slice = 0.5f; // [0, 1]
    static Axis s_Axis = Axis.X;

    SerializedProperty m_SDF;
    SerializedProperty m_Size;
    SerializedProperty m_Resolution;

    static class Uniforms
    {
        internal static int _Z = Shader.PropertyToID("_Z");
        internal static int _Mode = Shader.PropertyToID("_Mode");
        internal static int _Axis = Shader.PropertyToID("_Axis");
        internal static int _DistanceScale = Shader.PropertyToID("_DistanceScale");
    }

    void OnEnable()
    {
        // Unity creates multiple Editors for a target.
        // Sharing all state in static variables is an iffy way around it.
        s_SDFTexture = target as SDFTexture;

        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.SceneView.duringSceneGui += OnSceneGUI;

        m_SDF = serializedObject.FindProperty("m_SDF");
        m_Size = serializedObject.FindProperty("m_Size");
        m_Resolution = serializedObject.FindProperty("m_Resolution");
    }

    void OnDisable()
    {
        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
    }

    static void DoBounds(SDFTexture sdftexture)
    {
        Handles.color = Color.white;
        Handles.DrawWireCube(Vector3.zero, sdftexture.voxelBounds.size);
    }

    static void DoHandles(Bounds bounds)
    {
        Vector3 dir = Vector3.forward;
        Vector3 perp = Vector3.right;
        switch (s_Axis)
        {
            case Axis.X: dir = Vector3.right; perp = Vector3.up; break;
            case Axis.Y: dir = Vector3.up; perp = Vector3.forward; break;
        }

        int axis = (int)s_Axis;
        Vector3[] offsets = {perp * bounds.extents[(axis + 1)%3], -perp * bounds.extents[(axis + 1)%3]};

        foreach(var offset in offsets)
        {
            Vector3 handlePos = dir * (s_Slice - 0.5f) * bounds.size[axis] + offset;
            float handleSize = new Vector2(bounds.size[(axis + 1)%3], bounds.size[(axis + 2)%3]).magnitude * 0.03f;
            handlePos = Handles.Slider(handlePos, dir, handleSize, Handles.CubeHandleCap, snap:-1);
            s_Slice = Mathf.Clamp01(handlePos[axis]/bounds.size[axis] + 0.5f);
        }
    }

    static void DoSDFSlice(Matrix4x4 matrix, Camera camera, Vector3Int voxelResolution, Bounds voxelBounds, float distanceScale, Texture sdf)
    {
        if (s_Quad == null)
            s_Quad = Resources.GetBuiltinResource(typeof(Mesh), "Quad.fbx") as Mesh;

        if (s_Material == null)
            s_Material = new Material(Shader.Find("Hidden/SDFTexture"));

        Vector3 dir = Vector3.forward * voxelBounds.size.z;
        Quaternion rot = Quaternion.identity;
        switch (s_Axis)
        {
            case Axis.X: dir = Vector3.right * voxelBounds.size.x; rot = Quaternion.Euler(0, -90, 0); break;
            case Axis.Y: dir = Vector3.up * voxelBounds.size.y;  rot = Quaternion.Euler(90, 0, 0); break;
        }

        matrix *= Matrix4x4.Translate(dir * (s_Slice - 0.5f));
        matrix *= Matrix4x4.Scale(voxelBounds.size);
        matrix *= Matrix4x4.Rotate(rot);

        s_Material.SetFloat(Uniforms._Z, s_Slice);
        s_Material.SetInt(Uniforms._Axis, (int)s_Axis);
        s_Material.SetVector("_VoxelResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z));
        s_Material.SetFloat(Uniforms._DistanceScale, distanceScale);
        s_Material.SetTexture("_SDF", sdf);

        s_Material.SetPass(0);
        Graphics.DrawMeshNow(s_Quad, matrix);
    }

    static void OnSceneGUI(UnityEditor.SceneView sceneview)
    {
        if (s_SDFTexture == null)
            return;

        Bounds voxelBounds = s_SDFTexture.voxelBounds;
        var matrix = s_SDFTexture.transform.localToWorldMatrix;
        matrix *= Matrix4x4.Translate(voxelBounds.center);

        Handles.matrix = matrix;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        if (s_SDFTexture.sdf != null && voxelBounds.extents != Vector3.zero)
        {
            DoSDFSlice(matrix, sceneview.camera, s_SDFTexture.voxelResolution, voxelBounds, distanceScale:1, s_SDFTexture.sdf);
            DoHandles(voxelBounds);
        }

        DoBounds(s_SDFTexture);
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("Texture", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_SDF);

        SDFTexture sdftexture = target as SDFTexture;

        switch(sdftexture.mode)
        {   
            case SDFTexture.Mode.None:
                EditorGUILayout.HelpBox("Assign a static SDF Texture3D, or a RenderTexture if MeshToSDF is meant to output here.", MessageType.Warning);
                break;
            case SDFTexture.Mode.Static:
                GUI.enabled = false;
                EditorGUILayout.Vector3IntField("Resolution", sdftexture.voxelResolution);
                GUI.enabled = true;
                break;
            case SDFTexture.Mode.Dynamic:
                EditorGUILayout.PropertyField(m_Size);

                Rect GetColumnRect(Rect totalRect, int column)
                {
                    Rect rect = totalRect;
                    rect.xMin += (totalRect.width - 8) * (column / 3f) + column * 4;
                    rect.width = (totalRect.width - 8) / 3f;
                    return rect;
                }
                Rect position = EditorGUILayout.GetControlRect();
                var label = EditorGUI.BeginProperty(position, new GUIContent("Resolution"), m_Resolution);
                position = EditorGUI.PrefixLabel(position, label);
                EditorGUIUtility.labelWidth = 13; // EditorGUI.kMiniLabelW
                EditorGUI.PropertyField(GetColumnRect(position, 0), m_Resolution, new GUIContent("X"));
                GUI.enabled = false;
                Vector3Int voxelRes = sdftexture.voxelResolution;
                EditorGUI.IntField(GetColumnRect(position, 1), "Y", voxelRes.y);
                EditorGUI.IntField(GetColumnRect(position, 2), "Z", voxelRes.z);
                GUI.enabled = true;
                EditorGUIUtility.labelWidth = 0;
                EditorGUI.EndProperty();

                if (sdftexture.resolution >= sdftexture.maxResolution)
                    EditorGUILayout.HelpBox("Maximum voxel count reached. Recommended resolution below 64^3.", MessageType.Info);
                else if (voxelRes.x * voxelRes.y * voxelRes.z >= 64 * 64 * 64 * 2)
                    EditorGUILayout.HelpBox("High resolution might lead to poor performance. Recommended resolution below 64^3.", MessageType.Info);
                break;
        }

        if (serializedObject.hasModifiedProperties)
            serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        s_Axis = (Axis)EditorGUILayout.EnumPopup(new GUIContent("Axis", "Draw SDF visualisation across the selected axis."), s_Axis);

        float slice = EditorGUILayout.Slider(new GUIContent("Slice", "Draw SDF visualisation for this slice along the selected axis."), s_Slice, 0, 1);
        if (slice != s_Slice)
        {
            s_Slice = slice;
            SceneView.lastActiveSceneView?.Repaint();
        }
    }

    bool HasFrameBounds()
    {
        return true;
    }

    Bounds OnGetFrameBounds()
    {
        SDFTexture sdftexture = target as SDFTexture;
        Bounds bounds = sdftexture.voxelBounds;
        bounds.center += sdftexture.transform.position;
        bounds.size = Vector3.Scale(bounds.size, sdftexture.transform.lossyScale);
        return bounds;
    }
}