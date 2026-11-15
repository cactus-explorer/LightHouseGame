using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

public class ShipSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HexGridSystem hexGrid;
    [SerializeField] private GameObject shipPrefab;

    [Header("Settings")]
    [SerializeField] private int spawnInterval = 5;
    [SerializeField] private float shipHeightOffset = 0.2f;
    [SerializeField] private Color fallbackShipColor = Color.cyan;
    [SerializeField] private bool pressEnterToAdvance = true;

    private class ShipInstance
    {
        public GameObject gameObject;
        public Vector2Int coord;
    }

    private readonly List<ShipInstance> activeShips = new List<ShipInstance>();
    private List<Vector2Int> farLeftHexes = new List<Vector2Int>();
    private int advanceCount = 0;
    private bool isInitialized = false;
    private int pendingVerticalDelta = 0;

    void Start()
    {
        if (hexGrid == null)
        {
            hexGrid = FindAnyObjectByType<HexGridSystem>();
        }

        if (hexGrid == null)
        {
            Debug.LogError("ShipSpawner: No HexGridSystem found in the scene.");
            return;
        }

        TryInitializeSpawner();
    }

    void Update()
    {
        // Ensure initialization runs once HexGridSystem has populated its hexes
        if (!isInitialized)
        {
            TryInitializeSpawner();
        }

        // Optional debug input to test Advance() in Play Mode
        if (pressEnterToAdvance && Keyboard.current != null && 
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
        {
            Advance();
        }
    }

    private void TryInitializeSpawner()
    {
        if (hexGrid == null) return;

        CacheFarLeftHexes();
        if (farLeftHexes.Count > 0)
        {
            SpawnShipOnFarLeft();
            isInitialized = true;
        }
    }

    private void CacheFarLeftHexes()
    {
        farLeftHexes.Clear();
        List<Vector2Int> allCoords = hexGrid.GetAllHexCoords().ToList();

        if (allCoords.Count == 0) return;

        // A hex is on the "far left" if there is no adjacent hex directly to its left (-1, 0)
        foreach (Vector2Int coord in allCoords)
        {
            Vector2Int leftNeighbor = new Vector2Int(coord.x - 1, coord.y);
            if (!hexGrid.HasHex(leftNeighbor))
            {
                farLeftHexes.Add(coord);
            }
        }
    }

    /// <summary>
    /// Moves all active ships one hex to the right (+q) and spawns a new ship every 5 advances.
    /// </summary>
    public void Advance()
    {
        if (hexGrid == null) return;

        // Re-cache if Start() order caused this to be empty on first run
        if (farLeftHexes.Count == 0)
        {
            CacheFarLeftHexes();
        }

        // 1. Move existing ships one hex to the right (+1, 0 in pointy-top axial coordinates)
        //    plus any queued vertical movement from MoveShipsUp() / MoveShipsDown()
        int verticalDelta = pendingVerticalDelta;
        pendingVerticalDelta = 0;

        for (int i = activeShips.Count - 1; i >= 0; i--)
        {
            ShipInstance ship = activeShips[i];
            Vector2Int nextCoord;

            if (verticalDelta > 0)
            {
                // Up-right neighbor in pointy-top axial coordinates is (0, +1)
                nextCoord = new Vector2Int(ship.coord.x, ship.coord.y + 1);
                if (!hexGrid.HasHex(nextCoord))
                {
                    // Fallback to straight right if blocked at the top edge
                    nextCoord = new Vector2Int(ship.coord.x + 1, ship.coord.y);
                }
            }
            else if (verticalDelta < 0)
            {
                // Down-right neighbor in pointy-top axial coordinates is (+1, -1)
                nextCoord = new Vector2Int(ship.coord.x + 1, ship.coord.y - 1);
                if (!hexGrid.HasHex(nextCoord))
                {
                    // Fallback to straight right if blocked at the bottom edge
                    nextCoord = new Vector2Int(ship.coord.x + 1, ship.coord.y);
                }
            }
            else
            {
                // Straight right neighbor is (+1, 0)
                nextCoord = new Vector2Int(ship.coord.x + 1, ship.coord.y);
            }

            if (hexGrid.HasHex(nextCoord))
            {
                ship.coord = nextCoord;
                ship.gameObject.transform.position = hexGrid.GetHexWorldPosition(nextCoord) + Vector3.up * shipHeightOffset;
            }
            else
            {
                // Ship has sailed past the right edge of the grid
                Destroy(ship.gameObject);
                activeShips.RemoveAt(i);
            }
        }

        // 2. Increment advance counter and spawn a new ship every 5 advances
        advanceCount++;
        if (advanceCount % spawnInterval == 0)
        {
            SpawnShipOnFarLeft();
        }
    }

    /// <summary>
    /// Queues all active ships to move one hex upward (+r in axial coordinates) on the next Advance().
    /// </summary>
    public void MoveShipsUp()
    {
        pendingVerticalDelta = 1;
    }

    /// <summary>
    /// Queues all active ships to move one hex downward (-r in axial coordinates) on the next Advance().
    /// </summary>
    public void MoveShipsDown()
    {
        pendingVerticalDelta = -1;
    }

    private void SpawnShipOnFarLeft()
    {
        if (farLeftHexes.Count == 0) return;

        // Prefer spawning on an unoccupied far-left hex if available
        List<Vector2Int> availableSpawns = farLeftHexes
            .Where(c => !activeShips.Any(s => s.coord == c))
            .ToList();

        List<Vector2Int> spawnPool = availableSpawns.Count > 0 ? availableSpawns : farLeftHexes;
        Vector2Int spawnCoord = spawnPool[Random.Range(0, spawnPool.Count)];

        Vector3 worldPos = hexGrid.GetHexWorldPosition(spawnCoord) + Vector3.up * shipHeightOffset;
        GameObject shipObj = CreateShipObject(worldPos);

        activeShips.Add(new ShipInstance
        {
            gameObject = shipObj,
            coord = spawnCoord
        });
    }

    private GameObject CreateShipObject(Vector3 position)
    {
        if (shipPrefab != null)
        {
            return Instantiate(shipPrefab, position, Quaternion.Euler(0, 90, 0), transform);
        }

        // Fallback procedural ship visual if no prefab is assigned
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = "Ship";
        fallback.transform.SetParent(transform);
        fallback.transform.position = position;
        fallback.transform.localScale = new Vector3(0.6f, 0.25f, 0.35f);

        Renderer rend = fallback.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.material.color = fallbackShipColor;
        }

        return fallback;
    }
}