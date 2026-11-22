using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spawns ships on the left edge, moves them one turn at a time (one turn = one chord),
/// resolves collisions with rocks and other ships, and reports ships saved or wrecked.
///
/// Turn rules:
///  - Every ship moves east each turn. A queued "up" goes north-east, "down" goes south-east.
///  - Steering off the top or bottom of the map is ignored: the ship goes straight instead.
///  - Sailing off the right edge = saved.
///  - Moving into a rock, or into a hex another ship is in or entering = 1 damage, and the ship stays put.
///  - Ships with hexesPerTurn 2 (sloops) take a second, straight step after the first.
/// </summary>
public class ShipManager : MonoBehaviour
{
    [Serializable]
    public class ShipSpawnEntry
    {
        public ShipType type;
        [Tooltip("Leave empty to use a coloured placeholder box.")]
        public GameObject prefab;
        [Tooltip("This type can only spawn after this many turns, so players learn one ship at a time.")]
        public int unlockAfterTurn;
    }

    [Header("References (found automatically if empty)")]
    [SerializeField] private HexGridSystem gridSystem;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private ObstacleManager obstacleManager;

    [Header("Ships")]
    [SerializeField] private ShipSpawnEntry[] spawnTable =
    {
        new ShipSpawnEntry { type = ShipType.DinghyA,  unlockAfterTurn = 0 },
        new ShipSpawnEntry { type = ShipType.DinghyB,  unlockAfterTurn = 3 },
        new ShipSpawnEntry { type = ShipType.SloopA,   unlockAfterTurn = 6 },
        new ShipSpawnEntry { type = ShipType.SloopB,   unlockAfterTurn = 9 },
        new ShipSpawnEntry { type = ShipType.GalleonA, unlockAfterTurn = 12 },
        new ShipSpawnEntry { type = ShipType.GalleonB, unlockAfterTurn = 15 },
    };
    [Tooltip("Extra rotation if your models don't point along +Z.")]
    [SerializeField] private float modelYawOffset = 0f;

    [Header("Spawning")]
    [SerializeField, Min(1)] private int turnsPerSpawn = 2;
    [SerializeField] private int maxShipsOnBoard = 8;

    [Header("Movement")]
    [SerializeField] private float moveDuration = 0.4f;
    [SerializeField] private float shipHeight = 0.2f;

    [Header("Move Preview Arrows")]
    [SerializeField] private Material arrowMaterial;
    [SerializeField] private Color queuedArrowColor = new Color(1f, 0.85f, 0.1f, 0.95f);
    [SerializeField] private Color defaultArrowColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Debug")]
    [Tooltip("Enter advances a turn (editor and development builds only).")]
    [SerializeField] private bool enterAdvancesTurn = true;

    // Events for sound, HUD and GameManager. The Vector3 is the ship's world position.
    public event Action<Vector3> OnShipDamaged;
    public event Action<Vector3> OnShipWrecked;
    public event Action<Vector3> OnShipSaved;
    public event Action<Vector3> OnShipSpawned;
    public event Action<int> OnTurnAdvanced;

    public int SavedCount { get; private set; }
    public int WreckedCount { get; private set; }
    public int TurnNumber { get; private set; }
    public bool IsRunning { get; private set; } = true;
    public int ActiveShipCount => activeShips.Count;

    private class ShipInstance
    {
        public GameObject root;       // unscaled, sits on the hex; label and arrow hang off it
        public Transform visual;      // the model; rotated to face where it's going
        public Ship data;
        public Vector2Int coord;
        public int hp;
        public int pendingDirection;  // +1 up, -1 down, 0 straight
        public TMPro.TextMeshProUGUI label;
        public LineRenderer arrow;
        public readonly List<Vector3> path = new List<Vector3>();
        public bool removed, saved, wrecked;
        public Coroutine moveRoutine;
    }

    private readonly List<ShipInstance> activeShips = new List<ShipInstance>();
    private List<Vector2Int> spawnHexes = new List<Vector2Int>();

    void Start()
    {
        if (gridSystem == null) gridSystem = FindAnyObjectByType<HexGridSystem>();
        if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
        if (obstacleManager == null) obstacleManager = FindAnyObjectByType<ObstacleManager>();

        if (gridSystem == null)
        {
            Debug.LogError("ShipManager: no HexGridSystem in the scene.");
            enabled = false;
            return;
        }

        spawnHexes = gridSystem.GetLeftEdgeHexes();
        if (arrowMaterial == null) arrowMaterial = CreateArrowMaterial();

        if (timeManager != null) timeManager.OnChordChanged += HandleChordChanged;
        else Debug.LogWarning("ShipManager: no TimeManager found; turns only advance with Enter.");

        SpawnShip();
    }

