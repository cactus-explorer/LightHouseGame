using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps track of what's on each hex and spawns the level's hazards:
///  - rocks at the start (hand-placed and random)
///  - sea monsters from a set turn
///  - thunderstorms every few turns
/// Also tints hex outlines for storms and the monster's next move.
/// </summary>
public class ObstacleManager : MonoBehaviour
{
    /// <summary>The six axial neighbour directions.</summary>
    public static readonly Vector2Int[] HexDirections =
    {
        new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
    };

    [SerializeField] private HexGridSystem gridSystem;
    [SerializeField] private ShipManager shipManager;

    [Header("Rocks")]
    [Tooltip("Hand-placed rocks (axial q, r). Turn on 'Enable Click Select' on the grid to find coords.")]
    [SerializeField] private List<Vector2Int> rockCoords = new List<Vector2Int>();
    [Tooltip("Extra rocks placed at random on top of the hand-placed ones.")]
    [SerializeField] private int randomRockCount = 6;
    [Tooltip("0 = different layout every play. Any other number = the same layout every time.")]
    [SerializeField] private int randomSeed = 0;
    [Tooltip("Hazards stay at least this many columns away from the spawn edge.")]
    [SerializeField] private int spawnSafeColumns = 2;
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private Color placeholderRockColor = new Color(0.35f, 0.33f, 0.3f);

    [Header("Sea Monster")]
    [SerializeField] private int monsterCount = 1;
    [Tooltip("Turn the first monster appears on. Monsters after it appear one turn apart.")]
    [SerializeField] private int monsterFirstTurn = 6;
    [Tooltip("A new monster never appears within this many hexes of a ship.")]
    [SerializeField] private int monsterMinDistanceFromShips = 3;
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Color placeholderMonsterColor = new Color(0.45f, 0.15f, 0.55f);
    [SerializeField] private Color monsterTelegraphTint = new Color(1f, 0.3f, 0.25f);

    [Header("Thunderstorms")]
    [SerializeField] private bool enableStorms = true;
    [SerializeField] private int stormFirstTurn = 10;
    [SerializeField, Min(1)] private int turnsBetweenStorms = 5;
    [Tooltip("Hexes per storm (min, max).")]
    [SerializeField] private Vector2Int stormSize = new Vector2Int(2, 4);
    [Tooltip("Turns a storm stays dangerous after its warning turn.")]
    [SerializeField] private int stormActiveTurns = 3;
    [Tooltip("Optional cloud model. Its first child is scaled and recoloured.")]
    [SerializeField] private GameObject stormPrefab;

    // Sound / effects hooks. The Vector3 is a world position.
    public event Action<Vector3> OnMonsterAppeared;
    public event Action<Vector3> OnMonsterAttack;
    public event Action<Vector3> OnStormWarning;
    public event Action<Vector3> OnStormStrike;

    public HexGridSystem Grid => gridSystem;
    public bool AnyStormActive => obstacles.Values.OfType<Thunderstorm>().Any(s => s.BlocksShips);

    private readonly Dictionary<Vector2Int, Obstacle> obstacles = new Dictionary<Vector2Int, Obstacle>();
    private readonly HashSet<Vector2Int> unsafeHexes = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> tintedHexes = new HashSet<Vector2Int>();
    private System.Random rng;
    private int turn;
    private int monstersSpawned;
    private bool warnedThisTurn, struckThisTurn;

    void Start()
    {
        if (gridSystem == null) gridSystem = FindAnyObjectByType<HexGridSystem>();
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
        if (gridSystem == null)
        {
            Debug.LogError("ObstacleManager: no HexGridSystem in the scene.");
            return;
        }

        rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

        // Keep the first few columns clear so ships have room to react after spawning.
        foreach (var edge in gridSystem.GetLeftEdgeHexes())
        {
            for (int i = 0; i < spawnSafeColumns; i++) unsafeHexes.Add(edge + HexGridSystem.East * i);
        }

        PlaceRocks();
        RefreshTints();
    }

    /// <summary>Use a level's hazard settings. Called by LevelLoader before Start.</summary>
    public void ApplyLevel(LevelData level)
    {
        randomRockCount = level.randomRockCount;
        rockCoords = new List<Vector2Int>(level.rockCoords ?? new List<Vector2Int>());
        randomSeed = level.randomSeed;
        monsterCount = level.monsterCount;
        monsterFirstTurn = level.monsterFirstTurn;
        enableStorms = level.storms;
        stormFirstTurn = level.stormFirstTurn;
        turnsBetweenStorms = Mathf.Max(1, level.turnsBetweenStorms);
        stormSize = level.stormSize;
        stormActiveTurns = level.stormActiveTurns;
    }

