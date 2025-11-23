using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central service for managing inverted-hull outline hulls for GameObjects.
/// - Register objects (optionally with a string id)
/// - Enable/Disable highlight by GameObject or by id
/// - Sets the color from the shared material or a passed-in color using MaterialPropertyBlock.
/// </summary>
[DisallowMultipleComponent]
public class HighlightingService : MonoBehaviour
{
    [Tooltip("Material used for the outline hull. Use your working shader graph material here.")]
    [SerializeField] private Material outlineMaterial;

    // Name of the color property in your shader graph.
    private const string OUTLINE_COLOR_PROP = "_OutlineColor";

    // Hash ID for property lookup, initialized once to save performance
    private int _outlineColorID; 

    // Per-target storage
    private readonly Dictionary<GameObject, GameObject> _outlineHulls = new Dictionary<GameObject, GameObject>();
    private readonly Dictionary<string, GameObject> _idToTarget = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<GameObject> _highlighted = new HashSet<GameObject>();

    void Awake()
    {
        // Cache the shader property ID
        _outlineColorID = Shader.PropertyToID(OUTLINE_COLOR_PROP);
    }

    #region Registration

    /// <summary>
    /// Register a GameObject for highlighting. If the object has a HighlightableObject component with elementId set,
    /// the service will register the id automatically.
    /// </summary>
    public void RegisterObject(GameObject target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (_outlineHulls.ContainsKey(target))
        {
            // already registered
            return;
        }

        // Try to find a mesh filter and renderer (search children as well)
        var sourceFilter = target.GetComponentInChildren<MeshFilter>();
        var sourceRenderer = target.GetComponentInChildren<MeshRenderer>();

        if (sourceFilter == null || sourceRenderer == null)
        {
            Debug.LogWarning($"[HighlightingService] RegisterObject: '{target.name}' missing MeshFilter or MeshRenderer (in children). Cannot create outline hull.");
            return;
        }

        if (sourceFilter.sharedMesh == null)
        {
            Debug.LogWarning($"[HighlightingService] RegisterObject: '{target.name}' has no sharedMesh on its MeshFilter.");
            return;
        }

        // Create child object to render the inverted hull
        var outlineHull = new GameObject($"OutlineHull_{target.name}");
        outlineHull.transform.SetParent(target.transform, false);
        outlineHull.transform.localPosition = Vector3.zero;
        outlineHull.transform.localRotation = Quaternion.identity;
        outlineHull.transform.localScale = Vector3.one;
        outlineHull.layer = target.layer;

        // Add components
        var hullFilter = outlineHull.AddComponent<MeshFilter>();
        var hullRenderer = outlineHull.AddComponent<MeshRenderer>();

        hullFilter.sharedMesh = sourceFilter.sharedMesh;
        hullRenderer.sharedMaterial = outlineMaterial;
        hullRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        hullRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        hullRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // Start invisible
        hullRenderer.enabled = false;

        _outlineHulls[target] = outlineHull;

        // Auto-register id if HighlightableObject exists
        var hog = target.GetComponent<HighlightableObject>();
        if (hog != null && !string.IsNullOrEmpty(hog.elementId))
        {
            // ensure mapping exists
            _idToTarget[hog.elementId] = target;
            Debug.Log($"[HighlightingService] Auto-registered id '{hog.elementId}' -> GameObject '{target.name}'");
        }

        Debug.Log($"[HighlightingService] Registered object '{target.name}' with hull mesh '{sourceFilter.sharedMesh.name}'");
    }

    /// <summary>
    /// Register by id explicitly.
    /// </summary>
    public void RegisterObject(string elementId, GameObject target)
    {
        if (string.IsNullOrEmpty(elementId)) throw new ArgumentNullException(nameof(elementId));
        if (target == null) throw new ArgumentNullException(nameof(target));
        RegisterObject(target); // ensure hull exists
        _idToTarget[elementId] = target;
        Debug.Log($"[HighlightingService] RegisterObject(id): '{elementId}' -> '{target.name}'");
    }

