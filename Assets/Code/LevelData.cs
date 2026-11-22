using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything that changes from one level to the next. Make new ones with
/// Assets > Create > Lighthouse > Level, then add them to the LevelCatalog in Assets/Resources.
/// </summary>
[CreateAssetMenu(menuName = "Lighthouse/Level", fileName = "Level")]
public class LevelData : ScriptableObject
{
    [Serializable]
    public class ShipUnlock
    {
        public ShipType type;
        [Tooltip("This ship type starts appearing after this many turns.")]
        public int unlockAfterTurn;
    }

    public string levelName = "Calm Waters";
    [TextArea(2, 4)] public string description = "";

    [Header("Map")]
    [Range(2, 8)] public int gridRadius = 5;
    public int randomRockCount = 6;
    [Tooltip("Hand-placed rocks (axial q, r), on top of the random ones.")]
    public List<Vector2Int> rockCoords = new List<Vector2Int>();
    [Tooltip("0 = a different rock layout every play.")]
    public int randomSeed = 0;

    [Header("Goal")]
    [Min(1)] public int shipsToSave = 10;
    [Min(1)] public int maxWrecks = 3;

    [Header("Ships")]
    [Min(1)] public int turnsPerSpawn = 2;
    public int maxShipsOnBoard = 8;
    [Tooltip("Which ship types appear, and from which turn. Prefabs come from the ShipManager in the scene.")]
    public ShipUnlock[] ships =
    {
        new ShipUnlock { type = ShipType.DinghyA, unlockAfterTurn = 0 },
    };

    [Header("Sea Monster")]
    public int monsterCount = 0;
    public int monsterFirstTurn = 6;

    [Header("Thunderstorms")]
    public bool storms = false;
    public int stormFirstTurn = 10;
    [Min(1)] public int turnsBetweenStorms = 5;
    public Vector2Int stormSize = new Vector2Int(2, 4);
    public int stormActiveTurns = 3;

    [Header("Music")]
    [Tooltip("Leave empty to keep the scene's track.")]
    public AudioClip music;
    [Tooltip("Speeds the track up (shorter turns). Also raises its pitch, so a faster track from Vi is better.")]
    [Range(0.5f, 2f)] public float musicSpeed = 1f;

    [Header("Tutorial")]
    [Tooltip("Show the keeper's letter at the start of this level.")]
    public bool showTutorialLetter = false;
}
