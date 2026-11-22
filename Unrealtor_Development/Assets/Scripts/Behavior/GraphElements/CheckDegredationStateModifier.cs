using System;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CheckDegredationState", story: "Check [DegredationState] Status", category: "Flow", id: "8b55d22d0785c5d94c160261b58c8c4e")]
public partial class CheckDegredationStateModifier : Modifier
{
    [SerializeReference] public BlackboardVariable<DegredationStateData> DegredationState;

    protected override Status OnStart()
    {
        
        
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (DegredationState.Value.IsStateComplete)
        {
            return Status.Success;
        }
            
        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

