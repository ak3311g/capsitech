# Unity Grid Game — System Architecture & Code Flow

## 1. System Architecture Map (Current State)

This reflects the actual wiring in your scripts today — including the coupling that currently exists between state and rendering.

```mermaid
graph TD
    subgraph INPUT["Input Layer"]
        IM[InputManager<br/>MonoBehaviour]
    end

    subgraph ORCH["Orchestration Layer"]
        GM[GameManager<br/>MonoBehaviour · Singleton]
    end

    subgraph STATE["Core State Layer (plain C#)"]
        GE[GridEngine<br/>Grid, Players, Enemies,<br/>movement & combat rules]
        PD[PlayerData]
        ED[EnemyData]
    end

    subgraph RENDER["Rendering / View Layer"]
        GR[GridRenderer<br/>MonoBehaviour]
        PV[PlayerVisual<br/>MonoBehaviour]
        UI[TMP Text: health/steps/moves]
        PANELS[Win/Lose Panels]
    end

    IM -->|"direct method call<br/>OnPlayerMove(dir)"| GM
    GM -->|"gridRenderer.GetEngine()<br/>on Start()"| GR
    GM -->|"mutates via"| GE
    GE --> PD
    GE --> ED
    GM -->|"RenderGrid() / UpdatePlayerPosition()<br/>UpdateUI() — called inline"| GR
    GM -->|"SetEngine() on Undo"| GR
    GR -->|"owns & instantiates on Awake()"| GE
    GR --> PV
    GR --> UI
    PV --> PD
    GM --> PANELS

    style GE fill:#2d5,color:#000
    style PD fill:#2d5,color:#000
    style ED fill:#2d5,color:#000
    style GM fill:#e8a,color:#000
    style IM fill:#e8a,color:#000
    style GR fill:#7ad,color:#000
    style PV fill:#7ad,color:#000
```

### Coupling issues this reveals

