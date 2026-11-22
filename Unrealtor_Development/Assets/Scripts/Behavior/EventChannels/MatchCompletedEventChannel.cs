using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/MatchCompletedEventChannel")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "MatchCompletedEventChannel", message: "[Match] is Complete", category: "Events/Custom", id: "a8f1e9a2d7c14e0db0c4a5f1b84f9a3c")]
public sealed partial class MatchCompletedEventChannel : EventChannel<PuzzleBase>
{
}


