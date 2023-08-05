using UnityEngine;
using UnityEngine.Rendering;
using System;

public class SDFTexture : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Either a static 3DTexture asset containing an SDF, or a 3D RenderTexture. A 3D RenderTexture is where MeshToSDF writes the SDF.")]
    Texture m_SDF;
    [SerializeField]
    [Tooltip("Size of the volume. The effective size of the volume will be rounded off to the nearest full voxel, to keep voxels cubic.")]
    Vector3 m_Size = Vector3.one;
    [SerializeField]
    [Tooltip("Voxel count along each axis. Y and Z resolutions are calculated automatically from X and proportions of the volume. Voxel counts above 64^3 might lead to poor performance.")]
    int m_Resolution = 64;

    public Texture sdf { get { ValidateTexture(); return m_SDF; } set { m_SDF = value; } }
    public Vector3 size { get { return m_Size; } set { m_Size = value; ValidateSize(); } }
    public int resolution { get { return m_Resolution; } set { m_Resolution = value; ValidateResolution(); } }

    // Max 3D texture resolution in any dimension is 2048
    int kMaxResolution = 2048;
    // Max compute buffer size
    int kMaxVoxelCount = 1024 * 1024 * 1024 / 2;

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
            Texture3D tex3D = m_SDF as Texture3D;
            if (tex3D != null)
                return new Vector3Int(tex3D.width, tex3D.height, tex3D.depth);

            Vector3Int res = new Vector3Int();
            res.x = m_Resolution;
            res.y = (int)(m_Resolution * m_Size.y / m_Size.x);
            res.z = (int)(m_Resolution * m_Size.z / m_Size.x);
            res.y = Mathf.Clamp(res.y, 1, kMaxResolution);
            res.z = Mathf.Clamp(res.z, 1, kMaxResolution);
            return res;
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

    public int maxResolution
    {
        get
        {
            // res * (res * size.y / size.x) * (res * size.z / size.x) = voxel_count
            // res^3 = voxel_count * size.x * size.x / (size.y * size.z)
            int maxResolution = (int)(Mathf.Pow(kMaxVoxelCount * m_Size.x * m_Size.x / (m_Size.y * m_Size.z), 1.0f/3.0f));
            return Mathf.Clamp(maxResolution, 1, kMaxResolution);
        }
    }

    void ValidateSize()
    {
        m_Size.x = Mathf.Max(m_Size.x, 0.001f);
        m_Size.y = Mathf.Max(m_Size.y, 0.001f);
        m_Size.z = Mathf.Max(m_Size.z, 0.001f);
    }

    
    void ValidateResolution()
    {
        m_Resolution = Mathf.Clamp(m_Resolution, 1, maxResolution);
    }

    void ValidateTexture()
    {
        if (mode == Mode.Static)
            return;
        
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
        
        if (!rt.IsCreated())
        {
            rt.Create();
        }
    }

    public void OnValidate()
    {
        ValidateSize();
        ValidateResolution();
        ValidateTexture();
    }
}
