using System.Collections;
using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering;

public class MiddleIndicatorController : MonoBehaviour
{
    [Header("Global Toggles")]
    [SerializeField] private bool middleHintEnabled = true;
    [SerializeField] private bool onlyAfterStruggling = true;
    [SerializeField] private float struggleTimeSeconds = 10f;

    private float _struggleTimer = 0f;
    private MatchBase _currentMatch;
    private float _currentCloseness;

    [Header("Post Processing (Gaussian / radial juice)")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private string blurParameterName = "_Intensity";
    [SerializeField] private float maxBlurIntensity = 1.0f;

    [Header("Camera Nudge")]
    [SerializeField] private CinemachineVirtualCamera leftMoveCam;
    [SerializeField] private CinemachineVirtualCamera rightMoveCam;
    [SerializeField] private float baseScreenXLeft = 0.25f;
    [SerializeField] private float baseScreenXRight = 0.75f;
    [SerializeField] private float middleScreenX = 0.5f;

    [Header("Silhouette & Particles")]
    [SerializeField] private GameObject silhouettePrefab;
    [SerializeField] private float silhouetteMaxAlpha = 0.9f;
    [SerializeField] private float silhouetteMinAlpha = 0.0f;
    [SerializeField] private ParticleSystem gentleParticles;

    private Material _silhouetteMaterialInstance;
    private bool _isSilhouetteSpawned;

    private void Awake()
    {
        if (silhouettePrefab != null)
        {
            var renderer = silhouettePrefab.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                _silhouetteMaterialInstance = renderer.material;
            }
        }
    }

    public void EnableHints(bool enable)
    {
        middleHintEnabled = enable;
        if (!enable)
            ClearIndicator();
    }

    public void UpdateIndicator(MatchBase match, float closeness)
    {
        if (!middleHintEnabled)
            return;

        _currentMatch = match;
        _currentCloseness = closeness;

        if (onlyAfterStruggling)
        {
            if (closeness < 0.4f)
            {
                _struggleTimer += Time.deltaTime;
            }
            else
            {
                _struggleTimer = 0f;
            }

            if (_struggleTimer < struggleTimeSeconds)
            {
                ApplyNoHintVisuals();
                return;
            }
        }

        ApplyBlur(closeness);
        ApplyCameraNudge(closeness);
        ApplySilhouetteAndParticles(closeness);
    }

    public void ClearIndicator()
    {
        _currentMatch = null;
        _currentCloseness = 0f;
        _struggleTimer = 0f;

        ApplyBlur(0f);
        ApplyCameraNudge(0f);

        if (_silhouetteMaterialInstance != null)
        {
            SetSilhouetteAlpha(0f);
        }

        if (gentleParticles != null && gentleParticles.isPlaying)
        {
            gentleParticles.Stop();
        }
    }

    private void ApplyNoHintVisuals()
    {
        ApplyBlur(0f);
        ApplyCameraNudge(_currentCloseness * 0.25f);
        if (_silhouetteMaterialInstance != null)
            SetSilhouetteAlpha(0f);
    }


    private void ApplyBlur(float closeness)
    {
        if (postProcessVolume == null) return;
    
        
        if (postProcessVolume.profile.TryGet<RadialBlurVolumeComponent>(out var blur))
        {
            blur.intensity.value = Mathf.Lerp(0f, maxBlurIntensity, closeness);
        }

    }

    

    private void ApplyCameraNudge(float closeness)
    {
        if (leftMoveCam != null)
        {
            var framing = leftMoveCam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framing != null)
                framing.m_ScreenX = Mathf.Lerp(baseScreenXLeft, middleScreenX, closeness);
        }

        if (rightMoveCam != null)
        {
            var framing = rightMoveCam.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framing != null)
                framing.m_ScreenX = Mathf.Lerp(baseScreenXRight, middleScreenX, closeness);
        }
    }
    

    private void ApplySilhouetteAndParticles(float closeness)
    {
        if (!_isSilhouetteSpawned && silhouettePrefab != null && _currentMatch != null)
        {
            silhouettePrefab.SetActive(true);
            _isSilhouetteSpawned = true;
        }

        if (_silhouetteMaterialInstance != null)
        {
            float t = 1f - closeness;
            float alpha = Mathf.Lerp(silhouetteMinAlpha, silhouetteMaxAlpha, t);
            SetSilhouetteAlpha(alpha);
        }

        if (gentleParticles != null)
        {
            if (closeness >= 0.5f && !gentleParticles.isPlaying)
                gentleParticles.Play();
            else if (closeness < 0.3f && gentleParticles.isPlaying)
                gentleParticles.Stop();
        }
    }

    private void SetSilhouetteAlpha(float a)
    {
        if (_silhouetteMaterialInstance == null) return;

        Color c = _silhouetteMaterialInstance.color;
        c.a = a;
        _silhouetteMaterialInstance.color = c;
    }
}
