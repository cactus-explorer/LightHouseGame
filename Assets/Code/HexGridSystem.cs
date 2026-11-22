using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class HexGridSystem : MonoBehaviour
{
    // Axial neighbour offsets for this pointy-top layout (+r is up the screen / +Z).
    public static readonly Vector2Int East = new Vector2Int(1, 0);
    public static readonly Vector2Int NorthEast = new Vector2Int(0, 1);
    public static readonly Vector2Int SouthEast = new Vector2Int(1, -1);

    [Header("Grid Settings")]
    [SerializeField] private int gridRadius = 5;
    [SerializeField] private float hexRadius = 1.0f;
    [SerializeField] private float outlineThickness = 0.08f;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Material tileMaterial;

    [Header("Debug")]
    [Tooltip("Click a hex to highlight it. Off by default; handy for finding coords when placing rocks.")]
    [SerializeField] private bool enableClickSelect = false;

    private Dictionary<Vector2Int, Renderer> hexRenderers = new Dictionary<Vector2Int, Renderer>();
    private Vector2Int? selectedCoord = null;
    private Camera mainCamera;
    private Mesh sharedHexMesh;
    private MaterialPropertyBlock propBlock;
    private Plane groundPlane;

    public float HexRadius => hexRadius;

    // The grid is built in Awake so every other script can use it from its own Start().
    void Awake()
    {
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        propBlock = new MaterialPropertyBlock();

        if (tileMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            tileMaterial = new Material(shader);
        }

        sharedHexMesh = CreateHexOutlineMesh(hexRadius, outlineThickness);
        GenerateGrid();
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (enableClickSelect) HandleMouseClick();
    }

    private void GenerateGrid()
    {
        for (int q = -gridRadius; q <= gridRadius; q++)
        {
            int r1 = Mathf.Max(-gridRadius, -q - gridRadius);
            int r2 = Mathf.Min(gridRadius, -q + gridRadius);

            for (int r = r1; r <= r2; r++)
            {
                Vector2Int coord = new Vector2Int(q, r);
                Vector3 worldPos = AxialToWorld(coord);

                GameObject hexObj = new GameObject($"Hex_{q}_{r}");
                hexObj.transform.SetParent(transform, false);
                hexObj.transform.localPosition = worldPos;

                MeshFilter meshFilter = hexObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = sharedHexMesh;

                MeshRenderer meshRenderer = hexObj.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = tileMaterial;

                SetTileColor(meshRenderer, defaultColor);
                hexRenderers[coord] = meshRenderer;
            }
        }
    }

    private Vector3 AxialToWorld(Vector2Int hex)
    {
        float x = hexRadius * (Mathf.Sqrt(3.0f) * hex.x + Mathf.Sqrt(3.0f) / 2.0f * hex.y);
        float z = hexRadius * (3.0f / 2.0f * hex.y);
        return new Vector3(x, 0, z);
    }

    public bool HasHex(Vector2Int coord) => hexRenderers.ContainsKey(coord);

    public IEnumerable<Vector2Int> GetAllHexCoords() => hexRenderers.Keys;

    public Vector3 GetHexWorldPosition(Vector2Int coord) => transform.TransformPoint(AxialToWorld(coord));

    /// <summary>World position of an axial coord even if it's off the grid (used to sail ships off the edge).</summary>
    public Vector3 GetWorldPositionUnchecked(Vector2Int coord) => transform.TransformPoint(AxialToWorld(coord));

    /// <summary>Hexes whose west neighbour doesn't exist: where ships spawn.</summary>
    public List<Vector2Int> GetLeftEdgeHexes()
    {
        var result = new List<Vector2Int>();
        foreach (var coord in hexRenderers.Keys)
        {
            if (!HasHex(coord - East)) result.Add(coord);
        }
        return result;
    }

    /// <summary>World-space box around every hex (used to size the map and frame the camera).</summary>
    public Bounds GetWorldBounds()
    {
        bool first = true;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var coord in hexRenderers.Keys)
        {
            Vector3 p = GetHexWorldPosition(coord);
            var hex = new Bounds(p, new Vector3(hexRadius * 2f, 0f, hexRadius * 2f));
            if (first) { b = hex; first = false; }
            else b.Encapsulate(hex);
        }
        return b;
    }

    public void SetHexColor(Vector2Int coord, Color color)
    {
        if (hexRenderers.TryGetValue(coord, out Renderer r)) SetTileColor(r, color);
    }

    public void ResetHexColor(Vector2Int coord) => SetHexColor(coord, defaultColor);

    private Mesh CreateHexOutlineMesh(float outerRadius, float thickness)
    {
        Mesh mesh = new Mesh();
        float innerRadius = Mathf.Max(0.01f, outerRadius - thickness);

        Vector3[] vertices = new Vector3[12];
        int[] triangles = new int[36];

        for (int i = 0; i < 6; i++)
        {
            float angleRad = Mathf.Deg2Rad * (60 * i - 30);
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            vertices[i] = new Vector3(outerRadius * cos, 0, outerRadius * sin);
            vertices[i + 6] = new Vector3(innerRadius * cos, 0, innerRadius * sin);
        }

        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            int triIndex = i * 6;

            triangles[triIndex] = i;
            triangles[triIndex + 1] = i + 6;
            triangles[triIndex + 2] = next;

            triangles[triIndex + 3] = next;
            triangles[triIndex + 4] = i + 6;
            triangles[triIndex + 5] = next + 6;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private void HandleMouseClick()
    {
        if (mainCamera == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!groundPlane.Raycast(ray, out float enter)) return;

        Vector3 localPoint = transform.InverseTransformPoint(ray.GetPoint(enter));
        Vector2Int clickedCoord = WorldToAxial(localPoint);
        if (!hexRenderers.ContainsKey(clickedCoord)) return;

        if (selectedCoord.HasValue) ResetHexColor(selectedCoord.Value);
        selectedCoord = clickedCoord;
        SetHexColor(clickedCoord, highlightColor);
        Debug.Log($"Clicked hex ({clickedCoord.x}, {clickedCoord.y})");
    }

    private Vector2Int WorldToAxial(Vector3 point)
    {
        float px = point.x / hexRadius;
        float pz = point.z / hexRadius;
        float q = Mathf.Sqrt(3.0f) / 3.0f * px - 1.0f / 3.0f * pz;
        float r = 2.0f / 3.0f * pz;
        return HexRound(q, r);
    }

    private Vector2Int HexRound(float fq, float fr)
    {
        float fx = fq;
        float fz = fr;
        float fy = -fx - fz;

        int rx = Mathf.RoundToInt(fx);
        int ry = Mathf.RoundToInt(fy);
        int rz = Mathf.RoundToInt(fz);

        float xDiff = Mathf.Abs(rx - fx);
        float yDiff = Mathf.Abs(ry - fy);
        float zDiff = Mathf.Abs(rz - fz);

        if (xDiff > yDiff && xDiff > zDiff) rx = -ry - rz;
        else if (yDiff > zDiff) ry = -rx - rz;
        else rz = -rx - ry;

        return new Vector2Int(rx, rz);
    }

    private void SetTileColor(Renderer renderer, Color color)
    {
        renderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", color); // URP
        propBlock.SetColor("_Color", color);     // Built-in
        renderer.SetPropertyBlock(propBlock);
    }
}
