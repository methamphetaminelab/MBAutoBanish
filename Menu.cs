using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Inventory__Items__Pickups.Items;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AutoBanish;

public class Menu : MonoBehaviour
{
    public Menu(IntPtr ptr) : base(ptr) { }

    static readonly Color Accent = new(1f, 0.8f, 0.32f);
    static readonly Color PanelBg = new(0.075f, 0.075f, 0.1f, 0.98f);
    static readonly Color CardBg = new(0.2f, 0.2f, 0.26f);
    static readonly Color CardMarked = new(0.6f, 0.11f, 0.14f);
    static readonly Color TabIdle = new(0.22f, 0.22f, 0.28f);
    static readonly Color Muted = new(0.72f, 0.72f, 0.78f);
    static readonly Vector2 Zero = Vector2.zero, One = Vector2.one, Half = new(0.5f, 0.5f), Top = new(0.5f, 1f);

    sealed class Tab
    {
        public readonly string Category, Title, Hint;
        public readonly Color Frame;
        public Tab(string category, string title, Color frame, string hint) => (Category, Title, Frame, Hint) = (category, title, frame, hint);
    }

    static readonly Tab[] Tabs =
    {
        new(Plugin.Items, "Items", Color.clear,
            "Click a card to mark it. Marked items are banished automatically when they drop from a chest."),
        new(Plugin.Weapons, "Weapons", new(0.5f, 0.62f, 0.78f),
            "Marked weapons are banished automatically when a level-up offers them. That level-up is spent on the banish, same as banishing by hand."),
        new(Plugin.Tomes, "Tomes", new(0.62f, 0.48f, 0.82f),
            "Marked tomes are banished automatically when a level-up offers them. That level-up is spent on the banish, same as banishing by hand."),
    };

    GameObject root;
    TMP_FontAsset font;
    TextMeshProUGUI hint;
    ScrollRect scroll;
    readonly RectTransform[] contents = new RectTransform[Tabs.Length];
    readonly Image[] tabBgs = new Image[Tabs.Length];
    readonly TextMeshProUGUI[] tabTexts = new TextMeshProUGUI[Tabs.Length];
    int tab, cards;

