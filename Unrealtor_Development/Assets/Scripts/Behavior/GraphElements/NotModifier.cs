using System;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NOT", story: "Inverts Boolean [Input]", category: "Flow/Custom", id: "41ebfc9316ed08001572325df918073d")]
public partial class NotModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<bool> Input;
    
    protected override Status OnStart()
    {
        if (Input.Value == true)
        {
            return Status.Failure;
        }
        else 
        {
            return Status.Success;
        }
        
    }
}

