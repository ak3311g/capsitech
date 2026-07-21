using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyData
{
    public Vector2Int position;
    public int health = 3;
    public List<int> Powers = new List<int>();

    public EnemyData(Vector2Int pos)
    {
        position = pos;
        health = 3;
    }
}