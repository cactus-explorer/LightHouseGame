using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

public class ShipSpawner : MonoBehaviour
{
    [Header("Ship Prefabs (6 Slots: GalleonA, GalleonB, SloopA, SloopB, DinghyA, DinghyB)")]
    [SerializeField] private GameObject[] shipPrefabs = new GameObject[6];

    [Header("Spawn Settings")]
    [SerializeField] private int advancesPerSpawn = 5;
    [SerializeField] private Color fallbackShipColor = Color.cyan;
    [SerializeField] private bool pressEnterToAdvance = true;

    private HexGridSystem gridSystem;
    private List<ShipInstance> activeShips = new List<ShipInstance>();
    private int advanceCounter = 0;
    private List<Vector2Int> farLeftHexes = new List<Vector2Int>();

    private class ShipInstance
    {
        public GameObject shipObject;
        public Vector2Int axialCoord;
        public string shipType;
        public int pendingVerticalDelta; // Queue vertical move for next Advance()
        public GameObject uiCanvasObj;
    }

    void Start()
    {
        StartCoroutine(InitializeRoutine());
    }

    private System.Collections.IEnumerator InitializeRoutine()
    {
        // Wait until HexGridSystem is found and has generated its grid
        while (gridSystem == null)
        {
            gridSystem = FindAnyObjectByType<HexGridSystem>();
            yield return null;
        }

        // Wait one frame to ensure HexGridSystem's Start() has fully executed
        yield return null;

        InitializeSpawner();
    }

