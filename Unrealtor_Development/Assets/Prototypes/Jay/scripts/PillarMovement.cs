using System.Collections.Generic;
using UnityEngine;

public class PillarMovement : MonoBehaviour
{
    public Transform pillar;                  // The pillar object to move
    public Vector3 loweredOffset = Vector3.down;
    public float speed = 2f;

    private Vector3 originalPos;
    private Vector3 targetPos;
    private bool playerNear = false;
    [SerializeField] QuadBase quadBase;

    void Start()
    {
        if (pillar == null) pillar = transform.Find("Pillar");
        originalPos = pillar.position;
    }

    void Update()
    {
        targetPos = playerNear ? originalPos + loweredOffset : originalPos;
        pillar.position = Vector3.MoveTowards(pillar.position, targetPos, speed * Time.deltaTime);

        if (quadBase != null && playerNear) 
        {
            List<Vector3> vertexList = new List<Vector3>();
            Vector3[] vertices = quadBase.mesh.vertices;
            // Store in the ScriptableObject
            for (int i = 0; i < vertices.Length; i++)
            {
                vertexList.Add(transform.TransformPoint(vertices[i]));
            }
            quadBase.vertices = vertexList;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerNear = false;
    }
}
