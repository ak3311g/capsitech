using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SerializeField]
public class PlayerData
{
    public int ID;
    public Vector2Int position;
    public int health;
    public int bankedSteps;
    public int MaxBankedSteps = 6;
    public Color PlayerColor;
    public string PlayerName;
    public bool IsAlive => health > 0;

    public List<int> Powers = new List<int>();

    public PlayerData(int id, Vector2Int startPos, Color color, string name = "Player")
    {
        ID = id;
        position = startPos;
        health = 5;
        bankedSteps = 0;
        PlayerColor = color;
        PlayerName = name;
    }

    public void AddBankedSteps(int roll)
    {
        bankedSteps = Math.Min(bankedSteps + roll, MaxBankedSteps);
    }

    public bool SpendBankedStep()
    {
        if(bankedSteps <= 0) return false;
        bankedSteps--;
        return true;
    }
}
