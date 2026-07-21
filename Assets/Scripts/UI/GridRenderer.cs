using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GridRenderer : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject playerPrefab;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI stepsText;
    [SerializeField] private TextMeshProUGUI movesText;

    [Header("Containers")]
    [SerializeField] private Transform gridContainer;

    private GridEngine _engine;
    private GameObject[,] _tileObjects;
    private List<PlayerVisual> _playerVisuals = new List<PlayerVisual>();
    // Start is called before the first frame update
    void Start()
    {
        _engine = new GridEngine(7, 7, 30);
        _tileObjects = new GameObject[7, 7];

        var player1 = new PlayerData(1, new Vector2Int(1, 1), Color.blue, "p1");

        _engine.AddPlayer(player1);

        _engine.AddEnemy(new Vector2Int(3, 3));
        _engine.AddEnemy(new Vector2Int(5, 5));
        _engine.AddEnemy(new Vector2Int(3, 5));

        RenderGrid();
        SpawnPlayerVisuals();
        UpdateUI();
    }

    void SpawnPlayerVisuals()
    {
        foreach (var player in _engine.Players)
        {
            GameObject visual = Instantiate(playerPrefab, transform);
            PlayerVisual pv = visual.GetComponent<PlayerVisual>();
            pv.Bind(player);
            visual.transform.position = new Vector3(player.position.x, player.position.y, -1);
            _playerVisuals.Add(pv);
        }
    }

    public void UpdatePlayerPosition()
    {
        for (int i = 0; i < _engine.Players.Count; i++)
        {
            var player = _engine.Players[i];
            var visual = _playerVisuals[i];
            visual.transform.position = new Vector3(player.position.x, player.position.y, -1);
            visual.UpdateVisual();
        }
    }

    public void RenderGrid()
    {
        foreach (Transform child in gridContainer) Destroy(child.gameObject);

        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < 7; j++)
            {
                GameObject tile = Instantiate(tilePrefab, gridContainer);
                tile.transform.position = new Vector3(i, j, 0);

                TextMeshProUGUI text = tile.GetComponentInChildren<TextMeshProUGUI>();

                switch (_engine.Grid[i, j])
                {
                    case TileType.Empty:
                        text.text = "";
                        tile.GetComponent<SpriteRenderer>().color = new Color(0.2f, 0.2f, 0.2f);
                        break;

                    case TileType.Wall:
                        text.text = "■";
                        tile.GetComponent<SpriteRenderer>().color = Color.black;
                        break;

                    case TileType.Player:
                        text.text = "P";
                        tile.GetComponent<SpriteRenderer>().color = Color.blue;
                        break;

                    case TileType.Enemy:
                        text.text = "E";
                        tile.GetComponent<SpriteRenderer>().color = Color.red;
                        break;

                    case TileType.CenterGoal:
                        text.text = "⭐";
                        tile.GetComponent<SpriteRenderer>().color = Color.yellow;
                        break;
                }

                _tileObjects[i, j] = tile;
            }
        }

        UpdatePlayerPosition();
        UpdateUI();
    }

    public void UpdateUI()
    {
        var current = _engine.GetCurrentPlayer();
        if (current == null) return;
        healthText.text = $"P{current.ID} HP: {current.health}";
        stepsText.text = $"Steps: {current.bankedSteps}";
        movesText.text = $"Turns Left: {_engine.GlobalMovesRemaining}";
    }

    public void TestRollDice()
    {
        var current = _engine.GetCurrentPlayer();
        if (current != null)
        {
            int roll = _engine.RollPowerDie();
            current.AddBankedSteps(roll);
            UpdateUI();
        }
    }

    public void SetEngine(GridEngine newEngine)
    {
        _engine = newEngine;
    }

    public GridEngine GetEngine()
    {
        return _engine;
    }

}
