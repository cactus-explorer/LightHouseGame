using UnityEngine;

/// <summary>
/// Lays a flat map under the hex grid, sized to fit it, for the old-sea-map look
/// (semi-transparent hex grid over a 2D map). Assign a texture or a material;
/// with neither you get a plain parchment-coloured sea.
/// </summary>
public class MapBackground : MonoBehaviour
{
    [SerializeField] private HexGridSystem gridSystem;

    [Tooltip("The painted sea map. Ignored if a material is assigned.")]
    [SerializeField] private Texture2D mapTexture;
    [Tooltip("Use your own material instead (e.g. a lit one, or one with a paper normal map).")]
    [SerializeField] private Material mapMaterial;
    [SerializeField] private Color tint = new Color(0.86f, 0.78f, 0.6f);

    [Tooltip("Extra world units of map around the grid on every side.")]
    [SerializeField] private float padding = 2f;
    [Tooltip("Keep the texture's aspect ratio instead of stretching it to the grid.")]
    [SerializeField] private bool keepAspect = true;
    [SerializeField] private float heightBelowGrid = 0.02f;

    void Start()
    {
        if (gridSystem == null) gridSystem = FindAnyObjectByType<HexGridSystem>();
        if (gridSystem == null) return;

        Bounds b = gridSystem.GetWorldBounds();
        float width = b.size.x + padding * 2f;
        float depth = b.size.z + padding * 2f;

        Texture tex = mapMaterial != null ? mapMaterial.mainTexture : mapTexture;
        if (keepAspect && tex != null && tex.height > 0)
        {
            // Grow whichever side is too short, so the grid always stays on the map.
            float aspect = (float)tex.width / tex.height;
            if (width / depth > aspect) depth = width / aspect;
            else width = depth * aspect;
        }

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "SeaMap";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.position = new Vector3(b.center.x, b.center.y - heightBelowGrid, b.center.z);
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up
        quad.transform.localScale = new Vector3(width, depth, 1f);

        var rend = quad.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mapMaterial != null)
        {
            rend.sharedMaterial = mapMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            var mat = new Material(shader) { color = tint };
            if (mapTexture != null) mat.mainTexture = mapTexture;
            rend.sharedMaterial = mat;
        }
    }
}
