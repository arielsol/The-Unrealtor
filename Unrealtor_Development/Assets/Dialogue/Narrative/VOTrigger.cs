using System.Collections.Generic;

using UnityEngine;



public class VOTrigger : MonoBehaviour

{

    [Header("Raycast Settings")]

    public float maxDistance = 5f;

    public LayerMask interactableLayer;



    private DialogueManager dialogueManager;

    private Camera[] playerCameras;

    private HashSet<GameObject> objectsPlayed = new HashSet<GameObject>();

    public HashSet<GameObject> objectsSeen = new HashSet<GameObject>();

    private HashSet<string> dialoguePlayed = new HashSet<string>();



    void Awake()

    {

        GameObject[] cameraObjects = GameObject.FindGameObjectsWithTag("PlayerCamera");

        playerCameras = new Camera[cameraObjects.Length];



        for (int i = 0; i < cameraObjects.Length; i++)

        {

            playerCameras[i] = cameraObjects[i].GetComponent<Camera>();

        }



        GameObject managerObj = GameObject.Find("DialogueManager");

        if (managerObj != null)

        {

            dialogueManager = managerObj.GetComponent<DialogueManager>();

        }

    }



    void Update()

    {

        if (dialogueManager == null) return;


        objectsSeen.Clear();
        foreach (Camera cam in playerCameras)

        {

            //Debug.Log("Loop Entered");
            Vector3 frontPos = cam.transform.position;
            Ray ray = new Ray(frontPos, cam.transform.forward);

            RaycastHit[] VOHits = Physics.RaycastAll(ray, maxDistance);

            for (int i = 0; i < VOHits.Length; i++)
            {
                GameObject hitObj = VOHits[i].collider.gameObject;
                // Debug.Log(hitObj.name);
                VOCamTrigger VOComp = hitObj.GetComponent<VOCamTrigger>();
                Debug.DrawRay(frontPos, cam.transform.forward * maxDistance * -1, Color.blue);

                if (VOComp != null)

                {
                    //   Debug.Log("Hit VO");
                    if (VOComp.VOName != "" && !dialoguePlayed.Contains(VOComp.VOName))
                    {
                        bool played = dialogueManager.StartDialogue(VOComp.VOName);
                        if (played)
                        {
                            dialoguePlayed.Add(VOComp.VOName);
                        }

                    }

                    objectsPlayed.Add(hitObj);
                    objectsSeen.Add(hitObj);

                }

            }

            /* if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, interactableLayer))

             {
                 Debug.Log("Hit Object");
                 GameObject hitObj = hit.collider.gameObject;
                 Debug.Log(hitObj.name);
                 VOCamTrigger VOComp = hitObj.GetComponent<VOCamTrigger>();
                 Debug.DrawRay(cam.transform.position, cam.transform.forward * maxDistance, Color.blue);

                 if (VOComp != null)

                 {
                     // Debug.Log("Hit VO");
                     if (VOComp.VOName != "" && !dialoguePlayed.Contains(VOComp.VOName))
                     {
                         bool played = dialogueManager.StartDialogue(VOComp.VOName);
                         if (played)
                         {
                             dialoguePlayed.Add(VOComp.VOName);
                         }

                     }

                     objectsPlayed.Add(hitObj);
                     objectsSeen.Add(hitObj);

                 }

             }
             else
             {
                 Debug.DrawRay(cam.transform.position, cam.transform.forward * maxDistance, Color.yellow);
             }
            */

        }

    }

}