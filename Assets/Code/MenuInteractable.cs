using UnityEngine;

/// <summary>
/// Put this on a 3D object in the menu scene (the study window, the bed, the letter...)
/// to make it clickable. Needs a Collider. MainMenu handles hover and clicks.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MenuInteractable : MonoBehaviour
{
    public enum MenuAction { Play, HowToPlay, Quit }

    public MenuAction action = MenuAction.Play;

    [Tooltip("Shown on screen while the mouse is over this object.")]
    public string hoverText = "Start the night";

    [Tooltip("Tint applied to the object's renderers while hovered.")]
    public Color hoverTint = new Color(1f, 0.85f, 0.45f);

    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
    }

    public void SetHovered(bool hovered)
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (hovered)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", hoverTint); // URP
                block.SetColor("_Color", hoverTint);     // Built-in
                r.SetPropertyBlock(block);
            }
            else
            {
                r.SetPropertyBlock(null); // back to the material's own colours
            }
        }
    }
}
