using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PerspectiveAssistManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    public GameObject perspectiveRigPrefab;
    public Material gridLineMaterial;

    [Header("Sliders")]
    public Slider horizonSlider;
    public Slider vpSpreadSlider;
    public Slider opacitySlider;

    [Header("Demo UI Controls")]
    public GameObject controlPanel;
    public GameObject scanPrompt;
    public Button resetButton;
    public Button toggleUIButton;
    public TMP_Text toggleUIButtonText;
    public Button toggleRoomGridButton;
    public TMP_Text toggleRoomGridButtonText;
    public Button snapWallButton;
    public TMP_Text snapWallButtonText;

    private GameObject spawnedRig;
    private Vector3 baseAnchorPosition;
    private Transform vpLeft;
    private Transform vpRight;
    private LineRenderer horizonLine;
    private bool isPanelVisible = true;
    private bool areRoomGridsVisible = true;
    private Coroutine snapLabelRoutine;

    private static readonly List<ARRaycastHit> RaycastHits = new List<ARRaycastHit>();

    private void Start()
    {
        if (horizonSlider != null) horizonSlider.onValueChanged.AddListener(OnHorizonChanged);
        if (vpSpreadSlider != null) vpSpreadSlider.onValueChanged.AddListener(OnSpreadChanged);
        if (opacitySlider != null) opacitySlider.onValueChanged.AddListener(OnOpacityChanged);

        if (resetButton != null) resetButton.onClick.AddListener(ResetRig);
        if (toggleUIButton != null) toggleUIButton.onClick.AddListener(ToggleControlPanel);
        if (toggleRoomGridButton != null) toggleRoomGridButton.onClick.AddListener(ToggleRoomGrids);
        if (snapWallButton != null) snapWallButton.onClick.AddListener(SnapToWall);

        RoomSurfaceGrid.SetGlobalVisible(areRoomGridsVisible);
        UpdateRoomGridButtonLabel();
        if (snapWallButtonText != null) snapWallButtonText.text = "Snap Wall";
        SetPlacedUIState(false);
    }

    private void Update()
    {
        if (spawnedRig != null) return;

        if (!TryGetTapPosition(out Vector2 screenPos)) return;
        if (IsPointerOverUI(screenPos)) return;

        if (raycastManager != null &&
            raycastManager.Raycast(screenPos, RaycastHits, TrackableType.PlaneWithinPolygon))
        {
            PlaceRig(RaycastHits[0].pose);
        }
    }

    private void PlaceRig(Pose hitPose)
    {
        Quaternion facingRotation = hitPose.rotation;
        if (Camera.main != null)
        {
            Vector3 lookDir = Camera.main.transform.position - hitPose.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                facingRotation = Quaternion.LookRotation(-lookDir.normalized, Vector3.up);
            }
        }

        spawnedRig = Instantiate(perspectiveRigPrefab, hitPose.position, facingRotation);
        baseAnchorPosition = hitPose.position;

        vpLeft = spawnedRig.transform.Find("VP_Left");
        vpRight = spawnedRig.transform.Find("VP_Right");
        Transform horizonTf = spawnedRig.transform.Find("HorizonLine");
        if (horizonTf != null) horizonLine = horizonTf.GetComponent<LineRenderer>();

        if (horizonSlider != null) OnHorizonChanged(horizonSlider.value);
        if (vpSpreadSlider != null) OnSpreadChanged(vpSpreadSlider.value);
        if (opacitySlider != null) OnOpacityChanged(opacitySlider.value);

        SetPlacedUIState(true);
    }

    public void SnapToWall()
    {
        if (Camera.main == null) return;

        Vector3 camFwd = Camera.main.transform.forward;
        camFwd.y = 0f;
        camFwd = camFwd.sqrMagnitude > 0.001f ? camFwd.normalized : Vector3.forward;

        Vector3 targetDir = camFwd;
        bool snappedToWall = false;

        // 1. Try raycasting directly from the center of the screen onto a detected wall
        if (raycastManager != null && planeManager != null)
        {
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (raycastManager.Raycast(screenCenter, RaycastHits, TrackableType.PlaneWithinPolygon))
            {
                foreach (ARRaycastHit hit in RaycastHits)
                {
                    ARPlane plane = planeManager.GetPlane(hit.trackableId);
                    if (plane != null && plane.alignment == PlaneAlignment.Vertical)
                    {
                        Vector3 wallDir = plane.transform.up;
                        wallDir.y = 0f;
                        if (wallDir.sqrMagnitude > 0.001f)
                        {
                            targetDir = wallDir.normalized;
                            snappedToWall = true;
                            break;
                        }
                    }
                }
            }
        }

        // 2. Fallback: find the detected vertical wall most directly in front of the camera
        if (!snappedToWall && planeManager != null)
        {
            float bestScore = 0.25f;
            Vector3 camPos = Camera.main.transform.position;

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment != PlaneAlignment.Vertical) continue;

                Vector3 toPlane = plane.center - camPos;
                toPlane.y = 0f;
                if (toPlane.sqrMagnitude < 0.01f) continue;

                float facingScore = Vector3.Dot(camFwd, toPlane.normalized);
                if (facingScore > bestScore)
                {
                    Vector3 wallDir = plane.transform.up;
                    wallDir.y = 0f;
                    if (wallDir.sqrMagnitude > 0.001f)
                    {
                        bestScore = facingScore;
                        targetDir = wallDir.normalized;
                        snappedToWall = true;
                    }
                }
            }
        }

        // Keep direction facing forward away from camera so VP_Left / VP_Right never invert
        if (Vector3.Dot(targetDir, camFwd) < 0f) targetDir = -targetDir;

        // Re-align all floor & ceiling room grids
        RoomSurfaceGrid.SnapToDirection(targetDir);

        // Also square up the placed 2-point perspective rig to the same wall
        if (spawnedRig != null)
        {
            spawnedRig.transform.rotation = Quaternion.LookRotation(targetDir, Vector3.up);
            UpdateHorizonEndpoints();
        }

        if (snapWallButtonText != null)
        {
            if (snapLabelRoutine != null) StopCoroutine(snapLabelRoutine);
            snapLabelRoutine = StartCoroutine(FlashSnapFeedback(snappedToWall ? "Wall OK" : "Cam OK"));
        }
    }

    private IEnumerator FlashSnapFeedback(string statusText)
    {
        snapWallButtonText.text = statusText;
        yield return new WaitForSeconds(1.2f);
        if (snapWallButtonText != null) snapWallButtonText.text = "Snap Wall";
    }

    public void ResetRig()
    {
        if (spawnedRig != null)
        {
            Destroy(spawnedRig);
            spawnedRig = null;
        }

        vpLeft = null;
        vpRight = null;
        horizonLine = null;

        SetPlanesVisible(true);
        SetPlacedUIState(false);
    }

    public void ToggleControlPanel()
    {
        isPanelVisible = !isPanelVisible;
        if (controlPanel != null) controlPanel.SetActive(isPanelVisible);
        if (toggleUIButtonText != null) toggleUIButtonText.text = isPanelVisible ? "Hide Controls" : "Show Controls";
    }

    public void ToggleRoomGrids()
    {
        areRoomGridsVisible = !areRoomGridsVisible;
        RoomSurfaceGrid.SetGlobalVisible(areRoomGridsVisible);
        UpdateRoomGridButtonLabel();
    }

    private void UpdateRoomGridButtonLabel()
    {
        if (toggleRoomGridButtonText != null)
        {
            toggleRoomGridButtonText.text = areRoomGridsVisible ? "Guide Dots: On" : "Guide Dots: Off";
        }
    }

    private void SetPlacedUIState(bool placed)
    {
        isPanelVisible = placed;
        if (controlPanel != null) controlPanel.SetActive(placed);
        if (scanPrompt != null) scanPrompt.SetActive(!placed);
        if (resetButton != null) resetButton.gameObject.SetActive(placed);
        if (toggleUIButton != null) toggleUIButton.gameObject.SetActive(placed);
        if (toggleUIButtonText != null) toggleUIButtonText.text = "Hide UI";
    }

    private void SetPlanesVisible(bool visible)
    {
        if (planeManager == null) return;
        planeManager.enabled = visible;
        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.SetActive(visible);
        }
    }

    private void OnHorizonChanged(float value)
    {
        if (spawnedRig == null) return;
        spawnedRig.transform.position = baseAnchorPosition + Vector3.up * value;
        UpdateHorizonEndpoints();
    }

    private void OnSpreadChanged(float value)
    {
        if (vpLeft != null)
        {
            Vector3 p = vpLeft.localPosition;
            vpLeft.localPosition = new Vector3(-Mathf.Abs(value), p.y, p.z);
        }
        if (vpRight != null)
        {
            Vector3 p = vpRight.localPosition;
            vpRight.localPosition = new Vector3(Mathf.Abs(value), p.y, p.z);
        }
        UpdateHorizonEndpoints();
    }

    private void UpdateHorizonEndpoints()
    {
        if (horizonLine == null || vpLeft == null || vpRight == null) return;
        horizonLine.positionCount = 2;
        if (horizonLine.useWorldSpace)
        {
            horizonLine.SetPosition(0, vpLeft.position);
            horizonLine.SetPosition(1, vpRight.position);
        }
        else
        {
            horizonLine.SetPosition(0, vpLeft.localPosition);
            horizonLine.SetPosition(1, vpRight.localPosition);
        }
    }

    private void OnOpacityChanged(float alpha)
    {
        RoomSurfaceGrid.SetGlobalOpacity(alpha);

        if (gridLineMaterial != null)
        {
            if (gridLineMaterial.HasProperty("_BaseColor"))
            {
                Color c = gridLineMaterial.GetColor("_BaseColor");
                c.a = alpha;
                gridLineMaterial.SetColor("_BaseColor", c);
            }
            if (gridLineMaterial.HasProperty("_Color"))
            {
                Color c = gridLineMaterial.GetColor("_Color");
                c.a = alpha;
                gridLineMaterial.SetColor("_Color", c);
            }
        }

        if (spawnedRig == null) return;
        foreach (LineRenderer lr in spawnedRig.GetComponentsInChildren<LineRenderer>(true))
        {
            Color sc = lr.startColor;
            sc.a = alpha;
            lr.startColor = sc;

            Color ec = lr.endColor;
            ec.a = alpha;
            lr.endColor = ec;
        }
    }

    private bool TryGetTapPosition(out Vector2 screenPos)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }

        screenPos = default;
        return false;
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
// END OF PERSPECTIVEASSISTMANAGER