using System;
using UnityEngine;
using UnityEngine.VFX;

[ExecuteInEditMode]
public class UpdateFireIntensity : MonoBehaviour
{
    [SerializeField, Tooltip("Controls other intensity-related values for the fire."), Range(0, 1)] private float fireIntensity = 1f;
    [SerializeField] private Transform firePivot;

    [SerializeField, Range(0, 1),
     Tooltip("How much Fire Intensity affects the 'Flame Intensity' property in the fire VFX")]
    private float vfxWeight = 1f;
    [SerializeField, Range(0, 1),
     Tooltip("How much Fire Intensity affects the y-axis of the Fire Pivot scale.")]
    private float verticalScaleWeight = 1f;
    [SerializeField, Range(0, 1),
     Tooltip("How much Fire Intensity affects the x- and z-axes of the Fire Pivot scale.")]
    private float horizontalScaleWeight = 0f;

    [SerializeField] private VisualEffect[] visualEffects;
    
    //comparison values for efficiency
    private Transform prevFirePivot;
    private float prevVfxWeight;
    private float prevVerticalScaleWeight;
    private float prevHorizontalScaleWeight;
    private float prevFireIntensity;
    
    private void Awake()
    {
	    UpdateValues();
    }

    private void OnValidate()
    {
	    UpdateEffect();
    }

    private void UpdateEffect()
    {
	    //check if anything has changed
	    if (firePivot == prevFirePivot && prevHorizontalScaleWeight == horizontalScaleWeight &&
	        prevVerticalScaleWeight == verticalScaleWeight && prevVfxWeight == vfxWeight && prevFireIntensity == fireIntensity)
	    {
		    return;
	    }
	    
	    //update effects
	    
	    //vfx
	    float newIntensityValue = Mathf.Lerp(1, fireIntensity, vfxWeight);
	    foreach (VisualEffect v in visualEffects)
	    {
		    
		    v.SetFloat("Flame Intensity", newIntensityValue);
	    }
	    
	    //scale
	    float y = Mathf.Lerp(1, fireIntensity, verticalScaleWeight);
	    float xz = Mathf.Lerp(1, fireIntensity, horizontalScaleWeight);
	    
	    firePivot.localScale = new Vector3(xz, y, xz);
	    
	    UpdateValues();
    }

    private void UpdateValues()
    {
	    prevFirePivot = firePivot;
	    prevVfxWeight = vfxWeight;
	    prevVerticalScaleWeight = verticalScaleWeight;
	    prevHorizontalScaleWeight = horizontalScaleWeight;
	    prevFireIntensity = fireIntensity;
    }
}
