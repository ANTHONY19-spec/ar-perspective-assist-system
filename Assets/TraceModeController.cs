using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class TraceModeController : MonoBehaviour
{
    private static readonly int RemoveWhiteId = Shader.PropertyToID("_RemoveWhite");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Existing AR References")]
    public PerspectiveAssistManager perspectiveManager;
    public Canvas parentCanvas;
    public GameObject perspectiveModeButton;
    public ARRaycastManager raycastManager;

    [Header("Trace Overlay (Legacy Reference for Material)")]
    public RawImage traceImage;
    public Texture2D[] sampleSketches;
    public float baseImageSize = 700f;
    public float baseWorldSize = 0.35f; // ~35 cm wide on a desk/sketchbook

    [Header("Trace UI Controls")]
    public GameObject traceControlPanel;
    public Button traceModeButton;
    public TMP_Text traceModeButtonText;
    public Button nextSketchButton;
    public Button galleryButton;
    public Button removeBgButton;
    public TMP_Text removeBgButtonText;
    public Button lockButton;
    public TMP_Text lockButtonText;
    public Button resetPoseButton;

    [Header("Trace Sliders")]
    public Slider opacitySlider;
    public Slider scaleSlider;
    public Slider rotateSlider;

    private bool isTraceActive = false;
    private bool isLocked = false;
    private bool isWhiteBgRemoved = false;
    private int currentSketchIndex = 0;

    private bool prevControlPanelActive = false;
    private bool prevScanPromptActive = false;
    private bool prevPerspectiveButtonActive = false;
    private GameObject cachedRig;

    private Texture2D currentTexture;
    private Texture2D loadedGalleryTexture;
    private Material runtimeTraceMat;

    private GameObject traceAnchorRoot;
    private GameObject traceQuad;
    private ARAnchor currentAnchor;
    private float currentAspect = 1f;

    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();

    private void Start()
    {
        if (raycastManager == null)
            raycastManager = GetComponent<ARRaycastManager>() ?? FindFirstObjectByType<ARRaycastManager>();
        // Change "Trace" to "Trace Photo":
        if (traceModeButtonText != null) traceModeButtonText.text = "Trace Photo";
        if (traceModeButton != null) traceModeButton.onClick.AddListener(ToggleTraceMode);
        if (nextSketchButton != null) nextSketchButton.onClick.AddListener(NextSketch);
        if (galleryButton != null) galleryButton.onClick.AddListener(PickImageFromGallery);
        if (removeBgButton != null) removeBgButton.onClick.AddListener(ToggleRemoveWhiteBg);
        if (lockButton != null) lockButton.onClick.AddListener(ToggleLock);
        if (resetPoseButton != null) resetPoseButton.onClick.AddListener(ResetTracePose);

        // Reuse TraceWhiteKeyMat from TraceOverlayImage if assigned
        if (traceImage != null && traceImage.material != null && traceImage.material.HasProperty(RemoveWhiteId))
        {
            runtimeTraceMat = new Material(traceImage.material);
        }
        else
        {
            Shader s = Shader.Find("Custom/UI/TraceWhiteKey") ?? Shader.Find("Sprites/Default");
            runtimeTraceMat = new Material(s);
        }

        // Keep the old 2D RawImage hidden permanently
        if (traceImage != null) traceImage.gameObject.SetActive(false);

        if (opacitySlider != null)
        {
            opacitySlider.minValue = 0.1f;
            opacitySlider.maxValue = 1.0f;
            opacitySlider.value = 0.65f;
            opacitySlider.onValueChanged.AddListener(ApplyOpacity);
        }

        if (scaleSlider != null)
        {
            scaleSlider.minValue = 0.3f;
            scaleSlider.maxValue = 6.0f;
            scaleSlider.value = 1.0f;
            scaleSlider.onValueChanged.AddListener(ApplyScale);
        }

        if (rotateSlider != null)
        {
            rotateSlider.minValue = -180f;
            rotateSlider.maxValue = 180f;
            rotateSlider.value = 0f;
            rotateSlider.onValueChanged.AddListener(ApplyRotation);
        }

        if (sampleSketches != null && sampleSketches.Length > 0)
        {
            SetCustomTexture(sampleSketches[0]);
        }

        ApplyWhiteBgState();
        if (opacitySlider != null) ApplyOpacity(opacitySlider.value);

        if (traceControlPanel != null) traceControlPanel.SetActive(false);
        if (traceModeButtonText != null) traceModeButtonText.text = "Trace";
        UpdateLockUI();
    }

    private void Update()
    {
        if (!isTraceActive || isLocked) return;
        HandlePlacementTap();
    }

    public void ToggleTraceMode()
    {
        isTraceActive = !isTraceActive;
        if (isTraceActive) EnterTraceMode();
        else ExitTraceMode();
    }

    private void EnterTraceMode()
    {
        // Change "AR Grid" to "3D Grid":
        if (traceModeButtonText != null) traceModeButtonText.text = "3D Grid";
        if (perspectiveManager != null)
        {
            if (perspectiveManager.controlPanel != null)
            {
                prevControlPanelActive = perspectiveManager.controlPanel.activeSelf;
                perspectiveManager.controlPanel.SetActive(false);
            }
            if (perspectiveManager.scanPrompt != null)
            {
                prevScanPromptActive = perspectiveManager.scanPrompt.activeSelf;
                perspectiveManager.scanPrompt.SetActive(traceAnchorRoot == null);
            }
            perspectiveManager.enabled = false;
        }

        if (perspectiveModeButton != null)
        {
            prevPerspectiveButtonActive = perspectiveModeButton.activeSelf;
            perspectiveModeButton.SetActive(false);
        }

        PerspectiveGridGenerator activeRig = FindFirstObjectByType<PerspectiveGridGenerator>(FindObjectsInactive.Include);
        if (activeRig != null && activeRig.gameObject.activeSelf)
        {
            cachedRig = activeRig.gameObject;
            cachedRig.SetActive(false);
        }

        if (traceAnchorRoot != null) traceAnchorRoot.SetActive(true);
        if (traceControlPanel != null) traceControlPanel.SetActive(true);
        if (traceModeButtonText != null) traceModeButtonText.text = "AR Grid";
    }

    private void ExitTraceMode()
    {
        isLocked = false;
        UpdateLockUI();

        if (traceAnchorRoot != null) traceAnchorRoot.SetActive(false);
        if (traceControlPanel != null) traceControlPanel.SetActive(false);

        if (cachedRig != null)
        {
            cachedRig.SetActive(true);
            cachedRig = null;
        }

        if (perspectiveModeButton != null)
            perspectiveModeButton.SetActive(prevPerspectiveButtonActive);

        if (perspectiveManager != null)
        {
            if (perspectiveManager.controlPanel != null)
                perspectiveManager.controlPanel.SetActive(prevControlPanelActive);
            if (perspectiveManager.scanPrompt != null)
                perspectiveManager.scanPrompt.SetActive(prevScanPromptActive);
            perspectiveManager.enabled = true;
        }

        if (traceModeButtonText != null) traceModeButtonText.text = "Trace";
    }

    private void HandlePlacementTap()
    {
        bool tapped = false;
        Vector2 pointerPos = Vector2.zero;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            tapped = true;
            pointerPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            tapped = true;
            pointerPos = Mouse.current.position.ReadValue();
        }

        if (!tapped || IsPointerOverControl(pointerPos) || raycastManager == null) return;

        if (raycastManager.Raycast(pointerPos, arHits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = arHits[0].pose;
            AnchorTraceAtPose(hitPose);

            if (perspectiveManager != null && perspectiveManager.scanPrompt != null)
                perspectiveManager.scanPrompt.SetActive(false);
        }
    }

    private void AnchorTraceAtPose(Pose hitPose)
    {
        EnsureTraceQuadExists();

        if (currentAnchor != null)
        {
            Destroy(currentAnchor);
            currentAnchor = null;
        }

        // Align the anchored image horizontally relative to the camera's right vector
        Vector3 planeNormal = hitPose.up;
        Vector3 flatRight = Camera.main != null
            ? Vector3.ProjectOnPlane(Camera.main.transform.right, planeNormal).normalized
            : Vector3.right;
        if (flatRight.sqrMagnitude < 0.001f) flatRight = Vector3.right;
        Vector3 flatForward = Vector3.Cross(flatRight, planeNormal).normalized;

        Quaternion anchorRot = Quaternion.LookRotation(flatForward, planeNormal);
        traceAnchorRoot.transform.SetPositionAndRotation(hitPose.position, anchorRot);
        currentAnchor = traceAnchorRoot.AddComponent<ARAnchor>();
        traceAnchorRoot.SetActive(true);

        ApplyScale(scaleSlider != null ? scaleSlider.value : 1f);
        ApplyRotation(rotateSlider != null ? rotateSlider.value : 0f);
    }

    private void EnsureTraceQuadExists()
    {
        if (traceAnchorRoot != null && traceQuad != null) return;

        traceAnchorRoot = new GameObject("AnchoredTraceRoot");
        traceQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        traceQuad.name = "AnchoredTraceQuad";
        traceQuad.transform.SetParent(traceAnchorRoot.transform, false);
        traceQuad.transform.localPosition = new Vector3(0f, 0.002f, 0f); // 2mm lift prevents z-fighting

        Collider col = traceQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        MeshRenderer mr = traceQuad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = runtimeTraceMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    public void NextSketch()
    {
        if (sampleSketches == null || sampleSketches.Length == 0) return;
        currentSketchIndex = (currentSketchIndex + 1) % sampleSketches.Length;
        SetCustomTexture(sampleSketches[currentSketchIndex]);
    }

    public void PickImageFromGallery()
    {
        if (isLocked) return;
        if (NativeGallery.IsMediaPickerBusy()) return;

        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path)) return;

            Texture2D tex = NativeGallery.LoadImageAtPath(path, 2048);
            if (tex == null) return;

            if (loadedGalleryTexture != null)
                Destroy(loadedGalleryTexture);

            loadedGalleryTexture = tex;
            SetCustomTexture(loadedGalleryTexture);
        }, "Select an image to trace", "image/*");
    }

    public void ToggleRemoveWhiteBg()
    {
        if (isLocked) return;
        isWhiteBgRemoved = !isWhiteBgRemoved;
        ApplyWhiteBgState();
    }

    private void ApplyWhiteBgState()
    {
        if (runtimeTraceMat != null && runtimeTraceMat.HasProperty(RemoveWhiteId))
            runtimeTraceMat.SetFloat(RemoveWhiteId, isWhiteBgRemoved ? 1f : 0f);

        if (removeBgButtonText != null)
            removeBgButtonText.text = isWhiteBgRemoved ? "White: Off" : "White: On";
    }

    public void SetCustomTexture(Texture2D tex)
    {
        if (tex == null) return;
        currentTexture = tex;
        currentAspect = (float)tex.width / Mathf.Max(1, tex.height);

        if (runtimeTraceMat != null)
            runtimeTraceMat.mainTexture = currentTexture;

        ApplyScale(scaleSlider != null ? scaleSlider.value : 1f);
    }

    public void ToggleLock()
    {
        isLocked = !isLocked;
        UpdateLockUI();
    }

    private void UpdateLockUI()
    {
        if (lockButtonText != null) lockButtonText.text = isLocked ? "Unpin" : "Pin";
        if (opacitySlider != null) opacitySlider.interactable = !isLocked;
        if (scaleSlider != null) scaleSlider.interactable = !isLocked;
        if (rotateSlider != null) rotateSlider.interactable = !isLocked;
        if (nextSketchButton != null) nextSketchButton.interactable = !isLocked;
        if (galleryButton != null) galleryButton.interactable = !isLocked;
        if (removeBgButton != null) removeBgButton.interactable = !isLocked;
        if (resetPoseButton != null) resetPoseButton.interactable = !isLocked;
    }

    public void ResetTracePose()
    {
        if (scaleSlider != null) scaleSlider.value = 1.0f;
        if (rotateSlider != null) rotateSlider.value = 0f;
        ApplyScale(1.0f);
        ApplyRotation(0f);
    }

    private void ApplyOpacity(float alpha)
    {
        if (runtimeTraceMat == null) return;
        Color c = Color.white;
        c.a = alpha;
        if (runtimeTraceMat.HasProperty(ColorId))
            runtimeTraceMat.SetColor(ColorId, c);
    }

    private void ApplyScale(float scale)
    {
        if (traceQuad == null) return;
        float w = currentAspect >= 1f ? baseWorldSize : baseWorldSize * currentAspect;
        float h = currentAspect >= 1f ? baseWorldSize / currentAspect : baseWorldSize;
        traceQuad.transform.localScale = new Vector3(w * scale, h * scale, 1f);
    }

    private void ApplyRotation(float angle)
    {
        if (traceQuad == null) return;
        traceQuad.transform.localRotation = Quaternion.Euler(90f, angle, 0f);
    }

    private bool IsPointerOverControl(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData data = new PointerEventData(EventSystem.current) { position = screenPos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);
        for (int i = 0; i < uiHits.Count; i++)
        {
            if (uiHits[i].gameObject.GetComponentInParent<Selectable>() != null)
                return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (loadedGalleryTexture != null) Destroy(loadedGalleryTexture);
        if (runtimeTraceMat != null) Destroy(runtimeTraceMat);
        if (traceAnchorRoot != null) Destroy(traceAnchorRoot);
    }
}
// END OF TRACEMODECONTROLLER