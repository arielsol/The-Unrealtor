using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "DebugTest", story: "print a Debug Log [string] to console", category: "Action", id: "3ecffcce9c311084a7b1df67564172dd")]
public partial class DebugTestAction : Action
{
    [SerializeReference] public BlackboardVariable<string> String;

    protected override Status OnStart()
    {
        Debug.Log(String.Value);
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        Debug.Log("Debug update" );
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

