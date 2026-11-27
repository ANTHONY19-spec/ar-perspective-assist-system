using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PerspectiveModeController : MonoBehaviour
{
    [Header("UI Controls")]
    public Button modeToggleButton;
    public TMP_Text modeToggleButtonText;
    public Slider horizonSlider;
    public Slider vpSpreadSlider;
    public Slider opacitySlider;

    [Header("1-Point Grid Geometry")]
    public float gridWidth = 2.4f;
    public float frontDepth = 1.2f;
    public float vanishingDepth = 4.0f;
    public float horizonScale = 1.0f;
    public int convergingRayCount = 9;
    public int transversalCount = 6;
    public float lineWidth = 0.008f;

    [Header("1-Point Colors")]
    public Color horizonColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    public Color gridColor = new Color(0.25f, 0.9f, 1f, 0.85f);

    private bool isOnePointMode = false;
    private PerspectiveGridGenerator trackedRig;
    private GameObject onePointRoot;
    private Material lineMaterial;

    private LineRenderer horizonLine;
    private LineRenderer vpCrossH;
    private LineRenderer vpCrossV;
    private readonly List<LineRenderer> rayLines = new List<LineRenderer>();
    private readonly List<LineRenderer> transversalLines = new List<LineRenderer>();

    private void Start()
    {
        if (modeToggleButton != null)
            modeToggleButton.onClick.AddListener(TogglePerspectiveMode);

        if (horizonSlider != null)
            horizonSlider.onValueChanged.AddListener(_ => UpdateOnePointGeometry());

        if (opacitySlider != null)
            opacitySlider.onValueChanged.AddListener(_ => ApplyOnePointOpacity());

        UpdateModeUI();
    }

    private void LateUpdate()
    {
        if (trackedRig == null)
        {
            trackedRig = FindFirstObjectByType<PerspectiveGridGenerator>(FindObjectsInactive.Include);
            if (trackedRig != null)
            {
                BuildOnePointGrid(trackedRig.transform);
                ApplyModeToRig();
            }
        }

        if (trackedRig != null && isOnePointMode)
        {
            SetTwoPointRenderersVisible(false);
        }
    }

    public void TogglePerspectiveMode()
    {
        isOnePointMode = !isOnePointMode;
        UpdateModeUI();
        ApplyModeToRig();
    }

    private void UpdateModeUI()
    {
        // Find this inside UpdateModeUI():
if (modeToggleButtonText != null)
    modeToggleButtonText.text = isOnePointMode ? "1-Pt" : "2-Pt";

// Replace with:
if (modeToggleButtonText != null)
    modeToggleButtonText.text = isOnePointMode ? "1-Point" : "2-Point";
    }

    private void ApplyModeToRig()
    {
        if (trackedRig == null || onePointRoot == null) return;

        if (isOnePointMode)
        {
            SetTwoPointRenderersVisible(false);
            onePointRoot.SetActive(true);
            UpdateOnePointGeometry();
            ApplyOnePointOpacity();
        }
        else
        {
            onePointRoot.SetActive(false);
            SetTwoPointRenderersVisible(true);
        }
    }

    private void SetTwoPointRenderersVisible(bool visible)
    {
        if (trackedRig == null) return;
        Renderer[] renderers = trackedRig.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (onePointRoot != null && renderers[i].transform.IsChildOf(onePointRoot.transform))
                continue;
            renderers[i].enabled = visible;
        }
    }

    private void BuildOnePointGrid(Transform rigParent)
    {
        if (onePointRoot != null) Destroy(onePointRoot);
        rayLines.Clear();
        transversalLines.Clear();

        onePointRoot = new GameObject("OnePointGridRoot");
        onePointRoot.transform.SetParent(rigParent, false);
        onePointRoot.transform.localPosition = Vector3.zero;
        onePointRoot.transform.localRotation = Quaternion.identity;
        onePointRoot.transform.localScale = Vector3.one;

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader);
            }
            else
            {
                LineRenderer existing = rigParent.GetComponentInChildren<LineRenderer>(true);
                if (existing != null && existing.sharedMaterial != null)
                    lineMaterial = new Material(existing.sharedMaterial);
            }
        }

        horizonLine = CreateLine("HorizonLine", lineWidth * 1.35f);
        vpCrossH = CreateLine("VPCrossH", lineWidth * 1.5f);
        vpCrossV = CreateLine("VPCrossV", lineWidth * 1.5f);

        int rays = Mathf.Max(3, convergingRayCount);
        for (int i = 0; i < rays; i++)
            rayLines.Add(CreateLine("Ray_" + i, lineWidth));

        int trans = Mathf.Max(2, transversalCount);
        for (int i = 0; i < trans; i++)
            transversalLines.Add(CreateLine("Transversal_" + i, lineWidth * 0.85f));

        UpdateOnePointGeometry();
        ApplyOnePointOpacity();
        onePointRoot.SetActive(isOnePointMode);
    }

    private LineRenderer CreateLine(string lineName, float width)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(onePointRoot.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        return lr;
    }

    private void UpdateOnePointGeometry()
    {
        if (onePointRoot == null || horizonLine == null) return;

        float horizonY = (horizonSlider != null ? horizonSlider.value : 0.4f) * horizonScale;
        Vector3 vp = new Vector3(0f, horizonY, vanishingDepth);

        float halfHorizon = gridWidth * 1.25f;
        horizonLine.SetPosition(0, new Vector3(-halfHorizon, horizonY, vanishingDepth));
        horizonLine.SetPosition(1, new Vector3(halfHorizon, horizonY, vanishingDepth));

        float crossSize = 0.12f;
        vpCrossH.SetPosition(0, vp + Vector3.left * crossSize);
        vpCrossH.SetPosition(1, vp + Vector3.right * crossSize);
        vpCrossV.SetPosition(0, vp + Vector3.down * crossSize);
        vpCrossV.SetPosition(1, vp + Vector3.up * crossSize);

        float halfWidth = gridWidth * 0.5f;
        int rayCount = rayLines.Count;
        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount == 1 ? 0.5f : (float)i / (rayCount - 1);
            float x = Mathf.Lerp(-halfWidth, halfWidth, t);
            rayLines[i].SetPosition(0, new Vector3(x, 0f, -frontDepth));
            rayLines[i].SetPosition(1, vp);
        }

        Vector3 leftFront = new Vector3(-halfWidth, 0f, -frontDepth);
        Vector3 rightFront = new Vector3(halfWidth, 0f, -frontDepth);
        int transCount = transversalLines.Count;
        for (int i = 0; i < transCount; i++)
        {
            float frac = (float)i / transCount;
            transversalLines[i].SetPosition(0, Vector3.Lerp(leftFront, vp, frac));
            transversalLines[i].SetPosition(1, Vector3.Lerp(rightFront, vp, frac));
        }
    }

    private void ApplyOnePointOpacity()
    {
        if (onePointRoot == null || horizonLine == null) return;

        float alpha = 0.85f;
        if (opacitySlider != null)
        {
            alpha = opacitySlider.maxValue > 1.01f
                ? Mathf.InverseLerp(opacitySlider.minValue, opacitySlider.maxValue, opacitySlider.value)
                : opacitySlider.value;
            alpha = Mathf.Clamp(alpha, 0.08f, 1f);
        }

        Color hCol = horizonColor;
        hCol.a = alpha;
        SetLineColor(horizonLine, hCol);
        SetLineColor(vpCrossH, hCol);
        SetLineColor(vpCrossV, hCol);

        Color gCol = gridColor;
        gCol.a = alpha * 0.9f;
        for (int i = 0; i < rayLines.Count; i++) SetLineColor(rayLines[i], gCol);
        for (int i = 0; i < transversalLines.Count; i++) SetLineColor(transversalLines[i], gCol);
    }

    private static void SetLineColor(LineRenderer lr, Color c)
    {
        if (lr == null) return;
        lr.startColor = c;
        lr.endColor = c;
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
// END OF PERSPECTIVEMODECONTROLLER