    void Update()
    {
        if (gridSystem == null)
        {
            gridSystem = FindAnyObjectByType<HexGridSystem>();
            if (gridSystem != null)
            {
                InitializeSpawner();
            }
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Check Enter key for advancing
        if (pressEnterToAdvance && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            Advance();
        }
    }

    private void InitializeSpawner()
    {
        CacheFarLeftHexes();
        if (activeShips.Count == 0 && farLeftHexes.Count > 0)
        {
            SpawnInitialShip();
        }
    }

    private void CacheFarLeftHexes()
    {
        farLeftHexes.Clear();
        if (gridSystem == null) return;

        foreach (var coord in gridSystem.GetAllHexCoords())
        {
            // A hex is on the far left edge if its left-neighbor (q - 1) does not exist in the grid
            Vector2Int leftNeighbor = new Vector2Int(coord.x - 1, coord.y);
            if (!gridSystem.HasHex(leftNeighbor))
            {
                farLeftHexes.Add(coord);
            }
        }
    }

    private void SpawnInitialShip()
    {
        SpawnShipAtAvailableFarLeft();
    }

    public void Advance()
    {
        // 1. Move all existing ships right and apply pending vertical movement
        List<ShipInstance> shipsToRemove = new List<ShipInstance>();

        foreach (var ship in activeShips)
        {
            // Apply queued vertical delta combined with rightward step (+1, 0)
            int targetQ = ship.axialCoord.x + 1;
            int targetR = ship.axialCoord.y + ship.pendingVerticalDelta;

            // Reset pending vertical delta now that it's consumed
            ship.pendingVerticalDelta = 0;

            Vector2Int targetCoord = new Vector2Int(targetQ, targetR);

            if (gridSystem.HasHex(targetCoord))
            {
                ship.axialCoord = targetCoord;
                UpdateShipTransform(ship);
            }
            else
            {
                // Ship sailed off the right edge of the grid
                if (ship.shipObject != null) Destroy(ship.shipObject);
                if (ship.uiCanvasObj != null) Destroy(ship.uiCanvasObj);
                shipsToRemove.Add(ship);
            }
        }

        foreach (var ship in shipsToRemove)
        {
            activeShips.Remove(ship);
        }

        // 2. Increment advance counter and spawn new ship every N advances
        advanceCounter++;
        if (advanceCounter >= advancesPerSpawn)
        {
            advanceCounter = 0;
            SpawnShipAtAvailableFarLeft();
        }
    }

    private void SpawnShipAtAvailableFarLeft()
    {
        if (farLeftHexes.Count == 0) return;

        // Find far left hexes not currently occupied by a ship
        List<Vector2Int> availableHexes = farLeftHexes
        .Where(h => !activeShips.Any(s => s.axialCoord == h))
        .ToList();

        if (availableHexes.Count == 0)
        {
            availableHexes = farLeftHexes; // Fallback to any far-left hex if full
        }

        Vector2Int spawnCoord = availableHexes[Random.Range(0, availableHexes.Count)];
        Vector3 worldPos = gridSystem.GetHexWorldPosition(spawnCoord);

        string shipType;
        GameObject shipObj = CreateShipObject(worldPos, out shipType);

        // Determine up/down keys for this ship type to display on UI
        string upKey = GetUpKeyForType(shipType);
        string downKey = GetDownKeyForType(shipType);
        GameObject uiCanvas = CreateShipKeyUI(shipObj.transform, upKey, downKey);

        ShipInstance newShip = new ShipInstance
        {
            shipObject = shipObj,
            axialCoord = spawnCoord,
            shipType = shipType,
            pendingVerticalDelta = 0,
            uiCanvasObj = uiCanvas
        };

        activeShips.Add(newShip);
        UpdateShipTransform(newShip);
    }

    private string GetUpKeyForType(string typeName)
    {
        if (typeName.Contains("DinghyA")) return "A";
        if (typeName.Contains("DinghyB")) return "Space";
        if (typeName.Contains("SloopA")) return "F";
        if (typeName.Contains("SloopB")) return "Q / S";
        if (typeName.Contains("GalleonA")) return "A";
        if (typeName.Contains("GalleonB")) return "D";
        return "Up";
    }

    private string GetDownKeyForType(string typeName)
    {
        if (typeName.Contains("DinghyA")) return "S";
        if (typeName.Contains("DinghyB")) return "D";
        if (typeName.Contains("SloopA")) return "Space";
        if (typeName.Contains("SloopB")) return "D";
        if (typeName.Contains("GalleonA")) return "F";
        if (typeName.Contains("GalleonB")) return "Space";
        return "Down";
    }

    private void UpdateShipTransform(ShipInstance ship)
    {
        if (ship.shipObject != null && gridSystem != null)
        {
            Vector3 targetPos = gridSystem.GetHexWorldPosition(ship.axialCoord);
            targetPos.y += 0.2f; // Slight elevation above hex
            ship.shipObject.transform.position = targetPos;
        }

        if (ship.uiCanvasObj != null && ship.shipObject != null)
        {
            ship.uiCanvasObj.transform.position = ship.shipObject.transform.position + Vector3.up * 0.8f;
        }
    }

    public void MoveShipTypeUp(string shipType)
    {
        foreach (var ship in activeShips)
        {
            if (ship.shipType.Equals(shipType, System.StringComparison.OrdinalIgnoreCase))
            {
                ship.pendingVerticalDelta = 1; // Queue move up-right (+r)
            }
        }
    }

    public void MoveShipTypeDown(string shipType)
    {
        foreach (var ship in activeShips)
        {
            if (ship.shipType.Equals(shipType, System.StringComparison.OrdinalIgnoreCase))
            {
                ship.pendingVerticalDelta = -1; // Queue move down-right (-r)
            }
        }
    }

    private GameObject CreateShipKeyUI(Transform parent, string upKey, string downKey)
    {
        GameObject canvasObj = new GameObject("ShipKeyUI");
        canvasObj.transform.SetParent(parent, false);
        canvasObj.transform.localPosition = new Vector3(0, 1.2f, 0); // Raised higher above ship

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<BillboardConstraint>();

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 100);
        rect.localScale = new Vector3(0.015f, 0.015f, 0.015f); // Increased scale for better readability

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);

        TMPro.TextMeshProUGUI tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = $"▲ {upKey}\n▼ {downKey}";
        tmp.fontSize = 24; // Larger font size
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.yellow;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200, 100);
        textRect.anchoredPosition = Vector2.zero;

        return canvasObj;
    }

    private GameObject CreateShipObject(Vector3 position, out string shipType)
    {
        // Filter out any null slots in the 6-element prefab array
        List<GameObject> validPrefabs = shipPrefabs != null
        ? shipPrefabs.Where(p => p != null).ToList()
        : new List<GameObject>();

        if (validPrefabs.Count > 0)
        {
            GameObject chosenPrefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
            shipType = chosenPrefab.name;
            return Instantiate(chosenPrefab, position, Quaternion.Euler(0, 90, 0), transform);
        }

        // Fallback procedural ship visual if no prefab is assigned
        shipType = "DinghyA";
        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = shipType;
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

/// <summary>
/// Simple billboard component to keep world-space arrow indicator canvases facing the camera.
/// </summary>
public class BillboardConstraint : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // Rotate canvas to face the camera directly
        transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                         cam.transform.rotation * Vector3.up);
    }
}
