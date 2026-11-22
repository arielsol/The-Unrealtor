using UnityEngine;

[CreateAssetMenu(fileName = "BehaviorGraphData", menuName = "Scriptable Objects/BehaviorGraphData")]
public class BehaviorGraphData : ScriptableObject
{
    [Header("Behavior Graph Events")]
    [SerializeField] public PuzzleCompletedEC TriggerPuzzleCompletedGraphEvent;
    [SerializeField] public PuzzleCompletedEC ReceivePuzzleCompletedGraphEvent;
    [SerializeField] public DoorOpenEC ReceiveDoorOpenEC;

}
