using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public long lastUpdated;

    public bool isFirstTimeGame = true;

    // Player positions
    public Vector3 player1Position;
    public Vector3 player2Position;
    public bool hasPlayer1Position;
    public bool hasPlayer2Position;

    // Player rotations
    public Quaternion player1Rotation;
    public Quaternion player2Rotation;
    public bool hasPlayer1Rotation;
    public bool hasPlayer2Rotation;

    // Player gravity directions
    public Vector3 player1GravityDirection;
    public Vector3 player2GravityDirection;
    public bool hasPlayer1GravityDirection;
    public bool hasPlayer2GravityDirection;


    // Match completion states by MatchTypeEnum name
    public List<string> completedMatches = new List<string>();

    public GameData()
    {
        isFirstTimeGame = true;
    }
}