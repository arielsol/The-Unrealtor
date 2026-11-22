using System.Collections.Generic;
using UnityEngine;

public class ExtinguisherFragmentGroup : MonoBehaviour
{
    public enum HalfType
    {
        Left,
        Right
    }

    [Header("Group Type")]
    public HalfType halfType = HalfType.Left;

    [Header("Fragments")]
    [SerializeField] private List<ExtinguisherFragment> fragments = new List<ExtinguisherFragment>();
    [SerializeField] private Transform leader;

    [Header("Assemble Targets")]
    [SerializeField] private List<Transform> assembledAnchors = new List<Transform>();

    public void SetAssembleState(bool assembling)
    {
        if (assembling)
        {
            for (int i = 0; i < fragments.Count; i++)
            {
                if (i < assembledAnchors.Count)
                {
                    fragments[i].SetAssembledAnchor(assembledAnchors[i]);
                }
            }
        }

        foreach (var fragment in fragments)
        {
            if (assembling) fragment.BeginAssemble(); else fragment.EndAssemble();
        }
    }

    public void ConfigureIdleMotions()
    {
        foreach (var fragment in fragments)
        {
            if (halfType == HalfType.Left)
            {
                SetIdleType(fragment, ExtinguisherFragment.IdleMotionType.VerticalOscillation);
            }
            else
            {
                SetIdleType(fragment, ExtinguisherFragment.IdleMotionType.FollowLeaderFigureEight);
                if (leader == null)
                {
                    fragment.SendMessage("SetLeader", transform, SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    private void SetIdleType(ExtinguisherFragment fragment, ExtinguisherFragment.IdleMotionType type)
    {
        var fragmentSerializedObject = fragment;
    }
}