1. **State is instantiated by the view.** `GridRenderer.Awake()` does `_engine = new GridEngine(...)` and seeds players/enemies. `GameManager.Start()` then pulls it back out with `gridRenderer.GetEngine()`. The rendering component is currently the *owner* of game state — backwards from a clean architecture where a state/data layer is created independently and handed to both logic and view.
2. **GameManager directly drives rendering.** Nearly every state-changing method (`OnPlayerMove`, `ResolveCombat`, `EnemyTurnRoutine`, `Undo`) ends with explicit calls like `gridRenderer.RenderGrid()`, `UpdatePlayerPosition()`, `UpdateUI()`. There's no event/observer boundary — `GameManager` has a hard compile-time reference to `GridRenderer` and knows its render API.
3. **InputManager calls GameManager directly.** `DetectSwipe()` ends with `gameManager.OnPlayerMove(dir)` — a direct MonoBehaviour-to-MonoBehaviour reference rather than an event (`UnityEvent<Direction>` or a C# `Action<Direction>`) that `GameManager` subscribes to. This means `InputManager` cannot be reused or tested without a live `GameManager`.
4. **Undo re-parents state across the view.** `GameManager.Undo()` pops a `GridEngine` snapshot and calls `gridRenderer.SetEngine(_engine)` — state is being handed *through* the renderer again rather than the renderer simply re-reading from a single shared state owner.
5. **No interfaces.** `GameManager`, `InputManager`, and `GridRenderer` all reference each other's concrete classes. There's no `IInputSource`, `IGameView`, or `IGameState` seam, so you can't swap rendering (e.g., 3D board vs. 2D board) or input scheme (touch vs. AI-driven testing) without editing `GameManager` itself.

### Target (decoupled) shape

```mermaid
graph TD
    IM2[InputManager] -->|"OnSwipe event<br/>Action<Direction>"| GM2[GameManager]
    GM2 -->|"owns"| GE2[GridEngine]
    GE2 -->|"OnStateChanged event"| GM2
    GM2 -->|"OnStateChanged event"| GR2[GridRenderer / PlayerVisual]
    GR2 -.->|"read-only queries<br/>e.g. Grid, Players"| GE2

    style GE2 fill:#2d5,color:#000
    style GM2 fill:#e8a,color:#000
    style IM2 fill:#e8a,color:#000
    style GR2 fill:#7ad,color:#000
```
- `GameManager` (not `GridRenderer`) constructs `GridEngine` and owns the single source of truth.
- `GridEngine` raises a lightweight event (or `GameManager` raises one after mutating it) that `GridRenderer` subscribes to — rendering never gets an explicit method call telling it *what* changed, just *that* something changed, and reads current state itself.
- `InputManager` exposes an event; it has no reference to `GameManager` at all.

---

## 2. Functional Code Flow — Full Lifecycle

Traced through your actual method calls, from swipe to redraw.

```mermaid
sequenceDiagram
    actor Player
    participant IM as InputManager
    participant GM as GameManager
    participant GE as GridEngine
    participant PD as PlayerData
    participant GR as GridRenderer
    participant PV as PlayerVisual

    Player->>IM: Touch/Mouse swipe
    IM->>IM: DetectSwipe()<br/>compute distance, time, Direction
    IM->>GM: OnPlayerMove(dir)

    GM->>GM: check _currentState == PlayerTurn
    GM->>GM: current = _engine.GetCurrentPlayer()
    GM->>PD: check bankedSteps > 0
    GM->>GM: SaveStateForUndo()<br/>(pushes GridEngine snapshot)

    GM->>GE: MovePlayerStep(current, dir)
    GE->>GE: compute target cell
    GE->>GE: validate bounds / wall / occupancy
    GE->>GE: Grid[old] = Empty<br/>Grid[target] = Player
    GE->>PD: SpendBankedStep()
    GE->>GE: GlobalMovesRemaining--
    GE-->>GM: return moved (bool)

    alt moved == true
        GM->>GR: RenderGrid()
        GR->>GR: destroy & re-instantiate all tiles<br/>from Grid[,]
        GM->>GR: UpdatePlayerPosition()
        GR->>PV: UpdateVisual() per player
        GM->>GR: UpdateUI()
        GR->>GR: refresh health/steps/moves text

        GM->>GE: CheckPlayerEnemyCollision()
        opt collision found
            GM->>GM: ResolveCombat(player, enemy)
            GM->>GE: RollPowerDie() x2, RemoveEnemy / ResetPlayerToSpawn
            GM->>GR: RenderGrid(), UpdatePlayerPosition(), UpdateUI()
        end

        GM->>GE: CheckWin()
        alt win
            GM->>GM: OnGameWin() → show winPanel
        else steps <= 0
            GM->>GM: StartCoroutine(DelayedEndTurn)
            GM->>GM: EndTurn() → EnemyTurnRoutine()
        end
    else moved == false
        GM->>GM: UpdateStatusText("Move Blocked")
    end
```

### Where the pipeline breaks the stated ideal

The requested pipeline is **User Gesture → Input Controller → Grid Matrix Update → UI Rendering Framework** — a clean one-directional pipe. In the current code:

- `GameManager` sits *between* input and the grid matrix, which is correct, but it also reaches *past* the grid matrix and calls rendering methods directly (`RenderGrid`, `UpdatePlayerPosition`, `UpdateUI`) after every mutation, rather than the grid matrix update alone triggering a render pass. That means every new state-changing feature you add to `GameManager` (e.g., a new combat rule) requires you to remember to also call the three render methods — nothing enforces that render always follows a state change.
- `RenderGrid()` fully destroys and re-instantiates every tile on **every** move (`foreach (Transform child in gridContainer) Destroy(...)`), rather than diffing which cells changed. This is a rendering-layer efficiency issue riding on top of the coupling issue: because there's no "what changed" event payload, the renderer has no choice but to redraw everything.

If you want, I can sketch the concrete C# event/interface refactor (e.g., `IGameState`, an `OnGridChanged` event) that would close this gap without a full rewrite.
