using System.Collections;
using UnityEngine;

/// <summary>
/// One hex of a thunderstorm.
///  - Warning (1 turn): a small grey cloud and a grey hex. Ships can still pass.
///  - Active (a few turns): a dark cloud and a dark hex. Ships entering take damage,
///    and a ship sitting on the hex when the storm breaks takes 1 damage.
///  - Then it clears and removes itself.
/// </summary>
public class Thunderstorm : Obstacle
{
    public int activeTurns = 3;
    public Color warningTint = new Color(0.6f, 0.6f, 0.65f);
    public Color activeTint = new Color(0.35f, 0.25f, 0.55f);
    public Color warningCloudColor = new Color(0.75f, 0.75f, 0.78f);
    public Color activeCloudColor = new Color(0.22f, 0.22f, 0.28f);

    private enum Phase { Warning, Active }
    private Phase phase = Phase.Warning;
    private int turnsActive;
    private Renderer[] cloudRenderers;
    private Transform cloud;
    private Vector3 cloudScale;

    public override bool BlocksShips => phase == Phase.Active;
    public override Color? HexTint => phase == Phase.Warning ? warningTint : activeTint;

    public override void OnSpawned(ObstacleManager manager)
    {
        cloud = transform.childCount > 0 ? transform.GetChild(0) : transform;
        cloudRenderers = GetComponentsInChildren<Renderer>();
        cloudScale = cloud.localScale;
        cloud.localScale = cloudScale * 0.55f;
        SetCloudColor(warningCloudColor);
        manager.ReportStormWarning(this);
    }

    public override void OnTurnAdvanced(ObstacleManager manager)
    {
        if (phase == Phase.Warning)
        {
            phase = Phase.Active;
            cloud.localScale = cloudScale;
            SetCloudColor(activeCloudColor);
            StartCoroutine(Flash());
            manager.StormStrikes(this);
            return;
        }

        turnsActive++;
        if (turnsActive >= activeTurns)
        {
            manager.Remove(this);
            Destroy(gameObject);
        }
    }

    private void SetCloudColor(Color c)
    {
        foreach (var r in cloudRenderers) r.material.color = c;
    }

    private IEnumerator Flash()
    {
        for (int i = 0; i < 2; i++)
        {
            SetCloudColor(Color.white);
            yield return new WaitForSeconds(0.06f);
            SetCloudColor(activeCloudColor);
            yield return new WaitForSeconds(0.1f);
        }
    }
}
