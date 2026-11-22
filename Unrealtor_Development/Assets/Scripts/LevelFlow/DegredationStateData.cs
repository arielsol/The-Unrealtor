using System.Collections.Generic;
using UnityEngine;


//**
//DEPRECATED
//**

//[CreateAssetMenu(fileName = "DegredationStateData", menuName = "Scriptable Objects/DegredationStateData")]
public class DegredationStateData : ScriptableObject
{
    public enum GameState
    {
        TUTORIAL = 0,
        RENOVATED = 1,
        GENTRIFIED = 2,
        ESCHERISTIC = 3
    }
    
    [SerializeField] public GameState MyGameState;
    [SerializeField] public List<MatchBase> StatePuzzles; //currently, when the last puzzle is completed, it triggers next state. Order doesn't matter besides the last one
    [SerializeField] public bool IsStateComplete = false;
}
