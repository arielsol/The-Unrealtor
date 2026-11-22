using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EdgarSequenceDatabase", menuName = "Edgar/Edgar Sequence Database")]
public class EdgarSequenceDatabase : ScriptableObject
{
    [Header("All Edgar Sequences")]
    public List<EdgarSequence> sequences = new List<EdgarSequence>();
}

