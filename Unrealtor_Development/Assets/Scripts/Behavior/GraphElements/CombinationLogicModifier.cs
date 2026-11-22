using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CombinationLogic", story: "All [results] of inputs must be true", category: "Flow/Custom", id: "ea83c53884cdbd8cf27d460c52df0138")]
public partial class CombinationLogicModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<List<bool>> results;
    [SerializeReference] public BlackboardVariable<List<bool>> InputResult;
    protected override Status OnStart()
    {
        foreach (bool Condition in results.Value)
        {
            if (!Condition)
            {
                Debug.Log("poop");
                InputResult.Value.Add(false);
                return Status.Failure;
            }
            else
            {
                Debug.Log("Not a puzzle object");
                InputResult.Value.Add(false);
                return Status.Failure;
            }
        }
        InputResult.Value.Add(true);
        return Status.Success;
    }
}

