using System.Collections.Generic;
using UnityEngine;

/// <summary>The six controllable ship types. Each has its own up/down key pair.</summary>
public enum ShipType
{
    DinghyA,
    DinghyB,
    SloopA,
    SloopB,
    GalleonA,
    GalleonB
}

/// <summary>
/// Single source of truth for which piano key steers which ship.
/// NoteInputHandler reads it to move ships, ShipManager reads it for the labels.
/// </summary>
public static class ShipControls
{
    /// <summary>The 5 note keys, by index: 0=A, 1=S, 2=D, 3=F, 4=Space.</summary>
    public static readonly string[] KeyNames = { "A", "S", "D", "F", "Space" };

    // ship type -> (key index that steers it up, key index that steers it down)
    private static readonly Dictionary<ShipType, (int up, int down)> bindings =
        new Dictionary<ShipType, (int up, int down)>
        {
            { ShipType.DinghyA,  (0, 1) }, // up A,     down S
            { ShipType.DinghyB,  (4, 2) }, // up Space, down D
            { ShipType.SloopA,   (3, 4) }, // up F,     down Space
            { ShipType.SloopB,   (1, 2) }, // up S,     down D
            { ShipType.GalleonA, (0, 3) }, // up A,     down F
            { ShipType.GalleonB, (2, 4) }, // up D,     down Space
        };

    public static string UpKey(ShipType type) => KeyNames[bindings[type].up];
    public static string DownKey(ShipType type) => KeyNames[bindings[type].down];

    /// <summary>
    /// Every (ship type, direction) a key press triggers. Direction is +1 for up, -1 for down.
    /// </summary>
    public static List<(ShipType type, int direction)> CommandsForKey(int keyIndex)
    {
        var commands = new List<(ShipType, int)>();
        foreach (var pair in bindings)
        {
            if (pair.Value.up == keyIndex) commands.Add((pair.Key, +1));
            if (pair.Value.down == keyIndex) commands.Add((pair.Key, -1));
        }
        return commands;
    }

    public static bool IsGalleon(ShipType t) => t == ShipType.GalleonA || t == ShipType.GalleonB;
    public static bool IsSloop(ShipType t) => t == ShipType.SloopA || t == ShipType.SloopB;

    /// <summary>Placeholder colour per type, used when a prefab isn't assigned.</summary>
    public static Color PlaceholderColor(ShipType t)
    {
        switch (t)
        {
            case ShipType.DinghyA: return new Color(0.4f, 0.9f, 1f);
            case ShipType.DinghyB: return new Color(0.2f, 0.5f, 1f);
            case ShipType.SloopA: return new Color(0.5f, 1f, 0.5f);
            case ShipType.SloopB: return new Color(0.1f, 0.7f, 0.3f);
            case ShipType.GalleonA: return new Color(1f, 0.6f, 0.3f);
            default: return new Color(0.9f, 0.3f, 0.3f);
        }
    }
}