    void OnGUI()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown || e.keyCode != Plugin.MenuKey.Value) return;
        e.Use();
        if (root == null || cards == 0) Build();
        root.SetActive(!root.activeSelf);
    }

    void Build()
    {
        if (root != null) Destroy(root);
        font = FindFont();

        root = new GameObject("AutoBanishMenu");
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var backdrop = Box("Backdrop", root.transform, Zero, One, Zero, Zero, new Color(0, 0, 0, 0.72f));
        var border = Box("Border", backdrop, Half, Half, new(-520, -400), new(520, 400), Accent);
        var panel = Box("Panel", border, Zero, One, new(2, 2), new(-2, -2), PanelBg);

        Label(Node("Title", panel, new(0, 1), One, new(28, -66), new(-260, -18)), "AutoBanish", 38, TextAlignmentOptions.Left, Accent)
            .fontStyle = FontStyles.Bold;
        hint = Label(Node("Hint", panel, new(0, 1), One, new(28, -122), new(-28, -70)), "", 19, TextAlignmentOptions.TopLeft, Muted);
        hint.enableAutoSizing = true;
        hint.fontSizeMin = 14;
        hint.fontSizeMax = 19;

        var close = Box("Close", panel, One, One, new(-224, -64), new(-28, -20), new(0.3f, 0.3f, 0.36f));
        Label(Node("Text", close, Zero, One, Zero, Zero), $"Close  [{Plugin.MenuKey.Value}]", 20, TextAlignmentOptions.Center, Color.white);
        AddButton(close, close.GetComponent<Image>(), () => root.SetActive(false));

        Box("Divider", panel, new(0, 1), One, new(28, -188), new(-28, -186), new(1, 1, 1, 0.08f));

        var area = Node("Scroll", panel, Zero, One, new(28, 24), new(-28, -200));
        var viewport = Box("Viewport", area, Zero, One, Zero, new(-16, 0), new(0, 0, 0, 0.25f));
        viewport.gameObject.AddComponent<RectMask2D>();

        var bar = Box("Scrollbar", area, new(1, 0), One, new(-8, 0), Zero, new(1, 1, 1, 0.06f));
        var handle = Box("Handle", bar, Zero, One, Zero, Zero, new(Accent.r, Accent.g, Accent.b, 0.85f));
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        scroll.verticalScrollbar = scrollbar;

        cards = 0;
        var data = DataManager.Instance;
        var loaded = data != null && data.unsortedItems != null;
        if (!loaded) Plugin.Logger.LogError("Game data is not loaded yet, menu is empty");

        for (var i = 0; i < Tabs.Length; i++)
        {
            var index = i;
            var tabRect = Box("Tab", panel, new(0, 1), new(0, 1), new(28 + i * 232, -176), new(250 + i * 232, -132), TabIdle);
            tabBgs[i] = tabRect.GetComponent<Image>();
            tabTexts[i] = Label(Node("Text", tabRect, Zero, One, Zero, Zero), "", 21, TextAlignmentOptions.Center, Color.white);
            tabTexts[i].fontStyle = FontStyles.Bold;
            AddButton(tabRect, tabBgs[i], () => SelectTab(index));

            contents[i] = Grid(viewport);
            if (loaded)
                foreach (var e in Entries(data, Tabs[i])) Card(contents[i], Tabs[i], e.id, e.name, e.icon, e.frame);
            UpdateTabText(i);
        }

        SelectTab(tab);
        root.SetActive(false);
    }

    static IEnumerable<(string id, string name, Texture icon, Color frame)> Entries(DataManager data, Tab t)
    {
        if (t.Category == Plugin.Items)
        {
            var items = data.unsortedItems;
            return Enumerable.Range(0, items.Count).Select(i => items[i])
                .Where(it => it.rarity != EItemRarity.Quest)
                .OrderBy(it => it.rarity).ThenBy(it => it.GetName())
                .Select(it => (it.eItem.ToString(), it.GetName(), it.GetIcon(), it.GetColor()));
        }
        if (t.Category == Plugin.Weapons)
        {
            var weapons = data.unsortedWeapons;
            return Enumerable.Range(0, weapons.Count).Select(i => weapons[i])
                .Select(w => (w.eWeapon.ToString(), w.GetName(), w.GetIcon(), t.Frame)).OrderBy(x => x.Item2);
        }
        var tomes = data.unsortedTomes;
        return Enumerable.Range(0, tomes.Count).Select(i => tomes[i])
            .Select(x => (x.eTome.ToString(), x.GetName(), x.GetIcon(), t.Frame)).OrderBy(x => x.Item2);
    }

    void SelectTab(int i)
    {
        tab = i;
        for (var j = 0; j < Tabs.Length; j++)
        {
            contents[j].gameObject.SetActive(j == i);
            tabBgs[j].color = j == i ? Accent : TabIdle;
            tabTexts[j].color = j == i ? PanelBg : Color.white;
        }
        hint.text = Tabs[i].Hint;
        scroll.content = contents[i];
        scroll.verticalNormalizedPosition = 1;
    }

    void UpdateTabText(int i) => tabTexts[i].text = $"{Tabs[i].Title} ({Plugin.Marked[Tabs[i].Category].Count})";

    static RectTransform Grid(Transform viewport)
    {
        var content = Node("Content", viewport, new(0, 1), One, Zero, Zero);
        content.pivot = Top;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(108, 140);
        grid.spacing = new Vector2(10, 10);
        grid.padding.top = grid.padding.bottom = 10;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;
        grid.childAlignment = TextAnchor.UpperCenter;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    void Card(Transform parent, Tab t, string id, string title, Texture iconTexture, Color frame)
    {
        frame.a = 1;
        var card = Box("Card", parent, Zero, Zero, Zero, Zero, frame);
        var inner = Box("Inner", card, Zero, One, new(3, 3), new(-3, -3), CardBg);
        var bg = inner.GetComponent<Image>();
        var icon = Node("Icon", inner, Top, Top, new(-30, -86), new(30, -26)).gameObject.AddComponent<RawImage>();
        icon.texture = iconTexture;
        icon.raycastTarget = false;
        var name = Label(Node("Name", inner, Zero, new(1, 0), new(4, 4), new(-4, 50)), title, 15, TextAlignmentOptions.Center, Color.white);
        name.enableAutoSizing = true;
        name.fontSizeMin = 10;
        name.fontSizeMax = 15;
        var badge = Box("Badge", inner, new(0, 1), One, new(0, -22), Zero, new(0.86f, 0.14f, 0.17f));
        badge.GetComponent<Image>().raycastTarget = false;
        Label(Node("Text", badge, Zero, One, Zero, Zero), "BANISH", 13, TextAlignmentOptions.Center, Color.white).fontStyle = FontStyles.Bold;

        void Paint()
        {
            var on = Plugin.Marked[t.Category].Contains(id);
            bg.color = on ? CardMarked : CardBg;
            icon.color = on ? new Color(1, 1, 1, 0.45f) : Color.white;
            badge.gameObject.SetActive(on);
        }

        Paint();
        AddButton(card, bg, () =>
        {
            Plugin.Toggle(t.Category, id);
            Paint();
            UpdateTabText(Array.IndexOf(Tabs, t));
        });
        cards++;
    }

    static void AddButton(RectTransform rect, Graphic target, Action onClick)
    {
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        var colors = button.colors;
        colors.normalColor = colors.selectedColor = new Color(0.82f, 0.82f, 0.82f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.62f, 0.62f, 0.62f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
    }

    static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    static RectTransform Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var rect = Node(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    TextMeshProUGUI Label(RectTransform rect, string text, float size, TextAlignmentOptions align, Color color)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = align;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    static TMP_FontAsset FindFont()
    {
        foreach (var o in Object.FindObjectsOfType(Il2CppType.Of<TextMeshProUGUI>()))
        {
            var f = o.Cast<TextMeshProUGUI>().font;
            if (f != null && f.HasCharacter('Ж', true)) return f;
        }
        Plugin.Logger.LogWarning("No game font with Cyrillic glyphs found, using the TMP default");
        return TMP_Settings.defaultFontAsset;
    }
}
