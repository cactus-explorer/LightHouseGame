using UnityEngine;

/// <summary>
/// Put this on each ship prefab. If a prefab doesn't have one, ShipManager adds it
/// with the default stats for the type it was spawned as.
/// </summary>
public class Ship : MonoBehaviour
{
    public ShipType type = ShipType.DinghyA;

    [Tooltip("Hits it can take. It wrecks when HP reaches 0.")]
    [Min(1)] public int maxHP = 1;

    [Tooltip("Hexes moved per turn. Steering only applies to the first one.")]
    [Range(1, 3)] public int hexesPerTurn = 1;

    /// <summary>Default stats: dinghy is the baseline, sloop is fast but fragile, galleon is slow and tough.</summary>
    public void ApplyDefaultsFor(ShipType t)
    {
        type = t;
        if (ShipControls.IsSloop(t)) { maxHP = 2; hexesPerTurn = 2; }
        else if (ShipControls.IsGalleon(t)) { maxHP = 3; hexesPerTurn = 1; }
        else { maxHP = 1; hexesPerTurn = 1; }
    }
}