    void OnDestroy()
    {
        if (timeManager != null) timeManager.OnChordChanged -= HandleChordChanged;
    }

    void Update()
    {
        if (!enterAdvancesTurn || !Debug.isDebugBuild) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            AdvanceTurn();
        }
    }

    private void HandleChordChanged(int chordIndex, string chord) => AdvanceTurn();

    /// <summary>Stop or resume turns (GameManager stops them when the level ends).</summary>
    public void SetRunning(bool running)
    {
        IsRunning = running;
        foreach (var s in activeShips)
        {
            if (s.arrow != null) s.arrow.enabled = running;
        }
    }

    // ------------------------------------------------------------------ Input

    /// <summary>Queue a steer for every ship of this type. Last press in a turn wins.</summary>
    public void QueueMove(ShipType type, int direction)
    {
        if (!IsRunning) return;
        foreach (var s in activeShips)
        {
            if (s.removed || s.data.type != type) continue;
            s.pendingDirection = Mathf.Clamp(direction, -1, 1);
            UpdateArrow(s);
        }
    }

    // ------------------------------------------------------------------ Turn

    public void AdvanceTurn()
    {
        if (!IsRunning) return;

        TurnNumber++;
        if (obstacleManager != null) obstacleManager.AdvanceTurn();

        // Snapshot this turn's steering and reset it for next turn.
        var directions = new Dictionary<ShipInstance, int>();
        foreach (var s in activeShips)
        {
            directions[s] = s.pendingDirection;
            s.pendingDirection = 0;
            s.path.Clear();
            s.path.Add(ShipPos(s.coord));
        }

        int maxSteps = activeShips.Count == 0 ? 0 : activeShips.Max(s => s.data.hexesPerTurn);
        for (int step = 0; step < maxSteps; step++)
        {
            var movers = activeShips.Where(s => !s.removed && s.data.hexesPerTurn > step).ToList();
            ResolveStep(movers, step == 0 ? directions : null);
        }

        foreach (var s in activeShips.ToList())
        {
            if (s.removed) activeShips.Remove(s);
            AnimateShip(s);
        }

        if (TurnNumber % turnsPerSpawn == 0 || activeShips.Count == 0) SpawnShip();

        OnTurnAdvanced?.Invoke(TurnNumber);
    }

    private void ResolveStep(List<ShipInstance> movers, Dictionary<ShipInstance, int> directions)
    {
        var targets = new Dictionary<ShipInstance, Vector2Int>();

        // 1. Work out where each ship wants to go.
        foreach (var s in movers)
        {
            int dir = directions != null && directions.TryGetValue(s, out int d) ? d : 0;
            Vector2Int target = s.coord + Offset(dir);

            // Can't steer off the top or bottom: go straight instead.
            if (dir != 0 && !gridSystem.HasHex(target)) target = s.coord + HexGridSystem.East;

            if (!gridSystem.HasHex(target))
            {
                MarkSaved(s);
                continue;
            }
            targets[s] = target;
        }

        // 2. Rocks.
        foreach (var kv in targets.ToList())
        {
            if (obstacleManager != null && obstacleManager.IsBlocked(kv.Value))
            {
                targets.Remove(kv.Key);
                Crash(kv.Key, kv.Value);
            }
        }

        // 3. Other ships: two ships entering one hex, or entering a hex a ship is staying in.
        //    Repeat, because a ship that gets blocked becomes an obstacle for the ship behind it.
        bool changed = true;
        while (changed)
        {
            changed = false;
            var stayingHexes = new HashSet<Vector2Int>(
                activeShips.Where(s => !s.removed && !targets.ContainsKey(s)).Select(s => s.coord));

            var crashed = targets
                .GroupBy(kv => kv.Value)
                .Where(g => g.Count() > 1 || stayingHexes.Contains(g.Key))
                .SelectMany(g => g.Select(kv => kv.Key))
                .ToList();

            foreach (var s in crashed)
            {
                Vector2Int blocked = targets[s];
                targets.Remove(s);
                Crash(s, blocked);
                changed = true;
            }
        }

        // 4. Everyone left moves.
        foreach (var kv in targets)
        {
            kv.Key.coord = kv.Value;
            kv.Key.path.Add(ShipPos(kv.Value));
        }
    }

    private static Vector2Int Offset(int direction)
    {
        if (direction > 0) return HexGridSystem.NorthEast;
        if (direction < 0) return HexGridSystem.SouthEast;
        return HexGridSystem.East;
    }

    private void Crash(ShipInstance s, Vector2Int blockedHex)
    {
        // Bump toward the blocked hex and back, so the player sees what happened.
        Vector3 here = ShipPos(s.coord);
        s.path.Add(Vector3.Lerp(here, ShipPos(blockedHex), 0.35f));
        s.path.Add(here);

        s.hp--;
        OnShipDamaged?.Invoke(here);

        if (s.hp <= 0)
        {
            s.removed = true;
            s.wrecked = true;
            WreckedCount++;
            OnShipWrecked?.Invoke(here);
        }
    }

    private void MarkSaved(ShipInstance s)
    {
        s.removed = true;
        s.saved = true;
        SavedCount++;
        s.path.Add(ShipPos(s.coord + HexGridSystem.East)); // sail off the edge
        OnShipSaved?.Invoke(ShipPos(s.coord));
    }

    private Vector3 ShipPos(Vector2Int coord) =>
        gridSystem.GetWorldPositionUnchecked(coord) + Vector3.up * shipHeight;

    // ------------------------------------------------------------------ Spawning

    private void SpawnShip()
    {
        if (!IsRunning || activeShips.Count >= maxShipsOnBoard) return;

        var occupied = new HashSet<Vector2Int>(activeShips.Select(s => s.coord));
        var free = spawnHexes
            .Where(h => !occupied.Contains(h) && (obstacleManager == null || !obstacleManager.IsBlocked(h)))
            .ToList();
        if (free.Count == 0) return; // every spawn hex is taken: skip rather than stack ships

        var pool = spawnTable.Where(e => e.unlockAfterTurn <= TurnNumber).ToList();
        if (pool.Count == 0) return;

        ShipSpawnEntry entry = pool[UnityEngine.Random.Range(0, pool.Count)];
        Vector2Int coord = free[UnityEngine.Random.Range(0, free.Count)];

        var ship = CreateShip(entry, coord);
        activeShips.Add(ship);
        OnShipSpawned?.Invoke(ship.root.transform.position);
    }

    private ShipInstance CreateShip(ShipSpawnEntry entry, Vector2Int coord)
    {
        var root = new GameObject($"Ship_{entry.type}");
        root.transform.SetParent(transform, false);
        root.transform.position = ShipPos(coord);

        Transform visual;
        Ship data;
        if (entry.prefab != null)
        {
            GameObject model = Instantiate(entry.prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            visual = model.transform;
            data = model.GetComponentInChildren<Ship>();
            if (data == null)
            {
                data = root.AddComponent<Ship>();
                data.ApplyDefaultsFor(entry.type);
            }
            data.type = entry.type; // the spawn table decides the type, the prefab decides HP/speed
        }
        else
        {
            visual = CreatePlaceholderVisual(entry.type, root.transform);
            data = root.AddComponent<Ship>();
            data.ApplyDefaultsFor(entry.type);
        }

        visual.rotation = Heading(Vector3.right);

        var ship = new ShipInstance
        {
            root = root,
            visual = visual,
            data = data,
            coord = coord,
            hp = data.maxHP,
        };
        ship.label = CreateLabel(root.transform);
        ship.arrow = CreateArrow(root.transform);
        UpdateLabel(ship);
        UpdateArrow(ship);
        return ship;
    }

    private Transform CreatePlaceholderVisual(ShipType type, Transform parent)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "PlaceholderModel";
        Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);

        // Long axis along +Z so it faces the same way as a model would.
        if (ShipControls.IsGalleon(type)) box.transform.localScale = new Vector3(0.45f, 0.35f, 1.0f);
        else if (ShipControls.IsSloop(type)) box.transform.localScale = new Vector3(0.3f, 0.25f, 0.75f);
        else box.transform.localScale = new Vector3(0.25f, 0.18f, 0.5f);

        var rend = box.GetComponent<Renderer>();
        if (rend != null) rend.material.color = ShipControls.PlaceholderColor(type);
        return box.transform;
    }

    private Quaternion Heading(Vector3 direction) =>
        Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);

    // ------------------------------------------------------------------ Animation

    private void AnimateShip(ShipInstance s)
    {
        if (s.moveRoutine != null) StopCoroutine(s.moveRoutine);
        s.moveRoutine = StartCoroutine(AnimateRoutine(s, new List<Vector3>(s.path)));
    }

    private IEnumerator AnimateRoutine(ShipInstance s, List<Vector3> points)
    {
        if (s.arrow != null) s.arrow.enabled = false;

        if (points.Count >= 2)
        {
            float segmentTime = moveDuration / (points.Count - 1);
            s.root.transform.position = points[0];

            for (int i = 1; i < points.Count; i++)
            {
                Vector3 from = points[i - 1];
                Vector3 to = points[i];
                Vector3 flat = to - from;
                flat.y = 0f;

                // Only turn to face forward movement; the "bump back" after a crash shouldn't spin the ship.
                Quaternion startRot = s.visual.rotation;
                Quaternion endRot = flat.x > 0.001f ? Heading(flat.normalized) : startRot;

                for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, segmentTime))
                {
                    float e = Mathf.SmoothStep(0f, 1f, t);
                    s.root.transform.position = Vector3.Lerp(from, to, e);
                    s.visual.rotation = Quaternion.Slerp(startRot, endRot, e);
                    yield return null;
                }
                s.root.transform.position = to;
                s.visual.rotation = endRot;
            }
        }

        if (s.wrecked)
        {
            if (s.label != null) s.label.transform.parent.gameObject.SetActive(false);
            yield return SinkRoutine(s);
            s.moveRoutine = null;
            Destroy(s.root);
        }
        else if (s.saved)
        {
            s.moveRoutine = null;
            Destroy(s.root);
        }
        else
        {
            s.moveRoutine = null;
            UpdateLabel(s);
            UpdateArrow(s);
        }
    }

    private IEnumerator SinkRoutine(ShipInstance s)
    {
        const float duration = 1.0f;
        Vector3 start = s.root.transform.position;
        Quaternion startRot = s.visual.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, 60f);

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            s.root.transform.position = start + Vector3.down * (0.8f * t);
            s.visual.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Label

    private TMPro.TextMeshProUGUI CreateLabel(Transform parent)
    {
        GameObject canvasObj = new GameObject("ShipKeyUI");
        canvasObj.transform.SetParent(parent, false);
        canvasObj.transform.localPosition = new Vector3(0, 1.2f, 0);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<BillboardConstraint>();

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 130);
        rect.localScale = Vector3.one * 0.015f;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);

        var tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 24;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.yellow;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200, 130);
        textRect.anchoredPosition = Vector2.zero;
        return tmp;
    }

    private void UpdateLabel(ShipInstance s)
    {
        if (s.label == null) return;
        ShipType t = s.data.type;
        string text = $"▲ {ShipControls.UpKey(t)}\n▼ {ShipControls.DownKey(t)}";
        if (s.data.maxHP > 1) text += $"\n<size=70%>HP {s.hp}/{s.data.maxHP}</size>";
        s.label.text = text;
    }

    // ------------------------------------------------------------------ Arrow

    private LineRenderer CreateArrow(Transform parent)
    {
        var obj = new GameObject("MoveArrow");
        obj.transform.SetParent(parent, false);

        var line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.material = arrowMaterial;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // Thin shaft, then a wide head that narrows to a point.
        var width = new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.68f, 0.08f),
            new Keyframe(0.7f, 0.32f),
            new Keyframe(1f, 0f));
        line.widthCurve = width;
        line.widthMultiplier = 1f;
        return line;
    }

    /// <summary>Points the arrow at where the ship will end up if nothing blocks it.</summary>
    private void UpdateArrow(ShipInstance s)
    {
        if (s.arrow == null || s.removed) return;

        var points = new List<Vector3> { ShipPos(s.coord) };
        Vector2Int c = s.coord;
        for (int step = 0; step < s.data.hexesPerTurn; step++)
        {
            int dir = step == 0 ? s.pendingDirection : 0;
            Vector2Int next = c + Offset(dir);
            if (dir != 0 && !gridSystem.HasHex(next)) next = c + HexGridSystem.East;
            c = next;
            points.Add(ShipPos(c));
            if (!gridSystem.HasHex(c)) break; // sails off the edge
        }

        // Start the arrow a little ahead of the ship and keep it just above the water.
        float drop = shipHeight - 0.05f;
        for (int i = 0; i < points.Count; i++) points[i] += Vector3.down * drop;
        points[0] = Vector3.Lerp(points[0], points[1], 0.25f);

        s.arrow.positionCount = points.Count;
        s.arrow.SetPositions(points.ToArray());

        Color color = s.pendingDirection != 0 ? queuedArrowColor : defaultArrowColor;
        s.arrow.startColor = color;
        s.arrow.endColor = color;
        s.arrow.enabled = IsRunning && s.moveRoutine == null;
    }

    private static Material CreateArrowMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        return new Material(shader);
    }
}
