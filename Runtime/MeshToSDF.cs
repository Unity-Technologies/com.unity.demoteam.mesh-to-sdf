﻿using System;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class MeshToSDF : MonoBehaviour
{
    [SerializeField]
    SDFTexture m_SDFTexture;

    public enum FloodMode
    {
        Linear,
        Jump
    }

    public enum FloodFillQuality
    {
        Normal,
        Ultra
    }

    [Header("Flood fill")]
    [SerializeField]
    [Tooltip(@"Use jump flood if you need to fill the entire volume, but it only outputs unsigned distance.
If you need signed distance or just need a limited shell around your surface, use linear flood fill, but many iterations are expensive.")]
    FloodMode m_FloodMode = FloodMode.Linear;
    [SerializeField]
    [Tooltip("Normal - flood in orthogonal directions only, faster. \nUltra - flood in orthogonal and diagonal directions, slower.")]
    FloodFillQuality m_FloodFillQuality;
    [Range(0, 64), SerializeField]
    int m_FloodFillIterations = 0;

    public enum DistanceMode
    {
        Signed,
        Unsigned
    }

    [Header("Distance Mode")]
    [SerializeField]
    DistanceMode m_DistanceMode = DistanceMode.Signed;

    [SerializeField]
    ComputeShader m_Compute = null;

    SkinnedMeshRenderer m_SkinnedMeshRenderer = null;
    MeshFilter m_MeshFilter = null;

    ComputeBuffer m_SDFBuffer = null;
    ComputeBuffer m_SDFBufferBis = null;
    ComputeBuffer m_JumpBuffer = null;
    ComputeBuffer m_JumpBufferBis = null;
    int m_InitializeKernel = -1;
    int m_SplatTriangleDistancesSignedKernel = -1;
    int m_SplatTriangleDistancesUnsignedKernel = -1;
    int m_FinalizeKernel = -1;
    int m_LinearFloodStepKernel = -1;
    int m_LinearFloodStepUltraQualityKernel = -1;
    int m_LinearFinalizeFloodKernel = -1;
    int m_JumpFloodInitialize = -1;
    int m_JumpFloodStep = -1;
    int m_JumpFloodStepUltraQuality = -1;
    int m_JumpFloodFinalize = -1;
    int m_BufferToTextureCalcGradient = -1;

    GraphicsBuffer m_VertexBuffer = null;
    GraphicsBuffer m_IndexBuffer = null;
    int m_VertexBufferStride;
    int m_VertexBufferPosAttributeOffset;
    CommandBuffer m_CommandBuffer = null;

    const int kThreadCount = 64;
    int m_ThreadGroupCountTriangles;

    static class Uniforms
    {
        internal static int _SDF = Shader.PropertyToID("_SDF");
        internal static int _SDFBuffer = Shader.PropertyToID("_SDFBuffer");
        internal static int _SDFBufferRW = Shader.PropertyToID("_SDFBufferRW");
        internal static int _JumpBuffer = Shader.PropertyToID("_JumpBuffer");
        internal static int _JumpBufferRW = Shader.PropertyToID("_JumpBufferRW");
        internal static int _VoxelResolution = Shader.PropertyToID("_VoxelResolution");
        internal static int _FloodParams = Shader.PropertyToID("_FloodParams");
        internal static int _WorldToLocal = Shader.PropertyToID("_WorldToLocal");
        internal static int g_SignedDistanceField = Shader.PropertyToID("g_SignedDistanceField");
        internal static int g_NumCellsX = Shader.PropertyToID("g_NumCellsX");
        internal static int g_NumCellsY = Shader.PropertyToID("g_NumCellsY");
        internal static int g_NumCellsZ = Shader.PropertyToID("g_NumCellsZ");
        internal static int g_Origin = Shader.PropertyToID("g_Origin");
        internal static int g_CellSize = Shader.PropertyToID("g_CellSize");
        internal static int g_VertexBuffer = Shader.PropertyToID("g_VertexBuffer");
        internal static int g_IndexBuffer = Shader.PropertyToID("g_IndexBuffer");
        internal static int _VertexBufferStride = Shader.PropertyToID("_VertexBufferStride");
        internal static int _VertexBufferPosAttributeOffset = Shader.PropertyToID("_VertexBufferPosAttributeOffset");
        internal static int _JumpOffset = Shader.PropertyToID("_JumpOffset");
        internal static int _JumpOffsetInterleaved = Shader.PropertyToID("_JumpOffsetInterleaved");
    }

    static class Labels
    {
        internal static string MeshToSDF = "MeshToSDF";
        internal static string Initialize = "Initialize";
        internal static string SplatTriangleDistances = "SplatTriangleDistances";
        internal static string SplatTriangleDistancesSigned = "SplatTriangleDistancesSigned";
        internal static string SplatTriangleDistancesUnsigned = "SplatTriangleDistancesUnsigned";
        internal static string Finalize = "Finalize";
        internal static string LinearFloodStep = "LinearFloodStep";
        internal static string LinearFloodStepUltraQuality = "LinearFloodStepUltraQuality";
        internal static string LinearFloodFinalize = "LinearFloodFinalize";
        internal static string JumpFloodInitialize = "JumpFloodInitialize";
        internal static string JumpFloodStep = "JumpFloodStep";
        internal static string JumpFloodStepUltraQuality = "JumpFloodStepUltraQuality";
        internal static string JumpFloodFinalize = "JumpFloodFinalize";
        internal static string BufferToTexture = "BufferToTexture";
    }

    bool m_Initialized = false;

    void Init()
    {
        if (m_Initialized)
            return;

        m_Initialized = true;

        m_InitializeKernel = m_Compute.FindKernel(Labels.Initialize);
        m_SplatTriangleDistancesSignedKernel = m_Compute.FindKernel(Labels.SplatTriangleDistancesSigned);
        m_SplatTriangleDistancesUnsignedKernel = m_Compute.FindKernel(Labels.SplatTriangleDistancesUnsigned);
        m_FinalizeKernel = m_Compute.FindKernel(Labels.Finalize);
        m_LinearFloodStepKernel = m_Compute.FindKernel(Labels.LinearFloodStep);
        m_LinearFloodStepUltraQualityKernel = m_Compute.FindKernel(Labels.LinearFloodStepUltraQuality);
        m_LinearFinalizeFloodKernel = m_Compute.FindKernel(Labels.LinearFloodFinalize);
        m_JumpFloodInitialize = m_Compute.FindKernel(Labels.JumpFloodInitialize);
        m_JumpFloodStep = m_Compute.FindKernel(Labels.JumpFloodStep);
        m_JumpFloodStepUltraQuality = m_Compute.FindKernel(Labels.JumpFloodStepUltraQuality);
        m_JumpFloodFinalize = m_Compute.FindKernel(Labels.JumpFloodFinalize);
        m_BufferToTextureCalcGradient = m_Compute.FindKernel(Labels.BufferToTexture);

        m_SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        m_MeshFilter = GetComponent<MeshFilter>();
    }

    void LateUpdate()
    {
        // TODO: proper scheduling

        if (m_CommandBuffer == null)
        {
            m_CommandBuffer = new CommandBuffer();
            m_CommandBuffer.name = Labels.MeshToSDF;
        }

        RenderSDF(m_CommandBuffer);

        Graphics.ExecuteCommandBuffer(m_CommandBuffer);

        ReleaseGraphicsBuffer(ref m_VertexBuffer);
        ReleaseGraphicsBuffer(ref m_IndexBuffer);
    }

    void RenderSDF(CommandBuffer cmd)
    {
        if (m_SDFTexture == null || m_SDFTexture.mode == SDFTexture.Mode.None)
            return;

        Vector3Int voxelResolution = m_SDFTexture.voxelResolution;
        int voxelCount = voxelResolution.x * voxelResolution.y * voxelResolution.z;
        Bounds voxelBounds = m_SDFTexture.voxelBounds;
        float voxelSize = m_SDFTexture.voxelSize;
        int threadGroupCountVoxels = (int)Mathf.Ceil((float)voxelCount / (float) kThreadCount);

        CreateComputeBuffer(ref m_SDFBuffer, voxelCount, sizeof(float));
        CreateComputeBuffer(ref m_SDFBufferBis, voxelCount, sizeof(float));
        if (m_FloodMode == FloodMode.Jump)
        {
            CreateComputeBuffer(ref m_JumpBuffer, voxelCount, sizeof(int));
            CreateComputeBuffer(ref m_JumpBufferBis, voxelCount, sizeof(int));
        }
        else
        {
            ReleaseComputeBuffer(ref m_JumpBuffer);
            ReleaseComputeBuffer(ref m_JumpBufferBis);
        }

        Init();
        if (!LoadMeshToComputeBuffers())
        {
            ReleaseGraphicsBuffer(ref m_VertexBuffer);
            ReleaseGraphicsBuffer(ref m_IndexBuffer);
            return;
        }

        cmd.Clear();
        cmd.SetComputeVectorParam(m_Compute, Uniforms.g_Origin, voxelBounds.center - voxelBounds.extents);
        cmd.SetComputeFloatParam(m_Compute, Uniforms.g_CellSize, voxelSize);
        cmd.SetComputeIntParam(m_Compute, Uniforms.g_NumCellsX, voxelResolution.x);
        cmd.SetComputeIntParam(m_Compute, Uniforms.g_NumCellsY, voxelResolution.y);
        cmd.SetComputeIntParam(m_Compute, Uniforms.g_NumCellsZ, voxelResolution.z);
        int[] voxelResolutionArray = {voxelResolution.x, voxelResolution.y, voxelResolution.z, voxelCount};
        cmd.SetComputeIntParams(m_Compute, Uniforms._VoxelResolution, voxelResolutionArray);
        var floodParams = Vector4.zero;
        floodParams.x = voxelBounds.size.magnitude; // MAX_DISTANCE
        floodParams.y = floodParams.x * 2.0f; // BASE_DISTANCE
        floodParams.z = floodParams.y * 2.0f; // INITIAL_DISTANCE
        cmd.SetComputeVectorParam(m_Compute, Uniforms._FloodParams, floodParams);
        cmd.SetComputeMatrixParam(m_Compute, Uniforms._WorldToLocal, GetMeshToSDFMatrix());

        // Last FloodStep should finish writing into m_SDFBufferBis, so that we always end up
        // writing to m_SDFBuffer in FinalizeFlood
        ComputeBuffer bufferPing = m_SDFBufferBis;
        ComputeBuffer bufferPong = m_SDFBuffer;
        if (m_FloodFillIterations%2 == 1 && m_FloodMode == FloodMode.Linear)
        {
            bufferPing = m_SDFBuffer;
            bufferPong = m_SDFBufferBis;
        }
        
        cmd.BeginSample(Labels.Initialize);
        int kernel = m_InitializeKernel;
        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms.g_SignedDistanceField, bufferPing);
        cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
		cmd.EndSample(Labels.Initialize);

        cmd.BeginSample(Labels.SplatTriangleDistances);
        kernel = m_DistanceMode == DistanceMode.Signed && m_FloodMode == FloodMode.Linear ? m_SplatTriangleDistancesSignedKernel : m_SplatTriangleDistancesUnsignedKernel;
        
        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms.g_VertexBuffer, m_VertexBuffer);
        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms.g_IndexBuffer, m_IndexBuffer);
        cmd.SetComputeFloatParam(m_Compute, Uniforms._VertexBufferStride, m_VertexBufferStride);
        cmd.SetComputeFloatParam(m_Compute, Uniforms._VertexBufferPosAttributeOffset, m_VertexBufferPosAttributeOffset);
        
        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms.g_SignedDistanceField, bufferPing);
        cmd.DispatchCompute(m_Compute, kernel, m_ThreadGroupCountTriangles, 1, 1);
		cmd.EndSample(Labels.SplatTriangleDistances);

        cmd.BeginSample(Labels.Finalize);
        kernel = m_FinalizeKernel;
        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms.g_SignedDistanceField, bufferPing);
        cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
		cmd.EndSample(Labels.Finalize);

        if (m_FloodMode == FloodMode.Linear)
        {
            cmd.BeginSample(Labels.LinearFloodStep);
            kernel = m_FloodFillQuality == FloodFillQuality.Normal ? m_LinearFloodStepKernel : m_LinearFloodStepUltraQualityKernel;
            for (int i = 0; i < m_FloodFillIterations; i++)
            {
                cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBuffer, i%2 == 0 ? bufferPing : bufferPong);
                cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBufferRW, i%2 == 0 ? bufferPong : bufferPing);
                cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
            }
            cmd.EndSample(Labels.LinearFloodStep);

            cmd.BeginSample(Labels.LinearFloodFinalize);
            kernel = m_LinearFinalizeFloodKernel;
            // Final FloodStep output sits in m_SDFBufferBis
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBuffer, m_SDFBufferBis);
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBufferRW, m_SDFBuffer);
            cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
            cmd.EndSample(Labels.LinearFloodFinalize);
        }
        else
        {
            cmd.BeginSample(Labels.JumpFloodInitialize);
            kernel = m_JumpFloodInitialize;
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBuffer, bufferPing);
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._JumpBufferRW, m_JumpBuffer);
            cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
            cmd.EndSample(Labels.JumpFloodInitialize);

            int maxDim = Mathf.Max(Mathf.Max(voxelResolution.x, voxelResolution.y), voxelResolution.z);
            int jumpFloodStepCount = Mathf.FloorToInt(Mathf.Log(maxDim, 2)) - 1;

            cmd.BeginSample(Labels.JumpFloodStep);
            bool bufferFlip = true;
            int[] jumpOffsetInterleaved = new int[3];
            for (int i = 0; i < jumpFloodStepCount; i++)
            {
                int jumpOffset = Mathf.FloorToInt(Mathf.Pow(2, jumpFloodStepCount - 1 - i) + 0.5f);
                if (m_FloodFillQuality == FloodFillQuality.Normal)
                {
                    kernel = m_JumpFloodStep;
                    for (int j = 0; j < 3; j++)
                    {
                        jumpOffsetInterleaved[j] = jumpOffset;
                        jumpOffsetInterleaved[(j+1)%3] = jumpOffsetInterleaved[(j+2)%3] = 0;
                        cmd.SetComputeIntParams(m_Compute, Uniforms._JumpOffsetInterleaved, jumpOffsetInterleaved);
                        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._JumpBuffer, bufferFlip ? m_JumpBuffer : m_JumpBufferBis);
                        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._JumpBufferRW, bufferFlip ? m_JumpBufferBis : m_JumpBuffer);
                        cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);    
                        bufferFlip = !bufferFlip;
                    }
                }
                else
                {
                    kernel = m_JumpFloodStepUltraQuality;
                    cmd.SetComputeIntParam(m_Compute, Uniforms._JumpOffset, jumpOffset);
                    cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._JumpBuffer, bufferFlip ? m_JumpBuffer : m_JumpBufferBis);
                    cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._JumpBufferRW, bufferFlip ? m_JumpBufferBis : m_JumpBuffer);
                    cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
                    bufferFlip = !bufferFlip;
                }
            }
            cmd.EndSample(Labels.JumpFloodStep);

            cmd.BeginSample(Labels.JumpFloodFinalize);
            kernel = m_JumpFloodFinalize;
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._JumpBuffer, bufferFlip ? m_JumpBuffer : m_JumpBufferBis);
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBuffer, m_SDFBufferBis);
            cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBufferRW, m_SDFBuffer);
            cmd.SetComputeFloatParam(m_Compute, Uniforms.g_CellSize, voxelSize);
            cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
            cmd.EndSample(Labels.JumpFloodFinalize);
        }

        cmd.BeginSample(Labels.BufferToTexture);
        kernel = m_BufferToTextureCalcGradient;
        cmd.SetComputeBufferParam(m_Compute, kernel, Uniforms._SDFBuffer, m_SDFBuffer);
        cmd.SetComputeTextureParam(m_Compute, kernel, Uniforms._SDF, m_SDFTexture.sdf);
        cmd.DispatchCompute(m_Compute, kernel, threadGroupCountVoxels, 1, 1);
		cmd.EndSample(Labels.BufferToTexture);
    }

    bool LoadMeshToComputeBuffers()
    {
        Mesh mesh = null;

        if (m_SkinnedMeshRenderer != null)
        {
            mesh = m_SkinnedMeshRenderer.sharedMesh;
            if (mesh == null)
                return false;
            m_SkinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        }
        else if (m_MeshFilter != null)
        {
            mesh = m_MeshFilter.sharedMesh;
            if (mesh == null)
                return false;
            mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        }
        else
            return false;

        if (mesh.GetTopology(0) != MeshTopology.Triangles)
        {
            Debug.LogError("MeshToSDF needs a mesh with triangle topology.", this);
            return false;
        }
        if (mesh.indexFormat != IndexFormat.UInt16)
        {
            Debug.LogError("MeshToSDF needs a mesh with 16 bit index format (mesh probably too big).", this);
            return false;
        }

        int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
        m_VertexBufferStride = mesh.GetVertexBufferStride(stream);
        m_VertexBufferPosAttributeOffset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
        m_VertexBuffer = m_SkinnedMeshRenderer != null ? m_SkinnedMeshRenderer.GetVertexBuffer() : mesh.GetVertexBuffer(stream);

        mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
        m_IndexBuffer = mesh.GetIndexBuffer();

        int triangleCount = m_IndexBuffer.count / 3;
        m_ThreadGroupCountTriangles = (int)Mathf.Ceil((float)triangleCount / (float) kThreadCount);

        return m_VertexBuffer != null && m_IndexBuffer != null;
    }

    Matrix4x4 GetMeshToSDFMatrix()
    {
        Matrix4x4 meshToWorld = m_SkinnedMeshRenderer != null && m_SkinnedMeshRenderer.rootBone != null ? 
            m_SkinnedMeshRenderer.rootBone.localToWorldMatrix * Matrix4x4.Scale(m_SkinnedMeshRenderer.rootBone.lossyScale).inverse :
            transform.localToWorldMatrix * Matrix4x4.Scale(transform.lossyScale).inverse;

         return m_SDFTexture.transform.worldToLocalMatrix * meshToWorld;
    }

    static void CreateComputeBuffer(ref ComputeBuffer cb, int length, int stride)
    {
        if (cb != null && cb.count == length && cb.stride == stride)
            return;

        ReleaseComputeBuffer(ref cb);
        cb = new ComputeBuffer(length, stride);
    }

    static void ReleaseComputeBuffer(ref ComputeBuffer buffer)
    {
        if (buffer != null)
            buffer.Release();
        buffer = null;
    }

    static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
    {
        if (buffer != null)
            buffer.Release();
        buffer = null;
    }

#if UNITY_EDITOR
    void OnEnable() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload; 
    void OnDisable() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
    void OnBeforeAssemblyReload() => OnDestroy();
#endif

    void OnDestroy()
    {
        ReleaseComputeBuffer(ref m_SDFBuffer);
        ReleaseComputeBuffer(ref m_SDFBufferBis);
        ReleaseComputeBuffer(ref m_JumpBuffer);
        ReleaseComputeBuffer(ref m_JumpBufferBis);
        ReleaseGraphicsBuffer(ref m_VertexBuffer);
        ReleaseGraphicsBuffer(ref m_IndexBuffer);
    }
}