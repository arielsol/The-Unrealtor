using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CheckPuzzleInputValid", story: "Check if the [PuzzlePiece] belongs to an Input List", category: "Flow/Custom", id: "d01bbedaa3fab290cae77af610231fbb")]
public partial class CheckPuzzleInputValidModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<MatchBase> PuzzlePiece;
    [SerializeReference] public BlackboardVariable<MatchBase> ThisPuzzle;
    [SerializeReference] [NotNull] public BlackboardVariable<List<GameObject>> InputList1;
    [SerializeReference] [NotNull] public BlackboardVariable<List<GameObject>> InputList2;
    [SerializeReference] [NotNull] public BlackboardVariable<List<GameObject>> InputList3;
    [SerializeReference] [NotNull] public BlackboardVariable<List<GameObject>> InputList4;

    protected override Status OnStart()
    {
        //Check if the [PuzzlePiece] belongs to [InputList] && if it's a type MatchBase (may change?)

        bool returnValue = true;
        if (InputList1.Value.Count > 0)
        {
            Debug.Log("1");
            returnValue &= CheckList(InputList1.Value);
        }
        if (InputList2.Value.Count > 0)
        {
            Debug.Log("2");
            returnValue &= CheckList(InputList2.Value);
        }
        if (InputList3.Value.Count > 0)
        {
            Debug.Log("3");
            returnValue &= CheckList(InputList3.Value);
        }
        if (InputList4.Value.Count > 0)
        {
            Debug.Log("4");
            returnValue &= CheckList(InputList4.Value);
        }

        if (ThisPuzzle.Value != null)
        {
            Debug.Log(PuzzlePiece.Value.gameObject.name + " is valid using Modifier on "+ ThisPuzzle.Value.gameObject.name + " " + returnValue);
        }
        else
        {
            Debug.Log(PuzzlePiece.Value.gameObject.name + " is valid using Modifier " + returnValue);
        }
        return returnValue ? Status.Success : Status.Failure;
    }
    
    bool CheckList(List<GameObject> currList)
    {
        foreach (GameObject currPuzzlePiece in currList)
        {
            MatchBase puzzle = currPuzzlePiece.GetComponent<MatchBase>();
            if (puzzle.MatchTypeEnum == PuzzlePiece.Value.MatchTypeEnum)
            {
                //this puzzle piece belongs to the list
                Debug.Log(puzzle.gameObject.name + " is a valid PuzzlePiece");
                return true;
            }
            else if (!puzzle)
            {
                Debug.Log(puzzle.gameObject.name + " is NOT a valid PuzzlePiece");
                return false;
            }
        }
        Debug.Log(currList + " has nothing that matches");
        return false;
    }
}



