using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Behavior/Event Channels/DoorOpenEC")]
#endif
[Serializable, GeneratePropertyBag]
[EventChannelDescription(name: "DoorOpenEC", message: "[Door] can be open", category: "Events", id: "124cd7ea205ee0de43be60b96d0740ed")]
public sealed partial class DoorOpenEC : EventChannel<DoorBase> { }

