using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/PuzzleCompletedEC")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "PuzzleCompletedEC", message: "[Puzzle] is ready to be completed", category: "Events", id: "bdcd15faa44d56cec527a073250ea281")]
public sealed partial class PuzzleCompletedEC : EventChannel<PuzzleBase> { }