    // ------------------------------------------------------------------ Queries

    public bool IsBlocked(Vector2Int coord) =>
        obstacles.TryGetValue(coord, out Obstacle o) && o != null && o.BlocksShips;

    public bool HasObstacle(Vector2Int coord) => obstacles.ContainsKey(coord);

    public bool IsSpawnSafe(Vector2Int coord) => unsafeHexes.Contains(coord);

    /// <summary>True if some obstacle has marked this hex for next turn (e.g. a monster's next move).</summary>
    public bool IsTelegraphed(Vector2Int coord) =>
        obstacles.Values.Any(o => o != null && o.TelegraphHex.HasValue && o.TelegraphHex.Value == coord);

    public bool IsShipAt(Vector2Int coord) => shipManager != null && shipManager.IsShipAt(coord);

    public bool TryGetNearestShipDistance(Vector2Int from, out int distance)
    {
        distance = int.MaxValue;
        if (shipManager == null) return false;
        foreach (var c in shipManager.ShipCoords) distance = Mathf.Min(distance, HexDistance(from, c));
        return distance != int.MaxValue;
    }

    public static int HexDistance(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x, dr = a.y - b.y;
        return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2;
    }

    // ------------------------------------------------------------------ Turn

    /// <summary>Called by ShipManager at the start of each turn, before ships move.</summary>
    public void AdvanceTurn()
    {
        turn++;
        warnedThisTurn = struckThisTurn = false;

        // Copy the list: obstacles may move or remove themselves.
        foreach (var o in obstacles.Values.ToList())
        {
            if (o != null) o.OnTurnAdvanced(this);
        }

        if (monstersSpawned < monsterCount && turn >= monsterFirstTurn + monstersSpawned) SpawnMonster();

        if (enableStorms && turn >= stormFirstTurn && (turn - stormFirstTurn) % turnsBetweenStorms == 0) SpawnStorm();

        RefreshTints();
    }

    // ------------------------------------------------------------------ Called by obstacles

    public void Register(Obstacle obstacle, Vector2Int coord)
    {
        obstacle.Coord = coord;
        obstacles[coord] = obstacle;
        obstacle.transform.position = gridSystem.GetHexWorldPosition(coord);
    }

    public void Move(Obstacle obstacle, Vector2Int to)
    {
        if (obstacles.TryGetValue(obstacle.Coord, out Obstacle current) && current == obstacle)
            obstacles.Remove(obstacle.Coord);
        Register(obstacle, to);
    }

    public void Remove(Obstacle obstacle)
    {
        if (obstacles.TryGetValue(obstacle.Coord, out Obstacle current) && current == obstacle)
            obstacles.Remove(obstacle.Coord);
    }

    public void MonsterAttack(SeaMonster monster, Vector2Int target)
    {
        if (shipManager != null) shipManager.DamageShipAt(target);
        OnMonsterAttack?.Invoke(gridSystem.GetHexWorldPosition(target));
    }

    public void ReportStormWarning(Thunderstorm storm)
    {
        if (warnedThisTurn) return; // one sound per storm, not per hex
        warnedThisTurn = true;
        OnStormWarning?.Invoke(storm.transform.position);
    }

    public void StormStrikes(Thunderstorm storm)
    {
        if (shipManager != null) shipManager.DamageShipAt(storm.Coord);
        if (struckThisTurn) return;
        struckThisTurn = true;
        OnStormStrike?.Invoke(storm.transform.position);
    }

    // ------------------------------------------------------------------ Spawning

    private void PlaceRocks()
    {
        foreach (var c in rockCoords)
        {
            if (gridSystem.HasHex(c) && !obstacles.ContainsKey(c)) SpawnRock(c);
        }

        var candidates = gridSystem.GetAllHexCoords()
            .Where(c => !unsafeHexes.Contains(c) && !obstacles.ContainsKey(c))
            .ToList();

        for (int i = 0; i < randomRockCount && candidates.Count > 0; i++)
        {
            int pick = rng.Next(candidates.Count);
            SpawnRock(candidates[pick]);
            candidates.RemoveAt(pick);
        }
    }

