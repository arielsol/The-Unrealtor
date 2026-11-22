using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class QuadBase : MonoBehaviour
{
    ///<summary>
    ///IMPORTANT NOTE FOR DESIGNERS/PLACING IN ENGINE:
    ///make sure the blue arrow (forward vector) is facing the direction that you want facing the player
    /// </summary>

    public enum QuadType
    {
        LeftQuad,
        RightQuad
    }
    
    private MatchBase matchData;
    [SerializeField] public MeshFilter meshFilter;
    public QuadType quadType = QuadType.LeftQuad;
    public Mesh mesh { get; private set; }
    public bool IsNearby { get; private set; }

    [SerializeField] public List<Vector3> vertices = new List<Vector3>(4);

    public Collider HitCollider;
    public Collider MatchRangeCollider {get; private set;}
    
    void Awake()
    {
        IsNearby = false;
        
        // Get the MeshFilter component to access the mesh

        if (meshFilter != null && meshFilter.mesh.vertexCount >= 4)
        {

            vertices.Clear();
                
            // Store in the ScriptableObject
            for (int i = 0; i < meshFilter.mesh.vertexCount; i++)
            {
                vertices.Add(meshFilter.transform.TransformPoint(meshFilter.mesh.vertices[i]));
            }

            //Debug.Log("Set verticies " + gameObject.name + " " + transform.parent.name +  " " + vertices[0] + " " + vertices[1] + " " + vertices[2] + " " + vertices[3]);
        }
        else
        {
            if(MatchManager.Instance.DebugMode) Debug.LogWarning("Mesh has no vertices or not enough vertices." + gameObject.name);
        }
        
        MatchRangeCollider =  GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if ((quadType == QuadType.LeftQuad &&
                other.GetComponent<BasePlayerController>().playerID == BasePlayerController.PlayerID.Player1) ||
                (quadType == QuadType.RightQuad &&
                other.GetComponent<BasePlayerController>().playerID == BasePlayerController.PlayerID.Player2))
            {
                IsNearby = true;

                if (!MatchManager.Instance.IsNearbyMatch(matchData))
                {
                    MatchManager.Instance.AddNearbyMatch(matchData);
                }


                if(MatchManager.Instance.DebugMode) Debug.Log("Player entered " + quadType + " trigger for match: " + matchData.gameObject.name);
            }

        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if ((quadType == QuadType.LeftQuad &&
                 other.GetComponent<BasePlayerController>().playerID == BasePlayerController.PlayerID.Player1) ||
                (quadType == QuadType.RightQuad &&
                 other.GetComponent<BasePlayerController>().playerID == BasePlayerController.PlayerID.Player2))
            {
                IsNearby = false;
                if (!matchData.leftQuad.IsNearby && !matchData.rightQuad.IsNearby)
                {
                    MatchManager.Instance.RemoveNearbyMatch(matchData);

                    if (MatchManager.Instance.DebugMode)
                        Debug.Log("Player exited " + quadType + " trigger for match: " + matchData.gameObject.name);
                }
            }
        }
    }

    public void SetMatchData(MatchBase match)
    {
        matchData = match;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.75f, 0.25f, 0.0f, 0.75f);

        // Convert the local coordinate values into world
        // coordinates for the matrix transformation.
        float radius = 2.0f;
        float angleThreshold = 60.0f;
        if (matchData)
        {
            radius = matchData.smallRadiusThreshold;
            if (quadType == QuadType.LeftQuad)
            {
                angleThreshold = matchData.leftFacingAngleThreshold;
            }
            else if (quadType == QuadType.RightQuad)
                {
                angleThreshold = matchData.rightFacingAngleThreshold;
                }
        }
        Gizmos.DrawWireSphere(transform.position, radius);
        
        Gizmos.matrix = transform.localToWorldMatrix;
        MatchRangeCollider =  GetComponent<Collider>();
        float width=  (MatchRangeCollider.bounds.size.x+ MatchRangeCollider.bounds.size.z)/2; 
        Vector3 endPointL = Vector3.right * angleThreshold;
        Vector3 endPointR = Vector3.right * angleThreshold;
        
        endPointL = Vector3.forward -  endPointL.normalized;
        endPointR = Vector3.forward +  endPointR.normalized;
        
        Gizmos.DrawLine(Vector3.zero, endPointL);
        Gizmos.DrawLine(Vector3.zero, endPointR );
        
    }
}