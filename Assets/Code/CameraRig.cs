using UnityEngine;

/// <summary>
/// Points the camera at the hex grid from a fixed, Inscryption-style tilt and pulls it back
/// far enough to fit the whole grid. Put it on the camera (or anywhere, with the camera assigned).
/// Right-click the component and pick "Frame Grid Now" to preview it outside Play mode.
/// </summary>
public class CameraRig : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private HexGridSystem gridSystem;

    [Tooltip("Degrees down from horizontal. 90 = straight down, ~55 = looking across a table.")]
    [Range(20f, 90f)] [SerializeField] private float pitch = 55f;
    [Tooltip("Keep at 0: the game treats +Z as 'up' on screen.")]
    [SerializeField] private float yaw = 0f;
    [Tooltip("Extra room around the grid. 1 = tight fit.")]
    [SerializeField] private float margin = 1.15f;
    [Tooltip("Moves the view centre toward the top (+) or bottom (-) of the grid, in world units.")]
    [SerializeField] private float focusOffsetZ = 0f;
    [SerializeField] private bool frameOnStart = true;

    void Start()
    {
        if (frameOnStart) FrameGrid();
    }

    [ContextMenu("Frame Grid Now")]
    public void FrameGrid()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (gridSystem == null) gridSystem = FindAnyObjectByType<HexGridSystem>();
        if (targetCamera == null || gridSystem == null) return;

        // Outside Play mode the grid hasn't been built, so estimate its bounds from its transform.
        Bounds b = gridSystem.GetWorldBounds();
        if (b.size == Vector3.zero) b = new Bounds(gridSystem.transform.position, new Vector3(20f, 0f, 16f));

        Vector3 focus = b.center + Vector3.forward * focusOffsetZ;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        float halfWidth = b.extents.x;
        // A tilted camera sees the grid's depth foreshortened.
        float halfDepth = b.extents.z * Mathf.Sin(pitch * Mathf.Deg2Rad);

        float distance;
        if (targetCamera.orthographic)
        {
            targetCamera.orthographicSize = Mathf.Max(halfDepth, halfWidth / targetCamera.aspect) * margin;
            distance = 30f;
        }
        else
        {
            float vFov = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov / 2f) * targetCamera.aspect);
            distance = Mathf.Max(halfWidth / Mathf.Tan(hFov / 2f), halfDepth / Mathf.Tan(vFov / 2f)) * margin;
        }

        targetCamera.transform.rotation = rot;
        targetCamera.transform.position = focus - rot * Vector3.forward * distance;
    }
}
