using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple DigitalTwinMaskingService that groups elements into named regions (by collider)
/// and only activates highlights for elements that are inside an active region.
/// - Register regions by regionId + Collider
/// - Register elements by regionId + elementId (the elementId must be registered in HighlightingService)
/// - AI can call SetRegionActive / HighlightElementInRegion etc.
/// 
/// This is collider-based (no stencil). For many setups this is simpler and works with your existing highlight shader.
/// </summary>
[DisallowMultipleComponent]
public class DigitalTwinMaskingService : MonoBehaviour
{
    [Serializable]
    public class RegionInfo
    {
        public string regionId;
        public Collider regionCollider; // volume that defines the region
        public bool active = false;
        public Color glowColor = new Color(1f, 0.6f, 0.0f, 0.25f); // optional UI glow (if you have a separate renderer)
        public List<string> elementIds = new List<string>(); // element IDs that belong to this region
    }

    public List<RegionInfo> regions = new List<RegionInfo>();

    // quick lookup
    private Dictionary<string, RegionInfo> _regionLookup = new Dictionary<string, RegionInfo>(StringComparer.OrdinalIgnoreCase);

    // reference to highlighting service
    private HighlightingService _highlightingService;

    [Tooltip("When true, service will re-evaluate which elements are inside active regions each frame. Set false if elements are static for better perf.")]
    public bool continuousUpdate = false;

    void Awake()
    {
        _highlightingService = FindObjectOfType<HighlightingService>();
        if (_highlightingService == null)
            Debug.LogWarning("[DigitalTwinMaskingService] HighlightingService not found in scene.");

        BuildLookup();
    }

    void BuildLookup()
    {
        _regionLookup.Clear();
        foreach (var r in regions)
        {
            if (string.IsNullOrEmpty(r.regionId) || r.regionCollider == null) continue;
            _regionLookup[r.regionId] = r;
        }
    }

    void Update()
    {
        if (continuousUpdate)
        {
            foreach (var kv in _regionLookup)
            {
                var region = kv.Value;
                if (!region.active) continue;
                UpdateHighlightsForRegion(region);
            }
        }
    }

    /// <summary>
    /// Register a region at runtime (optional).
    /// </summary>
    public void RegisterRegion(string regionId, Collider regionCollider)
    {
        if (string.IsNullOrEmpty(regionId)) throw new ArgumentNullException(nameof(regionId));
        if (regionCollider == null) throw new ArgumentNullException(nameof(regionCollider));
        var info = new RegionInfo { regionId = regionId, regionCollider = regionCollider, active = false };
        regions.Add(info);
        _regionLookup[regionId] = info;
    }

    /// <summary>
    /// Register an element id (must match id registered in HighlightingService).
    /// </summary>
    public void RegisterElementToRegion(string regionId, string elementId)
    {
        if (!_regionLookup.TryGetValue(regionId, out var r))
        {
            Debug.LogWarning($"RegisterElementToRegion: region '{regionId}' not found.");
            return;
        }
        if (!r.elementIds.Contains(elementId))
            r.elementIds.Add(elementId);
    }

    /// <summary>
    /// Set whether the region mask is active.
    /// When active, elements inside will be highlighted (if previously requested via SetElementHighlightInRegion).
    /// </summary>
    public void SetRegionActive(string regionId, bool active)
    {
        if (!_regionLookup.TryGetValue(regionId, out var r))
        {
            Debug.LogWarning($"SetRegionActive: region '{regionId}' not found.");
            return;
        }
        r.active = active;
        Debug.Log($"[DigitalTwinMaskingService] Region '{regionId}' active={active}");

        // On activation, evaluate highlights
        if (active)
            UpdateHighlightsForRegion(r);
        else
            // clear all highlights for this region
            ClearHighlightsForRegion(r);
    }

