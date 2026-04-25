using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlacedObjectManager : MonoBehaviour
{
    private sealed class PooledInstanceMetadata : MonoBehaviour
    {
        public Vector3 baseLocalScale = Vector3.one;
    }

    [Serializable]
    private sealed class PlacedObjectRecord
    {
        public string guid;
        public string catalogId;
        public PlacedObjectPerformanceCategory category;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector2Int chunk;
        public GameObject sourcePrefab;
        public GameObject activeInstance;
    }

    private sealed class ChunkRecord
    {
        public Vector2Int coord;
        public Vector3 center;
        public float radius;
        public readonly List<PlacedObjectRecord> records = new List<PlacedObjectRecord>();
        public int sphereIndex = -1;
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform activeRoot;
    [SerializeField] private Transform poolRoot;
    [SerializeField] private PlacedObjectPerformanceConfig config;

    private readonly List<PlacedObjectRecord> _records = new List<PlacedObjectRecord>();
    private readonly Dictionary<Vector2Int, ChunkRecord> _chunks = new Dictionary<Vector2Int, ChunkRecord>();
    private readonly Dictionary<GameObject, Stack<GameObject>> _pool = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly List<ChunkRecord> _chunkList = new List<ChunkRecord>();

    private CullingGroup _cullingGroup;
    private BoundingSphere[] _chunkSpheres = Array.Empty<BoundingSphere>();
    private float _refreshTimer;
    private bool _chunkLayoutDirty = true;

    public int RecordCount => _records.Count;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (activeRoot == null)
        {
            activeRoot = transform;
        }

        if (poolRoot == null)
        {
            GameObject pool = new GameObject("PlacedObjectPoolRoot");
            pool.transform.SetParent(transform, false);
            poolRoot = pool.transform;
        }

        if (poolRoot != null)
        {
            poolRoot.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EnsureCullingGroup();
    }

    private void OnDisable()
    {
        DisposeCullingGroup();
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        EnsureCullingGroup();
        if (_cullingGroup == null || config == null)
        {
            return;
        }

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer > 0f)
        {
            return;
        }

        _refreshTimer = Mathf.Max(0.05f, config.visibilityUpdateInterval);
        RefreshVisibility();
    }

    public bool RegisterPlacement(
        string catalogId,
        PlacedObjectPerformanceCategory category,
        GameObject sourcePrefab,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        out GameObject activeInstance)
    {
        activeInstance = null;
        if (sourcePrefab == null)
        {
            return false;
        }

        var record = new PlacedObjectRecord
        {
            guid = Guid.NewGuid().ToString("N"),
            catalogId = catalogId,
            category = category,
            position = position,
            rotation = rotation,
            scale = scale,
            chunk = WorldToChunk(position),
            sourcePrefab = sourcePrefab
        };

        _records.Add(record);
        GetOrCreateChunk(record.chunk).records.Add(record);
        _chunkLayoutDirty = true;

        RefreshVisibilityForRecord(record);
        activeInstance = record.activeInstance;
        return true;
    }

    public bool TryDeleteNearest(Vector3 origin, float maxDistance, out float distance)
    {
        distance = 0f;
        if (_records.Count == 0)
        {
            return false;
        }

        PlacedObjectRecord best = null;
        float bestDistanceSqr = maxDistance * maxDistance;
        for (int i = 0; i < _records.Count; i++)
        {
            PlacedObjectRecord record = _records[i];
            float sqr = (record.position - origin).sqrMagnitude;
            if (sqr > bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = sqr;
            best = record;
        }

        if (best == null)
        {
            return false;
        }

        distance = Mathf.Sqrt(bestDistanceSqr);
        RemoveRecord(best);
        return true;
    }

    private void RefreshVisibility()
    {
        RebuildChunkLayoutIfNeeded();
        Vector3 cameraPosition = targetCamera.transform.position;
        float activeDistanceSqr = GetActiveDistanceSqr();
        float chunkSize = Mathf.Max(1f, config.chunkSize);
        Vector2Int cameraChunk = WorldToChunk(cameraPosition);

        for (int i = 0; i < _chunkList.Count; i++)
        {
            ChunkRecord chunk = _chunkList[i];
            Vector2Int delta = chunk.coord - cameraChunk;
            bool withinChunkRadius =
                Mathf.Abs(delta.x) <= Mathf.Max(1, config.maxTrackedChunkRadius) &&
                Mathf.Abs(delta.y) <= Mathf.Max(1, config.maxTrackedChunkRadius);

            float chunkDistanceSqr = new Vector2(chunk.center.x - cameraPosition.x, chunk.center.z - cameraPosition.z).sqrMagnitude;
            bool withinDistance = chunkDistanceSqr <= activeDistanceSqr;
            bool isVisible = chunk.sphereIndex >= 0 && chunk.sphereIndex < _chunkList.Count && _cullingGroup.IsVisible(chunk.sphereIndex);
            bool shouldActivateChunk = withinChunkRadius && withinDistance && (isVisible || chunkDistanceSqr <= activeDistanceSqr + (chunkSize * chunkSize));

            for (int recordIndex = 0; recordIndex < chunk.records.Count; recordIndex++)
            {
                RefreshRecordState(chunk.records[recordIndex], shouldActivateChunk, cameraPosition);
            }
        }
    }

    private void RefreshVisibilityForRecord(PlacedObjectRecord record)
    {
        if (record == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (config == null || targetCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        bool shouldActivate = (record.position - cameraPosition).sqrMagnitude <= GetActiveDistanceSqr();
        RefreshRecordState(record, shouldActivate, cameraPosition);
    }

    private void RefreshRecordState(PlacedObjectRecord record, bool chunkActive, Vector3 cameraPosition)
    {
        if (record == null)
        {
            return;
        }

        bool shouldBeActive = chunkActive && (record.position - cameraPosition).sqrMagnitude <= GetActiveDistanceSqr();
        if (!shouldBeActive)
        {
            ReleaseRecordInstance(record);
            return;
        }

        if (record.activeInstance == null)
        {
            record.activeInstance = AcquireInstance(record.sourcePrefab);
        }

        if (record.activeInstance == null)
        {
            return;
        }

        record.activeInstance.name = BuildActiveInstanceName(record);
        record.activeInstance.transform.SetParent(activeRoot, false);
        record.activeInstance.transform.SetPositionAndRotation(record.position, record.rotation);
        ApplyRecordScale(record.activeInstance, record.scale);
    }

    private ChunkRecord GetOrCreateChunk(Vector2Int coord)
    {
        if (_chunks.TryGetValue(coord, out ChunkRecord existing))
        {
            return existing;
        }

        float chunkSize = config != null ? Mathf.Max(1f, config.chunkSize) : 28f;
        var chunk = new ChunkRecord
        {
            coord = coord,
            center = new Vector3((coord.x + 0.5f) * chunkSize, 0f, (coord.y + 0.5f) * chunkSize),
            radius = Mathf.Sqrt(chunkSize * chunkSize * 2f) * 0.5f
        };

        _chunks.Add(coord, chunk);
        return chunk;
    }

    private Vector2Int WorldToChunk(Vector3 position)
    {
        float chunkSize = config != null ? Mathf.Max(1f, config.chunkSize) : 28f;
        return new Vector2Int(
            Mathf.FloorToInt(position.x / chunkSize),
            Mathf.FloorToInt(position.z / chunkSize));
    }

    private void RebuildChunkLayoutIfNeeded()
    {
        if (!_chunkLayoutDirty || _cullingGroup == null)
        {
            return;
        }

        _chunkList.Clear();
        foreach (ChunkRecord chunk in _chunks.Values)
        {
            if (chunk.records.Count == 0)
            {
                continue;
            }

            chunk.sphereIndex = _chunkList.Count;
            _chunkList.Add(chunk);
        }

        _chunkSpheres = new BoundingSphere[_chunkList.Count];
        for (int i = 0; i < _chunkList.Count; i++)
        {
            ChunkRecord chunk = _chunkList[i];
            _chunkSpheres[i] = new BoundingSphere(chunk.center, chunk.radius);
        }

        _cullingGroup.SetBoundingSpheres(_chunkSpheres);
        _cullingGroup.SetBoundingSphereCount(_chunkSpheres.Length);
        _chunkLayoutDirty = false;
    }

    private void EnsureCullingGroup()
    {
        if (_cullingGroup != null)
        {
            if (targetCamera != null)
            {
                _cullingGroup.targetCamera = targetCamera;
                _cullingGroup.SetDistanceReferencePoint(targetCamera.transform);
            }

            return;
        }

        if (targetCamera == null || config == null)
        {
            return;
        }

        _cullingGroup = new CullingGroup
        {
            targetCamera = targetCamera
        };
        _cullingGroup.SetDistanceReferencePoint(targetCamera.transform);
        _cullingGroup.SetBoundingDistances(new[] { GetActiveDistance() });
        _chunkLayoutDirty = true;
    }

    private void DisposeCullingGroup()
    {
        if (_cullingGroup == null)
        {
            return;
        }

        _cullingGroup.Dispose();
        _cullingGroup = null;
    }

    private GameObject AcquireInstance(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (_pool.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0)
            {
                GameObject pooled = stack.Pop();
                if (pooled == null)
                {
                    continue;
                }

                pooled.transform.SetParent(activeRoot, false);
                pooled.SetActive(true);
                return pooled;
            }
        }

        GameObject instance = Instantiate(prefab, activeRoot, false);
        EnsureMetadata(instance);
        instance.SetActive(true);
        return instance;
    }

    private void ReleaseRecordInstance(PlacedObjectRecord record)
    {
        if (record == null || record.activeInstance == null)
        {
            return;
        }

        GameObject prefab = record.sourcePrefab;
        GameObject instance = record.activeInstance;
        record.activeInstance = null;

        if (prefab == null)
        {
            Destroy(instance);
            return;
        }

        if (!_pool.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            _pool.Add(prefab, stack);
        }

        if (config != null && stack.Count >= Mathf.Max(1, config.maxPoolSizePerType))
        {
            Destroy(instance);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(poolRoot, false);
        stack.Push(instance);
    }

    private void RemoveRecord(PlacedObjectRecord record)
    {
        if (record == null)
        {
            return;
        }

        ReleaseRecordInstance(record);
        _records.Remove(record);
        if (_chunks.TryGetValue(record.chunk, out ChunkRecord chunk))
        {
            chunk.records.Remove(record);
            if (chunk.records.Count == 0)
            {
                _chunks.Remove(record.chunk);
                _chunkLayoutDirty = true;
            }
        }
    }

    private float GetActiveDistance()
    {
        return config != null ? Mathf.Max(1f, config.activeDistance) : 70f;
    }

    private float GetActiveDistanceSqr()
    {
        float activeDistance = GetActiveDistance();
        return activeDistance * activeDistance;
    }

    private static string BuildActiveInstanceName(PlacedObjectRecord record)
    {
        return $"{record.catalogId}_{record.guid}";
    }

    private static void ApplyRecordScale(GameObject instance, Vector3 scaleMultiplier)
    {
        if (instance == null)
        {
            return;
        }

        PooledInstanceMetadata metadata = EnsureMetadata(instance);
        instance.transform.localScale = Vector3.Scale(metadata.baseLocalScale, scaleMultiplier);
    }

    private static PooledInstanceMetadata EnsureMetadata(GameObject instance)
    {
        PooledInstanceMetadata metadata = instance.GetComponent<PooledInstanceMetadata>();
        if (metadata != null)
        {
            return metadata;
        }

        metadata = instance.AddComponent<PooledInstanceMetadata>();
        metadata.baseLocalScale = instance.transform.localScale;
        return metadata;
    }
}
