﻿using UnityEngine;
using UnityEngine.Rendering;
using System;

public class SDFTexture : MonoBehaviour
{
    [SerializeField]
    Texture m_SDF;
    public Vector3 m_Size = Vector3.one;
    [SerializeField]
    int m_Resolution = 64;

    public Texture sdf { get { OnValidate(); return m_SDF; } }

    public enum Mode
    {
        None,
        Static,
        Dynamic
    }

    public Mode mode
    {
        get
        {
            if ((m_SDF as Texture3D) != null)
                return Mode.Static;

            RenderTexture rt = m_SDF as RenderTexture;
            if (rt != null && rt.dimension == TextureDimension.Tex3D)
                return Mode.Dynamic;

            return Mode.None;
        }
    }

    public Vector3Int voxelResolution
    { 
        get
        {
            if (m_SDF == null)
                return Vector3Int.zero;
            
            Texture3D tex3D = m_SDF as Texture3D;
            if (tex3D != null)
                return new Vector3Int(tex3D.width, tex3D.height, tex3D.depth);

            RenderTexture rt = m_SDF as RenderTexture;
            if (rt != null && rt.dimension == TextureDimension.Tex3D)
            {
                Vector3Int res = new Vector3Int();
                res.x = m_Resolution;
                res.y = (int)(m_Resolution * m_Size.y / m_Size.x);
                res.z = (int)(m_Resolution * m_Size.z / m_Size.x);
                res.y = Mathf.Clamp(res.y, 1, 512);
                res.z = Mathf.Clamp(res.z, 1, 512);
                return res;
            }

            return Vector3Int.zero;
        }
    }

    public Bounds voxelBounds
    {
        get
        {
            Vector3Int voxelRes = voxelResolution;
            if (voxelRes == Vector3Int.zero)
                return new Bounds(Vector3.zero, Vector3.zero);

            // voxelBounds is m_Size, but adjusted to be filled by uniformly scaled voxels
            // voxelResolution quantizes to integer counts, so we just need to multiply by voxelSize
            Vector3 extent = new Vector3(voxelRes.x, voxelRes.y, voxelRes.z) * voxelSize;
            return new Bounds(Vector3.zero, extent);
        }
    }

    public float voxelSize
    {
        get
        {
            if (mode == Mode.Dynamic)
                return m_Size.x / m_Resolution;
            
            int resX = voxelResolution.x;
            return resX != 0 ? 1f / (float)resX : 0f;
        }
    }

    public Matrix4x4 worldToSDFTexCoords
    {
        get
        {
            Vector3 scale = voxelBounds.size;
            Matrix4x4 localToSDFLocal = Matrix4x4.Scale(new Vector3(1.0f / scale.x, 1.0f / scale.y, 1.0f / scale.z));
            Matrix4x4 worldToSDFLocal = localToSDFLocal * transform.worldToLocalMatrix;
            return Matrix4x4.Translate(Vector3.one * 0.5f) * worldToSDFLocal;
        }
    }

    public Matrix4x4 sdflocalToWorld
    {
        get
        {
            Vector3 scale = voxelBounds.size;
            return transform.localToWorldMatrix * Matrix4x4.Scale(scale);
        }
    }

    public void OnValidate()
    {
        m_Size.x = Mathf.Max(m_Size.x, 0.001f);
        m_Resolution = Mathf.Clamp(m_Resolution, 1, 512);

        if (mode != Mode.Static)
        {
            RenderTexture rt = m_SDF as RenderTexture;
            if (rt == null)
                return;
            
            Vector3Int res = voxelResolution;
            bool serializedPropertyChanged = rt.depth != 0 || rt.width != res.x || rt.height != res.y || rt.volumeDepth != res.z || rt.format != RenderTextureFormat.RHalf || rt.dimension != TextureDimension.Tex3D;

            if (!rt.enableRandomWrite || serializedPropertyChanged)
            {
                rt.Release();
                if (serializedPropertyChanged)
                {
                    rt.depth = 0;
                    rt.width = res.x;
                    rt.height = res.y;
                    rt.volumeDepth = res.z;
                    rt.format = RenderTextureFormat.RHalf;
                    rt.dimension = TextureDimension.Tex3D;
                }
                
                // For some reason this flag gets lost (not serialized?), so we don't want to write and dirty other properties if just this doesn't match
                rt.enableRandomWrite = true;
                rt.Create();
            }

            if (rt.wrapMode != TextureWrapMode.Clamp)
                rt.wrapMode = TextureWrapMode.Clamp;
        }
    }
}