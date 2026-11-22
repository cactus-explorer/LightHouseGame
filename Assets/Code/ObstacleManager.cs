using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Keeps track of which hexes have obstacles and places the rocks at the start of the level.
/// </summary>
public class ObstacleManager : MonoBehaviour
{
    [SerializeField] private HexGridSystem gridSystem;

    [Header("Rocks")]
    [Tooltip("Hand-placed rocks (axial q, r). Turn on 'Enable Click Select' on the grid to find coords.")]
    [SerializeField] private List<Vector2Int> rockCoords = new List<Vector2Int>();
    [Tooltip("Extra rocks placed at random on top of the hand-placed ones.")]
    [SerializeField] private int randomRockCount = 6;
    [Tooltip("0 = different layout every play. Any other number = the same layout every time.")]
    [SerializeField] private int randomSeed = 0;
    [Tooltip("Rocks stay at least this many columns away from the spawn edge.")]
    [SerializeField] private int spawnSafeColumns = 2;
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private Color placeholderRockColor = new Color(0.35f, 0.33f, 0.3f);

    private readonly Dictionary<Vector2Int, Obstacle> obstacles = new Dictionary<Vector2Int, Obstacle>();

    void Start()
    {
        if (gridSystem == null) gridSystem = FindAnyObjectByType<HexGridSystem>();
        if (gridSystem == null)
        {
            Debug.LogError("ObstacleManager: no HexGridSystem in the scene.");
            return;
        }
        PlaceRocks();
    }

    public bool IsBlocked(Vector2Int coord) =>
        obstacles.TryGetValue(coord, out Obstacle o) && o != null && o.BlocksShips;

    public bool HasObstacle(Vector2Int coord) => obstacles.ContainsKey(coord);

    /// <summary>Called by ShipManager at the start of each turn.</summary>
    public void AdvanceTurn()
    {
        // Copy the list: an obstacle may move or remove itself.
        foreach (var o in obstacles.Values.ToList())
        {
            if (o != null) o.OnTurnAdvanced(this);
        }
    }

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

    private void PlaceRocks()
    {
        foreach (var c in rockCoords)
        {
            if (gridSystem.HasHex(c) && !obstacles.ContainsKey(c)) SpawnRock(c);
        }

        // Keep the first few columns clear so ships have room to react after spawning.
        var unsafeHexes = new HashSet<Vector2Int>();
        foreach (var edge in gridSystem.GetLeftEdgeHexes())
        {
            for (int i = 0; i < spawnSafeColumns; i++) unsafeHexes.Add(edge + HexGridSystem.East * i);
        }

        var candidates = gridSystem.GetAllHexCoords()
            .Where(c => !unsafeHexes.Contains(c) && !obstacles.ContainsKey(c))
            .ToList();

        System.Random rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
        for (int i = 0; i < randomRockCount && candidates.Count > 0; i++)
        {
            int pick = rng.Next(candidates.Count);
            SpawnRock(candidates[pick]);
            candidates.RemoveAt(pick);
        }
    }

    private void SpawnRock(Vector2Int coord)
    {
        GameObject rockObj;
        if (rockPrefab != null)
        {
            rockObj = Instantiate(rockPrefab, transform);
        }
        else
        {
            rockObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rockObj.transform.SetParent(transform);
            rockObj.transform.localScale = new Vector3(0.9f, 0.25f, 0.9f);
            Destroy(rockObj.GetComponent<Collider>());
            var rend = rockObj.GetComponent<Renderer>();
            if (rend != null) rend.material.color = placeholderRockColor;
        }

        rockObj.name = $"Rock_{coord.x}_{coord.y}";
        Obstacle obstacle = rockObj.GetComponent<Obstacle>();
        if (obstacle == null) obstacle = rockObj.AddComponent<Obstacle>();
        Register(obstacle, coord);
    }
}
