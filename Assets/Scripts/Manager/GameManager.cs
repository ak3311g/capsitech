using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameState{Playing, PlayerTurn, EnemyTurn, Win, Lose};
    public static GameManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GridRenderer gridRenderer;

    [Header("Game Settings")]
    [SerializeField] private int gridSize = 7;
    [SerializeField] private int maxGlobalMoves=30;
    [SerializeField] private int maxUndoSteps = 5;

    [Header("UI")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private TextMeshProUGUI statusText;

    private GridEngine _engine;
    private Stack<GridEngine> _undoStack;
    private GameState _currentState = GameState.Playing;
    private bool _isEnemyTurn = false;
    private Coroutine _enemyTurnCoroutine;

    void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    // Start is called before the first frame update
    void Start()
    {
        _undoStack = new Stack<GridEngine>();

        if(gridRenderer == null)
        gridRenderer = FindObjectOfType<GridRenderer>();

        _engine = gridRenderer.GetEngine();
        _currentState = GameState.PlayerTurn;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        UpdateStatusText("Player 1's Turn! Roll the die.");
    }

    // Update is called once per frame
    void Update()
    {
        CheckGameConditions();
    }

    void CheckGameConditions()
    {
        if (_currentState == GameState.Win || _currentState == GameState.Lose)
            return;

        if (_engine.CheckWin())
        {
            _currentState = GameState.Win;
            OnGameWin();
            return;
        }

        bool allDead = true;
        foreach (var player in _engine.Players)
        {
            if (player.IsAlive) { allDead = false; break; }
        }
        if (allDead)
        {
            _currentState = GameState.Lose;
            OnGameLose("All players are dead!");
            return;
        }

        if (_engine.GlobalMovesRemaining <= 0)
        {
            _currentState = GameState.Lose;
            OnGameLose("Out of moves!");
            return;
        }
    }

    void OnGameWin()
    {
        var current = _engine.GetCurrentPlayer();
        Debug.Log($"Player {current.ID} WINS! Reached the center!");
        
        if (winPanel != null)
            winPanel.SetActive(true);
        
        UpdateStatusText($"Player {current.ID} WINS! 🎉");
    }

    void OnGameLose(string reason)
    {
        Debug.Log($"Game Over: {reason}");
        
        if (losePanel != null)
            losePanel.SetActive(true);
        
        UpdateStatusText($"Game Over! {reason}");
    }

     void UpdateStatusText(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    public void OnRollDice()
    {
        if (_currentState != GameState.PlayerTurn)
        {
            Debug.Log("It's not your turn!");
            return;
        }

        var current = _engine.GetCurrentPlayer();
        if (current == null) return;

        int roll = Random.Range(1, 5);

        current.AddBankedSteps(roll);
        gridRenderer.UpdateUI();

        UpdateStatusText($"Player {current.ID} rolled {roll}! Swipe to move.");
    }

    public void OnPlayerMove(Direction dir)
    {
        if (_currentState != GameState.PlayerTurn)
        {
            Debug.Log("It's not your turn!");
            return;
        }

        var current = _engine.GetCurrentPlayer();
        if (current == null) return;

        if (current.bankedSteps <= 0)
        {
            UpdateStatusText("No steps! Roll the die first.");
            return;
        }

        SaveStateForUndo();

        bool moved = _engine.MovePlayerStep(current, dir);

        if (moved)
        {
            gridRenderer.UpdatePlayerPosition();
            gridRenderer.UpdateUI();

            CheckPlayerEnemyCollision(current);

            if (_engine.CheckWin())
            {
                _currentState = GameState.Win;
                OnGameWin();
                return;
            }

            if(current.bankedSteps <= 0)
            {
                UpdateStatusText($"Player {current.ID} used all steps! Ending turn.");
                StartCoroutine(DelayedEndTurn(1f));
            }
            else
            {
                UpdateStatusText($"Player {current.ID} moved! Steps remaining: {current.bankedSteps}");
            }
        }
        else
        {
            UpdateStatusText("Move Blocked");
        }
    }

    void CheckPlayerEnemyCollision(PlayerData player)
    {
        foreach (var enemy in _engine.Enemies)
        {
            if (player.position == enemy.position)
            {
                ResolveCombat(player, enemy);
                break;
            }
        }
    }

    public void EndTurn()
    {
        if(_currentState != GameState.PlayerTurn)
        {
            return;
        }

        _currentState = GameState.EnemyTurn;
        UpdateStatusText("EnemyTurn");

        StartCoroutine(EnemyTurnRoutine());
    }

    IEnumerator DelayedEndTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        EndTurn();
    }

    IEnumerator EnemyTurnRoutine()
    {
        _isEnemyTurn = true;
        var enemies = new List<EnemyData>(_engine.Enemies);
        foreach(var enemy in enemies)
        {
            if(!_engine.Enemies.Contains(enemy)) continue;

            int steps = Random.Range(1,5);
            Direction dir = (Direction)Random.Range(0,4);

            for(int i = 0; i < steps; i++)
            {
                if(!_engine.Enemies.Contains(enemy)) break;

                SaveStateForUndo();

                MoveEnemyOneStep(enemy, dir);

                gridRenderer.RenderGrid();
                gridRenderer.UpdatePlayerPosition();
                gridRenderer.UpdateUI();

                CheckEnemyPlayerCollision(enemy);
                yield return new WaitForSeconds(0.3f);
            }
        }

        _isEnemyTurn = false;
        _engine.SwitchToNextPlayer();
        _currentState = GameState.PlayerTurn;

        var current = _engine.GetCurrentPlayer();
        if (current != null)
        {
            UpdateStatusText($"Player {current.ID}'s Turn! Roll the die.");
            Debug.Log($"Turn switched to Player {current.ID}");
        }
        else
        {
            _currentState = GameState.Lose;
            OnGameLose("No players alive!");
        }

        gridRenderer.UpdateUI();
    }

    void MoveEnemyOneStep(EnemyData enemy, Direction dir)
    {
        Vector2Int target = enemy.position;
        switch (dir)
        {
            case Direction.Up: target.y += 1; break;
            case Direction.Down: target.y -= 1; break;
            case Direction.Left: target.x -= 1; break;
            case Direction.Right: target.x += 1; break;
        }
        
        if (target.x < 0 || target.x >= gridSize || target.y < 0 || target.y >= gridSize) return;

        if (_engine.Grid[target.x, target.y] == TileType.Wall) return;

        foreach (var e in _engine.Enemies)
        {
            if (e != enemy && e.position == target)
                return;
        }

        foreach (var p in _engine.Players)
        {
            if (p.position == target && p.IsAlive)
                return;
        }

         _engine.Grid[enemy.position.x, enemy.position.y] = TileType.Empty;
        enemy.position = target;
        _engine.Grid[target.x, target.y] = TileType.Enemy;
    }

    void CheckEnemyPlayerCollision(EnemyData enemy)
    {
        foreach (var player in _engine.Players)
        {
            if (player.IsAlive && player.position == enemy.position)
            {
                ResolveCombat(player, enemy);
                break;
            }
        }
    }

    void ResolveCombat(PlayerData player, EnemyData enemy)
    {
        SaveStateForUndo();

        int playerPower = _engine.RollPowerDie();
        int enemyPower = _engine.RollPowerDie();

        UpdateStatusText($"COMBAT! Player {player.ID}: {playerPower} vs Enemy: {enemyPower}");

        if(playerPower > enemyPower)
        {
            _engine.RemoveEnemy(enemy);
            _engine.ForceMovePlayerTo(player, enemy.position);

            gridRenderer.RenderGrid();
            gridRenderer.UpdatePlayerPosition();
            gridRenderer.UpdateUI();
            
            UpdateStatusText($"Player {player.ID} destroyed the enemy! 💪");
        }
        else if(playerPower < enemyPower)
        {
            player.health--;
            
            if (player.IsAlive)
            {
                _engine.ResetPlayerToSpawn(player);
                UpdateStatusText($"Player {player.ID} lost! Sent to spawn. HP: {player.health}");
            }
            else
            {
                _engine.Grid[player.position.x, player.position.y] = TileType.Empty;
                UpdateStatusText($"Player {player.ID} is dead! 💀");
            }

            gridRenderer.RenderGrid();
            gridRenderer.UpdatePlayerPosition();
            gridRenderer.UpdateUI();
        }
        else
        {
            UpdateStatusText($"Tie! No one wins. Roll again!");
        }
    }

    public void SaveStateForUndo()
    {
        if (_undoStack.Count >= maxUndoSteps)
            _undoStack.Pop(); // Remove oldest if at limit
        
        GridEngine snapshot = _engine.GetSnapShot();
        _undoStack.Push(snapshot);
        
        Debug.Log($"State saved. Undo stack size: {_undoStack.Count}");
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            Debug.Log("Nothing to undo!");
            UpdateStatusText("Nothing to undo!");
            return;
        }
        
        // Restore state
        _engine = _undoStack.Pop();
        
        // Update GridRenderer with new engine
        gridRenderer.SetEngine(_engine);
        
        // Re-render everything
        gridRenderer.RenderGrid();
        gridRenderer.UpdateUI();
        
        // Reset game state
        _currentState = GameState.PlayerTurn;
        
        UpdateStatusText("Undo successful!");
        Debug.Log("Undo successful!");
    }

    public GridEngine GetEngine() => _engine;
    public GameState GetCurrentState() => _currentState;
    public bool IsEnemyTurn() => _isEnemyTurn;
}
