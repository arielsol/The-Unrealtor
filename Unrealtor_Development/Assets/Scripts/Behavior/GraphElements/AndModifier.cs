using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

/**
 * Summary:
 * To evaluate "Success" aka true:
 * All inputs must be true
 */

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "AND", story: "All [Inputs] must be true", category: "Flow/Custom", id: "09f9cb0ed2ff2bd35444617d4d04fca5")]
public partial class AndModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<List<GameObject>> Inputs;
    [SerializeReference] public BlackboardVariable<List<bool>> InputResult;
    protected override Status OnStart()
    {
        Debug.Log("Hi");
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
                if (!puzzle.isSolved)
                {
                    Debug.Log(puzzle.gameObject.name+  " is NOT matched in AND list");
                    //InputResult.Value.Add(false);
                    return Status.Failure;
                }
                else
                {
                    Debug.Log(puzzle.gameObject.name+  " is matched in AND list");
                }
            }
            
        }
        //InputResult.Value.Add(true);
        Debug.Log( "All AND list is matched");
        return Status.Success;
    }

  
}

