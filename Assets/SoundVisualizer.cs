using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(WaveManager))]
public class SoundVisualizer : MonoBehaviour
{
    public List<AudioSource> audioSources;
    public float heightMultiplier = 10f;
    public float updateInterval = 0.1f;

    private WaveManager waveManager;

    void Start()
    {
        waveManager = GetComponent<WaveManager>();
        StartCoroutine(PulseAudioSourcesRoutine());
    }
    IEnumerator PulseAudioSourcesRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            List<Vector3> effects = new List<Vector3>();

            foreach (var audioSource in audioSources.ToArray())
            {
                if (audioSource == null || !audioSource.isPlaying) continue;

                Vector2Int coord = waveManager.WorldToTextureCoord(audioSource.transform.position);
                float intensity = GetAudioIntensity(audioSource);
                effects.Add(new Vector3(coord.x, coord.y, intensity * heightMultiplier));
            }

            waveManager.effects = effects.ToArray();
        }
    }

    float GetAudioIntensity(AudioSource source)
    {
        float[] samples = new float[1024];
        source.GetOutputData(samples, 0);
        float sum = 0;
        foreach (var sample in samples) sum += sample * sample;
        return Mathf.Sqrt(sum / samples.Length);
    }

    void OnDestroy() => StopAllCoroutines();
}