using System;
using UnityEngine;

/**
 * SUMMARY:
 * The ultimate parent class for all puzzle elements.
 * Objects like MatchBase, DoorNotes, etc should all inherit from PuzzleBase
 * Has abstract classes: OnTriggered, OnReceived 
 */

public abstract class PuzzleBase : MonoBehaviour
{
    public enum PuzzleSide
    {
        LeftPuzzle,
        RightPuzzle
    }
    
    [NonSerialized] public bool isSolved = false;
    public virtual void OnPuzzleTriggered()
    {
        
    }

    public virtual void OnPuzzleReceived()
    {
        
    }
}
