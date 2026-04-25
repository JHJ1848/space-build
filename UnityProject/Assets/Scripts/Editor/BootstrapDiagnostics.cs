using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceBuild.Editor
{
    public static class BootstrapDiagnostics
    {
        private const string PlayerSourcePath = "Assets/Art/Characters/Player/Source/20260411144703_885c4d6d.fbx";
        private const string PlayerGeneratedPath = "Assets/Art/Characters/Player/Generated/Bootstrap_Player.prefab";
        private const string ScenePath = "Assets/Scenes/BootstrapWorld.unity";
        private const string StoneGeneratedPath = "Assets/Art/Environment/Rocks/Generated/Bootstrap_stone01.prefab";
        private const string TreeSourcePath = "Assets/Art/Environment/Trees/Source/tree01.glb";
        private const string TreeGeneratedPath = "Assets/Art/Environment/Trees/Generated/Bootstrap_Tree01.prefab";
        private static readonly string[] ExpectedCatalogIds =
        {
            "stone01",
            "tree01",
            "powerstation",
            "solar_panel",
            "water_equipment"
        };

        private static readonly string[] ExpectedBaseBuildingIds =
        {
            "powerstation",
            "solar_panel",
            "water_equipment"
        };

        public static void LogBootstrapAssetAudit()
        {
            AuditAsset("Player Source", PlayerSourcePath);
            AuditAsset("Player Generated", PlayerGeneratedPath);
            AuditAsset("Tree Source", TreeSourcePath);
            AuditAsset("Tree Generated", TreeGeneratedPath);
        }

        public static void DumpModelImporterApi()
        {
            var methods = typeof(ModelImporter)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name.Contains("Extract", StringComparison.OrdinalIgnoreCase)
                    || method.Name.Contains("Material", StringComparison.OrdinalIgnoreCase)
                    || method.Name.Contains("Remap", StringComparison.OrdinalIgnoreCase))
                .OrderBy(method => method.Name)
                .Select(method => $"{method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}"))})");

            Debug.Log("[BootstrapDiagnostics] ModelImporter API\n" + string.Join("\n", methods));
        }

        public static void ValidatePlacementSurfaceContract()
        {
            EditorSceneManager.OpenScene(ScenePath);

            MonoBehaviour buildSystem = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .FirstOrDefault(component => component != null && component.GetType().Name == "MinimalBuildSystem");
            if (buildSystem == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] MinimalBuildSystem was not found in BootstrapWorld.");
            }

            GameObject stonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StoneGeneratedPath);
            if (stonePrefab == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] Missing generated stone prefab for placement validation.");
            }

            Vector3[] positions =
            {
                new Vector3(0f, 0.15f, 0f),
                new Vector3(0.8f, 0.15f, 0f),
                new Vector3(-0.8f, 0.15f, 0f)
            };

            foreach (Vector3 position in positions)
            {
                GameObject stone = PrefabUtility.InstantiatePrefab(stonePrefab) as GameObject;
                if (stone == null)
                {
                    throw new InvalidOperationException("[BootstrapDiagnostics] Failed to instantiate generated stone prefab.");
                }

                stone.transform.position = position;
            }

            MethodInfo tryGetBuildSurfaceHit = buildSystem.GetType().GetMethod(
                "TryGetBuildSurfaceHit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (tryGetBuildSurfaceHit == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] TryGetBuildSurfaceHit reflection failed.");
            }

            object[] parameters =
            {
                new Ray(new Vector3(0f, 10f, 0f), Vector3.down),
                default(RaycastHit)
            };
            bool hit = (bool)tryGetBuildSurfaceHit.Invoke(buildSystem, parameters);
            RaycastHit surfaceHit = (RaycastHit)parameters[1];
            string colliderName = surfaceHit.collider != null ? surfaceHit.collider.name : "<null>";
            string colliderTag = surfaceHit.collider != null ? surfaceHit.collider.tag : "<null>";
            int colliderLayer = surfaceHit.collider != null ? surfaceHit.collider.gameObject.layer : -1;

            Debug.Log($"[BootstrapDiagnostics] PlacementSurfaceValidation hit={hit} collider={colliderName} tag={colliderTag} layer={colliderLayer} point={surfaceHit.point}");

            if (!hit || surfaceHit.collider == null || !surfaceHit.collider.CompareTag("BuildSurface"))
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] Build surface raycast did not resolve to the BuildSurface collider after placing stones.");
            }
        }

        public static void ValidateBaseBuildingCatalogPlacement()
        {
            EditorSceneManager.OpenScene(ScenePath);

            MinimalBuildSystem buildSystem = UnityEngine.Object.FindFirstObjectByType<MinimalBuildSystem>();
            if (buildSystem == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] MinimalBuildSystem was not found in BootstrapWorld.");
            }

            PlacedObjectManager placedObjectManager = UnityEngine.Object.FindFirstObjectByType<PlacedObjectManager>();
            if (placedObjectManager == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] PlacedObjectManager was not found in BootstrapWorld.");
            }

            var serializedObject = new SerializedObject(buildSystem);
            SerializedProperty catalogProperty = serializedObject.FindProperty("catalog");
            if (catalogProperty == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] MinimalBuildSystem.catalog is unavailable.");
            }

            string[] actualIds = Enumerable.Range(0, catalogProperty.arraySize)
                .Select(index => catalogProperty.GetArrayElementAtIndex(index).FindPropertyRelative("id")?.stringValue ?? string.Empty)
                .ToArray();

            if (catalogProperty.arraySize != ExpectedCatalogIds.Length ||
                !ExpectedCatalogIds.OrderBy(id => id).SequenceEqual(actualIds.OrderBy(id => id), StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"[BootstrapDiagnostics] Unexpected catalog ids. Expected: {string.Join(", ", ExpectedCatalogIds)}. Actual: {string.Join(", ", actualIds)}.");
            }

            MethodInfo buildTemplateCache = buildSystem.GetType().GetMethod("BuildTemplateCache", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo trySetSelection = buildSystem.GetType().GetMethod("TrySetSelection", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo getSelectedTemplate = buildSystem.GetType().GetMethod("GetSelectedTemplate", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo getSelectedSourcePrefab = buildSystem.GetType().GetMethod("GetSelectedSourcePrefab", BindingFlags.Instance | BindingFlags.NonPublic);

            if (buildTemplateCache == null || trySetSelection == null || getSelectedTemplate == null || getSelectedSourcePrefab == null)
            {
                throw new InvalidOperationException("[BootstrapDiagnostics] Reflection binding failed for MinimalBuildSystem preview verification.");
            }

            buildTemplateCache.Invoke(buildSystem, null);

            int baselineRecordCount = placedObjectManager.RecordCount;
            for (int i = 0; i < ExpectedBaseBuildingIds.Length; i++)
            {
                string catalogId = ExpectedBaseBuildingIds[i];
                int catalogIndex = Array.FindIndex(actualIds, id => string.Equals(id, catalogId, StringComparison.OrdinalIgnoreCase));
                if (catalogIndex < 0)
                {
                    throw new InvalidOperationException($"[BootstrapDiagnostics] Missing catalog entry for {catalogId}.");
                }

                trySetSelection.Invoke(buildSystem, new object[] { catalogIndex });

                GameObject templateObject = getSelectedTemplate.Invoke(buildSystem, null) as GameObject;
                if (templateObject == null)
                {
                    throw new InvalidOperationException($"[BootstrapDiagnostics] Template cache is missing for {catalogId}.");
                }

                GameObject sourcePrefab = getSelectedSourcePrefab.Invoke(buildSystem, null) as GameObject;
                if (sourcePrefab == null)
                {
                    throw new InvalidOperationException($"[BootstrapDiagnostics] Missing source prefab for {catalogId}.");
                }

                Vector3 position = new Vector3(i * 6f, 0.15f, 0f);
                bool placed = placedObjectManager.RegisterPlacement(
                    catalogId,
                    PlacedObjectPerformanceCategory.Generic,
                    sourcePrefab,
                    position,
                    Quaternion.identity,
                    Vector3.one,
                    out _);
                if (!placed)
                {
                    throw new InvalidOperationException($"[BootstrapDiagnostics] RegisterPlacement failed for {catalogId}.");
                }

                if (!placedObjectManager.TryDeleteNearest(position, 2f, out _))
                {
                    throw new InvalidOperationException($"[BootstrapDiagnostics] TryDeleteNearest failed for {catalogId}.");
                }
            }

            if (placedObjectManager.RecordCount != baselineRecordCount)
            {
                throw new InvalidOperationException(
                    $"[BootstrapDiagnostics] Placement smoke left residual records. Expected {baselineRecordCount}, got {placedObjectManager.RecordCount}.");
            }

            Debug.Log(
                $"[BootstrapDiagnostics] BaseBuildingPlacementValidation catalog={string.Join(", ", actualIds)} verified={string.Join(", ", ExpectedBaseBuildingIds)}");
        }

        private static void AuditAsset(string label, string assetPath)
        {
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            UnityEngine.Object[] representedAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);

            string header = $"[BootstrapDiagnostics] {label}\nPath: {assetPath}\nMain: {DescribeAsset(mainAsset)}\nSubAssets: {string.Join(", ", subAssets.Select(DescribeAsset))}\nRepresentations: {string.Join(", ", representedAssets.Select(DescribeAsset))}";
            Debug.Log(header);

            if (mainAsset is GameObject gameObject)
            {
                foreach (Renderer renderer in gameObject.GetComponentsInChildren<Renderer>(true))
                {
                    string rendererDetails = string.Join(" | ", renderer.sharedMaterials.Select(DescribeMaterial));
                    Debug.Log($"[BootstrapDiagnostics] {label} Renderer {renderer.name}: {renderer.GetType().Name} => {rendererDetails}");
                }
            }

            foreach (Material material in subAssets.OfType<Material>())
            {
                Debug.Log($"[BootstrapDiagnostics] {label} EmbeddedMaterial {material.name}: {DescribeMaterial(material)}");
            }

            foreach (Texture texture in subAssets.OfType<Texture>())
            {
                Debug.Log($"[BootstrapDiagnostics] {label} EmbeddedTexture {texture.name}: {texture.GetType().Name}");
            }
        }

        private static string DescribeAsset(UnityEngine.Object asset)
        {
            return asset == null ? "<null>" : $"{asset.GetType().Name}:{asset.name}";
        }

        private static string DescribeMaterial(Material material)
        {
            if (material == null)
            {
                return "<null material>";
            }

            Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Texture bumpMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
            return $"Material:{material.name}, Shader:{material.shader?.name ?? "<null>"}, BaseMap:{baseMap?.name ?? "<null>"}, MainTex:{mainTex?.name ?? "<null>"}, Bump:{bumpMap?.name ?? "<null>"}";
        }
    }
}
