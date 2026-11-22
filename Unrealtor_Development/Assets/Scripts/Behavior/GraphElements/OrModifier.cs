using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

/**
 * Summary:
 * To evaluate "Success" aka true:
 * All inputs must be true
 */

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "OR", story: "One [Inputs] must be true", category: "Flow/Custom", id: "99ba6c3924fe86a29fef0222f0bb5021")]
public partial class OrModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<List<GameObject>> Inputs;
    [SerializeReference] public BlackboardVariable<List<bool>> InputResult;
    protected override Status OnStart()
    {
        Debug.Log("ORORRRR");
        if (Inputs.Value.Count == 0)
        {
            Debug.Log("No items in OR list");
            return Status.Success;
        }
        
        foreach (GameObject puzzlePiece in Inputs.Value)
        {
            if (!puzzlePiece)
            {
                Debug.Log("Missing item in AND list");
                continue;
                
            }

            
            PuzzleBase puzzle = puzzlePiece.GetComponent<PuzzleBase>();
            if (puzzle)
            {
                if (puzzle.isSolved)
                {
                    Debug.Log(puzzle.gameObject.name+  " is matched in OR list");
                    //InputResult.Value.Add(true);
                    return Status.Success;
                }
            }
        }
        Debug.Log( "No matches in OR list");
        //InputResult.Value.Add(false);
        return Status.Failure;
    }

}

