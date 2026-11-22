using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Esc pauses the level (music and turn timer stop too) and shows
/// Resume / Restart / Main Menu and a volume control. Esc again resumes.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TutorialLetter tutorialLetter;
    [SerializeField, Range(0f, 1f)] private float volumeStep = 0.1f;

    private GameObject panel;
    private TextMeshProUGUI volumeText;
    private bool open;

    void Start()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (tutorialLetter == null) tutorialLetter = FindAnyObjectByType<TutorialLetter>();
        UIBuilder.EnsureEventSystem();
        Build();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (open) SetOpen(false);
        else if (CanOpen() && !GamePause.JustResumed) SetOpen(true);
    }

    private bool CanOpen() =>
        !TutorialLetter.IsOpen &&
        (gameManager == null || gameManager.CurrentState == GameManager.State.Playing);

    public void SetOpen(bool value)
    {
        open = value;
        panel.SetActive(value);
        GamePause.Set(value);
        if (value) RefreshVolume();
    }

    private void ChangeVolume(float delta)
    {
        GameSettings.MasterVolume = GameSettings.MasterVolume + delta;
        RefreshVolume();
    }

    private void RefreshVolume()
    {
        volumeText.text = $"Volume  {Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
    }

    private void Build()
    {
        RectTransform root = UIBuilder.Canvas(transform, "Pause Canvas", 50);

        panel = UIBuilder.Stretch(root, "PausePanel").gameObject;
        UIBuilder.Fill(panel.GetComponent<RectTransform>(), new Color(0f, 0f, 0f, 0.6f));
        var p = panel.GetComponent<RectTransform>();

        var box = UIBuilder.Rect(p, "Box", Vector2.zero, new Vector2(620, 760));
        UIBuilder.Fill(box, UIBuilder.PanelColor);

        UIBuilder.Label(UIBuilder.Rect(box, "Title", new Vector2(0, 300), new Vector2(560, 90)), "<b>Paused</b>", 64);

        var size = new Vector2(420, 84);
        UIBuilder.MakeButton(box, "Resume", new Vector2(0, 180), size, () => SetOpen(false));
        UIBuilder.MakeButton(box, "Restart", new Vector2(0, 80), size, () => { if (gameManager != null) gameManager.Restart(); });
        if (gameManager != null && gameManager.HasMenu)
            UIBuilder.MakeButton(box, "Main Menu", new Vector2(0, -20), size, () => gameManager.ReturnToMenu());
        if (tutorialLetter != null)
            UIBuilder.MakeButton(box, "Keeper's Letter", new Vector2(0, -120), size, () => { SetOpen(false); tutorialLetter.Open(); });

        volumeText = UIBuilder.Label(UIBuilder.Rect(box, "Volume", new Vector2(0, -230), new Vector2(300, 70)), "", 36);
        UIBuilder.MakeButton(box, "-", new Vector2(-210, -230), new Vector2(80, 70), () => ChangeVolume(-volumeStep), 48);
        UIBuilder.MakeButton(box, "+", new Vector2(210, -230), new Vector2(80, 70), () => ChangeVolume(volumeStep), 48);

        UIBuilder.Label(UIBuilder.Rect(box, "Hint", new Vector2(0, -320), new Vector2(560, 50)), "Esc to resume", 26)
            .color = new Color(UIBuilder.TextColor.r, UIBuilder.TextColor.g, UIBuilder.TextColor.b, 0.6f);

        panel.SetActive(false);
    }
}
