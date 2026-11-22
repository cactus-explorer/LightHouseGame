using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu, built in code so the scene only needs this one component (plus a camera).
///  - Title, Play, How to Play, Quit buttons
///  - How to Play panel with the rules and the key for every ship (from ShipControls)
///  - Enter starts the game, Esc closes the How to Play panel
///  - Any MenuInteractable in the scene (window, bed...) can be clicked too, for the keeper's study idea
///  - Optional looping menu music, and a fade to black when starting
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Name of the gameplay scene. It must be in the build's scene list.")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Text")]
    [SerializeField] private string title = "Untitled Lighthouse Game";
    [SerializeField] private string subtitle = "(But The Lighthouse Is Also A Belltower)";

    [Header("Look")]
    [Tooltip("Turn off once the keeper's study is built and its objects do the job instead.")]
    [SerializeField] private bool showButtons = true;
    [Tooltip("Darken the screen behind the menu. Turn down if there's a 3D study scene behind it.")]
    [SerializeField, Range(0f, 1f)] private float backgroundDim = 0.85f;
    [SerializeField] private Color backgroundColor = new Color(0.07f, 0.09f, 0.12f);
    [SerializeField] private Color panelColor = new Color(0.1f, 0.08f, 0.05f, 0.9f);
    [SerializeField] private Color textColor = new Color(0.98f, 0.93f, 0.8f);
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.2f, 0.14f, 1f);
    [SerializeField] private Color buttonHoverColor = new Color(0.55f, 0.42f, 0.2f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField] private AudioClip clickSound;

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 0.8f;

    private AudioSource audioSource;
    private GameObject howToPlayPanel;
    private CanvasGroup fadeGroup;
    private TextMeshProUGUI hoverLabel;
    private MenuInteractable hovered;
    private bool starting;

    void Start()
    {
        EnsureEventSystem();
        BuildUI();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (menuMusic != null)
        {
            audioSource.clip = menuMusic;
            audioSource.loop = true;
            audioSource.volume = musicVolume;
            audioSource.Play();
        }

        StartCoroutine(Fade(1f, 0f)); // fade in from black
    }

    void Update()
    {
        if (starting) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame && howToPlayPanel.activeSelf) ShowHowToPlay(false);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Play();
        }

        HandleInteractables();
    }

    // ------------------------------------------------------------------ Actions

    public void Play()
    {
        if (starting) return;
        starting = true;
        PlayClick();
        StartCoroutine(StartGameRoutine());
    }

    public void ShowHowToPlay(bool show)
    {
        PlayClick();
        howToPlayPanel.SetActive(show);
    }

    public void Quit()
    {
        PlayClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Run(MenuInteractable.MenuAction action)
    {
        switch (action)
        {
            case MenuInteractable.MenuAction.Play: Play(); break;
            case MenuInteractable.MenuAction.HowToPlay: ShowHowToPlay(true); break;
            case MenuInteractable.MenuAction.Quit: Quit(); break;
        }
    }

    private IEnumerator StartGameRoutine()
    {
        float startVolume = audioSource.volume;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeDuration)
        {
            fadeGroup.alpha = t;
            audioSource.volume = startVolume * (1f - t);
            yield return null;
        }
        fadeGroup.alpha = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator Fade(float from, float to)
    {
        fadeGroup.blocksRaycasts = true;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeDuration)
        {
            fadeGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        fadeGroup.alpha = to;
        fadeGroup.blocksRaycasts = to > 0.5f;
    }

    private void PlayClick()
    {
        if (clickSound != null && audioSource != null) audioSource.PlayOneShot(clickSound);
    }

    // ------------------------------------------------------------------ 3D objects

    private void HandleInteractables()
    {
        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;
        MenuInteractable over = null;

        // Ignore the 3D scene while the pointer is over a button or the How to Play panel is open.
        bool pointerOnUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (cam != null && mouse != null && !pointerOnUI && !howToPlayPanel.activeSelf)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                over = hit.collider.GetComponentInParent<MenuInteractable>();
        }

        if (over != hovered)
        {
            if (hovered != null) hovered.SetHovered(false);
            hovered = over;
            if (hovered != null) hovered.SetHovered(true);
            hoverLabel.text = hovered != null ? hovered.hoverText : "";
        }

        if (hovered != null && mouse != null && mouse.leftButton.wasPressedThisFrame) Run(hovered.action);
    }

    // ------------------------------------------------------------------ Building

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        // Buttons need an EventSystem; this project uses the Input System package's UI module.
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void BuildUI()
    {
        var canvasObj = new GameObject("Menu Canvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();
        RectTransform root = canvasObj.GetComponent<RectTransform>();

        // Background dim
        var bg = Stretch(root, "Background");
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundDim);
        bgImage.raycastTarget = false;

        // Title
        var titleText = Label(root, "Title", title, 96, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1600, 130));
        titleText.fontStyle = FontStyles.Bold;
        Label(root, "Subtitle", subtitle, 38, new Vector2(0.5f, 1f), new Vector2(0, -255), new Vector2(1600, 60))
            .fontStyle = FontStyles.Italic;

        // Buttons
        if (showButtons)
        {
            bool canQuit = Application.platform != RuntimePlatform.WebGLPlayer; // Quit does nothing in a browser
            AddButton(root, "Play", new Vector2(0, 40), Play);
            AddButton(root, "How to Play", new Vector2(0, -80), () => ShowHowToPlay(true));
            if (canQuit) AddButton(root, "Quit", new Vector2(0, -200), Quit);
        }

        Label(root, "Hint", "Press Enter to start", 28, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(800, 50))
            .color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);

        // Hover text for 3D objects
        hoverLabel = Label(root, "HoverText", "", 40, new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1200, 60));

        BuildHowToPlay(root);

        // Fade overlay, on top of everything
        var fade = Stretch(root, "Fade");
        var fadeImage = fade.gameObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 1f;
    }

    private void BuildHowToPlay(RectTransform root)
    {
        var panel = Rect(root, "HowToPlay", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 820));
        panel.gameObject.AddComponent<Image>().color = panelColor;
        howToPlayPanel = panel.gameObject;

        var sb = new StringBuilder();
        sb.AppendLine("<size=130%><b>How to Play</b></size>\n");
        sb.AppendLine("You keep the lighthouse bells. Guide every ship safely to the right side of the map.\n");
        sb.AppendLine("• Ships sail one step right on every chord change (every 8 seconds).");
        sb.AppendLine("• Press a ship's ▲ key to steer it up, or its ▼ key to steer it down, before the turn ends.");
        sb.AppendLine("• Hitting a rock or another ship costs 1 HP. Galleons are tough; sloops are fast.");
        sb.AppendLine("• Bring enough ships home to win. Lose too many and the night is over.\n");
        sb.AppendLine("<b>Keys</b>  <size=80%>(the same keys show above each ship)</size>");

        foreach (ShipType type in System.Enum.GetValues(typeof(ShipType)))
        {
            sb.AppendLine($"{type}<pos=25%>▲ {ShipControls.UpKey(type)}<pos=45%>▼ {ShipControls.DownKey(type)}");
        }

        var body = Rect(panel, "Body", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1180, 680));
        var text = body.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = sb.ToString();
        text.fontSize = 32;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;

        AddButton(panel, "Back", new Vector2(0, -340), () => ShowHowToPlay(false));
        howToPlayPanel.SetActive(false);
    }

    private void AddButton(RectTransform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var rect = Rect(parent, $"Button_{text}", new Vector2(0.5f, 0.5f), position, new Vector2(420, 90));
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white; // tinted by the button's colour block

        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHoverColor;
        colors.selectedColor = buttonColor;
        colors.pressedColor = buttonHoverColor * 0.8f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var label = Stretch(rect, "Label").gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 44;
        label.alignment = TextAlignmentOptions.Center;
        label.color = textColor;
        label.raycastTarget = false;
    }

    private TextMeshProUGUI Label(RectTransform parent, string name, string text, float size,
                                  Vector2 anchor, Vector2 position, Vector2 boxSize)
    {
        var rect = Rect(parent, name, anchor, position, boxSize);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        return tmp;
    }

    private RectTransform Rect(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private RectTransform Stretch(RectTransform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
