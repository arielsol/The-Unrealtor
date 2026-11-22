using UnityEngine;

using FMODUnity;

public class SetAmbienceOnEnable : MonoBehaviour
{
    [SerializeField] private AmbienceType ambienceType;

    private void OnEnable()

    {
        RuntimeManager.StudioSystem.setParameterByName("AmbienceTypeLeft", (int)ambienceType);
        RuntimeManager.StudioSystem.setParameterByName("AmbienceTypeRight", (int)ambienceType);
    }
}
