using System;
using System.Collections;
using System.Collections.Generic;
using UnityCommunity.UnitySingleton;
using UnityEngine;

public class TutorialLighting : MonoSingleton<TutorialLighting>
{
    [SerializeField] private float defaultAmbientIntensity;
    [SerializeField] float defaultReflectionIntensity;
    [SerializeField] private float lightTime = .5f;
    [SerializeField] private Light[] lights;
    private List<float> lightValues = new List<float>();

    private void Awake()
    {
        CacheLightIntensities();
    }

    private void CacheLightIntensities()
    {
        lightValues.Clear();
        if (lights == null)
            return;

        foreach (Light light in lights)
        {
            if (light != null)
                lightValues.Add(light.intensity);
            else
                lightValues.Add(0f);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnStartInputTutorial += DarkenScene;
        //GameEvents.OnEndInputTutorial += BrightenScene;

        // Same-frame and ordering: Start() runs after all OnEnable calls, so events fired before
        // Start() used to subscribe were missed. Disabled-then-enabled objects also miss the event.
        // If the input tutorial is already active, apply dark state now.
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsInputTutorialActive)
            DarkenScene();
    }

    private void OnDisable()
    {
        GameEvents.OnStartInputTutorial -= DarkenScene;
        GameEvents.OnEndInputTutorial -= BrightenScene;
    }

    public void BrightenScene()
    {
        StartCoroutine(LightOverTime(lightTime));
    }

    private IEnumerator LightOverTime(float seconds)
    {
        float timePassed = 0f;
        while (timePassed < seconds)
        {
            float frameTime = Time.deltaTime;

            RenderSettings.ambientIntensity += (defaultAmbientIntensity / seconds * frameTime);
            RenderSettings.reflectionIntensity += (defaultReflectionIntensity / seconds * frameTime);

            if (lights != null)
            {
                for (int i = 0; i < lights.Length && i < lightValues.Count; i++)
                {
                    if (lights[i] != null)
                        lights[i].intensity += (lightValues[i] / seconds * frameTime);
                }
            }

            timePassed += frameTime;
            yield return new WaitForEndOfFrame();
        }

        if (lights != null)
        {
            for (int i = 0; i < lights.Length && i < lightValues.Count; i++)
            {
                if (lights[i] != null)
                    lights[i].intensity = lightValues[i];
            }
        }

        ResetRenderSettings();
    }

    private void DarkenScene()
    {
        RenderSettings.ambientIntensity = 0;
        RenderSettings.reflectionIntensity = 0;

        if (lights == null)
            return;

        foreach (Light light in lights)
        {
            if (light != null)
                light.intensity = 0f;
        }
    }

    private void ResetRenderSettings()
    {
        RenderSettings.ambientIntensity = defaultAmbientIntensity;
        RenderSettings.reflectionIntensity = defaultReflectionIntensity;
    }

    private void OnApplicationQuit()
    {
        ResetRenderSettings();
    }
}
