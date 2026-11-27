using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARPlane))]
public class RoomSurfaceGrid : MonoBehaviour
{
    [Header("Grid Appearance")]
    public Material gridLineMaterial;
    [Range(0.15f, 1.0f)] public float gridSpacing = 0.35f;
    public float lineWidth = 0.0025f;
    public bool hideDefaultPlaneDots = true;

    [Header("Surface Color Coding")]
    public bool colorCodeBySurface = true;
    public Color floorColor = new Color(0f, 0.95f, 1f, 0.85f);      // Cyan
    public Color wallColor = new Color(1f, 0.45f, 0f, 0.85f);       // Electric Orange
    public Color ceilingColor = new Color(1f, 0.9f, 0.1f, 0.85f);   // Bright Yellow

    private static float globalOpacity = 0.85f;
    private static bool globalVisible = true;
    private static bool hasInitialDir = false;
    private static bool hasWallDir = false;
    private static bool isManuallyAligned = false;
    private static float largestWallArea = 0f;
    private static Vector3 sharedRoomDir = Vector3.forward;
    private static readonly HashSet<RoomSurfaceGrid> ActiveGrids = new HashSet<RoomSurfaceGrid>();

    private ARPlane arPlane;
    private readonly List<LineRenderer> linePool = new List<LineRenderer>();
    private readonly List<Vector2> alignedPoly = new List<Vector2>();
    private readonly List<float> intersections = new List<float>();

    private void Awake()
    {
        arPlane = GetComponent<ARPlane>();
        if (hideDefaultPlaneDots && TryGetComponent(out MeshRenderer mr)) mr.enabled = false;
    }

    private void OnEnable()
    {
        ActiveGrids.Add(this);
        if (arPlane != null)
        {
            arPlane.boundaryChanged += OnBoundaryChanged;
            RebuildGrid();
        }
    }

    private void OnDisable()
    {
        ActiveGrids.Remove(this);
        if (arPlane != null) arPlane.boundaryChanged -= OnBoundaryChanged;
    }

    public static void SetGlobalOpacity(float alpha)
    {
        globalOpacity = Mathf.Clamp01(alpha);
        foreach (RoomSurfaceGrid grid in ActiveGrids)
            if (grid != null) grid.ApplyCurrentColor();
    }

    public static void SetGlobalVisible(bool visible)
    {
        globalVisible = visible;
        foreach (RoomSurfaceGrid grid in ActiveGrids)
            if (grid != null) grid.ApplyVisibility();
    }

    public static void SnapToDirection(Vector3 worldDir)
    {
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.001f) return;

        sharedRoomDir = worldDir.normalized;
        hasInitialDir = true;
        hasWallDir = true;
        isManuallyAligned = true;

