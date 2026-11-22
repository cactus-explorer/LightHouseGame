using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Cheap game feel: camera shake, a splash of spray, and a floating word whenever
/// something happens to a ship or a hazard strikes. Everything is placeholder-procedural,
/// so it works with no assets; swap in a real splash prefab if you have one.
/// </summary>
public class FeedbackFX : MonoBehaviour
{
    [SerializeField] private ShipManager shipManager;
    [SerializeField] private ObstacleManager obstacleManager;

    [Header("Camera Shake")]
    [SerializeField] private float damageShake = 0.08f;
    [SerializeField] private float wreckShake = 0.2f;
    [SerializeField] private float strikeShake = 0.12f;
    [SerializeField] private float shakeDuration = 0.25f;

    [Header("Splash")]
    [Tooltip("Optional particle prefab. If empty, a simple spray is made in code.")]
    [SerializeField] private ParticleSystem splashPrefab;
    [SerializeField] private Color splashColor = new Color(0.85f, 0.95f, 1f, 0.9f);

    [Header("Floating Text")]
    [SerializeField] private bool showFloatingText = true;
    [SerializeField] private Color damageColor = new Color(1f, 0.45f, 0.35f);
    [SerializeField] private Color savedColor = new Color(0.55f, 1f, 0.55f);
    [SerializeField] private Color hazardColor = new Color(0.85f, 0.7f, 1f);

    private ParticleSystem splash;
    private Transform cam;
    private Vector3 camRestPosition;
    private float shakeTimeLeft;
    private float shakeStrength;

    void Start()
    {
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
        if (obstacleManager == null) obstacleManager = FindAnyObjectByType<ObstacleManager>();

        splash = splashPrefab != null ? Instantiate(splashPrefab, transform) : CreateSplash();

        if (shipManager != null)
        {
            shipManager.OnShipDamaged += HandleDamaged;
            shipManager.OnShipWrecked += HandleWrecked;
            shipManager.OnShipSaved += HandleSaved;
        }
        if (obstacleManager != null)
        {
            obstacleManager.OnMonsterAppeared += HandleMonsterAppeared;
            obstacleManager.OnStormStrike += HandleStorm;
        }
    }

    void OnDestroy()
    {
        if (shipManager != null)
        {
            shipManager.OnShipDamaged -= HandleDamaged;
            shipManager.OnShipWrecked -= HandleWrecked;
            shipManager.OnShipSaved -= HandleSaved;
        }
        if (obstacleManager != null)
        {
            obstacleManager.OnMonsterAppeared -= HandleMonsterAppeared;
            obstacleManager.OnStormStrike -= HandleStorm;
        }
    }

    // ------------------------------------------------------------------ Events

    private void HandleDamaged(Vector3 pos)
    {
        Shake(damageShake);
        Splash(pos, 12);
        Popup(pos, "-1", damageColor);
    }

    private void HandleWrecked(Vector3 pos)
    {
        Shake(wreckShake);
        Splash(pos, 30);
        Popup(pos + Vector3.up * 0.5f, "Wrecked!", damageColor);
    }

    private void HandleSaved(Vector3 pos) => Popup(pos, "Home!", savedColor);

    private void HandleMonsterAppeared(Vector3 pos)
    {
        Splash(pos, 25);
        Popup(pos, "Something stirs...", hazardColor);
    }

    private void HandleStorm(Vector3 pos) => Shake(strikeShake);

    // ------------------------------------------------------------------ Shake

    public void Shake(float strength)
    {
        if (Camera.main == null) return;
        if (shakeTimeLeft <= 0f)
        {
            cam = Camera.main.transform;
            camRestPosition = cam.position; // where CameraRig left it
        }
        shakeStrength = Mathf.Max(shakeStrength * (shakeTimeLeft / shakeDuration), strength);
        shakeTimeLeft = shakeDuration;
    }

    void LateUpdate()
    {
        if (shakeTimeLeft <= 0f || cam == null) return;

        shakeTimeLeft -= Time.deltaTime; // stops while paused, since deltaTime is 0
        if (shakeTimeLeft <= 0f)
        {
            cam.position = camRestPosition;
            shakeStrength = 0f;
            return;
        }

        float falloff = shakeTimeLeft / shakeDuration;
        cam.position = camRestPosition + Random.insideUnitSphere * (shakeStrength * falloff);
    }

    // ------------------------------------------------------------------ Splash

    private void Splash(Vector3 pos, int count)
    {
        if (splash == null) return;
        splash.transform.position = pos;
        splash.Emit(count);
    }

    private ParticleSystem CreateSplash()
    {
        var obj = new GameObject("SplashFX");
        obj.transform.SetParent(transform, false);
        var ps = obj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = splashColor;
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.enabled = false; // only bursts via Emit()

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // spray upward

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        var renderer = obj.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) renderer.sharedMaterial = new Material(shader);
        return ps;
    }

    // ------------------------------------------------------------------ Floating text

    private void Popup(Vector3 pos, string text, Color color)
    {
        if (!showFloatingText) return;
        StartCoroutine(PopupRoutine(pos + Vector3.up * 1.0f, text, color));
    }

    private IEnumerator PopupRoutine(Vector3 start, string text, Color color)
    {
        var obj = new GameObject("Popup");
        obj.transform.SetParent(transform, false);
        obj.transform.position = start;
        obj.AddComponent<BillboardConstraint>();

        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(30, 20, 10, 255);

        const float duration = 1.1f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            obj.transform.position = start + Vector3.up * (0.8f * t);
            var c = color;
            c.a = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            tmp.color = c;
            yield return null;
        }
        Destroy(obj);
    }
}
