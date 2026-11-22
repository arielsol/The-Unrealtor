using UnityEngine;
using System.Collections.Generic;

public class MultiVOTrigger : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public VOTrigger Manager;
    public List<GameObject> requiredObjs = new List<GameObject>();
    public bool played = false;
    public string dialogue = "";
    void Start()
    {
        Manager = GameObject.Find("VOTriggerManager").GetComponent<VOTrigger>();
    }

    // Update is called once per frame
    void Update()
    {
        if (played)
        {
            return;
        }
        int count = 0;
        foreach (GameObject obj in requiredObjs)
        {
            if (Manager.objectsSeen.Contains(obj))
            {
                count++;
                //Debug.Log("Count: " + count);
            }
        }
        if (count == requiredObjs.Count && dialogue != "" && requiredObjs.Count != 0)
        {
            bool p = DialogueManager.Instance.StartDialogue(dialogue);
            if (p)
            {
                played = true;
            }

        }
    }
}