    private void SpawnRock(Vector2Int coord)
    {
        GameObject rockObj = rockPrefab != null
            ? Instantiate(rockPrefab, transform)
            : Placeholder(PrimitiveType.Cylinder, new Vector3(0.9f, 0.25f, 0.9f), Vector3.zero, placeholderRockColor);

        rockObj.name = $"Rock_{coord.x}_{coord.y}";
        Obstacle obstacle = rockObj.GetComponent<Obstacle>();
        if (obstacle == null) obstacle = rockObj.AddComponent<Obstacle>();
        Register(obstacle, coord);
        obstacle.OnSpawned(this);
    }

    private void SpawnMonster()
    {
        var candidates = FreeHexes()
            .Where(c => !TryGetNearestShipDistance(c, out int d) || d >= monsterMinDistanceFromShips)
            .ToList();
        if (candidates.Count == 0) return; // try again next turn

        Vector2Int coord = candidates[rng.Next(candidates.Count)];
        GameObject obj;
        if (monsterPrefab != null)
        {
            obj = Instantiate(monsterPrefab, transform);
        }
        else
        {
            obj = new GameObject();
            obj.transform.SetParent(transform, false);
            var body = Placeholder(PrimitiveType.Capsule, new Vector3(0.5f, 0.35f, 0.5f), new Vector3(0, 0.2f, 0), placeholderMonsterColor);
            body.transform.SetParent(obj.transform, false);
        }

        obj.name = "SeaMonster";
        var monster = obj.GetComponent<SeaMonster>();
        if (monster == null) monster = obj.AddComponent<SeaMonster>();
        Register(monster, coord);
        monster.OnSpawned(this);
        monstersSpawned++;
        OnMonsterAppeared?.Invoke(obj.transform.position);
    }

    private void SpawnStorm()
    {
        var free = new HashSet<Vector2Int>(FreeHexes());
        if (free.Count == 0) return;

        int size = rng.Next(Mathf.Max(1, stormSize.x), Mathf.Max(stormSize.x, stormSize.y) + 1);
        var cells = new List<Vector2Int> { free.ElementAt(rng.Next(free.Count)) };
        free.Remove(cells[0]);

        // Grow the storm outward from its first hex.
        while (cells.Count < size)
        {
            var frontier = cells.SelectMany(c => HexDirections.Select(d => c + d)).Where(free.Contains).Distinct().ToList();
            if (frontier.Count == 0) break;
            var next = frontier[rng.Next(frontier.Count)];
            cells.Add(next);
            free.Remove(next);
        }

        foreach (var c in cells)
        {
            GameObject obj;
            if (stormPrefab != null)
            {
                obj = Instantiate(stormPrefab, transform);
            }
            else
            {
                obj = new GameObject();
                obj.transform.SetParent(transform, false);
                var cloud = Placeholder(PrimitiveType.Sphere, new Vector3(1.2f, 0.4f, 1.2f), new Vector3(0, 1.3f, 0), Color.grey);
                cloud.transform.SetParent(obj.transform, false);
            }

            obj.name = $"Storm_{c.x}_{c.y}";
            var storm = obj.GetComponent<Thunderstorm>();
            if (storm == null) storm = obj.AddComponent<Thunderstorm>();
            storm.activeTurns = stormActiveTurns;
            Register(storm, c);
            storm.OnSpawned(this);
        }
    }

    /// <summary>Hexes with no obstacle, not marked for next turn, and outside the spawn-safe columns.</summary>
    private IEnumerable<Vector2Int> FreeHexes() =>
        gridSystem.GetAllHexCoords().Where(c => !unsafeHexes.Contains(c) && !obstacles.ContainsKey(c) && !IsTelegraphed(c));

    private GameObject Placeholder(PrimitiveType type, Vector3 scale, Vector3 offset, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.transform.SetParent(transform);
        obj.transform.localScale = scale;
        obj.transform.localPosition = offset;
        Destroy(obj.GetComponent<Collider>());
        var rend = obj.GetComponent<Renderer>();
        if (rend != null) rend.material.color = color;
        return obj;
    }

    // ------------------------------------------------------------------ Hex tints

    private void RefreshTints()
    {
        foreach (var c in tintedHexes) gridSystem.ResetHexColor(c);
        tintedHexes.Clear();

        foreach (var o in obstacles.Values)
        {
            if (o == null) continue;
            if (o.HexTint.HasValue)
            {
                gridSystem.SetHexColor(o.Coord, o.HexTint.Value);
                tintedHexes.Add(o.Coord);
            }
            if (o.TelegraphHex.HasValue)
            {
                gridSystem.SetHexColor(o.TelegraphHex.Value, monsterTelegraphTint);
                tintedHexes.Add(o.TelegraphHex.Value);
            }
        }
    }
}
