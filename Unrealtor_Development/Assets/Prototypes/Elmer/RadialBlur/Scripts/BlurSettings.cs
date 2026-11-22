using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Post-processing/Radial Blur")]

public class BlurSettings : VolumeComponent, IPostProcessComponent
{

    [Tooltip("Standard Deviation (spread) of the blur. Grid size is approx. 3x larger.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0.0f, 0.0f, 15.0f);

    [Tooltip("Center point of the radial blur in screen space (0-1 range)")]
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

    [Tooltip("Number of samples for blur quality")]
    public ClampedIntParameter samples = new ClampedIntParameter(11, 4, 32);


    public bool IsActive()
    {
        return (strength.value > 0.0f) && active;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
