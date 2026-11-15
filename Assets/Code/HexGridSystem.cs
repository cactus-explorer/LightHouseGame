using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class HexGridSystem : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridRadius = 5;
    [SerializeField] private float hexRadius = 1.0f;
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Material tileMaterial;

    private Dictionary<Vector2Int, Renderer> hexRenderers = new Dictionary<Vector2Int, Renderer>();
    private Vector2Int? selectedCoord = null;
    private Camera mainCamera;
    private Mesh sharedHexMesh;
    private MaterialPropertyBlock propBlock;
    private Plane groundPlane;

    void Start()
    {
        mainCamera = Camera.main;
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        propBlock = new MaterialPropertyBlock();

        // Setup fallback material if none assigned
        if (tileMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            tileMaterial = new Material(shader);
        }

        sharedHexMesh = CreateHexMesh(hexRadius);
        GenerateGrid();
    }

    void Update()
    {
        HandleMouseClick();
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
                hexObj.transform.position = worldPos;
                hexObj.transform.parent = transform;

                MeshFilter mf = hexObj.AddComponent<MeshFilter>();
                MeshRenderer mr = hexObj.AddComponent<MeshRenderer>();

                mf.sharedMesh = sharedHexMesh;
                mr.sharedMaterial = tileMaterial;

                SetTileColor(mr, defaultColor);
                hexRenderers.Add(coord, mr);
            }
        }
    }

    private Vector3 AxialToWorld(Vector2Int coord)
    {
        // Pointy-topped hex math
        float x = hexRadius * Mathf.Sqrt(3.0f) * (coord.x + coord.y / 2.0f);
        float z = hexRadius * (3.0f / 2.0f) * coord.y;
        return new Vector3(x, 0, z);
    }

    private Mesh CreateHexMesh(float radius)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[7];
        int[] triangles = new int[18];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60 * i - 30;
            float angleRad = Mathf.Deg2Rad * angleDeg;
            vertices[i + 1] = new Vector3(radius * Mathf.Cos(angleRad), 0, radius * Mathf.Sin(angleRad));
        }

        // Generate triangles with corrected winding order (clockwise from above) so normals point up (+Y)
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    private void HandleMouseClick()
    {
        // Check using the new Input System package
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 localPoint = transform.InverseTransformPoint(hitPoint);

                // Convert world position back to axial coordinates
                Vector2Int clickedCoord = WorldToAxial(localPoint);

                if (hexRenderers.ContainsKey(clickedCoord))
                {
                    // Reset previous selection
                    if (selectedCoord.HasValue && hexRenderers.ContainsKey(selectedCoord.Value))
                    {
                        SetTileColor(hexRenderers[selectedCoord.Value], defaultColor);
                    }

                    // Highlight new selection
                    selectedCoord = clickedCoord;
                    SetTileColor(hexRenderers[clickedCoord], highlightColor);
                }
            }
        }
    }

    private Vector2Int WorldToAxial(Vector3 point)
    {
        float px = point.x / hexRadius;
        float pz = point.z / hexRadius;

        float q = (Mathf.Sqrt(3.0f) / 3.0f * px - 1.0f / 3.0f * pz);
        float r = (2.0f / 3.0f * pz);

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

        float x_diff = Mathf.Abs(rx - fx);
        float y_diff = Mathf.Abs(ry - fy);
        float z_diff = Mathf.Abs(rz - fz);

        if (x_diff > y_diff && x_diff > z_diff)
        {
            rx = -ry - rz;
        }
        else if (y_diff > z_diff)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }

        return new Vector2Int(rx, rz);
    }

    private void SetTileColor(Renderer renderer, Color color)
    {
        renderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", color); // URP compatibility
        propBlock.SetColor("_Color", color);     // Built-in pipeline compatibility
        renderer.SetPropertyBlock(propBlock);
    }
}