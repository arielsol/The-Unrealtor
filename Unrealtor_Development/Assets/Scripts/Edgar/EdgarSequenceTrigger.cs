using UnityEngine;
using System.Collections.Generic;

public class EdgarSequenceTrigger : MonoBehaviour
{
    public string sequenceId;
    [Header("Collider Trigger")]
    [Tooltip("Leave empty to accept any collider; otherwise require this tag.")]
    public string requiredTag = "Player";
    [Tooltip("If true, will only trigger once.")]
    public bool triggerOnce = true;
    bool hasTriggered;

    [Header("VO Trigger")]
    [Tooltip("Optional VO ID. If set, will wait for this VO to complete before checking if player is in trigger.")]
    public string voId;
    
    private bool playerInTrigger = false;
    private bool waitingForVO = false;
    private HashSet<Collider> playersInTrigger = new HashSet<Collider>();

    private void Awake()
    {
        if (!string.IsNullOrEmpty(voId))
        {
            waitingForVO = true;
        }
    }

    void OnEnable()
    {
        DialogueManager.OnDialogueCompleted += OnDialogueCompleted;
    }

    void OnDisable()
    {
        DialogueManager.OnDialogueCompleted -= OnDialogueCompleted;
    }

    void OnDialogueCompleted(string dialogueName)
    {
        if (waitingForVO && dialogueName == voId)
        {
            waitingForVO = false;
            CheckAndTriggerSequence();
        }
    }

    public void Play()
    {
        if (EdgarManager.Instance != null && !string.IsNullOrEmpty(sequenceId))
        {
            EdgarManager.Instance.PlaySequence(sequenceId);
        }
        else
        {
            Debug.LogWarning("EdgarSequenceTrigger: No valid EdgarManager or sequenceId.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered)
            return;

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        playersInTrigger.Add(other);
        playerInTrigger = true;

        // If waitingForVO is true, wait for the VO to complete
        if (waitingForVO)
        {
            return;
        }
        else
        {
            // No VO, trigger sequence
            Play();
            hasTriggered = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        playersInTrigger.Remove(other);
        playerInTrigger = playersInTrigger.Count > 0;
    }

    void CheckAndTriggerSequence()
    {
        // Check if player is still (or now) in trigger
        if (playerInTrigger && !hasTriggered)
        {
            hasTriggered = true;
            Play();
        }
    }
}


