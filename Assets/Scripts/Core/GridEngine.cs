using System;
using System.Collections.Generic;
using UnityEngine;

public enum TileType { Empty, Wall, Player, Enemy, CenterGoal };
public enum Direction { Up, Down, Left, Right };

public class GridEngine
{
    public TileType[,] Grid { get; private set; }
    public List<PlayerData> Players { get; private set; }
    public List<EnemyData> Enemies { get; private set; }
    public int CurrentPlayerIndex { get; private set; }
    public int GlobalMovesRemaining { get; private set; } // change it later _r;

    private int _rows, _cols;
    private System.Random _rng;

    public GridEngine(int rows, int cols, int maxGlobalMoves = 30)
    {
        _rows = rows;
        _cols = cols;
        _rng = new System.Random();
        Grid = new TileType[rows, cols];
        GlobalMovesRemaining = maxGlobalMoves; // _r
        Players = new List<PlayerData>();
        Enemies = new List<EnemyData>();

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                if (i == 0 || i == rows - 1 || j == 0 || j == cols - 1)
                    Grid[i, j] = TileType.Wall;

        Vector2Int center = new Vector2Int(rows / 2, cols / 2);
        Debug.Log(center);
        Grid[center.x, center.y] = TileType.CenterGoal;
    }

    public void AddPlayer(PlayerData player)
    {
        Players.Add(player);
        Grid[player.position.x, player.position.y] = TileType.Player;
    }

    public PlayerData GetCurrentPlayer() => Players[CurrentPlayerIndex];

    public void SwitchToNextPlayer()
    {
        if (Players.Count == 0) return;
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;

        int safety = 0;
        while (!GetCurrentPlayer().IsAlive && safety < Players.Count)
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
            safety++;
        }
    }

    public void AddEnemy(Vector2Int pos)
    {
        Enemies.Add(new EnemyData(pos));
        Grid[pos.x, pos.y] = TileType.Enemy;
    }

    public bool MovePlayerStep(PlayerData player, Direction dir)
    {
        if (player.bankedSteps <= 0 || GlobalMovesRemaining <= 0 || !player.IsAlive) return false;

        Vector2Int target = player.position;
        switch (dir)
        {
            case Direction.Up: target.y += 1; break;
            case Direction.Down: target.y -= 1; break;
            case Direction.Left: target.x -= 1; break;
            case Direction.Right: target.x += 1; break;
        }

        if (target.x < 0 || target.x >= _rows || target.y < 0 || target.y >= _cols) return false;

        if (Grid[target.x, target.y] == TileType.Wall) return false;

        foreach (var p in Players)
        {
            if (p != player && p.position == target && p.IsAlive) return false;
        }

        foreach (var e in Enemies)
        {
            if (e.position == target) return false;
        }

        Grid[player.position.x, player.position.y] = TileType.Empty;
        player.position = target;
        Grid[target.x, target.y] = TileType.Player;

        player.SpendBankedStep();
        GlobalMovesRemaining--;
        return true;
    }

    public void ForceMovePlayerTo(PlayerData player, Vector2Int target)
    {
        Grid[player.position.x, player.position.y] = TileType.Empty;
        player.position = target;
        Grid[target.x, target.y] = TileType.Player;
        player.SpendBankedStep();
        GlobalMovesRemaining--;
    }

    public void ExecuteEnemyTurn()
    {
        foreach (var enemy in Enemies.ToArray())
        {
            if (!Enemies.Contains(enemy)) continue;

            int steps = _rng.Next(1, 5);
            Direction dir = (Direction)_rng.Next(0, 4);

            Vector2Int currentPos = enemy.position;

            for (int i = 0; i < steps; i++)
            {
                Vector2Int target = currentPos;
                switch (dir)
                {
                    case Direction.Up: target.y += 1; break;
                    case Direction.Down: target.y -= 1; break;
                    case Direction.Left: target.x -= 1; break;
                    case Direction.Right: target.x += 1; break;
                }

                if (target.x < 0 || target.x >= _rows || target.y < 0 || target.y >= _cols)
                    break;

                if (Grid[target.x, target.y] == TileType.Wall)
                    break;

                bool occupied = false;
                foreach (var e in Enemies)
                {
                    if (e != enemy && e.position == target) { occupied = true; break; }
                }
                if (occupied) break;

                bool playerOccupied = false;
                foreach (var p in Players)
                {
                    if (p.position == target && p.IsAlive) { playerOccupied = true; break; }
                }
                if (playerOccupied) break;

                Grid[currentPos.x, currentPos.y] = TileType.Empty;  
                enemy.position = target;                            
                Grid[target.x, target.y] = TileType.Enemy;        
                currentPos = target;
            }
        }
    }

    public int RollPowerDie() => _rng.Next(1, 5);
    public void RemoveEnemy(EnemyData enemy)
    {
        if (Enemies.Contains(enemy))
        {
            Grid[enemy.position.x, enemy.position.y] = TileType.Empty;
            Enemies.Remove(enemy);
        }
    }

    public void ResetPlayerToSpawn(PlayerData player)
    {   // Add player spawn point variable later in the player data
        Grid[player.position.x, player.position.y] = TileType.Empty;
        player.position = new Vector2Int(1, 1);
        Grid[1, 1] = TileType.Player;
    }

    public bool CheckWin()
    {
        foreach (var p in Players)
        {
            if (p.IsAlive && Grid[p.position.x, p.position.y] == TileType.CenterGoal) return true;
        }
        return false;
    }

    public GridEngine GetSnapShot()
    {
        GridEngine snap = new GridEngine(_rows, _cols, GlobalMovesRemaining);
        snap.CurrentPlayerIndex = this.CurrentPlayerIndex;
        Array.Copy(this.Grid, snap.Grid, this.Grid.Length);

        foreach (var p in this.Players)
        {
            PlayerData copy = new PlayerData(p.ID, p.position, p.PlayerColor, p.PlayerName);
            copy.health = p.health;
            copy.bankedSteps = p.bankedSteps;
            copy.Powers = new List<int>(p.Powers);
            snap.Players.Add(copy);
        }

        foreach (var e in this.Enemies)
        {
            EnemyData copy = new EnemyData(e.position);
            copy.health = e.health;
            copy.Powers = new List<int>(e.Powers);
            snap.Enemies.Add(copy);
        }

        return snap;
    }
}