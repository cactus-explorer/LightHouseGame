using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a simple placeholder HUD in code, so nothing has to be laid out by hand:
///  - top left: ships saved / wrecked
///  - top centre: current chord and a bar that empties as the turn runs out
///  - bottom: the 5 note keys, which flash when pressed
///  - centre: win / lose message
/// Swap it for diegetic in-world UI later if there's time.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] private ShipManager shipManager;
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private GameManager gameManager;

    [Header("Look")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.08f, 0.05f, 0.75f);
    [SerializeField] private Color textColor = new Color(0.98f, 0.93f, 0.8f);
    [SerializeField] private Color timerColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color keyIdleColor = new Color(0.2f, 0.17f, 0.12f, 0.85f);
    [SerializeField] private Color keyFlashColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float keyFlashTime = 0.25f;

    private TextMeshProUGUI statsText;
    private TextMeshProUGUI chordText;
    private RectTransform timerFill;
    private Image[] keyImages;
    private float[] keyFlash;
    private GameObject gameOverPanel;
    private TextMeshProUGUI gameOverText;

    void Start()
    {
        if (shipManager == null) shipManager = FindAnyObjectByType<ShipManager>();
        if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        BuildUI();

        NoteInputHandler.OnNoteKeyPressed += FlashKey;
        if (gameManager != null) gameManager.OnGameOver += ShowGameOver;
    }

    void OnDestroy()
    {
        NoteInputHandler.OnNoteKeyPressed -= FlashKey;
        if (gameManager != null) gameManager.OnGameOver -= ShowGameOver;
    }

    void Update()
    {
        if (shipManager != null)
        {
            string goal = gameManager != null ? $"/{gameManager.ShipsToSave}" : "";
            string limit = gameManager != null ? $"/{gameManager.MaxWrecks}" : "";
            statsText.text = $"Saved  {shipManager.SavedCount}{goal}\nWrecked  {shipManager.WreckedCount}{limit}";
        }

        if (timeManager != null && timeManager.IsPlaying)
        {
            chordText.text = $"Chord {timeManager.CurrentNote}   <size=70%>{timeManager.TimeUntilNextChord:0.0}s</size>";
            timerFill.anchorMax = new Vector2(1f - timeManager.ChordProgress, 1f);
        }
        else
        {
            chordText.text = "No music playing";
        }

        for (int i = 0; i < keyImages.Length; i++)
        {
            keyFlash[i] = Mathf.Max(0f, keyFlash[i] - Time.deltaTime / keyFlashTime);
            keyImages[i].color = Color.Lerp(keyIdleColor, keyFlashColor, keyFlash[i]);
        }
    }

    private void FlashKey(int index)
    {
        if (keyFlash != null && index >= 0 && index < keyFlash.Length) keyFlash[index] = 1f;
    }

    private void ShowGameOver(bool won)
    {
        gameOverPanel.SetActive(true);
        gameOverText.text = won
            ? "All ships home safe!\n<size=50%>Press R to play again</size>"
            : "Too many ships lost...\n<size=50%>Press R to try again</size>";
    }

    // ------------------------------------------------------------------ Building

    private void BuildUI()
    {
        var canvasObj = new GameObject("HUD Canvas");
        canvasObj.transform.SetParent(transform, false);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = canvasObj.GetComponent<RectTransform>();

        // Stats, top left
        var statsPanel = Panel(root, "Stats", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(300, 110));
        statsText = Text(statsPanel, "StatsText", 34, TextAlignmentOptions.Left);
        statsText.margin = new Vector4(20, 0, 20, 0);

        // Chord + turn timer, top centre
        var timerPanel = Panel(root, "Timer", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(420, 110));
        chordText = Text(timerPanel, "ChordText", 40, TextAlignmentOptions.Top);
        chordText.margin = new Vector4(0, 10, 0, 0);

        var barBg = Rect(timerPanel, "TimerBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 18), new Vector2(-40, 18));
        barBg.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        timerFill = Rect(barBg, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        timerFill.pivot = new Vector2(0, 0.5f);
        timerFill.gameObject.AddComponent<Image>().color = timerColor;

        // Note keys, bottom centre
        int count = ShipControls.KeyNames.Length;
        keyImages = new Image[count];
        keyFlash = new float[count];
        const float keyWidth = 120f, gap = 16f;
        float total = count * keyWidth + (count - 1) * gap;
        for (int i = 0; i < count; i++)
        {
            float x = -total / 2f + keyWidth / 2f + i * (keyWidth + gap);
            var key = Panel(root, $"Key_{ShipControls.KeyNames[i]}", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                            new Vector2(x, 30), new Vector2(keyWidth, 90));
            key.pivot = new Vector2(0.5f, 0);
            keyImages[i] = key.GetComponent<Image>();
            keyImages[i].color = keyIdleColor;
            Text(key, "Label", 36, TextAlignmentOptions.Center).text = ShipControls.KeyNames[i];
        }

        // Game over, centre (hidden until the level ends)
        var overRect = Panel(root, "GameOver", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 260));
        overRect.pivot = new Vector2(0.5f, 0.5f);
        gameOverText = Text(overRect, "GameOverText", 64, TextAlignmentOptions.Center);
        gameOverPanel = overRect.gameObject;
        gameOverPanel.SetActive(false);
    }

    private RectTransform Rect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                               Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 position, Vector2 size)
    {
        var rect = Rect(parent, name, anchorMin, anchorMax, position, size);
        rect.gameObject.AddComponent<Image>().color = panelColor;
        return rect;
    }

    private TextMeshProUGUI Text(RectTransform parent, string name, float size, TextAlignmentOptions align)
    {
        var rect = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        return tmp;
    }
}
