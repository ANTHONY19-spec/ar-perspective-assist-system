using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PerspectiveGridGenerator : MonoBehaviour
{
    public enum GridOrientation
    {
        VerticalXY,     // Upright drafting plane (best when viewing a 3D subject or tilting the phone)
        FlatOnPaperXZ   // Flat on the table (best when aiming straight down at a sketchbook page)
    }

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    public Transform vpLeft;
    [Tooltip("Auto-found if left empty.")]
    public Transform vpRight;
    public Material gridLineMaterial;

    [Header("Grid Layout")]
    public GridOrientation orientation = GridOrientation.FlatOnPaperXZ;
    [Range(2, 12)] public int raysAboveAndBelow = 6;   // 6 above + 6 below = 12 rays per VP
    [Range(15f, 80f)] public float maxFanAngle = 60f;  // Maximum spread angle from the horizon
    public float rayLength = 4.0f;                     // Length of each converging line in meters
    public float gridLineWidth = 0.0018f;              // Thinner than HorizonLine (0.004) for clear hierarchy

    [Header("Parallel Reference Lines (Verticals)")]
    public bool includeCrossGuides = true;
    [Range(3, 11)] public int crossGuideCount = 7;
    public float crossGuideSpread = 0.8f;              // Total width covered by vertical guides (meters)
    public float crossGuideLength = 1.2f;              // Total length of each vertical guide (meters)

    private readonly List<LineRenderer> _leftRays = new List<LineRenderer>();
    private readonly List<LineRenderer> _rightRays = new List<LineRenderer>();
    private readonly List<LineRenderer> _crossGuides = new List<LineRenderer>();

    private Vector3 _lastLeftPos = new Vector3(float.MaxValue, 0f, 0f);
    private Vector3 _lastRightPos = new Vector3(float.MaxValue, 0f, 0f);
    private GridOrientation _lastOrientation;

    void Start()
    {
        // Auto-locate your existing VP children if you didn't drag them into the Inspector
        if (vpLeft == null) vpLeft = transform.Find("VP_Left");
        if (vpRight == null) vpRight = transform.Find("VP_Right");

        BuildLinePool();
        UpdateGridGeometry();
    }

    void LateUpdate()
    {
        if (vpLeft == null || vpRight == null) return;

        // Only recalculate line vertices when a slider actually moves a vanishing point
        bool moved = (vpLeft.localPosition - _lastLeftPos).sqrMagnitude > 0.000001f
                  || (vpRight.localPosition - _lastRightPos).sqrMagnitude > 0.000001f
                  || orientation != _lastOrientation;

        if (moved)
        {
            UpdateGridGeometry();
        }
    }

    private void BuildLinePool()
    {
        int totalRaysPerSide = raysAboveAndBelow * 2;

        for (int i = 0; i < totalRaysPerSide; i++)
        {
            _leftRays.Add(CreateLine($"Ray_Left_{i}", gridLineWidth));
            _rightRays.Add(CreateLine($"Ray_Right_{i}", gridLineWidth));
        }

        if (includeCrossGuides)
        {
            for (int i = 0; i < crossGuideCount; i++)
            {
                _crossGuides.Add(CreateLine($"CrossGuide_{i}", gridLineWidth * 0.8f));
            }
        }
    }

    private LineRenderer CreateLine(string lineName, float width)
    {
        GameObject lineObj = new GameObject(lineName);
        lineObj.transform.SetParent(transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;

        // sharedMaterial ensures your OpacitySlider fades all generated lines in sync
        if (gridLineMaterial != null)
        {
            lr.sharedMaterial = gridLineMaterial;
        }

        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    private void UpdateGridGeometry()
    {
        Vector3 leftPos = vpLeft.localPosition;
        Vector3 rightPos = vpRight.localPosition;

        _lastLeftPos = leftPos;
        _lastRightPos = rightPos;
        _lastOrientation = orientation;

        Vector3 spanVector = rightPos - leftPos;
        Vector3 dirToRight = spanVector.sqrMagnitude > 0.0001f ? spanVector.normalized : Vector3.right;
        Vector3 dirToLeft = -dirToRight;

        // Fan along Y for an upright view pane, or along Z to lay the grid flat on a sketchbook
        Vector3 fanAxis = (orientation == GridOrientation.VerticalXY) ? Vector3.up : Vector3.forward;

        int rayIndex = 0;
        for (int i = 1; i <= raysAboveAndBelow; i++)
        {
            float angleRad = (maxFanAngle / raysAboveAndBelow) * i * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            // Positive side (above horizon / forward on paper)
            Vector3 leftDirPos = (dirToRight * cos + fanAxis * sin).normalized;
            Vector3 rightDirPos = (dirToLeft * cos + fanAxis * sin).normalized;
            SetLine(_leftRays[rayIndex], leftPos, leftPos + leftDirPos * rayLength);
            SetLine(_rightRays[rayIndex], rightPos, rightPos + rightDirPos * rayLength);
            rayIndex++;

            // Negative side (below horizon / toward you on paper)
            Vector3 leftDirNeg = (dirToRight * cos - fanAxis * sin).normalized;
            Vector3 rightDirNeg = (dirToLeft * cos - fanAxis * sin).normalized;
            SetLine(_leftRays[rayIndex], leftPos, leftPos + leftDirNeg * rayLength);
            SetLine(_rightRays[rayIndex], rightPos, rightPos + rightDirNeg * rayLength);
            rayIndex++;
        }

        // Parallel vertical/plumb reference lines centered between the two vanishing points
        if (includeCrossGuides && _crossGuides.Count > 0)
        {
            Vector3 center = (leftPos + rightPos) * 0.5f;
            float halfLength = crossGuideLength * 0.5f;

            for (int i = 0; i < _crossGuides.Count; i++)
            {
                float t = (_crossGuides.Count == 1) ? 0.5f : (float)i / (_crossGuides.Count - 1);
                float offset = Mathf.Lerp(-crossGuideSpread * 0.5f, crossGuideSpread * 0.5f, t);

                Vector3 guideCenter = center + dirToRight * offset;
                SetLine(_crossGuides[i], guideCenter - fanAxis * halfLength, guideCenter + fanAxis * halfLength);
            }
        }
    }

    private static void SetLine(LineRenderer lr, Vector3 start, Vector3 end)
    {
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }
}