    /// <summary>
    /// Optional utility: register all HighlightableObject components in the scene.
    /// </summary>
    [ContextMenu("Register All HighlightableObjects")]
    public void RegisterAllInScene()
    {
        var all = FindObjectsOfType<HighlightableObject>();
        int count = 0;
        foreach (var ho in all)
        {
            if (ho == null || ho.gameObject == null) continue;
            RegisterObject(ho.gameObject);
            if (!string.IsNullOrEmpty(ho.elementId))
            {
                _idToTarget[ho.elementId] = ho.gameObject;
                count++;
            }
        }
        Debug.Log($"[HighlightingService] Registered {all.Length} HighlightableObjects, id-mapped {count}");
    }

    #endregion

    #region Public API by ID

    public bool IsRegistered(string elementId) => !string.IsNullOrEmpty(elementId) && _idToTarget.ContainsKey(elementId);

    public void HighlightById(string elementId, Color? color = null)
    {
        if (string.IsNullOrEmpty(elementId))
        {
            Debug.LogWarning("[HighlightingService] HighlightById called with null/empty id.");
            return;
        }

        if (!_idToTarget.TryGetValue(elementId, out var target))
        {
            Debug.LogWarning($"[HighlightingService] HighlightById: id '{elementId}' not registered.");
            return;
        }

        EnableHighlight(target, color);
    }

    public void UnhighlightById(string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return;
        if (!_idToTarget.TryGetValue(elementId, out var target)) return;
        DisableHighlight(target);
    }

    public void ToggleById(string elementId, Color? color = null)
    {
        if (string.IsNullOrEmpty(elementId))
        {
            Debug.LogWarning("[HighlightingService] ToggleById called with null/empty id.");
            return;
        }

        if (!_idToTarget.TryGetValue(elementId, out var target))
        {
            Debug.LogWarning($"[HighlightingService] ToggleById: id '{elementId}' not registered.");
            return;
        }

        ToggleHighlight(target, color);
    }

    // Convenience explicit enable/disable by id
    public void EnableById(string elementId, Color? color = null) => HighlightById(elementId, color);
    public void DisableById(string elementId) => UnhighlightById(elementId);

    #endregion

    #region Public API by GameObject

    public void EnableHighlight(GameObject target, Color? color = null)
    {
        if (target == null) return;
        if (!_outlineHulls.ContainsKey(target))
            RegisterObject(target);

        if (!_outlineHulls.TryGetValue(target, out var hull))
        {
            Debug.LogWarning($"[HighlightingService] EnableHighlight: no hull for target '{target.name}' after registration attempt.");
            return;
        }

        var hullRen = hull.GetComponent<MeshRenderer>();
        if (hullRen == null)
        {
            Debug.LogWarning($"[HighlightingService] EnableHighlight: hull renderer missing for '{target.name}'.");
            return;
        }

        // Set color via MaterialPropertyBlock so we don't generate instances
        var mpb = new MaterialPropertyBlock();
        hullRen.GetPropertyBlock(mpb);
        
        // --- MODIFIED LOGIC START ---
        // Get the default color from the shared material
        Color defaultColor = outlineMaterial.GetColor(_outlineColorID);
        
        // Use the passed-in color (if provided), or the material's default color
        Color c = color ?? defaultColor; 
        
        mpb.SetColor(_outlineColorID, c);
        hullRen.SetPropertyBlock(mpb);
        // --- MODIFIED LOGIC END ---

        // Always ensure renderer is enabled and internal state tracks it
        hullRen.enabled = true;
        if (!_highlighted.Contains(target))
            _highlighted.Add(target);

        Debug.Log($"[HighlightingService] Enabled highlight for '{target.name}' color={c}");
    }

    public void DisableHighlight(GameObject target)
    {
        if (target == null) return;
        if (!_outlineHulls.TryGetValue(target, out var hull)) return;
        var hullRen = hull.GetComponent<MeshRenderer>();
        if (hullRen == null) return;

        hullRen.enabled = false;
        if (_highlighted.Contains(target))
            _highlighted.Remove(target);

        Debug.Log($"[HighlightingService] Disabled highlight for '{target.name}'");
    }

    public void ToggleHighlight(GameObject target, Color? color = null)
    {
        if (target == null) return;
        if (_highlighted.Contains(target))
            DisableHighlight(target);
        else
            EnableHighlight(target, color);
    }

    #endregion

    #region Helpers

    // The GetDefaultColor helper has been removed.

    /// <summary>
    /// For debugging: get a list of registered ids.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredIds() => _idToTarget.Keys;

    #endregion
}