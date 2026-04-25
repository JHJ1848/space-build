using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class MinimalBuildSystem : MonoBehaviour
{
    private const string BuildSurfaceLayerName = "BuildSurface";
    private const string BuildSurfaceTagName = "BuildSurface";
    private const string BuildPreviewLayerName = "BuildPreview";

    [Serializable]
    private class PlaceableDefinition
    {
        public string id;
        public string displayName;
        public string[] searchKeywords;
        public GameObject prefabOverride;
        public GameObject[] prefabVariants;
        public PlacedObjectPerformanceCategory performanceCategory;
        public Vector3 rotationOffsetEuler;
        public float minScaleMultiplier = 1f;
        public float maxScaleMultiplier = 1f;
    }

    [Header("References")]
    [SerializeField] private Camera buildCamera;
    [SerializeField] private Transform previewRoot;
    [SerializeField] private Transform placedRoot;
    [SerializeField] private PlacedObjectManager placedObjectManager;

    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float maxRayDistance = 500f;
    [SerializeField] private float rotateStepDegrees = 30f;
    [SerializeField] private bool lockToGroundUp = true;

    [Header("Delete")]
    [SerializeField] private float deleteMaxDistance = 20f;

    [Header("Catalog")]
    [SerializeField] private List<PlaceableDefinition> catalog = new List<PlaceableDefinition>();

    [Header("Debug")]
    [SerializeField] private bool showDebugHud = true;

    private readonly List<GameObject> _placedObjects = new List<GameObject>();
    private readonly List<GameObject> _templateRoots = new List<GameObject>();
    private readonly List<GameObject> _templateSourcePrefabs = new List<GameObject>();

    private int _selectedIndex;
    private float _yawRotation;
    private bool _hasPreviewPose;
    private Vector3 _previewPosition;
    private Quaternion _previewRotation;
    private Vector3 _lastAimPoint;
    private bool _hasAimPoint;
    private string _statusMessage = "Build system ready.";
    private GameObject _previewObject;
    private Material _previewValidMaterial;
    private Material _previewInvalidMaterial;
    private float _previewBottomOffset;
    private bool _hasPreviewBottomOffset;

    private void Reset()
    {
        buildCamera = Camera.main;
        previewRoot = transform;
        placedRoot = transform;
        EnsureDefaultCatalog();
    }

    private void Awake()
    {
        if (buildCamera == null)
        {
            buildCamera = Camera.main;
        }

        if (previewRoot == null)
        {
            previewRoot = transform;
        }

        if (placedRoot == null)
        {
            placedRoot = transform;
        }

        EnsureBuildSurfaceMask();
        EnsureDefaultCatalog();
        BuildTemplateCache();
        EnsurePreviewObject();
    }

    private void OnDestroy()
    {
        if (_previewObject != null)
        {
            Destroy(_previewObject);
        }

        if (_previewValidMaterial != null)
        {
            Destroy(_previewValidMaterial);
        }

        if (_previewInvalidMaterial != null)
        {
            Destroy(_previewInvalidMaterial);
        }

        for (int i = 0; i < _templateRoots.Count; i++)
        {
            if (_templateRoots[i] != null)
            {
                Destroy(_templateRoots[i]);
            }
        }
    }

    private void Update()
    {
        if (buildCamera == null)
        {
            buildCamera = Camera.main;
        }

        HandleSelectionInput();
        HandleRotationInput();
        UpdatePreviewPose();
        HandlePlaceInput();
        HandleDeleteInput();
        CleanupDestroyedPlacedObjects();
    }

    private void OnGUI()
    {
        if (!showDebugHud)
        {
            return;
        }

        const float width = 440f;
        GUILayout.BeginArea(new Rect(12f, 12f, width, 170f), GUI.skin.box);
        GUILayout.Label("MVP Build Controls");
        GUILayout.Label("1-9: Select object | R: Rotate | LMB: Place | MMB/X: Delete nearest placed");
        GUILayout.Space(4f);

        string selected = GetSelectedDisplayName();
        GUILayout.Label($"Selected: {selected}");
        GUILayout.Label($"Rotation Y: {_yawRotation:0} deg");
        int placedCount = placedObjectManager != null ? placedObjectManager.RecordCount : _placedObjects.Count;
        GUILayout.Label($"Placed Count: {placedCount}");
        GUILayout.Label($"Status: {_statusMessage}");
        GUILayout.EndArea();
    }

    private void EnsureDefaultCatalog()
    {
        if (catalog != null && catalog.Count > 0)
        {
            return;
        }

        catalog = new List<PlaceableDefinition>
        {
            new PlaceableDefinition
            {
                id = "stone01",
                displayName = "Stone 01",
                searchKeywords = new[] { "stone01", "stone" },
                performanceCategory = PlacedObjectPerformanceCategory.Stone,
                minScaleMultiplier = 0.9f,
                maxScaleMultiplier = 1.1f
            },
            new PlaceableDefinition
            {
                id = "tree01",
                displayName = "Tree 01",
                searchKeywords = new[] { "tree01", "tree" },
                performanceCategory = PlacedObjectPerformanceCategory.Tree,
                minScaleMultiplier = 0.9f,
                maxScaleMultiplier = 1.1f
            }
        };
    }

    private void BuildTemplateCache()
    {
        for (int i = 0; i < _templateRoots.Count; i++)
        {
            if (_templateRoots[i] != null)
            {
                Destroy(_templateRoots[i]);
            }
        }

        _templateRoots.Clear();
        _templateSourcePrefabs.Clear();

        for (int i = 0; i < catalog.Count; i++)
        {
            _templateRoots.Add(null);
            _templateSourcePrefabs.Add(null);
            RefreshTemplateAt(i);
        }
    }

    private void RefreshTemplateAt(int index)
    {
        if (index < 0 || index >= catalog.Count)
        {
            return;
        }

        if (index < _templateRoots.Count && _templateRoots[index] != null)
        {
            Destroy(_templateRoots[index]);
            _templateRoots[index] = null;
        }

        PlaceableDefinition definition = catalog[index];
        GameObject source = ResolveCatalogSource(definition, randomizeVariants: true);
        if (index < _templateSourcePrefabs.Count)
        {
            _templateSourcePrefabs[index] = source;
        }

        if (source == null)
        {
            return;
        }

        GameObject template = Instantiate(source);
        template.name = $"_BuildTemplate_{definition.id}";
        template.transform.SetParent(previewRoot != null ? previewRoot : transform, false);
        template.transform.localRotation = template.transform.localRotation * Quaternion.Euler(definition.rotationOffsetEuler);
        template.SetActive(false);

        if (index < _templateRoots.Count)
        {
            _templateRoots[index] = template;
        }
    }

    private GameObject ResolveCatalogSource(PlaceableDefinition definition, bool randomizeVariants)
    {
        List<GameObject> candidates = CollectCatalogSources(definition);
        if (candidates.Count > 0)
        {
            if (!randomizeVariants || candidates.Count == 1)
            {
                return candidates[0];
            }

            int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[randomIndex];
        }

        return ResolveCatalogSourceFromScene(definition);
    }

    private static List<GameObject> CollectCatalogSources(PlaceableDefinition definition)
    {
        var candidates = new List<GameObject>();
        var seen = new HashSet<GameObject>();

        if (definition?.prefabVariants != null)
        {
            for (int i = 0; i < definition.prefabVariants.Length; i++)
            {
                GameObject variant = definition.prefabVariants[i];
                if (variant == null || !seen.Add(variant))
                {
                    continue;
                }

                candidates.Add(variant);
            }
        }

        if (definition?.prefabOverride != null && seen.Add(definition.prefabOverride))
        {
            candidates.Add(definition.prefabOverride);
        }

        return candidates;
    }

    private GameObject ResolveCatalogSourceFromScene(PlaceableDefinition definition)
    {
        if (definition == null || definition.searchKeywords == null || definition.searchKeywords.Length == 0)
        {
            return null;
        }

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject candidate in allObjects)
        {
            if (candidate == null || candidate == gameObject)
            {
                continue;
            }

            if (!candidate.activeInHierarchy)
            {
                continue;
            }

            if (!HasRenderable(candidate))
            {
                continue;
            }

            string loweredName = candidate.name.ToLowerInvariant();
            for (int i = 0; i < definition.searchKeywords.Length; i++)
            {
                string keyword = definition.searchKeywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                if (loweredName.Contains(keyword.ToLowerInvariant()))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private void EnsurePreviewObject()
    {
        if (_previewValidMaterial == null)
        {
            _previewValidMaterial = CreatePreviewMaterial(new Color(0.2f, 0.95f, 0.35f, 0.42f));
        }

        if (_previewInvalidMaterial == null)
        {
            _previewInvalidMaterial = CreatePreviewMaterial(new Color(0.95f, 0.25f, 0.25f, 0.45f));
        }

        RebuildPreviewObject();
    }

    private Material CreatePreviewMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            color = color
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        ConfigureMaterialAsTransparent(material);
        return material;
    }

    private static void ConfigureMaterialAsTransparent(Material material)
    {
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void RebuildPreviewObject()
    {
        if (_previewObject != null)
        {
            Destroy(_previewObject);
            _previewObject = null;
        }

        _hasPreviewBottomOffset = false;
        _previewBottomOffset = 0f;

        GameObject selectedTemplate = GetSelectedTemplate();
        if (selectedTemplate == null)
        {
            _statusMessage = $"Missing source model: {GetSelectedDisplayName()}";
            return;
        }

        _previewObject = Instantiate(selectedTemplate);
        _previewObject.name = "_BuildPreview";
        _previewObject.transform.SetParent(previewRoot != null ? previewRoot : transform, false);
        SetLayerRecursively(_previewObject, ResolvePreviewLayer());
        _previewObject.SetActive(true);
        SetPreviewVisualState(true);
        SetObjectCollidersEnabled(_previewObject, false);
        SetObjectBehavioursEnabled(_previewObject, false);
        _hasPreviewBottomOffset = TryGetBottomOffset(_previewObject, out _previewBottomOffset);
    }

    private void HandleSelectionInput()
    {
        int selectedNumber = ReadPressedDigitKey();
        if (selectedNumber <= 0)
        {
            return;
        }

        bool changed = TrySetSelection(selectedNumber - 1);
        if (!changed)
        {
            return;
        }

        _statusMessage = $"Selected: {GetSelectedDisplayName()}";
        RebuildPreviewObject();
    }

    private bool TrySetSelection(int newIndex)
    {
        if (newIndex < 0 || newIndex >= catalog.Count || _selectedIndex == newIndex)
        {
            return false;
        }

        _selectedIndex = newIndex;
        return true;
    }

    private void HandleRotationInput()
    {
        if (!WasRotatePressed())
        {
            return;
        }

        _yawRotation = Mathf.Repeat(_yawRotation + rotateStepDegrees, 360f);
        _statusMessage = $"Rotated to {_yawRotation:0} deg.";
    }

    private void UpdatePreviewPose()
    {
        _hasPreviewPose = TryComputePlacementPose(out _previewPosition, out _previewRotation, out _lastAimPoint);
        _hasAimPoint = _hasPreviewPose;

        if (_previewObject == null)
        {
            return;
        }

        _previewObject.SetActive(_hasPreviewPose);
        if (!_hasPreviewPose)
        {
            return;
        }

        _previewObject.transform.SetPositionAndRotation(_previewPosition, _previewRotation);
        if (_hasPreviewBottomOffset)
        {
            SnapBottomToSurface(_previewObject, _previewPosition.y, _previewBottomOffset);
        }
        else
        {
            SnapBottomToSurface(_previewObject, _previewPosition.y);
        }

        SetPreviewVisualState(true);
    }

    private bool TryComputePlacementPose(out Vector3 position, out Quaternion rotation, out Vector3 aimPoint)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        aimPoint = Vector3.zero;

        if (buildCamera == null)
        {
            _statusMessage = "No active camera for build ray.";
            return false;
        }

        if (!TryGetPointerRay(out Ray ray))
        {
            _statusMessage = "No pointer available.";
            return false;
        }

        if (TryGetBuildSurfaceHit(ray, out RaycastHit hit))
        {
            aimPoint = hit.point;
            position = hit.point;
            Quaternion surfaceRotation = lockToGroundUp
                ? Quaternion.Euler(0f, _yawRotation, 0f)
                : Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, _yawRotation, 0f);
            rotation = surfaceRotation * GetSelectedTemplateRotation();
            return true;
        }

        _statusMessage = "No valid placement hit.";
        return false;
    }

    private bool TryGetBuildSurfaceHit(Ray ray, out RaycastHit terrainHit)
    {
        terrainHit = default;
        if (!Physics.Raycast(
            ray,
            out terrainHit,
            maxRayDistance,
            placementMask,
            QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return IsBuildSurfaceHit(terrainHit.collider);
    }

    private bool IsBuildSurfaceHit(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        int colliderLayer = collider.gameObject.layer;
        if ((placementMask.value & (1 << colliderLayer)) == 0)
        {
            return false;
        }

        return collider.CompareTag(BuildSurfaceTagName);
    }

    private void HandlePlaceInput()
    {
        if (!WasPlacePressed() || IsPointerOverUi())
        {
            return;
        }

        if (!_hasPreviewPose)
        {
            _statusMessage = "Cannot place: no valid target.";
            return;
        }

        GameObject template = GetSelectedTemplate();
        if (template == null)
        {
            _statusMessage = $"Cannot place: missing {GetSelectedDisplayName()} template.";
            return;
        }

        PlaceableDefinition definition = catalog[_selectedIndex];
        float placementScaleMultiplier = ResolvePlacementScaleMultiplier(definition);
        Vector3 placementScale = Vector3.one * placementScaleMultiplier;
        GameObject sourcePrefab = GetSelectedSourcePrefab();

        if (placedObjectManager != null && sourcePrefab != null)
        {
            bool registered = placedObjectManager.RegisterPlacement(
                definition.id,
                definition.performanceCategory,
                sourcePrefab,
                _previewPosition,
                _previewRotation,
                placementScale,
                out _);
            if (!registered)
            {
                _statusMessage = $"Cannot place: manager rejected {GetSelectedDisplayName()}.";
                return;
            }

            _statusMessage = $"Placed {definition.displayName}.";
            RefreshTemplateAt(_selectedIndex);
            RebuildPreviewObject();
            return;
        }

        GameObject placed = Instantiate(template);
        placed.name = definition.id;
        if (placedRoot != null)
        {
            placed.transform.SetParent(placedRoot, false);
        }

        ApplyPlacementScale(placed, placementScaleMultiplier);
        placed.transform.SetPositionAndRotation(_previewPosition, _previewRotation);
        placed.SetActive(true);
        SetObjectCollidersEnabled(placed, true);
        SetObjectBehavioursEnabled(placed, true);
        SnapBottomToSurface(placed, _previewPosition.y);

        _placedObjects.Add(placed);
        _statusMessage = $"Placed {definition.displayName}.";

        RefreshTemplateAt(_selectedIndex);
        RebuildPreviewObject();
    }

    private void HandleDeleteInput()
    {
        if (!WasDeletePressed() || IsPointerOverUi())
        {
            return;
        }

        Vector3 origin = _hasAimPoint ? _lastAimPoint : (buildCamera != null ? buildCamera.transform.position : transform.position);
        if (placedObjectManager != null)
        {
            if (placedObjectManager.TryDeleteNearest(origin, deleteMaxDistance, out float deletedDistance))
            {
                _statusMessage = $"Deleted nearest placed object ({deletedDistance:0.0}m).";
            }
            else
            {
                _statusMessage = placedObjectManager.RecordCount > 0
                    ? $"No placed object within {deleteMaxDistance:0.0}m."
                    : "No placed objects to delete.";
            }

            return;
        }

        if (_placedObjects.Count == 0)
        {
            _statusMessage = "No placed objects to delete.";
            return;
        }

        float bestDistanceSqr = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < _placedObjects.Count; i++)
        {
            GameObject candidate = _placedObjects[i];
            if (candidate == null)
            {
                continue;
            }

            float sqr = (candidate.transform.position - origin).sqrMagnitude;
            if (sqr < bestDistanceSqr)
            {
                bestDistanceSqr = sqr;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            _statusMessage = "No valid placed object found.";
            return;
        }

        float bestDistance = Mathf.Sqrt(bestDistanceSqr);
        if (bestDistance > deleteMaxDistance)
        {
            _statusMessage = $"No placed object within {deleteMaxDistance:0.0}m.";
            return;
        }

        GameObject target = _placedObjects[bestIndex];
        _placedObjects.RemoveAt(bestIndex);
        if (target != null)
        {
            Destroy(target);
        }

        _statusMessage = $"Deleted nearest placed object ({bestDistance:0.0}m).";
    }

    private void CleanupDestroyedPlacedObjects()
    {
        for (int i = _placedObjects.Count - 1; i >= 0; i--)
        {
            if (_placedObjects[i] == null)
            {
                _placedObjects.RemoveAt(i);
            }
        }
    }

    private void SnapBottomToSurface(GameObject target, float surfaceY)
    {
        if (!TryGetBottomOffset(target, out float bottomOffset))
        {
            return;
        }

        SnapBottomToSurface(target, surfaceY, bottomOffset);
    }

    private static void SnapBottomToSurface(GameObject target, float surfaceY, float bottomOffset)
    {
        if (target == null)
        {
            return;
        }

        float offset = surfaceY - (target.transform.position.y + bottomOffset);
        if (Mathf.Abs(offset) > 0.0001f)
        {
            target.transform.position += Vector3.up * offset;
        }
    }

    private void SetPreviewVisualState(bool isValid)
    {
        if (_previewObject == null)
        {
            return;
        }

        Material mat = isValid ? _previewValidMaterial : _previewInvalidMaterial;
        foreach (Renderer renderer in _previewObject.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static bool HasRenderable(GameObject gameObject)
    {
        return gameObject.GetComponentInChildren<Renderer>(true) != null;
    }

    private static bool TryGetRenderableBounds(GameObject gameObject, out Bounds bounds)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private static bool TryGetBottomOffset(GameObject gameObject, out float bottomOffset)
    {
        if (!TryGetRenderableBounds(gameObject, out Bounds bounds))
        {
            bottomOffset = 0f;
            return false;
        }

        bottomOffset = bounds.min.y - gameObject.transform.position.y;
        return true;
    }

    private static void SetObjectCollidersEnabled(GameObject root, bool enabled)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = enabled;
        }
    }

    private static void SetObjectBehavioursEnabled(GameObject root, bool enabled)
    {
        foreach (MonoBehaviour monoBehaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (monoBehaviour == null)
            {
                continue;
            }

            if (monoBehaviour is MinimalBuildSystem)
            {
                continue;
            }

            monoBehaviour.enabled = enabled;
        }
    }

    private static void ApplyPlacementScale(GameObject root, float multiplier)
    {
        if (root == null)
        {
            return;
        }

        root.transform.localScale *= multiplier;
    }

    private static float ResolvePlacementScaleMultiplier(PlaceableDefinition definition)
    {
        float minScale = Mathf.Max(0.01f, definition != null ? definition.minScaleMultiplier : 1f);
        float maxScale = Mathf.Max(minScale, definition != null ? definition.maxScaleMultiplier : minScale);
        return Mathf.Approximately(minScale, maxScale)
            ? minScale
            : UnityEngine.Random.Range(minScale, maxScale);
    }

    private string GetSelectedDisplayName()
    {
        if (catalog == null || catalog.Count == 0 || _selectedIndex < 0 || _selectedIndex >= catalog.Count)
        {
            return "None";
        }

        PlaceableDefinition definition = catalog[_selectedIndex];
        if (!string.IsNullOrWhiteSpace(definition.displayName))
        {
            return definition.displayName;
        }

        return definition.id;
    }

    private GameObject GetSelectedTemplate()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _templateRoots.Count)
        {
            return null;
        }

        return _templateRoots[_selectedIndex];
    }

    private GameObject GetSelectedSourcePrefab()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _templateSourcePrefabs.Count)
        {
            return null;
        }

        return _templateSourcePrefabs[_selectedIndex];
    }

    private bool TryGetPointerRay(out Ray ray)
    {
        ray = default;
        if (buildCamera == null)
        {
            return false;
        }

        Vector2 pointer = ReadPointerPosition();
        if (pointer.sqrMagnitude <= 0.001f)
        {
            pointer = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        ray = buildCamera.ScreenPointToRay(pointer);
        return true;
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        return eventSystem.IsPointerOverGameObject();
    }

    private static Vector2 ReadPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if (position.sqrMagnitude > 0.001f)
            {
                return position;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private static bool WasPlacePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static bool WasDeletePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.middleButton.wasPressedThisFrame)
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.xKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.X);
#else
        return false;
