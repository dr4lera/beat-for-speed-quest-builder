using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuestGui
{
    public Transform root;
    public Text title, status, hud;
    public GameObject hudRoot;
    RectTransform body;
    Material material;
    Font font;
    readonly List<GameObject> rows = new List<GameObject>();
    static readonly Color Accent = new Color(.1f, .85f, .88f);
    public QuestGui(Transform panel, Transform origin, Material material, Action pause)
    {
        this.material = material; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); root = panel;
        var canvas = Canvas(panel, new Vector2(900, 950), .0027f);
        Image("Panel", canvas, Vector2.zero, new Vector2(900, 950), new Color(.025f, .04f, .075f, .97f));
        Image("Accent", canvas, new Vector2(0, 466), new Vector2(900, 8), Accent);
        title = Label("BEAT FOR SPEED", canvas, new Vector2(0, 390), new Vector2(820, 65), 40, Color.white);
        status = Label("", canvas, new Vector2(0, 304), new Vector2(810, 85), 23, new Color(.65f, .77f, .87f));
        body = Rect("Body", canvas, Vector2.zero, new Vector2(840, 540));
        Label("Steady gaze to select  ·  Trigger or pinch to click", canvas, new Vector2(0, -445), new Vector2(820, 28), 19, new Color(.45f, .62f, .72f));
        hudRoot = new GameObject("Ride HUD"); hudRoot.transform.SetParent(origin, false);
        hudRoot.transform.localPosition = new Vector3(0, 2.12f, 3.2f);
        var hc = Canvas(hudRoot.transform, new Vector2(700, 110), .0025f);
        Image("HUD panel", hc, Vector2.zero, new Vector2(700, 110), new Color(.015f, .03f, .05f, .7f));
        hud = Label("", hc, new Vector2(40, 0), new Vector2(540, 100), 25, Color.white);
        var pb = UiButton("Pause", hc, new Vector2(-275, 0), new Vector2(130, 74), pause);
        pb.GetComponent<Image>().color = new Color(.06f, .22f, .27f);
        hudRoot.SetActive(false);
    }
    RectTransform Canvas(Transform parent, Vector2 size, float scale)
    {
        var rect = Rect("Canvas", parent, Vector2.zero, size); rect.localScale = Vector3.one * scale;
        var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 100;
        rect.gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2;
        return rect;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.sizeDelta = size; rt.anchoredPosition = position; return rt;
    }
    Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var rt = Rect(name, parent, position, size); var image = rt.gameObject.AddComponent<Image>();
        image.color = color; image.material = material; image.raycastTarget = false; return image;
    }
    Text Label(string text, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var rt = Rect("Label", parent, position, size); var label = rt.gameObject.AddComponent<Text>();
        label.font = font; label.text = text; label.fontSize = fontSize; label.color = color;
        label.alignment = TextAnchor.MiddleCenter; label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate; label.raycastTarget = false; label.material = material; return label;
    }
    GameObject UiButton(string text, Transform parent, Vector2 position, Vector2 size, Action action)
    {
        var image = Image(text, parent, position, size, new Color(.075f, .12f, .19f));
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = Accent; colors.pressedColor = new Color(.05f, .45f, .55f); button.colors = colors;
        var menuItem = image.gameObject.AddComponent<QuestMenuItem>(); menuItem.action = action; menuItem.graphic = image;
        var progress = Image("Dwell progress", image.transform, new Vector2(0, -size.y / 2 + 2), new Vector2(0, 4), Accent);
        menuItem.progressBar = progress.rectTransform; menuItem.buttonWidth = size.x;
        image.gameObject.layer = 30; var collider = image.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(size.x, size.y, 8);
        Label(text, image.transform, Vector2.zero, size - new Vector2(25, 6), 27, Color.white);
        return image.gameObject;
    }
    public void Clear(string heading, string info)
    {
        foreach (var row in rows) if (row != null) UnityEngine.Object.Destroy(row); rows.Clear();
        title.text = heading; status.text = info;
    }
    public void Button(string text, float y, Action action, bool accent = false)
    {
        var go = UiButton(text, body, new Vector2(0, y), new Vector2(800, 70), action); rows.Add(go);
        if (accent) go.GetComponent<Image>().color = new Color(.035f, .32f, .36f);
    }
    public void Note(string text, float y)
    { rows.Add(Label(text, body, new Vector2(0, y), new Vector2(800, 140), 24, new Color(.7f, .82f, .89f)).gameObject); }
}