    /// <summary>
    /// Highlights element within the region if it is inside the region volume.
    /// If the region is not active, this will register the element under that region and only highlight once the region becomes active.
    /// </summary>
    public void SetElementHighlightInRegion(string regionId, string elementId, Color color, bool highlight)
    {
        if (!_regionLookup.TryGetValue(regionId, out var r))
        {
            Debug.LogWarning($"SetElementHighlightInRegion: region '{regionId}' not found.");
            return;
        }

        // Ensure element known to region
        if (!r.elementIds.Contains(elementId)) r.elementIds.Add(elementId);

        var hs = _highlightingService;
        if (hs == null)
        {
            Debug.LogWarning("SetElementHighlightInRegion: HighlightingService missing.");
            return;
        }

        if (!hs.IsRegistered(elementId))
        {
            Debug.LogWarning($"SetElementHighlightInRegion: elementId '{elementId}' not registered in HighlightingService.");
            return;
        }

        if (!r.active)
        {
            // defer until region active: if highlight==false remove any flagged highlight
            Debug.Log($"[DigitalTwinMaskingService] Region '{regionId}' inactive; storing highlight request for '{elementId}'. Will apply when region active.");
            // nothing else to do now
            return;
        }

        // If region active -> attempt to highlight only if inside
        ApplyHighlightIfInsideRegion(r, elementId, color, highlight);
    }

    /// <summary>
    /// Check all elementIds for this region and enable highlight only for those inside the region collider.
    /// </summary>
    private void UpdateHighlightsForRegion(RegionInfo r)
    {
        var hs = _highlightingService;
        if (hs == null) return;
        foreach (var elementId in r.elementIds)
        {
            if (!hs.IsRegistered(elementId)) continue;
            var target = GetTargetFromHighlightingService(hs, elementId);
            if (target == null) continue;
            bool inside = IsPointInsideCollider(r.regionCollider, target.transform.position);
            if (inside)
                hs.HighlightById(elementId);
            else
                hs.UnhighlightById(elementId);
        }
    }

    private void ClearHighlightsForRegion(RegionInfo r)
    {
        var hs = _highlightingService;
        if (hs == null) return;
        foreach (var elementId in r.elementIds)
        {
            if (hs.IsRegistered(elementId))
                hs.UnhighlightById(elementId);
        }
    }

    private void ApplyHighlightIfInsideRegion(RegionInfo r, string elementId, Color color, bool highlight)
    {
        var hs = _highlightingService;
        if (hs == null) return;
        var target = GetTargetFromHighlightingService(hs, elementId);
        if (target == null) return;
        bool inside = IsPointInsideCollider(r.regionCollider, target.transform.position);
        if (highlight && inside)
            hs.HighlightById(elementId, color);
        else if (!highlight)
            hs.UnhighlightById(elementId);
        // if highlight requested but not inside -> do nothing (deferred until region activated or element moves inside with continuousUpdate)
    }

    // Helper - uses HighlightingService internals by id mapping via public API if available
    private GameObject GetTargetFromHighlightingService(HighlightingService hs, string elementId)
    {
        // HighlightingService keeps id->target privately; we used public IsRegistered and GetRegisteredIds earlier.
        // But GetRegisteredIds returns only keys. We need the GameObject instance.
        // To keep encapsulation, require that HighlightingService registers elementId => GameObject via RegisterObject(id, target)
        // and here we can find the GameObject by searching all HighlightableObject components with matching elementId.

        // Fallback: find GameObject in scene with HighlightableObject having this elementId
        var all = FindObjectsOfType<HighlightableObject>();
        foreach (var ho in all)
        {
            if (string.Equals(ho.elementId, elementId, StringComparison.OrdinalIgnoreCase))
                return ho.gameObject;
        }

        return null;
    }

    // Simple point-in-collider test:
    private bool IsPointInsideCollider(Collider c, Vector3 point)
    {
        if (c == null) return false;
        // For primitive colliders Bounds check is sufficient:
        if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider)
            return c.bounds.Contains(point);

        // For MeshCollider or complex shapes, use ClosestPoint:
        Vector3 closest = c.ClosestPoint(point);
        // If ClosestPoint == point, point is inside the collider (for non-convex mesh colliders, behavior depends on settings)
        return Vector3.Distance(closest, point) < 0.0001f;
    }
}