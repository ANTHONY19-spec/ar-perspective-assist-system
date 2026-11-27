using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[DefaultExecutionOrder(1000)]
public class PaperHorizonAligner : MonoBehaviour
{
    [Header("Surface Alignment")]
    [Tooltip("Keep 0 so the horizon runs left-to-right across the ground or paper.")]
    public float yawOffsetDegrees = 0f;
    [Tooltip("Slight lift (5mm) above the ground/paper so lines never clip into the floor.")]
    public float surfaceLiftMeters = 0.005f;

    private ARPlaneManager planeManager;
    private PerspectiveGridGenerator lastTrackedRig;
    private Vector3 lastAnchoredPos;
    private Quaternion lockedRotation;
    private bool hasLockedRotation = false;
    private bool wallModeRequested = false;

    private void Awake()
    {
        planeManager = FindAnyObjectByType<ARPlaneManager>();

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name.IndexOf("SnapWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                buttons[i].onClick.AddListener(OnSnapWallPressed);
                break;
            }
        }
    }

    private void OnSnapWallPressed()
    {
        wallModeRequested = !wallModeRequested;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name.IndexOf("SnapWall", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                TMPro.TMP_Text label = buttons[i].GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null)
                    label.text = wallModeRequested ? "Snap Paper" : "Snap Wall";
                break;
            }
        }

        if (lastTrackedRig != null && lastTrackedRig.gameObject.activeInHierarchy)
        {
            AlignRigToSurface(lastTrackedRig.transform, forceWall: wallModeRequested);
        }
    }

    private void LateUpdate()
    {
        PerspectiveGridGenerator rig = FindAnyObjectByType<PerspectiveGridGenerator>(FindObjectsInactive.Include);
        if (rig == null || !rig.gameObject.activeInHierarchy)
        {
            lastTrackedRig = null;
            hasLockedRotation = false;
            wallModeRequested = false;
            return;
        }

        // Re-compute orientation only when a new grid is placed or moved to a distant spot (> 8cm)
        if (rig != lastTrackedRig || Vector3.Distance(rig.transform.position, lastAnchoredPos) > 0.08f)
        {
            lastTrackedRig = rig;
            AlignRigToSurface(rig.transform, forceWall: wallModeRequested);
            return;
        }

        // Keep the grid flat on the surface while dragging Horizon Elevation or VP Spread
        if (hasLockedRotation)
        {
            rig.transform.rotation = lockedRotation;
        }
    }

    public void AlignRigToSurface(Transform rigTransform, bool forceWall)
    {
        if (Camera.main == null) return;

        Vector3 surfaceNormal = DetectNearestSurfaceNormal(rigTransform.position);
        bool isVerticalWall = forceWall || Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.up)) < 0.65f;

        // Remove any ARAnchor on the rig itself so it cannot override flat rotation during slider drags
        ARAnchor existingAnchor = rigTransform.GetComponent<ARAnchor>();
        if (existingAnchor != null)
        {
            Destroy(existingAnchor);
        }

        Vector3 targetPos = rigTransform.position;
        Quaternion targetRot;

        if (isVerticalWall)
        {
            Vector3 wallNormal = Vector3.ProjectOnPlane(surfaceNormal, Vector3.up);
            if (wallNormal.sqrMagnitude < 0.001f)
                wallNormal = Vector3.ProjectOnPlane(-Camera.main.transform.forward, Vector3.up);
            wallNormal.Normalize();

            targetPos += wallNormal * surfaceLiftMeters;
            targetRot = Quaternion.LookRotation(-wallNormal, Vector3.up);
        }
        else
        {
            // Pitch the grid's local XY plane 100% flat onto the floor, desk, or paper
            Vector3 flatRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
            if (flatRight.sqrMagnitude < 0.001f) flatRight = Vector3.right;
            Vector3 flatForward = Vector3.Cross(flatRight, Vector3.up).normalized;

            Quaternion flatOnGround = Quaternion.LookRotation(-Vector3.up, flatForward);
            Quaternion yawAdjust = Quaternion.AngleAxis(yawOffsetDegrees, Vector3.up);

            targetPos += Vector3.up * surfaceLiftMeters;
            targetRot = yawAdjust * flatOnGround;
        }

        rigTransform.SetPositionAndRotation(targetPos, targetRot);
        lastAnchoredPos = targetPos;
        lockedRotation = targetRot;
        hasLockedRotation = true;
    }

    private Vector3 DetectNearestSurfaceNormal(Vector3 worldPos)
    {
        if (planeManager == null) return Vector3.up;

        ARPlane closestPlane = null;
        float minDist = float.MaxValue;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null || !plane.gameObject.activeInHierarchy) continue;
            float dist = Vector3.Distance(plane.center, worldPos);
            if (dist < minDist)
            {
                minDist = dist;
                closestPlane = plane;
            }
        }

        if (closestPlane != null && closestPlane.alignment == PlaneAlignment.Vertical)
            return closestPlane.normal;

        return Vector3.up;
    }
}
// END OF PAPERHORIZONALIGNER