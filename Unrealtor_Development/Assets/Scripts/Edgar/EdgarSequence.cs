using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EdgarSequence", menuName = "Edgar/Edgar Sequence")]
public class EdgarSequence : ScriptableObject
{
    public string sequenceId;          // Used to reference in Inspector / external code
    public List<EdgarStep> steps = new List<EdgarStep>();      // List of steps to execute in order
}


