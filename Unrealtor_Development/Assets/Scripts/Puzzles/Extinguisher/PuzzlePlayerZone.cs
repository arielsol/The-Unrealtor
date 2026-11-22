using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PuzzlePlayerZone : MonoBehaviour
{
    public bool IsPlayerInside { get; private set; }

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            IsPlayerInside = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            IsPlayerInside = false;
        }
    }
}


