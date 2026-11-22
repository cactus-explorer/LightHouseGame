using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves one hex per turn. At the end of each turn it picks its next hex and shows an arrow
/// (and a red hex) there, like enemy intents in Slay the Spire.
/// At the start of the next turn it moves there. If a ship is on that hex, it attacks instead:
/// the ship takes 1 damage and the monster stays put.
/// Ships can't enter the monster's hex (it counts as an obstacle).
/// </summary>
public class SeaMonster : Obstacle
{
    [Tooltip("Chance each turn that it heads toward the nearest ship instead of wandering.")]
    [Range(0f, 1f)] public float chaseChance = 0.6f;
    public float moveDuration = 0.4f;
    public Color arrowColor = new Color(1f, 0.25f, 0.2f, 0.95f);
    public Material arrowMaterial;

    private Vector2Int? plannedHex;
    private LineRenderer arrow;

    public override Vector2Int? TelegraphHex => plannedHex;

    public override void OnSpawned(ObstacleManager manager)
    {
        CreateArrow();
        Plan(manager);
    }

    public override void OnTurnAdvanced(ObstacleManager manager)
    {
        if (plannedHex.HasValue)
        {
            Vector2Int target = plannedHex.Value;
            plannedHex = null; // so its own warning doesn't block it

            if (manager.IsShipAt(target))
            {
                manager.MonsterAttack(this, target);
                StartCoroutine(Lunge(manager.Grid.GetHexWorldPosition(target)));
            }
            else if (!manager.HasObstacle(target))
            {
                Vector3 from = transform.position;
                manager.Move(this, target);
                StartCoroutine(Slide(from, transform.position));
            }
        }

        Plan(manager);
    }

    private void Plan(ObstacleManager manager)
    {
        var options = new List<Vector2Int>();
        foreach (var dir in ObstacleManager.HexDirections)
        {
            Vector2Int c = Coord + dir;
            if (manager.Grid.HasHex(c) && !manager.HasObstacle(c) && !manager.IsTelegraphed(c) && !manager.IsSpawnSafe(c))
                options.Add(c);
        }

        if (options.Count == 0)
        {
            plannedHex = null;
        }
        else if (Random.value < chaseChance && manager.TryGetNearestShipDistance(Coord, out _))
        {
            // Head for whichever option is closest to a ship.
            Vector2Int best = options[0];
            int bestDist = int.MaxValue;
            foreach (var c in options)
            {
                manager.TryGetNearestShipDistance(c, out int d);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            plannedHex = best;
        }
        else
        {
            plannedHex = options[Random.Range(0, options.Count)];
        }

        UpdateArrow(manager);
    }

    // ------------------------------------------------------------------ Visuals

    private void CreateArrow()
    {
        var obj = new GameObject("IntentArrow");
        obj.transform.SetParent(transform.parent, false); // not on the monster, so it doesn't move with it mid-tween
        arrow = obj.AddComponent<LineRenderer>();
        arrow.useWorldSpace = true;
        if (arrowMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            arrowMaterial = new Material(shader);
        }
        arrow.material = arrowMaterial;
        arrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrow.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.1f), new Keyframe(0.6f, 0.1f), new Keyframe(0.62f, 0.4f), new Keyframe(1f, 0f));
        arrow.startColor = arrow.endColor = arrowColor;
        arrow.positionCount = 2;
    }

    private void UpdateArrow(ObstacleManager manager)
    {
        if (arrow == null) return;
        arrow.enabled = plannedHex.HasValue;
        if (!plannedHex.HasValue) return;

        Vector3 from = manager.Grid.GetHexWorldPosition(Coord) + Vector3.up * 0.15f;
        Vector3 to = manager.Grid.GetHexWorldPosition(plannedHex.Value) + Vector3.up * 0.15f;
        arrow.SetPosition(0, Vector3.Lerp(from, to, 0.3f));
        arrow.SetPosition(1, Vector3.Lerp(from, to, 0.9f));
    }

    private IEnumerator Slide(Vector3 from, Vector3 to)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / moveDuration)
        {
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = to;
    }

    private IEnumerator Lunge(Vector3 target)
    {
        Vector3 home = transform.position;
        Vector3 hit = Vector3.Lerp(home, target, 0.45f);
        yield return Slide(home, hit);
        yield return Slide(hit, home);
    }

    void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