#endif
    }

    private static bool WasRotatePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }

    private static int ReadPressedDigitKey()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return 1;
            if (keyboard.digit2Key.wasPressedThisFrame) return 2;
            if (keyboard.digit3Key.wasPressedThisFrame) return 3;
            if (keyboard.digit4Key.wasPressedThisFrame) return 4;
            if (keyboard.digit5Key.wasPressedThisFrame) return 5;
            if (keyboard.digit6Key.wasPressedThisFrame) return 6;
            if (keyboard.digit7Key.wasPressedThisFrame) return 7;
            if (keyboard.digit8Key.wasPressedThisFrame) return 8;
            if (keyboard.digit9Key.wasPressedThisFrame) return 9;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) return 6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) return 7;
        if (Input.GetKeyDown(KeyCode.Alpha8)) return 8;
        if (Input.GetKeyDown(KeyCode.Alpha9)) return 9;
#endif
        return 0;
    }

    private void EnsureBuildSurfaceMask()
    {
        int buildSurfaceLayer = LayerMask.NameToLayer(BuildSurfaceLayerName);
        if (buildSurfaceLayer < 0)
        {
            return;
        }

        if (placementMask.value == ~0 || placementMask.value == 0)
        {
            placementMask = 1 << buildSurfaceLayer;
        }
    }

    private static int ResolvePreviewLayer()
    {
        int previewLayer = LayerMask.NameToLayer(BuildPreviewLayerName);
        if (previewLayer >= 0)
        {
            return previewLayer;
        }

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        return ignoreRaycastLayer >= 0 ? ignoreRaycastLayer : 2;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private Quaternion GetSelectedTemplateRotation()
    {
        GameObject template = GetSelectedTemplate();
        return template != null ? template.transform.localRotation : Quaternion.identity;
    }
}
