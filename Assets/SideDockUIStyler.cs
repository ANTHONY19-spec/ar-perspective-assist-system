using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SideDockUIStyler : MonoBehaviour
{
    [Header("Ergonomics & Layout")]
    [Tooltip("Keep checked for right-thumb access; uncheck for left-handed layout.")]
    public bool dockOnRightSide = true;
    public float dockWidth = 148f;
    public float buttonHeight = 46f;
    public float buttonSpacing = 10f;
    public float edgeMargin = 18f;

    [Header("Theme Palette")]
    public Color dockBackgroundColor = new Color(0.055f, 0.071f, 0.102f, 0.80f);
    public Color warningBannerColor = new Color(0.62f, 0.11f, 0.11f, 0.92f);
    public Color modalBackdropColor = new Color(0.055f, 0.071f, 0.102f, 0.96f);
    public Color primaryButtonColor = new Color(0.145f, 0.388f, 0.922f, 0.95f);
    public Color secondaryButtonColor = new Color(0.133f, 0.165f, 0.227f, 0.90f);
    public Color subtleActionColor = new Color(0.180f, 0.204f, 0.251f, 0.88f);
    public Color textColor = new Color(0.96f, 0.97f, 0.99f, 1f);

    private RectTransform dockRect;
    private Image dockImage;
    private Transform howToUseModalTransform;
    private Sprite roundedPillSprite;
    private readonly List<Button> dockedButtons = new List<Button>();

    private void Start()
    {
        roundedPillSprite = CreateRoundedRectSprite(64, 64, 18);
        BuildSideDock();
        StyleTopBanners();
        StyleBottomSliderPanels();
        StyleHowToUseModal();
    }

    private void LateUpdate()
    {
        if (howToUseModalTransform != null && howToUseModalTransform.gameObject.activeSelf)
        {
            howToUseModalTransform.SetAsLastSibling();
        }

        if (dockRect == null) return;

        int visibleCount = 0;
        for (int i = 0; i < dockedButtons.Count; i++)
        {
            if (dockedButtons[i] != null && dockedButtons[i].gameObject.activeSelf)
                visibleCount++;
        }

        if (visibleCount == 0)
        {
            dockImage.enabled = false;
            return;
        }

        dockImage.enabled = true;
        float pad = 12f;
        float targetHeight = (visibleCount * buttonHeight) + ((visibleCount - 1) * buttonSpacing) + (pad * 2f);
        dockRect.sizeDelta = new Vector2(dockWidth + (pad * 2f), targetHeight);
    }

    private void BuildSideDock()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject dockObj = new GameObject("SideToolDock", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        dockObj.transform.SetParent(canvas.transform, false);

        dockRect = dockObj.GetComponent<RectTransform>();
        dockImage = dockObj.GetComponent<Image>();
        dockImage.sprite = roundedPillSprite;
        dockImage.type = Image.Type.Sliced;
        dockImage.color = dockBackgroundColor;
        dockImage.raycastTarget = false;

        float anchorX = dockOnRightSide ? 1f : 0f;
        dockRect.anchorMin = new Vector2(anchorX, 0.5f);
        dockRect.anchorMax = new Vector2(anchorX, 0.5f);
        dockRect.pivot = new Vector2(anchorX, 0.5f);
        dockRect.anchoredPosition = new Vector2(dockOnRightSide ? -edgeMargin : edgeMargin, 10f);

        VerticalLayoutGroup layout = dockObj.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = buttonSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button helpBtn = FindButtonByName("HelpButton");
        Button perspectiveBtn = FindButtonByName("PerspectiveModeButton");
        Button traceBtn = FindButtonByName("TraceModeButton");
        Button snapWallBtn = FindButtonByNameOrLabel("SnapWallButton", "Snap Wall", "Snap Paper", "Align Wall");
        Button guideDotsBtn = FindButtonByNameOrLabel("ToggleRoomGridButton", "Guide Dots", "Room");
        Button resetBtn = FindButtonByNameOrLabel("ResetButton", "Reset");
        Button hideUiBtn = FindButtonByNameOrLabel("HideUIButton", "Hide UI", "Hide Controls", "Show UI");

        AddToDock(helpBtn, subtleActionColor, isAccent: false);
        AddToDock(perspectiveBtn, primaryButtonColor, isAccent: true);
        AddToDock(traceBtn, primaryButtonColor, isAccent: true);
        AddToDock(snapWallBtn, secondaryButtonColor, isAccent: false);
        AddToDock(guideDotsBtn, secondaryButtonColor, isAccent: false);
        AddToDock(resetBtn, subtleActionColor, isAccent: false);
        AddToDock(hideUiBtn, subtleActionColor, isAccent: false);

        Transform topBar = canvas.transform.Find("TopBar");
        if (topBar != null && topBar.childCount == 0)
        {
            Image topBarImg = topBar.GetComponent<Image>();
            if (topBarImg != null) topBarImg.enabled = false;
        }
    }

    private void StyleTopBanners()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // Sleek top-center pill for TrackingWarning ("Too dark..." / "Move more slowly...")
        Transform warningTr = FindChildRecursive(canvas.transform, "TrackingWarning");
        if (warningTr != null)
        {
            StyleBannerPill(warningTr, warningBannerColor, topOffset: 28f, height: 68f);
        }

        // Sleek pill directly below for ScanPrompt ("Scan a flat surface, then tap...")
        Transform scanTr = FindChildRecursive(canvas.transform, "ScanPrompt");
        if (scanTr != null)
        {
            StyleBannerPill(scanTr, dockBackgroundColor, topOffset: 106f, height: 58f);
        }
    }

    private void StyleBannerPill(Transform bannerTr, Color bgColor, float topOffset, float height)
    {
        RectTransform rt = bannerTr.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(22f, -(topOffset + height));
            rt.offsetMax = new Vector2(-22f, -topOffset);
            rt.localScale = Vector3.one;
        }

        Image bg = bannerTr.GetComponent<Image>();
        if (bg == null) bg = bannerTr.gameObject.AddComponent<Image>();
        bg.sprite = roundedPillSprite;
        bg.type = Image.Type.Sliced;
        bg.color = bgColor;
        bg.raycastTarget = false;

        TMP_Text[] labels = bannerTr.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform textRt = labels[i].GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.offsetMin = new Vector2(18f, 6f);
            textRt.offsetMax = new Vector2(-18f, -6f);

            labels[i].color = textColor;
            labels[i].alignment = TextAlignmentOptions.Center;
            labels[i].textWrappingMode = TextWrappingModes.Normal;
            labels[i].enableAutoSizing = true;
            labels[i].fontSizeMin = 13f;
            labels[i].fontSizeMax = 17.5f;
        }
    }

    private void StyleHowToUseModal()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        Transform modalTr = FindChildRecursive(canvas.transform, "HowToUseModal");
        if (modalTr == null)
        {
            GameObject modalObj = GameObject.Find("HowToUseModal");
            if (modalObj != null)
            {
                modalObj.transform.SetParent(canvas.transform, false);
                modalTr = modalObj.transform;
            }
        }
        if (modalTr == null) return;

        howToUseModalTransform = modalTr;
        modalTr.SetAsLastSibling();

        RectTransform modalRect = modalTr.GetComponent<RectTransform>();
        if (modalRect != null)
        {
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;
            modalRect.localScale = Vector3.one;
        }

        Image modalImg = modalTr.GetComponent<Image>();
        if (modalImg == null) modalImg = modalTr.gameObject.AddComponent<Image>();
        modalImg.sprite = null;
        modalImg.color = modalBackdropColor;
        modalImg.raycastTarget = true;

        TMP_Text[] texts = modalTr.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].GetComponentInParent<Button>() != null) continue;

            RectTransform tr = texts[i].GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.offsetMin = new Vector2(36f, 155f);
            tr.offsetMax = new Vector2(-36f, -80f);

            texts[i].color = textColor;
            texts[i].alignment = TextAlignmentOptions.Left;
            texts[i].textWrappingMode = TextWrappingModes.Normal;
            texts[i].enableAutoSizing = true;
            texts[i].fontSizeMin = 15f;
            texts[i].fontSizeMax = 25f;
        }

        Button closeBtn = modalTr.GetComponentInChildren<Button>(true);
        if (closeBtn != null)
        {
            RectTransform btnRect = closeBtn.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 54f);
            btnRect.sizeDelta = new Vector2(260f, 54f);

            Image btnImg = closeBtn.GetComponent<Image>();
            if (btnImg == null) btnImg = closeBtn.gameObject.AddComponent<Image>();
            btnImg.sprite = roundedPillSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = primaryButtonColor;
            btnImg.raycastTarget = true;
            closeBtn.targetGraphic = btnImg;

            TMP_Text btnLabel = closeBtn.GetComponentInChildren<TMP_Text>(true);
            if (btnLabel != null)
            {
                RectTransform lblRect = btnLabel.GetComponent<RectTransform>();
                lblRect.anchorMin = Vector2.zero;
                lblRect.anchorMax = Vector2.one;
                lblRect.offsetMin = Vector2.zero;
                lblRect.offsetMax = Vector2.zero;

                btnLabel.color = textColor;
                btnLabel.fontSize = 18f;
                btnLabel.fontStyle = FontStyles.Bold;
                btnLabel.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    private void AddToDock(Button btn, Color bgColor, bool isAccent)
    {
        if (btn == null) return;

        btn.transform.SetParent(dockRect, false);
        dockedButtons.Add(btn);

        LayoutElement le = btn.GetComponent<LayoutElement>();
        if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = dockWidth;
        le.preferredHeight = buttonHeight;
        le.minHeight = buttonHeight;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = roundedPillSprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;
            img.raycastTarget = true;
        }

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        cb.pressedColor = new Color(0.82f, 0.85f, 0.90f, 1f);
        cb.selectedColor = Color.white;
        btn.colors = cb;

        TMP_Text label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = textColor;
            label.fontSize = 15.5f;
            label.fontStyle = isAccent ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }
    }

    private void StyleBottomSliderPanels()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        string[] panelNames = { "ControlPanel", "TraceControlPanel" };
        float sideReservation = dockWidth + (edgeMargin * 2f) + 28f;

        foreach (string panelName in panelNames)
        {
            Transform panelTr = FindChildRecursive(canvas.transform, panelName);
            if (panelTr == null) continue;

            RectTransform rt = panelTr.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);

                float leftPad = dockOnRightSide ? edgeMargin : sideReservation;
                float rightPad = dockOnRightSide ? -sideReservation : -edgeMargin;
                rt.offsetMin = new Vector2(leftPad, edgeMargin + 6f);
                rt.offsetMax = new Vector2(rightPad, 256f);
            }

            Image bg = panelTr.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = roundedPillSprite;
                bg.type = Image.Type.Sliced;
                bg.color = dockBackgroundColor;
            }
        }
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == targetName) return child;
            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null) return nested;
        }
        return null;
    }

    private Button FindButtonByName(string objName)
    {
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allButtons.Length; i++)
        {
            if (allButtons[i].name == objName)
                return allButtons[i];
        }
        return null;
    }

    private Button FindButtonByNameOrLabel(string objName, params string[] labelKeywords)
    {
        Button exact = FindButtonByName(objName);
        if (exact != null) return exact;

        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allButtons.Length; i++)
        {
            TMP_Text txt = allButtons[i].GetComponentInChildren<TMP_Text>(true);
            if (txt == null) continue;

            for (int k = 0; k < labelKeywords.Length; k++)
            {
                if (txt.text.IndexOf(labelKeywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return allButtons[i];
            }
        }
        return null;
    }

    private Sprite CreateRoundedRectSprite(int width, int height, int radius)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = 1f;
                int dx = 0;
                int dy = 0;

                if (x < radius && y < radius) { dx = radius - x; dy = radius - y; }
                else if (x >= width - radius && y < radius) { dx = x - (width - radius - 1); dy = radius - y; }
                else if (x < radius && y >= height - radius) { dx = radius - x; dy = y - (height - radius - 1); }
                else if (x >= width - radius && y >= height - radius) { dx = x - (width - radius - 1); dy = y - (height - radius - 1); }

                if (dx > 0 || dy > 0)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Clamp01(radius - dist + 0.5f);
                }

                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
// END OF SIDEDOCKUISTYLER