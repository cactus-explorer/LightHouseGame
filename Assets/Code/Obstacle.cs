using UnityEngine;

/// <summary>
/// Something on a hex that damages ships trying to enter it.
/// Rocks use this as-is. Moving obstacles (sea monster) or temporary ones (storms)
/// can subclass it and override OnTurnAdvanced.
/// </summary>
public class Obstacle : MonoBehaviour
{
    public Vector2Int Coord { get; set; }

    /// <summary>Whether ships entering this hex take damage right now. A storm that's only telegraphed returns false.</summary>
    public virtual bool BlocksShips => true;

    /// <summary>Called once per turn, before ships move.</summary>
    public virtual void OnTurnAdvanced(ObstacleManager manager) { }
}
