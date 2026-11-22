using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Small helpers for building placeholder UI in code (pause menu, tutorial letter).</summary>
public static class UIBuilder
{
    public static readonly Color TextColor = new Color(0.98f, 0.93f, 0.8f);
    public static readonly Color PanelColor = new Color(0.1f, 0.08f, 0.05f, 0.92f);
    public static readonly Color ButtonColor = new Color(0.25f, 0.2f, 0.14f, 1f);
    public static readonly Color ButtonHoverColor = new Color(0.55f, 0.42f, 0.2f, 1f);

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static RectTransform Canvas(Transform parent, string name, int sortingOrder)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        obj.AddComponent<GraphicRaycaster>();
        return obj.GetComponent<RectTransform>();
    }

    public static RectTransform Rect(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform parent, string name)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image Fill(RectTransform rect, Color color, bool blocksClicks = true)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksClicks;
        return image;
    }

    public static TextMeshProUGUI Label(RectTransform rect, string text, float size,
                                        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = TextColor;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static UnityEngine.UI.Button MakeButton(RectTransform parent, string text, Vector2 position, Vector2 size,
                                                   UnityAction onClick, float fontSize = 40)
    {
        var rect = Rect(parent, $"Button_{text}", position, size);
        Fill(rect, Color.white); // tinted by the colour block below

        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        var colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.selectedColor = ButtonColor;
        colors.pressedColor = ButtonHoverColor * 0.8f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Label(Stretch(rect, "Label"), text, fontSize);
        return button;
    }
}