        foreach (RoomSurfaceGrid grid in ActiveGrids)
        {
            if (grid != null && grid.arPlane != null &&
                grid.arPlane.alignment != PlaneAlignment.Vertical)
            {
                grid.RebuildGrid();
            }
        }
    }

    private void ApplyVisibility()
    {
        if (!globalVisible) HideUnusedLines(0);
        else RebuildGrid();
    }

    private void OnBoundaryChanged(ARPlaneBoundaryChangedEventArgs args) => RebuildGrid();

    private Color GetSurfaceColor()
    {
        Color c = wallColor;
        if (colorCodeBySurface && arPlane != null)
        {
            if (arPlane.alignment == PlaneAlignment.HorizontalUp) c = floorColor;
            else if (arPlane.alignment == PlaneAlignment.HorizontalDown) c = ceilingColor;
        }
        c.a = globalOpacity;
        return c;
    }

    private void ApplyCurrentColor()
    {
        Color c = GetSurfaceColor();
        for (int i = 0; i < linePool.Count; i++)
        {
            if (linePool[i] != null && linePool[i].gameObject.activeSelf)
            {
                linePool[i].startColor = c;
                linePool[i].endColor = c;
            }
        }
    }

    private void TryUpdateWallDirection()
    {
        if (isManuallyAligned || arPlane.alignment != PlaneAlignment.Vertical) return;

        float area = arPlane.size.x * arPlane.size.y;
        if (!hasWallDir || area > largestWallArea + 0.15f)
        {
            Vector3 wallNormal = transform.up;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude > 0.01f)
            {
                sharedRoomDir = wallNormal.normalized;
                hasWallDir = true;
                hasInitialDir = true;
                largestWallArea = area;

                foreach (RoomSurfaceGrid grid in ActiveGrids)
                {
                    if (grid != null && grid != this && grid.arPlane != null &&
                        grid.arPlane.alignment != PlaneAlignment.Vertical)
                    {
                        grid.RebuildGrid();
                    }
                }
            }
        }
    }

    private void GetAlignedAxes(out Vector2 axisU, out Vector2 axisV)
    {
        axisU = Vector2.right;
        axisV = Vector2.up;

        if (arPlane.alignment == PlaneAlignment.Vertical)
        {
            Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
            Vector2 v = new Vector2(localUp.x, localUp.z);
            if (v.sqrMagnitude > 0.001f)
            {
                axisV = v.normalized;
                axisU = new Vector2(axisV.y, -axisV.x);
            }
            return;
        }

        if (!hasInitialDir && Camera.main != null)
        {
            Vector3 camFwd = Camera.main.transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f)
            {
                sharedRoomDir = camFwd.normalized;
                hasInitialDir = true;
            }
        }

        Vector3 localDir = transform.InverseTransformDirection(sharedRoomDir);
        Vector2 dir2D = new Vector2(localDir.x, localDir.z);
        if (dir2D.sqrMagnitude > 0.001f)
        {
            axisV = dir2D.normalized;
            axisU = new Vector2(axisV.y, -axisV.x);
        }
    }

    public void RebuildGrid()
    {
        if (!globalVisible || arPlane == null) { HideUnusedLines(0); return; }

        NativeArray<Vector2> boundary = arPlane.boundary;
        if (!boundary.IsCreated || boundary.Length < 3) { HideUnusedLines(0); return; }

        TryUpdateWallDirection();
        GetAlignedAxes(out Vector2 axisU, out Vector2 axisV);

        alignedPoly.Clear();
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        for (int i = 0; i < boundary.Length; i++)
        {
            Vector2 pt = boundary[i];
            float u = Vector2.Dot(pt, axisU);
            float v = Vector2.Dot(pt, axisV);
            alignedPoly.Add(new Vector2(u, v));
            if (u < minU) minU = u;
            if (u > maxU) maxU = u;
            if (v < minV) minV = v;
            if (v > maxV) maxV = v;
        }

        Color surfaceColor = GetSurfaceColor();
        int usedCount = 0;
        const float yOff = 0.002f;

        LineRenderer border = GetLineRenderer(usedCount++, surfaceColor);
        border.loop = true;
        border.positionCount = boundary.Length;
        for (int i = 0; i < boundary.Length; i++)
            border.SetPosition(i, new Vector3(boundary[i].x, yOff, boundary[i].y));

        for (float u = Mathf.Ceil(minU / gridSpacing) * gridSpacing; u < maxU; u += gridSpacing)
        {
            if (TryClipLine(alignedPoly, u, true, out float v0, out float v1))
            {
                Vector2 p0 = axisU * u + axisV * v0;
                Vector2 p1 = axisU * u + axisV * v1;
                LineRenderer lr = GetLineRenderer(usedCount++, surfaceColor);
                lr.loop = false;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(p0.x, yOff, p0.y));
                lr.SetPosition(1, new Vector3(p1.x, yOff, p1.y));
            }
        }

        for (float v = Mathf.Ceil(minV / gridSpacing) * gridSpacing; v < maxV; v += gridSpacing)
        {
            if (TryClipLine(alignedPoly, v, false, out float u0, out float u1))
            {
                Vector2 p0 = axisU * u0 + axisV * v;
                Vector2 p1 = axisU * u1 + axisV * v;
                LineRenderer lr = GetLineRenderer(usedCount++, surfaceColor);
                lr.loop = false;
                lr.positionCount = 2;
                lr.SetPosition(0, new Vector3(p0.x, yOff, p0.y));
                lr.SetPosition(1, new Vector3(p1.x, yOff, p1.y));
            }
        }

        HideUnusedLines(usedCount);
    }

    private bool TryClipLine(List<Vector2> poly, float coord, bool vertical, out float minVal, out float maxVal)
    {
        intersections.Clear();
        int count = poly.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % count];
            float a1 = vertical ? a.x : a.y;
            float b1 = vertical ? b.x : b.y;
            float a2 = vertical ? a.y : a.x;
            float b2 = vertical ? b.y : b.x;

            if ((a1 <= coord && b1 > coord) || (b1 <= coord && a1 > coord))
            {
                float t = (coord - a1) / (b1 - a1);
                intersections.Add(Mathf.Lerp(a2, b2, t));
            }
        }

        if (intersections.Count < 2) { minVal = maxVal = 0f; return false; }

        minVal = float.MaxValue;
        maxVal = float.MinValue;
        for (int i = 0; i < intersections.Count; i++)
        {
            if (intersections[i] < minVal) minVal = intersections[i];
            if (intersections[i] > maxVal) maxVal = intersections[i];
        }
        return (maxVal - minVal) > 0.02f;
    }

    private LineRenderer GetLineRenderer(int index, Color color)
    {
        while (linePool.Count <= index)
        {
            GameObject child = new GameObject($"GridSegment_{linePool.Count}");
            child.transform.SetParent(transform, false);
            LineRenderer lr = child.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (gridLineMaterial != null) lr.sharedMaterial = gridLineMaterial;
            linePool.Add(lr);
        }

        LineRenderer activeLr = linePool[index];
        activeLr.gameObject.SetActive(true);
        activeLr.startWidth = lineWidth;
        activeLr.endWidth = lineWidth;
        activeLr.startColor = color;
        activeLr.endColor = color;
        return activeLr;
    }

    private void HideUnusedLines(int startIndex)
    {
        for (int i = startIndex; i < linePool.Count; i++)
            if (linePool[i] != null && linePool[i].gameObject.activeSelf)
                linePool[i].gameObject.SetActive(false);
    }
}
// END OF ROOMSURFACEGRID