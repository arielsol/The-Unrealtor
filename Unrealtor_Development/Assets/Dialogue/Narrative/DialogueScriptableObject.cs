using UnityEngine;
using FMODUnity;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "DialogueScriptableObject", menuName = "Scriptable Objects/DialogueScriptableObject")]
public class DialogueScriptableObject : ScriptableObject
{
    [Header("Voice Lines (FMOD Events)")]
    public List<EventReference> VOSoundEvents = new List<EventReference>();

    [Header("Priority")]
    [Range(0, 10)] 
    public int priority = 0;

    [Header("VO Volume")]
    [Range(0f, 1f)]
    public float dialogueVOVolume = 1f;

    [Header("Category")]
    public SoundCategory category = SoundCategory.VO;
}