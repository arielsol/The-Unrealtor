using UnityEngine;

public class FlickerLight : MonoBehaviour
{
    public Light flickerLight;

    [Header("Basic Flicker Settings")]
    [Tooltip("How long the object stays OFF during a single blink.")]
    public float flickerDuration = 0.05f;

    [Tooltip("Minimum seconds between flicker events.")]
    public float minInterval = 3f;

    [Tooltip("Maximum seconds between flicker events.")]
    public float maxInterval = 10f;

    [Header("Double Blink Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance that a flicker will be a double blink instead of single.")]
    public float doubleBlinkProbability = 0.2f;

    [Tooltip("Time between the first and second blink in a double-blink sequence.")]
    public float doubleBlinkGap = 0.1f;

    void Awake()
    {
        // Auto-grab components if user forgot
        if (!flickerLight) flickerLight = GetComponent<Light>();
    }

    void Start()
    {
        StartCoroutine(FlickerRoutine());
    }

    private System.Collections.IEnumerator FlickerRoutine()
    {
        while (true)
        {
            // Wait random time before starting next blink (single or double)
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            // Decide single or double blink
            bool doDoubleBlink = Random.value < doubleBlinkProbability;

            // First blink
            yield return StartCoroutine(DoBlink());

            // Second blink if needed
            if (doDoubleBlink)
            {
                yield return new WaitForSeconds(doubleBlinkGap);
                yield return StartCoroutine(DoBlink());
            }
        }
    }

    private System.Collections.IEnumerator DoBlink()
    {
        flickerLight.enabled = false;
        yield return new WaitForSeconds(flickerDuration);
        flickerLight.enabled = true;
    }
}
