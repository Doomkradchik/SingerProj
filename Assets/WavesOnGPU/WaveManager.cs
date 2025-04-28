using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public Renderer wavePlane; // Add reference to the wave plane in inspector
    public Material waveMaterial;
    public ComputeShader waveCompute;
    public RenderTexture NState, Nm1State, Np1State;
    public RenderTexture obstaclesTex;
    public Vector3[] effects; // Changed from Vector3 to Vector3[]
    public float dispersion = 0.98f;

    private Vector2Int resolution;
    private ComputeBuffer effectBuffer;

    public bool flipHorizontal;
    public bool flipVertical;

    void Start()
    {
        resolution = new Vector2Int(obstaclesTex.width, obstaclesTex.height);
        InitializeTexture(ref NState);
        InitializeTexture(ref Nm1State);
        InitializeTexture(ref Np1State);

        obstaclesTex.enableRandomWrite = true;
        waveMaterial.mainTexture = NState;

        // Initialize buffer if effects exist
        if (effects != null && effects.Length > 0)
        {
            effectBuffer = new ComputeBuffer(effects.Length, sizeof(float) * 3);
        }
    }

     public Vector2 GetUVCoordinates(Vector3 worldPosition)
    {
        if (wavePlane == null)
        {
            Debug.LogError("Wave plane reference not set!");
            return Vector2.zero;
        }

        // Convert world position to plane's local space
        Vector3 localPos = wavePlane.transform.InverseTransformPoint(worldPosition);
        
        // Calculate normalized UV coordinates
        Vector2 uv = new Vector2(
            (localPos.x + 5f) / 10f,  // -5 to +5 in local space becomes 0-1
            (localPos.z + 5f) / 10f
        );

        // Apply vertical flip if needed
        if (flipHorizontal) uv.x = 1 - uv.x;
        if (flipVertical) uv.y = 1 - uv.y;

        // Clamp values to stay within texture bounds
        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        Debug.Log($"World: {worldPosition} => " +
                      $"Local: {localPos} => " +
                      $"UV: {uv} => " +
                      $"Texture: {UVToTextureCoord(uv)}");

        return uv;
    }

    public Vector2Int UVToTextureCoord(Vector2 uv)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(uv.x * resolution.x), 0, resolution.x - 1),
            Mathf.Clamp(Mathf.FloorToInt(uv.y * resolution.y), 0, resolution.y - 1)
        );
    }

      public Vector2Int WorldToTextureCoord(Vector3 worldPosition)
    {
        return UVToTextureCoord(GetUVCoordinates(worldPosition));
    }

    void InitializeTexture(ref RenderTexture tex)
    {
        tex = new RenderTexture(resolution.x, resolution.y, 1, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SNorm);
        tex.enableRandomWrite = true;
        tex.Create();
    }

    void Update()
    {
        // Update effect buffer data
        if (effects != null && effects.Length > 0)
        {
            if (effectBuffer == null || effectBuffer.count != effects.Length)
            {
                effectBuffer?.Release();
                effectBuffer = new ComputeBuffer(effects.Length, sizeof(float) * 3);
            }
            effectBuffer.SetData(effects);
            waveCompute.SetBuffer(0, "effects", effectBuffer);
            waveCompute.SetInt("numEffects", effects.Length);
        }
        else
        {
            waveCompute.SetInt("numEffects", 0);
        }

        Graphics.CopyTexture(NState, Nm1State);
        Graphics.CopyTexture(Np1State, NState);
        waveCompute.SetTexture(0, "NState", NState);
        waveCompute.SetTexture(0, "Nm1State", Nm1State);
        waveCompute.SetTexture(0, "Np1State", Np1State);
        waveCompute.SetVector("resolution", new Vector2(resolution.x, resolution.y));
        waveCompute.SetFloat("dispersion", dispersion);
        waveCompute.SetTexture(0, "obstaclesTex", obstaclesTex);
        waveCompute.Dispatch(0, resolution.x / 8, resolution.y / 8, 1);
    }

    void OnDestroy()
    {
        // Release the buffer to prevent memory leaks
        effectBuffer?.Release();
    }
}