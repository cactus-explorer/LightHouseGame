using UnityEngine;

/// <summary>
/// Something on a hex that damages ships trying to enter it.
/// Rocks use this as-is. SeaMonster and Thunderstorm override the virtual members.
/// </summary>
public class Obstacle : MonoBehaviour
{
    public Vector2Int Coord { get; set; }

    /// <summary>Whether ships entering this hex take damage right now. A storm that's only a warning returns false.</summary>
    public virtual bool BlocksShips => true;

    /// <summary>Colour to tint this obstacle's own hex outline, or null to leave it alone.</summary>
    public virtual Color? HexTint => null;

    /// <summary>A hex this obstacle will move into or affect next turn (shown as a warning), or null.</summary>
    public virtual Vector2Int? TelegraphHex => null;

    /// <summary>Called once, right after the obstacle is placed.</summary>
    public virtual void OnSpawned(ObstacleManager manager) { }

    /// <summary>Called once per turn, before ships move.</summary>
    public virtual void OnTurnAdvanced(ObstacleManager manager) { }
}
