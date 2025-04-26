using UnityEngine;

public class SoundVisualizer : MonoBehaviour
{
    // Prefab for the visualizer (for example, a simple cube).
    public GameObject visualizerPrefab;

    // Number of frequency samples (visualizer bars) to display.
    public int numberOfSamples = 64;

    // How much to scale the visualizer bars based on audio amplitude.
    public float visualizerScale = 50f;

    // Smoothing factor for visualizer movement.
    public float smoothSpeed = 0.5f;

    // Array to hold the current frequency samples.
    private float[] samples;

    // Array to store the instantiated visualizer objects.
    private GameObject[] visualizers;

    // AudioSource retrieved from the AudioSource component attached to this GameObject.
    private AudioSource audioSource;

    void Start()
    {
        // Retrieve the AudioSource component attached to this GameObject.
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("No AudioSource component found on this GameObject!");
            return;
        }

        // Initialize the samples array.
        samples = new float[numberOfSamples];

        // Instantiate visualizer objects.
        visualizers = new GameObject[numberOfSamples];
        for (int i = 0; i < numberOfSamples; i++)
        {
            // Create a new visualizer bar and position them horizontally.
            GameObject instance = Instantiate(visualizerPrefab);
            float xPos = i - numberOfSamples / 2f;
            instance.transform.position = transform.position + new Vector3(xPos, 0, 0);
            visualizers[i] = instance;
        }
    }

    void Update()
    {
        if (audioSource == null) return;

        // Get spectrum data from the AudioSource.
        audioSource.GetSpectrumData(samples, 0, FFTWindow.Blackman);

        // Loop through each sample and update the corresponding visualizer.
        for (int i = 0; i < numberOfSamples; i++)
        {
            // Determine the new height based on sample amplitude.
            float targetYScale = Mathf.Lerp(visualizers[i].transform.localScale.y, samples[i] * visualizerScale, smoothSpeed);
            Vector3 newScale = new Vector3(visualizers[i].transform.localScale.x, targetYScale, visualizers[i].transform.localScale.z);
            visualizers[i].transform.localScale = newScale;
        }
    }
}
