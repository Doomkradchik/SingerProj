using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(AudioSource))]
public class AcousticFluidSimWithAudioAndCurvature : MonoBehaviour
{
    [Header("Visible Mesh Settings")]
    public int gridWidth = 100;
    public int gridHeight = 100;
    public float gridSpacing = 0.1f;

    [Header("Extended Simulation Domain")]
    public int simBorder = 20;
    private int simWidth, simHeight;

    [Header("Acoustic Simulation Settings")]
    public float waveSpeed = 1.0f;
    public float timeStep = 0.02f;
    public float damping = 0.999f;
    // gridSpacing is used as the spatial step (dx)

    // Simulation fields: pressure and velocity
    private float[,] p, pNew;
    private float[,] vx, vxNew;
    private float[,] vy, vyNew;

    [Header("Obstacle Settings")]
    public LayerMask obstacleLayer;
    public float obstacleCheckRadius = 0.05f;
    private bool[,] isObstacle;
    // Update the obstacle mask every 60 frames.
    public int obstacleUpdateFrequency = 60;
    private int frameCounter = 0;

    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] vertexColors;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public int audioSampleSize = 128;
    public float audioThreshold = 0.01f;
    public float audioImpulseMultiplier = 100f;

    [Header("Visualization Settings")]
    public float amplitudeMultiplier = 10f;
    public float curvatureMultiplier = 10f;
    public Color lowCurvatureColor = Color.blue;
    public Color highCurvatureColor = Color.red;

    [Header("Central Impulse Settings")]
    public float impulseRadius = 2f;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        simWidth = gridWidth + 2 * simBorder;
        simHeight = gridHeight + 2 * simBorder;

        p = new float[simWidth, simHeight];
        pNew = new float[simWidth, simHeight];
        vx = new float[simWidth, simHeight];
        vxNew = new float[simWidth, simHeight];
        vy = new float[simWidth, simHeight];
        vyNew = new float[simWidth, simHeight];

        isObstacle = new bool[simWidth, simHeight];
        // Initial update of obstacle mask.
        UpdateObstacleMask();

        MeshFilter mf = GetComponent<MeshFilter>();
        mesh = new Mesh();
        mf.mesh = mesh;

        vertices = new Vector3[gridWidth * gridHeight];
        vertexColors = new Color[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[(gridWidth - 1) * (gridHeight - 1) * 6];

        float offsetX = ((gridWidth - 1) * gridSpacing) / 2f;
        float offsetZ = ((gridHeight - 1) * gridSpacing) / 2f;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int index = x + y * gridWidth;
                float posX = x * gridSpacing - offsetX;
                float posZ = y * gridSpacing - offsetZ;
                vertices[index] = new Vector3(posX, 0, posZ);
                uv[index] = new Vector2((float)x / (gridWidth - 1), (float)y / (gridHeight - 1));
                vertexColors[index] = lowCurvatureColor;
            }
        }

        int triIndex = 0;
        for (int y = 0; y < gridHeight - 1; y++)
        {
            for (int x = 0; x < gridWidth - 1; x++)
            {
                int index = x + y * gridWidth;
                triangles[triIndex++] = index;
                triangles[triIndex++] = index + gridWidth;
                triangles[triIndex++] = index + 1;

                triangles[triIndex++] = index + 1;
                triangles[triIndex++] = index + gridWidth;
                triangles[triIndex++] = index + gridWidth + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.colors = vertexColors;
        mesh.RecalculateNormals();
    }

    // Updates the obstacle mask.
    void UpdateObstacleMask()
    {
        for (int y = 0; y < simHeight; y++)
        {
            for (int x = 0; x < simWidth; x++)
            {
                Vector3 worldPos = transform.position + new Vector3((x - simBorder) * gridSpacing, 0, (y - simBorder) * gridSpacing);
                isObstacle[x, y] = Physics.CheckSphere(worldPos, obstacleCheckRadius, obstacleLayer);
            }
        }
    }

    void Update()
    {
        // Update obstacle mask every obstacleUpdateFrequency frames.
        frameCounter++;
        if (frameCounter >= obstacleUpdateFrequency)
        {
            UpdateObstacleMask();
            frameCounter = 0;
        }

        // ---- AUDIO-DRIVEN CENTRAL IMPULSE ----
        float[] audioSamples = new float[audioSampleSize];
        audioSource.GetOutputData(audioSamples, 0);
        float sum = 0f;
        for (int i = 0; i < audioSampleSize; i++)
            sum += audioSamples[i] * audioSamples[i];
        float rms = Mathf.Sqrt(sum / audioSampleSize);

        if (rms > audioThreshold)
        {
            float impulse = rms * audioImpulseMultiplier;
            ApplyCentralImpulse(impulse);
        }

        // ---- UPDATE VELOCITY FIELD (Momentum) ----
        for (int y = 1; y < simHeight - 1; y++)
        {
            for (int x = 1; x < simWidth - 1; x++)
            {
                float dpdx = (p[x + 1, y] - p[x - 1, y]) / (2f * gridSpacing);
                float dpdy = (p[x, y + 1] - p[x, y - 1]) / (2f * gridSpacing);

                vxNew[x, y] = (vx[x, y] - timeStep * dpdx) * damping;
                vyNew[x, y] = (vy[x, y] - timeStep * dpdy) * damping;
            }
        }
        for (int y = 0; y < simHeight; y++)
        {
            for (int x = 0; x < simWidth; x++)
            {
                vx[x, y] = vxNew[x, y];
                vy[x, y] = vyNew[x, y];
            }
        }

        // ---- UPDATE PRESSURE FIELD ----
        for (int y = 1; y < simHeight - 1; y++)
        {
            for (int x = 1; x < simWidth - 1; x++)
            {
                float dvxdx = (vx[x + 1, y] - vx[x - 1, y]) / (2f * gridSpacing);
                float dvydy = (vy[x, y + 1] - vy[x, y - 1]) / (2f * gridSpacing);
                float divergence = dvxdx + dvydy;

                pNew[x, y] = (p[x, y] - timeStep * (waveSpeed * waveSpeed) * divergence) * damping;
            }
        }

        // ---- ABSORBING BOUNDARIES ----
        for (int x = 0; x < simWidth; x++)
        {
            pNew[x, 0] = 0;
            pNew[x, simHeight - 1] = 0;
        }
        for (int y = 0; y < simHeight; y++)
        {
            pNew[0, y] = 0;
            pNew[simWidth - 1, y] = 0;
        }
        for (int y = 0; y < simHeight; y++)
        {
            for (int x = 0; x < simWidth; x++)
            {
                p[x, y] = pNew[x, y];
            }
        }

        // ---- APPLY OBSTACLE BOUNDARY CONDITIONS ----
        for (int y = 0; y < simHeight; y++)
        {
            for (int x = 0; x < simWidth; x++)
            {
                if (isObstacle[x, y])
                {
                    p[x, y] = 0;
                    vx[x, y] = 0;
                    vy[x, y] = 0;
                }
            }
        }

        // ---- REFLECT VELOCITY AT OBSTACLE BOUNDARIES ----
        for (int y = 1; y < simHeight - 1; y++)
        {
            for (int x = 1; x < simWidth - 1; x++)
            {
                if (!isObstacle[x, y])
                {
                    if (isObstacle[x - 1, y] && vx[x, y] < 0)
                        vx[x, y] = -vx[x, y];
                    if (isObstacle[x + 1, y] && vx[x, y] > 0)
                        vx[x, y] = -vx[x, y];
                    if (isObstacle[x, y - 1] && vy[x, y] < 0)
                        vy[x, y] = -vy[x, y];
                    if (isObstacle[x, y + 1] && vy[x, y] > 0)
                        vy[x, y] = -vy[x, y];
                }
            }
        }

        // ---- UPDATE VISIBLE MESH ----
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int meshIndex = x + y * gridWidth;
                int simX = x + simBorder;
                int simY = y + simBorder;
                float height = p[simX, simY] * amplitudeMultiplier;
                Vector3 v = vertices[meshIndex];
                v.y = height;
                vertices[meshIndex] = v;
            }
        }

        // ---- COMPUTE CURVATURE FOR COLORING ----
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int meshIndex = x + y * gridWidth;
                int simX = x + simBorder;
                int simY = y + simBorder;
                float curvature = 0f;
                if (simX > 0 && simX < simWidth - 1 && simY > 0 && simY < simHeight - 1)
                {
                    float avg = (p[simX - 1, simY] + p[simX + 1, simY] +
                                 p[simX, simY - 1] + p[simX, simY + 1]) / 4f;
                    curvature = Mathf.Abs(p[simX, simY] - avg);
                }
                float t = Mathf.Clamp01(curvature * curvatureMultiplier);
                vertexColors[meshIndex] = Color.Lerp(lowCurvatureColor, highCurvatureColor, t);
            }
        }
        mesh.vertices = vertices;
        mesh.colors = vertexColors;
        mesh.RecalculateNormals();
    }

    void ApplyCentralImpulse(float intensity)
    {
        int centerX = simWidth / 2;
        int centerY = simHeight / 2;

        for (int y = 0; y < simHeight; y++)
        {
            for (int x = 0; x < simWidth; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance < impulseRadius)
                {
                    float factor = 1f - (distance / impulseRadius);
                    p[x, y] += intensity * factor;
                }
            }
        }
    }
}
