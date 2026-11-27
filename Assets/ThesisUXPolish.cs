using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ThesisUXPolish : MonoBehaviour
{
    [Header("Prompt & Tracking Warning UI")]
    public GameObject scanPrompt;
    public GameObject trackingWarningBanner;
    public TMP_Text trackingWarningText;

    [Header("Grid & Panel References")]
    public GameObject arControlPanel;
    public GameObject traceControlPanel;

    [Header("One-Screen How To Use Guide")]
    public GameObject howToUseModal;
    public Button openHelpButton;
    public Button closeHelpButton;

    private float badTrackingTimer = 0f;

    private void Start()
    {
        // Keep phone screen awake while drawing or demonstrating
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (openHelpButton != null)
            openHelpButton.onClick.AddListener(ShowHowToUse);

        if (closeHelpButton != null)
            closeHelpButton.onClick.AddListener(HideHowToUse);

        if (trackingWarningBanner != null)
            trackingWarningBanner.SetActive(false);

        // Show the 1-screen guide automatically at launch (tap Got It to dismiss)
        if (howToUseModal != null)
            howToUseModal.SetActive(true);
    }

    private void Update()
    {
        UpdateTrackingWarning();
        EnforcePromptVisibility();
    }

    private void UpdateTrackingWarning()
    {
        if (trackingWarningBanner == null || trackingWarningText == null) return;

        string warning = GetTrackingWarningMessage();
        if (!string.IsNullOrEmpty(warning))
        {
            badTrackingTimer += Time.deltaTime;
            // Wait 0.6s so brief camera movements don't flicker the banner
            if (badTrackingTimer >= 0.6f)
            {
                trackingWarningText.text = warning;
                if (!trackingWarningBanner.activeSelf)
                    trackingWarningBanner.SetActive(true);
            }
        }
        else
        {
            badTrackingTimer = 0f;
            if (trackingWarningBanner.activeSelf)
                trackingWarningBanner.SetActive(false);
        }
    }

    private string GetTrackingWarningMessage()
    {
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
            return null;

        switch (ARSession.notTrackingReason)
        {
            case NotTrackingReason.InsufficientLight:
                return "Too dark — turn on a light or move to a brighter area";
            case NotTrackingReason.ExcessiveMotion:
                return "Moving too fast — move your phone slowly";
            case NotTrackingReason.InsufficientFeatures:
                return "Low surface detail — include the paper edge or desk in view";
            case NotTrackingReason.Relocalizing:
                return "Re-aligning tracking — hold steady for a moment";
            default:
                return null;
        }
    }

    private void EnforcePromptVisibility()
    {
        if (scanPrompt == null) return;

        // Hide prompt while How-To-Use modal is open
        if (howToUseModal != null && howToUseModal.activeSelf)
        {
            if (scanPrompt.activeSelf) scanPrompt.SetActive(false);
            return;
        }

        // Check if either 3D Perspective Grid or Anchored Trace Image is currently placed
        PerspectiveGridGenerator activeGrid = FindAnyObjectByType<PerspectiveGridGenerator>(FindObjectsInactive.Exclude);
        GameObject anchoredTrace = GameObject.Find("AnchoredTraceRoot");

        bool gridPlaced = (activeGrid != null && activeGrid.gameObject.activeInHierarchy)
                          || (arControlPanel != null && arControlPanel.activeInHierarchy);
        bool tracePlaced = (anchoredTrace != null && anchoredTrace.activeInHierarchy);
        bool inTraceMode = (traceControlPanel != null && traceControlPanel.activeInHierarchy);

        bool shouldShowPrompt = !gridPlaced && !tracePlaced && !inTraceMode;
        if (scanPrompt.activeSelf != shouldShowPrompt)
            scanPrompt.SetActive(shouldShowPrompt);
    }

    public void ShowHowToUse()
    {
        if (howToUseModal != null) howToUseModal.SetActive(true);
    }

    public void HideHowToUse()
    {
        if (howToUseModal != null) howToUseModal.SetActive(false);
    }
}
// END OF THESISUXPOLISH