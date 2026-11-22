using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A letter from the previous lighthouse keeper that explains the game, shown when the level starts.
/// The game (and the music, and so the turn timer) stays paused until the letter is closed.
///  - Enter / Space / Next turns the page, Backspace / Back goes back, Esc skips.
///  - Shown once per player by default (remembered in PlayerPrefs). Always shown in the editor unless turned off.
///  - Can be reopened from the pause menu.
/// Edit the pages in the Inspector; {goal} and {wrecks} are replaced with the level's numbers.
/// </summary>
public class TutorialLetter : MonoBehaviour
{
    private const string SeenKey = "TutorialLetterSeen";

    public static bool IsOpen { get; private set; }

    [SerializeField] private GameManager gameManager;
    [SerializeField] private bool showAtStart = true;
    [Tooltip("Only show it automatically the first time someone plays.")]
    [SerializeField] private bool onlyFirstTime = true;
    [Tooltip("Ignore 'Only First Time' in the editor, so it's easy to test.")]
    [SerializeField] private bool alwaysShowInEditor = true;

    [SerializeField, TextArea(4, 10)] private string[] pages =
    {
        "To whoever takes up the lamp after me,\n\n" +
        "This old tower is no ordinary lighthouse. Its bells carry over the water, and the ships out there " +
        "have learned to steer by them. Ring well and they'll make it home.",

        "The sea keeps time with the music. Every time the chord changes, every ship sails one step east.\n\n" +
        "Get a ship off the eastern edge of the chart and it's safe in harbour.",

        "Each ship answers to two bells, and the keys float above its mast:\n" +
        "▲ steers it up and east, ▼ steers it down and east.\n\n" +
        "Ring before the chord changes. If you ring twice, the last bell is the one they hear. " +
        "The arrow on the water shows where each ship will go.",

        "Mind the hazards.\n\n" +
        "Rocks don't move, but they'll crack a hull.\n" +
        "The beast marks the water red where it means to strike next.\n" +
        "Grey clouds mean a storm will break there on the next chord.",

        "Galleons can take a few knocks. Sloops are quick, two steps a turn, but fragile.\n\n" +
        "Bring {goal} ships home. Lose {wrecks} and the night is lost.\n\n" +
        "Good luck, keeper.\n<i>— the old keeper</i>",
    };

    [Header("Look")]
    [SerializeField] private Color paperColor = new Color(0.93f, 0.87f, 0.72f);
    [SerializeField] private Color inkColor = new Color(0.22f, 0.15f, 0.08f);
    [SerializeField] private AudioClip pageTurnSound;

    private GameObject root;
    private TextMeshProUGUI bodyText;
    private TextMeshProUGUI pageText;
    private GameObject backButton;
    private TextMeshProUGUI nextLabel;
    private AudioSource audioSource;
    private int page;
    private int openedFrame = -1;

    void Start()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        UIBuilder.EnsureEventSystem();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.ignoreListenerPause = true;
        Build();

        bool seen = PlayerPrefs.GetInt(SeenKey, 0) == 1;
        bool force = alwaysShowInEditor && Application.isEditor;
        if (showAtStart && (!onlyFirstTime || !seen || force)) Open();
    }

    void OnDestroy()
    {
        if (IsOpen) IsOpen = false;
    }

    void Update()
    {
        if (!IsOpen || Time.frameCount == openedFrame) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame) Close();
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                 keyboard.spaceKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) Next();
        else if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) Back();
    }

    /// <summary>LevelLoader turns the letter on for the levels that want it.</summary>
    public void SetShowAtStart(bool show) => showAtStart = show;

    public void Open()
    {
        if (IsOpen || pages == null || pages.Length == 0) return;
        IsOpen = true;
        openedFrame = Time.frameCount;
        page = 0;
        root.SetActive(true);
        GamePause.Set(true);
        ShowPage();
    }

    public void Close()
    {
        IsOpen = false;
        root.SetActive(false);
        GamePause.Set(false);
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    private void Next()
    {
        if (page >= pages.Length - 1) { Close(); return; }
        page++;
        PlayTurn();
        ShowPage();
    }

    private void Back()
    {
        if (page == 0) return;
        page--;
        PlayTurn();
        ShowPage();
    }

    private void PlayTurn()
    {
        if (pageTurnSound != null) audioSource.PlayOneShot(pageTurnSound);
    }

    private void ShowPage()
    {
        string goal = gameManager != null ? gameManager.ShipsToSave.ToString() : "the";
        string wrecks = gameManager != null ? gameManager.MaxWrecks.ToString() : "too many";
        bodyText.text = pages[page].Replace("{goal}", goal).Replace("{wrecks}", wrecks);
        pageText.text = $"{page + 1} / {pages.Length}";
        backButton.SetActive(page > 0);
        nextLabel.text = page >= pages.Length - 1 ? "Set sail" : "Next";
    }

    [ContextMenu("Forget That The Letter Was Seen")]
    private void ResetSeen()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        Debug.Log("Tutorial letter will show again.");
    }

    private void Build()
    {
        RectTransform canvas = UIBuilder.Canvas(transform, "Letter Canvas", 60);
        var dim = UIBuilder.Stretch(canvas, "Dim");
        UIBuilder.Fill(dim, new Color(0f, 0f, 0f, 0.55f));
        root = dim.gameObject;

        var paper = UIBuilder.Rect(dim, "Paper", Vector2.zero, new Vector2(1100, 760));
        UIBuilder.Fill(paper, paperColor);
        paper.localRotation = Quaternion.Euler(0, 0, -1.2f); // slightly askew, like it was dropped on the desk

        var body = UIBuilder.Rect(paper, "Body", new Vector2(0, 40), new Vector2(940, 560));
        bodyText = UIBuilder.Label(body, "", 38, TextAlignmentOptions.TopLeft);
        bodyText.color = inkColor;
        bodyText.fontStyle = FontStyles.Italic;
        bodyText.lineSpacing = 12f;

        pageText = UIBuilder.Label(UIBuilder.Rect(paper, "Page", new Vector2(0, -320), new Vector2(200, 50)), "", 28);
        pageText.color = new Color(inkColor.r, inkColor.g, inkColor.b, 0.6f);

        var buttonSize = new Vector2(240, 76);
        backButton = UIBuilder.MakeButton(paper, "Back", new Vector2(-360, -320), buttonSize, Back, 34).gameObject;
        var next = UIBuilder.MakeButton(paper, "Next", new Vector2(360, -320), buttonSize, Next, 34);
        nextLabel = next.GetComponentInChildren<TextMeshProUGUI>();

        UIBuilder.Label(UIBuilder.Rect(dim, "Hint", new Vector2(0, -470), new Vector2(900, 50)),
                        "Enter: next   ·   Backspace: back   ·   Esc: skip", 26);

        root.SetActive(false);
    }
}
