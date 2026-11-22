using UnityEngine;
using Yarn.Unity;

public class DialogueTriggerBox : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private DialogueManager DialogueManagerObj;
    public string DialogueName = "";
    private bool HasPlayedOnce = false;
    void Awake()
    {
        DialogueManagerObj = GameObject.Find("DialogueManager").GetComponent<DialogueManager>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnTriggerEnter(Collider collision)
    {
        Debug.Log("Collision");
        if (DialogueName != "" && !HasPlayedOnce)
        {
            DialogueManagerObj.StartDialogue(DialogueName);
            HasPlayedOnce = true;
        }
    }
}
