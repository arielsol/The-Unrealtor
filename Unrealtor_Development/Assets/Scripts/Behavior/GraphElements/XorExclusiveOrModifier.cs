using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

/**
 * Summary:
 * To evaluate "Success" aka true:
 * ONLY one input can be true
 */

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "XOR_ExclusiveOr", story: "Only one input can be true from [Inputs]", category: "Flow/Custom", id: "d14223dcd0131d757bd43ffbbc1d0e6c")]
public partial class XorExclusiveOrModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<List<bool>> Inputs;
    
    protected override Status OnStart()
    {
        int numTrue = 0;
        foreach (bool thispuzzle in Inputs.Value)
        {
            if (thispuzzle)
            {
                numTrue++;
            }
        }

        if (numTrue == 1)
        {
            return Status.Success;
        }
        return Status.Failure;
    }
}

