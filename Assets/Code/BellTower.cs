using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The lighthouse's five bells, in the world. Each bell swings and glows when its key is played,
/// and all of them take on the current chord's colour, so the player sees the bells "swap" each turn.
/// Placeholder: a tower cylinder with five sphere bells. Assign a bell prefab (and a tower prefab) later.
/// By default it sits on the shore just off the grid's bottom-left corner.
/// </summary>
public class BellTower : MonoBehaviour
{
    [SerializeField] private HexGridSystem gridSystem;
    [SerializeField] private TimeManager timeManager;

    [Header("Placement")]
    [Tooltip("Place the tower automatically next to the grid. Turn off to place this object by hand.")]
    [SerializeField] private bool autoPlace = true;
    [SerializeField] private Vector3 offsetFromGridCorner = new Vector3(-1.5f, 0f, -0.5f);

    [Header("Models (optional)")]
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private GameObject bellPrefab;

    [Header("Bells")]
    [SerializeField] private float bellSpacing = 0.9f;
    [SerializeField] private float bellHeight = 3.2f;
    [SerializeField] private float swingAngle = 35f;
    [SerializeField] private float swingDamping = 4f;
    [SerializeField] private Color idleColor = new Color(0.55f, 0.42f, 0.2f);
    [SerializeField] private bool showKeyLabels = true;

    [Header("Chord Colours")]
    [SerializeField] private Color colorC = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color colorF = new Color(0.35f, 0.85f, 0.8f);
    [SerializeField] private Color colorG = new Color(0.95f, 0.45f, 0.6f);

    private class Bell
    {
        public Transform pivot;
        public Renderer[] renderers;
        public float swing;     // current angle
        public float velocity;  // angular velocity
        public float glow;      // 0..1
    }

    private readonly List<Bell> bells = new List<Bell>();
    private Transform root; // built as its own scene object, so placing it never moves this GameObject

    public bool IsBuilt => root != null;
    private MaterialPropertyBlock block;
    private Color chordColor;

    void Start()
    {
        if (gridSystem == null) gridSystem = FindAnyObjectByType<HexGridSystem>();
        if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
        block = new MaterialPropertyBlock();

        root = new GameObject("BellTower").transform;
        root.position = transform.position;
        root.rotation = transform.rotation;
        if (autoPlace && gridSystem != null)
        {
            Bounds b = gridSystem.GetWorldBounds();
            root.position = new Vector3(b.min.x, b.center.y, b.min.z) + offsetFromGridCorner;
        }

        Build();
        chordColor = ColorFor(timeManager != null ? timeManager.CurrentNote : "C");

        NoteInputHandler.OnNoteKeyPressed += Ring;
        if (timeManager != null) timeManager.OnChordChanged += HandleChord;

        // The camera framed the grid before this existed; reframe to include the tower.
        var rig = FindAnyObjectByType<CameraRig>();
        if (rig != null) rig.FrameGrid();
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        NoteInputHandler.OnNoteKeyPressed -= Ring;
        if (timeManager != null) timeManager.OnChordChanged -= HandleChord;
    }

    /// <summary>World-space box around the tower, so CameraRig can keep it in view.</summary>
    public Bounds GetBounds()
    {
        if (root == null) return new Bounds(transform.position, Vector3.zero);
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    private void Ring(int index)
    {
        if (index < 0 || index >= bells.Count) return;
        var bell = bells[index];
        bell.velocity += swingAngle * 8f;
        bell.glow = 1f;
    }

    private void HandleChord(int index, string chord)
    {
        chordColor = ColorFor(chord);
        // A small shiver through every bell as they "swap" to the new key.
        foreach (var bell in bells)
        {
            bell.velocity += swingAngle * 3f;
            bell.glow = Mathf.Max(bell.glow, 0.5f);
        }
    }

    private Color ColorFor(string chord)
    {
        switch (chord)
        {
            case "F": return colorF;
            case "G": return colorG;
            default: return colorC;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime; // 0 while paused
        foreach (var bell in bells)
        {
            // Damped spring back to hanging straight down.
            bell.velocity += -bell.swing * 60f * dt;
            bell.velocity *= Mathf.Exp(-swingDamping * dt);
            bell.swing += bell.velocity * dt;
            bell.pivot.localRotation = Quaternion.Euler(0f, 0f, bell.swing);

            bell.glow = Mathf.Max(0f, bell.glow - dt * 1.5f);
            Color baseColor = Color.Lerp(idleColor, chordColor, 0.35f);
            Color c = Color.Lerp(baseColor, chordColor * 1.4f, bell.glow);
            foreach (var r in bell.renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                r.SetPropertyBlock(block);
            }
        }
    }

    // ------------------------------------------------------------------ Building

    private void Build()
    {
        if (towerPrefab != null)
        {
            Instantiate(towerPrefab, root);
        }
        else
        {
            var tower = Primitive(PrimitiveType.Cylinder, root, new Vector3(0f, 1.4f, 0f), new Vector3(0.9f, 1.4f, 0.9f));
            tower.name = "Tower";
            SetColor(tower, new Color(0.9f, 0.88f, 0.82f));
            var beam = Primitive(PrimitiveType.Cube, root, new Vector3(0f, bellHeight + 0.35f, 0f),
                                 new Vector3(bellSpacing * 5f, 0.12f, 0.12f));
            beam.name = "Beam";
            SetColor(beam, new Color(0.3f, 0.2f, 0.12f));
        }

        int count = ShipControls.KeyNames.Length;
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) / 2f) * bellSpacing;

            var pivot = new GameObject($"Bell_{ShipControls.KeyNames[i]}").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(x, bellHeight + 0.3f, 0f);

            GameObject model = bellPrefab != null
                ? Instantiate(bellPrefab, pivot)
                : Primitive(PrimitiveType.Sphere, pivot, new Vector3(0f, -0.35f, 0f), new Vector3(0.5f, 0.55f, 0.5f));
            model.transform.localPosition = new Vector3(0f, -0.35f, 0f);

            if (showKeyLabels) CreateLabel(pivot, ShipControls.KeyNames[i]);

            bells.Add(new Bell { pivot = pivot, renderers = model.GetComponentsInChildren<Renderer>() });
        }
    }

    private void CreateLabel(Transform bellPivot, string key)
    {
        var obj = new GameObject("KeyLabel");
        obj.transform.SetParent(root, false); // on the tower, so it doesn't swing with the bell
        obj.transform.localPosition = bellPivot.localPosition + new Vector3(0f, -1.0f, 0f);
        obj.AddComponent<BillboardConstraint>();
        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = key;
        tmp.fontSize = key.Length > 1 ? 2.2f : 3f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.98f, 0.93f, 0.8f);
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(30, 20, 10, 255);
        tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
    }

    private static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale)
    {
        var obj = GameObject.CreatePrimitive(type);
        Destroy(obj.GetComponent<Collider>());
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localScale = scale;
        return obj;
    }

    private static void SetColor(GameObject obj, Color c)
    {
        var r = obj.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }
}
