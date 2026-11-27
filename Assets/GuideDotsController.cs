using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class GuideDotsController : MonoBehaviour
{
    [Header("UI & Visual Settings")]
    public Button guideDotsButton;
    [Range(0.05f, 1f)]
    public float scanningDotOpacity = 0.25f;
    public bool autoHideOnPlacement = true;

    private ARPlaneManager planeManager;
    private TMP_Text buttonLabel;
    private bool dotsVisible = true;
    private bool wasContentPlaced = false;

    private void Awake()
    {
        planeManager = FindAnyObjectByType<ARPlaneManager>();

        if (guideDotsButton != null)
        {
            buttonLabel = guideDotsButton.GetComponentInChildren<TMP_Text>(true);
            guideDotsButton.onClick.RemoveAllListeners();
            guideDotsButton.onClick.AddListener(ToggleGuideDots);
        }

        UpdateButtonText();
    }

    private void OnEnable()
    {
        if (planeManager != null)
            planeManager.planesChanged += OnPlanesChanged;
    }

    private void OnDisable()
    {
        if (planeManager != null)
            planeManager.planesChanged -= OnPlanesChanged;
    }

    private void Update()
    {
        if (!autoHideOnPlacement) return;

        PerspectiveGridGenerator activeGrid = FindAnyObjectByType<PerspectiveGridGenerator>(FindObjectsInactive.Exclude);
        GameObject anchoredTrace = GameObject.Find("AnchoredTraceRoot");

        bool isContentPlaced = (activeGrid != null && activeGrid.gameObject.activeInHierarchy)
                               || (anchoredTrace != null && anchoredTrace.activeInHierarchy);

        if (isContentPlaced && !wasContentPlaced)
        {
            SetDotsVisible(false);
        }
        else if (!isContentPlaced && wasContentPlaced)
        {
            SetDotsVisible(true);
        }

        wasContentPlaced = isContentPlaced;
    }

    public void ToggleGuideDots()
    {
        SetDotsVisible(!dotsVisible);
    }

    public void SetDotsVisible(bool visible)
    {
        dotsVisible = visible;
        ApplyDotsVisibility();
        UpdateButtonText();
    }

    private void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        foreach (var plane in args.added)
            ConfigurePlaneVisual(plane);

        foreach (var plane in args.updated)
            ConfigurePlaneVisual(plane);
    }

    private void ConfigurePlaneVisual(ARPlane plane)
    {
        if (plane == null) return;

        plane.gameObject.SetActive(dotsVisible);

        MeshRenderer mr = plane.GetComponent<MeshRenderer>();
        if (mr != null && mr.material != null && mr.material.HasProperty("_Color"))
        {
            Color c = mr.material.color;
            c.a = scanningDotOpacity;
            mr.material.color = c;
        }
    }

    private void ApplyDotsVisibility()
    {
        if (planeManager != null)
        {
            foreach (ARPlane plane in planeManager.trackables)
                ConfigurePlaneVisual(plane);
        }

        RoomSurfaceGrid roomGrid = FindAnyObjectByType<RoomSurfaceGrid>(FindObjectsInactive.Include);
        if (roomGrid != null)
            roomGrid.enabled = dotsVisible;
    }

    private void UpdateButtonText()
    {
        if (buttonLabel != null)
            buttonLabel.text = dotsVisible ? "Guide Dots: On" : "Guide Dots: Off";
    }
}
// END OF GUIDEDOTSCONTROLLER