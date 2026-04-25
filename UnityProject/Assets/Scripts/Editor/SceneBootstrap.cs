using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceBuild.Editor
{
    public static class SceneBootstrap
    {
        private const string MenuRoot = "Tools/Project Bootstrap/";
        public const string SceneAssetPath = "Assets/Scenes/BootstrapWorld.unity";

        private const string TerrainDataAssetPath = "Assets/Scenes/BootstrapTerrainData.asset";
        private const string TerrainSourceFolder = "Assets/Art/Environment/Terrain/Source";
        private const string TerrainThemesFolder = "Assets/Art/Environment/Terrain/Themes";
        private const string GroundTileConfigAssetPath = "Assets/Settings/GroundTileConfig.asset";
        private const string PlacedObjectPerformanceConfigAssetPath = "Assets/Settings/PlacedObjectPerformanceConfig.asset";
        private const string GrassTexturePath = "Assets/Scenes/Terrain_Grass.png";
        private const string SoilTexturePath = "Assets/Scenes/Terrain_Soil.png";
        private const string GrassLayerPath = "Assets/Scenes/Terrain_Grass.terrainlayer";
        private const string SoilLayerPath = "Assets/Scenes/Terrain_Soil.terrainlayer";
        private const string GeneratedMaterialsFolder = "Assets/Materials/Generated";
        private const string PlayerGeneratedPrefabPath = "Assets/Art/Characters/Player/Generated/Bootstrap_Player.prefab";
        private const string PlayerFallbackMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_PlayerFallback.mat";
        private const string StoneFallbackMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_StoneFallback.mat";
        private const string TreeFallbackMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_TreeFallback.mat";
        private const string GroundDirtMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_Ground_Dirt.mat";
        private const string GroundGrassMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_Ground_Grass.mat";
        private const string GroundRockyMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_Ground_Rocky.mat";
        private const string GroundSandMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_Ground_Sand.mat";
        private const string GroundVisualMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_Ground_Visual.mat";
        private const string StylizedSkyMaterialPath = GeneratedMaterialsFolder + "/Bootstrap_StylizedSky.mat";
        private const string StoneGeneratedPrefabsFolder = "Assets/Art/Environment/Rocks/Generated";
        private const string TreeGeneratedPrefabPath = "Assets/Art/Environment/Trees/Generated/Bootstrap_Tree01.prefab";
        private const string BuildSurfaceTag = "BuildSurface";
        private const int BuildSurfaceLayer = 6;
        private const int BuildPreviewLayer = 7;
        private const float CameraPitchDegrees = 60f;
        private const float CameraHeight = 16f;
        private const float CameraDistance = 14f;
        private const float CameraMinHeight = 4f;
        private const float CameraMinDistance = 2.5f;
        private const float DefaultGroundTileWorldSize = 7f;
        private const int DefaultGroundGridColumns = 36;
        private const int DefaultGroundGridRows = 36;
        private const int BuildSurfaceChunkColumns = 4;
        private const int BuildSurfaceChunkRows = 4;
        private const float BuildSurfaceHeight = 0.02f;
        private const float BuildSurfaceThickness = 0.2f;
        private const float UnityPlaneWorldSize = 10f;
        private const float TreeGeneratedScaleMultiplier = 5f;
        private const string PlayerAnimatorControllerPath = "Assets/Art/Characters/Player/PlayerLocomotion.controller";
        private static readonly Color BuildZoneColor = new Color(0.73f, 0.61f, 0.42f, 1f);
        private static readonly Color ResourceMarkerColor = new Color(0.27f, 0.50f, 0.28f, 1f);

        [MenuItem(MenuRoot + "Rebuild Bootstrap Scene")]
        public static void BuildOrUpdateBootstrapScene()
        {
            CreateOrUpdateScene();
            Debug.Log($"[SceneBootstrap] Scene generated: {SceneAssetPath}");
        }

        private static void CreateOrUpdateScene()
        {
            EnsureFolder("Assets/Scenes");
            if (!ConfirmSceneOverwrite())
            {
                return;
            }

            List<GameObject> requiredEnvironmentAssets = ResolveRequiredEnvironmentAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("_BootstrapRoot");

            Light mainLight = SetupDirectionalLight(root.transform);
            Terrain terrain = SetupTerrain(root.transform);
            GameObject playerRoot = SetupPlayer(root.transform, terrain);
            Camera mainCamera = SetupMainCamera(root.transform, terrain, playerRoot);
            ConfigurePlayerMovementBindings(playerRoot, mainCamera != null ? mainCamera.transform : null);
            ConfigureTopDownCamera(mainCamera, playerRoot != null ? playerRoot.transform : null);
            PlaceEnvironmentModels(root.transform, terrain, requiredEnvironmentAssets);
            SetupDayNightCycle(root.transform, mainLight);
            SetupBuildingMvp(root.transform, terrain, requiredEnvironmentAssets, mainCamera);

            EditorSceneManager.SaveScene(scene, SceneAssetPath, true);
            EnsureBuildSettingsEntry(SceneAssetPath);
            EditorSceneManager.OpenScene(SceneAssetPath, OpenSceneMode.Single);
        }

        private static Camera SetupMainCamera(Transform parent, Terrain terrain, GameObject playerRoot)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);

            float terrainHeightAtCenter = terrain != null ? terrain.SampleHeight(Vector3.zero) : 0f;
            Vector3 focus = playerRoot != null ? playerRoot.transform.position : new Vector3(0f, terrainHeightAtCenter, 0f);
            cameraObject.transform.position = focus + new Vector3(0f, 20f, -30f);
            cameraObject.transform.rotation = Quaternion.Euler(22f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.farClipPlane = 1500f;
            camera.nearClipPlane = 0.02f;

            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void ConfigureTopDownCamera(Camera mainCamera, Transform followTarget)
        {
            if (mainCamera == null)
            {
                return;
            }

            Component cameraFollow = AddRuntimeComponent(mainCamera.gameObject, "TopDownFollowCamera");
            if (cameraFollow == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(cameraFollow);
            SerializedProperty targetProperty = serializedObject.FindProperty("target");
            if (targetProperty != null)
            {
                targetProperty.objectReferenceValue = followTarget;
            }

            SerializedProperty heightProperty = serializedObject.FindProperty("height");
            if (heightProperty != null)
            {
                heightProperty.floatValue = CameraHeight;
            }

            SerializedProperty distanceProperty = serializedObject.FindProperty("distance");
            if (distanceProperty != null)
            {
                distanceProperty.floatValue = CameraDistance;
            }

            SerializedProperty pitchProperty = serializedObject.FindProperty("pitch");
            if (pitchProperty != null)
            {
                pitchProperty.floatValue = CameraPitchDegrees;
            }

            SerializedProperty minHeightProperty = serializedObject.FindProperty("minHeight");
            if (minHeightProperty != null)
            {
                minHeightProperty.floatValue = CameraMinHeight;
            }

            SerializedProperty minDistanceProperty = serializedObject.FindProperty("minDistance");
            if (minDistanceProperty != null)
            {
                minDistanceProperty.floatValue = CameraMinDistance;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlayerMovementBindings(GameObject playerRoot, Transform cameraTransform)
        {
            if (playerRoot == null)
            {
                return;
            }

            Type moverType = ResolveRuntimeType("SimpleCharacterMover");
            if (moverType == null)
            {
                return;
            }

            Component mover = playerRoot.GetComponent(moverType);
            if (mover == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(mover);
            SerializedProperty cameraTransformProperty = serializedObject.FindProperty("cameraTransform");
            if (cameraTransformProperty != null && cameraTransform != null)
            {
                cameraTransformProperty.objectReferenceValue = cameraTransform;
            }

            SerializedProperty animatorProperty = serializedObject.FindProperty("targetAnimator");
            if (animatorProperty != null)
            {
                Animator targetAnimator = playerRoot.GetComponentInChildren<Animator>();
                animatorProperty.objectReferenceValue = targetAnimator;
            }

            SerializedProperty instantPlanarMovementProperty = serializedObject.FindProperty("instantPlanarMovement");
            if (instantPlanarMovementProperty != null)
            {
                instantPlanarMovementProperty.boolValue = true;
            }

            SerializedProperty snapMoveFacingToDirectionsProperty = serializedObject.FindProperty("snapMoveFacingToDirections");
            if (snapMoveFacingToDirectionsProperty != null)
            {
                snapMoveFacingToDirectionsProperty.boolValue = true;
            }

            SerializedProperty moveFacingStepDegreesProperty = serializedObject.FindProperty("moveFacingStepDegrees");
            if (moveFacingStepDegreesProperty != null)
            {
                moveFacingStepDegreesProperty.floatValue = 45f;
            }

            SerializedProperty instantMoveFacingRotationProperty = serializedObject.FindProperty("instantMoveFacingRotation");
            if (instantMoveFacingRotationProperty != null)
            {
                instantMoveFacingRotationProperty.boolValue = true;
            }

            SerializedProperty orientToMoveDirectionProperty = serializedObject.FindProperty("orientToMoveDirection");
            if (orientToMoveDirectionProperty != null)
            {
                orientToMoveDirectionProperty.boolValue = true;
            }

            SerializedProperty moveSpeedDampTimeProperty = serializedObject.FindProperty("moveSpeedDampTime");
            if (moveSpeedDampTimeProperty != null)
            {
                moveSpeedDampTimeProperty.floatValue = 0f;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Light SetupDirectionalLight(Transform parent)
        {
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(parent);
            lightObject.transform.rotation = Quaternion.Euler(38f, -40f, 0f);

            Light directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.2f;
            directional.shadows = LightShadows.Soft;
            RenderSettings.sun = directional;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.39f, 0.42f, 0.48f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.67f, 0.74f, 0.82f);
            RenderSettings.fogDensity = 0.003f;
            return directional;
        }

        private static void SetupDayNightCycle(Transform parent, Light directionalLight)
        {
            Material skyMaterial = EnsureStylizedSkyboxMaterial();
            if (skyMaterial != null)
            {
                RenderSettings.skybox = skyMaterial;
                DynamicGI.UpdateEnvironment();
            }

            if (directionalLight == null)
            {
                return;
            }

            GameObject cycleRoot = new GameObject("SkySystem");
            cycleRoot.transform.SetParent(parent, false);

            Component cycle = AddRuntimeComponent(cycleRoot, "StylizedDayNightCycle");
            if (cycle == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(cycle);
            SerializedProperty lightProperty = serializedObject.FindProperty("sunLight");
            if (lightProperty != null)
            {
                lightProperty.objectReferenceValue = directionalLight;
            }

            SerializedProperty skyboxProperty = serializedObject.FindProperty("skyboxMaterial");
            if (skyboxProperty != null)
            {
                skyboxProperty.objectReferenceValue = skyMaterial;
            }

            SerializedProperty durationProperty = serializedObject.FindProperty("cycleDurationSeconds");
            if (durationProperty != null)
            {
                durationProperty.floatValue = 180f;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureStylizedSkyboxMaterial()
        {
            string folder = Path.GetDirectoryName(StylizedSkyMaterialPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(StylizedSkyMaterialPath);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                return material;
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, StylizedSkyMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_SkyTint"))
            {
                material.SetColor("_SkyTint", new Color(0.37f, 0.62f, 1f));
            }

            if (material.HasProperty("_GroundColor"))
            {
                material.SetColor("_GroundColor", new Color(0.94f, 0.68f, 0.78f));
            }

            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", 1.25f);
            }

            if (material.HasProperty("_AtmosphereThickness"))
            {
                material.SetFloat("_AtmosphereThickness", 0.75f);
            }

            if (material.HasProperty("_SunSize"))
            {
                material.SetFloat("_SunSize", 0.05f);
            }

            if (material.HasProperty("_SunSizeConvergence"))
            {
                material.SetFloat("_SunSizeConvergence", 5f);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Terrain SetupTerrain(Transform parent)
        {
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataAssetPath);
            if (terrainData == null)
            {
                terrainData = new TerrainData
                {
                    heightmapResolution = 513,
                    size = new Vector3(400f, 45f, 400f)
                };

                AssetDatabase.CreateAsset(terrainData, TerrainDataAssetPath);
            }

            ApplyTerrainShape(terrainData);
            EnsureTerrainLayers(terrainData);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "Terrain";
            terrainObject.transform.SetParent(parent);
            terrainObject.transform.position = Vector3.zero;

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 1f;
            terrain.basemapDistance = 1000f;
            return terrain;
        }

        private static void ApplyTerrainShape(TerrainData terrainData)
        {
            int resolution = terrainData.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            terrainData.SetHeights(0, 0, heights);
        }

        private static void EnsureTerrainLayers(TerrainData terrainData)
        {
            Texture2D grassTexture = EnsureTextureAsset(
                GrassTexturePath,
                new Color(0.44f, 0.51f, 0.38f),
                new Color(0.48f, 0.56f, 0.42f));

            Texture2D soilTexture = EnsureTextureAsset(
                SoilTexturePath,
                new Color(0.47f, 0.39f, 0.30f),
                new Color(0.52f, 0.44f, 0.35f));

            TerrainLayer groundLayer = EnsureTerrainLayerAsset(SoilLayerPath, "Ground", soilTexture, new Vector2(32f, 32f));
            EnsureTerrainLayerAsset(GrassLayerPath, "Legacy_Grass", grassTexture, new Vector2(34f, 34f));

            terrainData.terrainLayers = new[] { groundLayer };
            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssets();
        }

        private static Texture2D EnsureTextureAsset(string assetPath, Color a, Color b)
        {
            var generated = new Texture2D(256, 256, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };

            for (int y = 0; y < generated.height; y++)
            {
                for (int x = 0; x < generated.width; x++)
                {
                    float broad = Mathf.PerlinNoise((x + 31f) * 0.018f, (y + 47f) * 0.018f);
                    float medium = Mathf.PerlinNoise((x + 11f) * 0.045f, (y + 23f) * 0.045f);
                    float sample = Mathf.Clamp01((broad * 0.92f) + (medium * 0.08f));
                    generated.SetPixel(x, y, Color.Lerp(a, b, sample));
                }
            }

            generated.Apply();
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string absoluteAssetPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(absoluteAssetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(absoluteAssetPath, generated.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureTerrainTextureImporter(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static void ConfigureTerrainTextureImporter(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static TerrainLayer EnsureTerrainLayerAsset(string assetPath, string layerName, Texture2D diffuse, Vector2 tileSize)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
            if (layer != null)
            {
                layer.diffuseTexture = diffuse;
                layer.tileSize = tileSize;
                EditorUtility.SetDirty(layer);
                return layer;
            }

            layer = new TerrainLayer
            {
                name = layerName,
                diffuseTexture = diffuse,
                tileSize = tileSize,
                metallic = 0f,
                smoothness = 0.08f
            };

            AssetDatabase.CreateAsset(layer, assetPath);
            return layer;
        }

        private static void PaintTerrainAlphamap(TerrainData terrainData)
        {
            int width = terrainData.alphamapWidth;
            int height = terrainData.alphamapHeight;
            float[,,] splatmap = new float[height, width, 2];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1);
                    float ny = y / (float)(height - 1);
                    float sample = Mathf.PerlinNoise(nx * 4f + 0.11f, ny * 4f + 0.27f);
                    float soilWeight = Mathf.Clamp01((sample - 0.45f) * 1.8f);
                    splatmap[y, x, 0] = 1f - soilWeight;
                    splatmap[y, x, 1] = soilWeight;
                }
            }

            terrainData.SetAlphamaps(0, 0, splatmap);
        }

        private static GameObject SetupPlayer(Transform parent, Terrain terrain)
        {
            GameObject playerRoot = new GameObject("PlayerRoot");
            playerRoot.transform.SetParent(parent);

            Vector3 spawnPosition = new Vector3(0f, terrain != null ? terrain.SampleHeight(Vector3.zero) : 0f, 0f);
            playerRoot.transform.position = spawnPosition + Vector3.up * 0.2f;

            CharacterController controller = playerRoot.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            AddRuntimeComponent(playerRoot, "SimpleCharacterMover");

            GameObject playerAsset = FindFirstImportedCharacter();
            if (playerAsset != null)
            {
                Material playerFallbackMaterial = EnsurePreferredFallbackMaterialAsset(
                    playerAsset,
                    PlayerFallbackMaterialPath,
                    new Color(0.72f, 0.78f, 0.92f),
                    0.18f);
                GameObject preparedPlayerAsset = CreateOrUpdateGeneratedPrefab(
                    playerAsset,
                    PlayerGeneratedPrefabPath,
                    Vector3.zero,
                    playerFallbackMaterial);

                GameObject instance = PrefabUtility.InstantiatePrefab(preparedPlayerAsset != null ? preparedPlayerAsset : playerAsset) as GameObject;
                if (instance != null)
                {
                    instance.name = "PlayerCharacter";
                    instance.transform.SetParent(playerRoot.transform, false);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    ConfigurePlayerAnimator(instance, playerAsset);
                }
            }
            else
            {
                GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                proxy.name = "PlayerProxy";
                proxy.transform.SetParent(playerRoot.transform, false);
                proxy.transform.localPosition = Vector3.zero;
            }

            return playerRoot;
        }

        private static GameObject FindFirstImportedCharacter()
        {
            string[] searchFolders = { "Assets/Art/Characters" };
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", searchFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Player/", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("/Characters/", StringComparison.OrdinalIgnoreCase))
                {
                    if (path.Contains("/Generated/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (LooksLikeAnimationAssetName(fileName))
                    {
                        continue;
                    }

                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }

            return null;
        }

        private static bool LooksLikeAnimationAssetName(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return false;
            }

            string[] keywords =
            {
                "idle",
                "stand",
                "standing",
                "run",
                "running",
                "walk",
                "jump",
                "anim"
            };

            for (int i = 0; i < keywords.Length; i++)
            {
                if (assetName.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigurePlayerAnimator(GameObject playerInstance, GameObject playerAsset)
        {
            if (playerInstance == null || playerAsset == null)
            {
                return;
            }

            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorControllerPath);
            if (controller == null)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(playerAsset);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Avatar>()
                .FirstOrDefault(candidate => candidate != null && candidate.isValid);
            if (avatar == null)
            {
                return;
            }

            Animator animator = playerInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = playerInstance.AddComponent<Animator>();
            }

            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        private static List<GameObject> ResolveRequiredEnvironmentAssets()
        {
            string[] requiredNames = { "stone01", "tree01" };
            var resolved = new List<GameObject>();
            var missing = new List<string>();

            foreach (string requiredName in requiredNames)
            {
                GameObject asset = FindFirstEnvironmentAssetByName(requiredName);
                if (asset == null)
                {
                    missing.Add(requiredName);
                    continue;
                }

                resolved.Add(asset);
            }

            if (missing.Count > 0)
            {
                ThrowMissingEnvironmentImportError(missing);
            }

            return resolved;
        }

        private static GameObject FindFirstEnvironmentAssetByName(string requiredName)
        {
            string[] searchFolders = { "Assets/Art/Environment" };
            foreach (string guid in AssetDatabase.FindAssets($"{requiredName} t:GameObject", searchFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Generated/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    continue;
                }

                if (asset.name.Contains(requiredName, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        private static void ThrowMissingEnvironmentImportError(List<string> missingModelNames)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string repoRoot = Directory.GetParent(projectRoot)?.FullName ?? string.Empty;
            string sourceTreeGlb = Path.Combine(repoRoot, "models", "scene", "tree", "tree01.glb");
            bool treeSourceExists = File.Exists(sourceTreeGlb);

            string hint = treeSourceExists
                ? "tree01.glb source exists, but no importable GameObject was found. Install a glTF importer or convert tree01.glb via Blender to FBX/Prefab."
                : "Required source files are missing. Check models/scene/tree/tree01.glb and models/scene/stone/stone01.fbx.";

            throw new InvalidOperationException(
                $"[SceneBootstrap] Missing required environment model imports: {string.Join(", ", missingModelNames)}. {hint}");
        }

        private static GameObject FindAssetByName(IEnumerable<GameObject> assets, string modelName)
        {
            foreach (GameObject asset in assets)
            {
                if (asset == null || IsGeneratedAsset(asset))
                {
                    continue;
                }

                if (asset.name.Contains(modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return asset;
                }
            }

            return null;
        }

        private static List<GameObject> PrepareStoneVariants(IEnumerable<GameObject> stoneAssets)
        {
            var prepared = new List<GameObject>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Material fallbackMaterial = EnsureFallbackMaterialAsset(StoneFallbackMaterialPath, new Color(0.54f, 0.49f, 0.42f), 0.08f);

            foreach (GameObject stoneAsset in stoneAssets)
            {
                if (stoneAsset == null)
                {
                    continue;
                }

                string normalizedId = NormalizeCatalogId(stoneAsset.name);
                if (!seenIds.Add(normalizedId))
                {
                    continue;
                }

                string prefabAssetPath = BuildGeneratedVariantPrefabPath(StoneGeneratedPrefabsFolder, "Bootstrap_", stoneAsset);
                GameObject generated = CreateOrUpdateGeneratedPrefab(
                    stoneAsset,
                    prefabAssetPath,
                    Vector3.zero,
                    fallbackMaterial,
                    forceFallbackMaterial: true);

                prepared.Add(generated != null ? generated : stoneAsset);
            }

            return prepared;
        }

        private static bool IsGeneratedAsset(GameObject asset)
        {
            if (asset == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            return path.Contains("/Generated/", StringComparison.OrdinalIgnoreCase);
        }

        private static void PlaceEnvironmentModels(Transform parent, Terrain terrain, List<GameObject> environmentAssets)
        {
            Transform environmentRoot = new GameObject("EnvironmentRoot").transform;
            environmentRoot.SetParent(parent, false);
            CreateGroundVisualSurface(environmentRoot, terrain);
        }

        private static void CreateGroundVisualSurface(Transform parent, Terrain terrain)
        {
            GroundTileConfig config = LoadOrCreateGroundTileConfig();
            Material terrainMaterial = EnsureTerrainSurfaceMaterial(config);
            if (terrainMaterial == null)
            {
                throw new InvalidOperationException(
                    "[SceneBootstrap] Missing ground theme texture under Assets/Art/Environment/Terrain/Themes. Expected dirt01/grass01/rocky01/sand01 after bootstrap import.");
            }

            float tileWorldSize = Mathf.Max(0.1f, config != null ? config.tileWorldSize : DefaultGroundTileWorldSize);
            int gridColumns = Mathf.Max(1, config != null ? config.gridColumns : DefaultGroundGridColumns);
            int gridRows = Mathf.Max(1, config != null ? config.gridRows : DefaultGroundGridRows);
            float totalWidth = gridColumns * tileWorldSize;
            float totalDepth = gridRows * tileWorldSize;
            float surfaceY = terrain != null ? terrain.SampleHeight(Vector3.zero) + BuildSurfaceHeight : BuildSurfaceHeight;
            Material visualMaterial = CreateGroundVisualMaterial(terrainMaterial, gridColumns, gridRows);

            Transform visualRoot = new GameObject("GroundVisualRoot").transform;
            visualRoot.SetParent(parent, false);
            visualRoot.gameObject.isStatic = true;

            GameObject groundVisual = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundVisual.name = "GroundVisualPlane";
            groundVisual.transform.SetParent(visualRoot, false);
            groundVisual.transform.position = new Vector3(0f, surfaceY, 0f);
            groundVisual.transform.rotation = Quaternion.identity;
            groundVisual.transform.localScale = new Vector3(
                totalWidth / UnityPlaneWorldSize,
                1f,
                totalDepth / UnityPlaneWorldSize);

            ApplyMaterialToRenderableHierarchy(groundVisual, visualMaterial);
            ConfigureGroundVisual(groundVisual);

            CreateBuildSurfaceChunks(parent, totalWidth, totalDepth, surfaceY);
            HideTerrainForSurfaceSampling(terrain);
        }

        private static GroundTileConfig LoadOrCreateGroundTileConfig()
        {
            string folder = Path.GetDirectoryName(GroundTileConfigAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            GroundTileConfig config = AssetDatabase.LoadAssetAtPath<GroundTileConfig>(GroundTileConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<GroundTileConfig>();
            config.theme = GroundTileTheme.Rocky;
            config.tileWorldSize = DefaultGroundTileWorldSize;
            config.gridColumns = DefaultGroundGridColumns;
            config.gridRows = DefaultGroundGridRows;
            AssetDatabase.CreateAsset(config, GroundTileConfigAssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static PlacedObjectPerformanceConfig LoadOrCreatePlacedObjectPerformanceConfig()
        {
            string folder = Path.GetDirectoryName(PlacedObjectPerformanceConfigAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            PlacedObjectPerformanceConfig config = AssetDatabase.LoadAssetAtPath<PlacedObjectPerformanceConfig>(PlacedObjectPerformanceConfigAssetPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<PlacedObjectPerformanceConfig>();
            config.chunkSize = 28f;
            config.visibilityUpdateInterval = 0.15f;
            config.activeDistance = 70f;
            config.maxTrackedChunkRadius = 4;
            config.maxPoolSizePerType = 128;
            AssetDatabase.CreateAsset(config, PlacedObjectPerformanceConfigAssetPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static Material EnsureTerrainSurfaceMaterial(GroundTileConfig config)
        {
            Material dirtMaterial = EnsureGroundThemeMaterial(GroundTileTheme.Dirt);
            Material grassMaterial = EnsureGroundThemeMaterial(GroundTileTheme.Grass);
            Material rockyMaterial = EnsureGroundThemeMaterial(GroundTileTheme.Rocky);
            Material sandMaterial = EnsureGroundThemeMaterial(GroundTileTheme.Sand);

            GroundTileTheme theme = config != null ? config.theme : GroundTileTheme.Rocky;
            return theme switch
            {
                GroundTileTheme.Dirt => dirtMaterial ?? rockyMaterial,
                GroundTileTheme.Grass => grassMaterial ?? rockyMaterial,
                GroundTileTheme.Sand => sandMaterial ?? rockyMaterial,
                _ => rockyMaterial ?? dirtMaterial ?? grassMaterial ?? sandMaterial
            };
        }

        private static Material CreateGroundVisualMaterial(Material sourceMaterial, int gridColumns, int gridRows)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            Material visualMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundVisualMaterialPath);
            if (visualMaterial == null)
            {
                visualMaterial = new Material(sourceMaterial)
                {
                    name = "Bootstrap_Ground_Visual"
                };
                AssetDatabase.CreateAsset(visualMaterial, GroundVisualMaterialPath);
            }
            else
            {
                visualMaterial.shader = sourceMaterial.shader;
                visualMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            }

            Vector2 tiling = new Vector2(Mathf.Max(1, gridColumns), Mathf.Max(1, gridRows));
            if (visualMaterial.HasProperty("_BaseMap"))
            {
                visualMaterial.SetTextureScale("_BaseMap", tiling);
            }

            if (visualMaterial.HasProperty("_MainTex"))
            {
                visualMaterial.SetTextureScale("_MainTex", tiling);
            }

            visualMaterial.enableInstancing = true;
            EditorUtility.SetDirty(visualMaterial);
            AssetDatabase.SaveAssets();
            return visualMaterial;
        }

        private static Material EnsureGroundThemeMaterial(GroundTileTheme theme)
        {
            string texturePath = GetGroundThemeTexturePath(theme);
            string materialPath = GetGroundThemeMaterialPath(theme);
            if (string.IsNullOrWhiteSpace(texturePath) || string.IsNullOrWhiteSpace(materialPath))
            {
                return null;
            }

            ConfigureSurfaceTextureImporter(texturePath, false, true);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                return null;
            }

            string folder = Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", Vector2.one);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", Vector2.one);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.06f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.06f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static string GetGroundThemeTexturePath(GroundTileTheme theme)
        {
            string fileName = theme switch
            {
                GroundTileTheme.Dirt => "dirt01.png",
                GroundTileTheme.Grass => "grass01.png",
                GroundTileTheme.Sand => "sand01.png",
                _ => "lunar-surface.png"
            };

            string path = $"{TerrainThemesFolder}/{fileName}";
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null ? path : string.Empty;
        }

        private static string GetGroundThemeMaterialPath(GroundTileTheme theme)
        {
            return theme switch
            {
                GroundTileTheme.Dirt => GroundDirtMaterialPath,
                GroundTileTheme.Grass => GroundGrassMaterialPath,
                GroundTileTheme.Sand => GroundSandMaterialPath,
                _ => GroundRockyMaterialPath
            };
        }

        private static GameObject SaveTemporaryPrefab(GameObject root, string prefabAssetPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                return null;
            }

            string folder = Path.GetDirectoryName(prefabAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabAssetPath);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabAssetPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab != null ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        }

        private static void ConfigureGroundVisual(GameObject tile)
        {
            if (tile == null)
            {
                return;
            }

            RemoveCollidersImmediate(tile);
            SetLayerRecursively(tile, 0);
            SetTagRecursively(tile, "Untagged");
            SetStaticRecursively(tile);

            Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void CreateBuildSurfaceChunks(Transform parent, float totalWidth, float totalDepth, float surfaceY)
        {
            Transform buildSurfaceRoot = new GameObject("BuildSurfaceRoot").transform;
            buildSurfaceRoot.SetParent(parent, false);

            float chunkWidth = totalWidth / BuildSurfaceChunkColumns;
            float chunkDepth = totalDepth / BuildSurfaceChunkRows;
            float originX = (-totalWidth * 0.5f) + (chunkWidth * 0.5f);
            float originZ = (-totalDepth * 0.5f) + (chunkDepth * 0.5f);
            float centerY = surfaceY - (BuildSurfaceThickness * 0.5f);

            for (int column = 0; column < BuildSurfaceChunkColumns; column++)
            {
                for (int row = 0; row < BuildSurfaceChunkRows; row++)
                {
                    GameObject chunk = new GameObject($"BuildSurfaceChunk_{column}_{row}");
                    chunk.transform.SetParent(buildSurfaceRoot, false);
                    chunk.transform.position = new Vector3(
                        originX + (column * chunkWidth),
                        centerY,
                        originZ + (row * chunkDepth));

                    BoxCollider collider = chunk.AddComponent<BoxCollider>();
                    collider.size = new Vector3(chunkWidth, BuildSurfaceThickness, chunkDepth);
                    ConfigureBuildSurface(chunk);
                    chunk.isStatic = true;
                }
            }
        }

        private static void HideTerrainForSurfaceSampling(Terrain terrain)
        {
            if (terrain == null)
            {
                return;
            }

            terrain.drawHeightmap = false;
            terrain.drawTreesAndFoliage = false;

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider != null)
            {
                terrainCollider.enabled = false;
            }

            SetLayerRecursively(terrain.gameObject, LayerMask.NameToLayer("Ignore Raycast"));
            terrain.gameObject.tag = "Untagged";
        }

        private static void ConfigureSurfaceTextureImporter(string assetPath, bool normalMap, bool sRgb)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
            {
                return;
            }

            bool changed = false;
            TextureImporterType desiredType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != desiredType)
            {
                importer.textureType = desiredType;
                changed = true;
            }

            if (importer.sRGBTexture != sRgb)
            {
                importer.sRGBTexture = sRgb;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Trilinear)
            {
                importer.filterMode = FilterMode.Trilinear;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ApplyMaterialToRenderableHierarchy(GameObject root, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] replacements = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int materialIndex = 0; materialIndex < replacements.Length; materialIndex++)
                {
                    replacements[materialIndex] = material;
                }

                renderer.sharedMaterials = replacements;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void AlignObjectBottomToHeight(GameObject instance, float targetHeight)
        {
            if (instance == null || !TryGetCombinedRendererBounds(instance, out Bounds bounds))
            {
                return;
            }

            float offset = targetHeight - bounds.min.y;
            Vector3 position = instance.transform.position;
            position.y += offset;
            instance.transform.position = position;
        }

        private static bool TryGetCombinedRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static void SetupBuildingMvp(
            Transform parent,
            Terrain terrain,
            List<GameObject> environmentAssets,
            Camera mainCamera)
        {
            Transform gameplayRoot = new GameObject("GameplayRoot").transform;
            gameplayRoot.SetParent(parent, false);

            Transform buildSystemRoot = new GameObject("BuildSystemRoot").transform;
            buildSystemRoot.SetParent(gameplayRoot, false);

            Vector3 buildCenter = new Vector3(12f, 0f, 10f);
            buildCenter.y = terrain != null ? terrain.SampleHeight(buildCenter) : 0f;

            Transform anchorsRoot = new GameObject("BuildAnchorsRoot").transform;
            anchorsRoot.SetParent(buildSystemRoot, false);
            CreateBuildAnchor(anchorsRoot, "BuildAnchor_Center", buildCenter);
            CreateBuildAnchor(anchorsRoot, "BuildAnchor_North", buildCenter + new Vector3(0f, 0f, 6f));
            CreateBuildAnchor(anchorsRoot, "BuildAnchor_East", buildCenter + new Vector3(6f, 0f, 0f));
            CreateBuildAnchor(anchorsRoot, "BuildAnchor_South", buildCenter + new Vector3(0f, 0f, -6f));
            CreateBuildAnchor(anchorsRoot, "BuildAnchor_West", buildCenter + new Vector3(-6f, 0f, 0f));

            Transform previewRoot = new GameObject("BuildPreviewRoot").transform;
            previewRoot.SetParent(buildSystemRoot, false);
            CreatePreviewMarker(previewRoot, buildCenter + new Vector3(0f, 0.02f, 0f));
            SetLayerRecursively(previewRoot.gameObject, BuildPreviewLayer);

            Transform placedRoot = new GameObject("PlacedStructuresRoot").transform;
            placedRoot.SetParent(buildSystemRoot, false);
            Transform placedPoolRoot = new GameObject("PlacedStructuresPoolRoot").transform;
            placedPoolRoot.SetParent(buildSystemRoot, false);
            placedPoolRoot.gameObject.SetActive(false);

            GameObject treeAsset = FindAssetByName(environmentAssets, "tree01");
            GameObject stoneAsset = FindAssetByName(environmentAssets, "stone01");
            List<GameObject> stoneAssets = ResolveOptionalAssetsByFolders(
                new[] { "Assets/Art/Environment/Rocks/Source" },
                new[] { "stone01", "stone02", "stone03" })
                .Where(asset => asset != null && asset.name.Contains("stone", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (stoneAssets.Count == 0 && stoneAsset != null)
            {
                stoneAssets.Add(stoneAsset);
            }

            List<GameObject> preparedStoneAssets = PrepareStoneVariants(stoneAssets);
            GameObject preparedStoneAsset = preparedStoneAssets.Count > 0
                ? preparedStoneAssets[0]
                : stoneAsset;
            Material treeFallbackMaterial = EnsurePreferredFallbackMaterialAsset(
                treeAsset,
                TreeFallbackMaterialPath,
                Color.white,
                0f);
            GameObject preparedTreeAsset = CreateOrUpdateGeneratedPrefab(
                treeAsset,
                TreeGeneratedPrefabPath,
                ResolveCatalogRootRotation("tree", treeAsset),
                treeFallbackMaterial,
                rootScale: Vector3.one * TreeGeneratedScaleMultiplier);
            PlacedObjectPerformanceConfig performanceConfig = LoadOrCreatePlacedObjectPerformanceConfig();

            Component placedObjectManager = AddRuntimeComponent(buildSystemRoot.gameObject, "PlacedObjectManager");
            ConfigurePlacedObjectManager(
                placedObjectManager,
                mainCamera,
                placedRoot,
                placedPoolRoot,
                performanceConfig);
            Component buildSystem = AddRuntimeComponent(buildSystemRoot.gameObject, "MinimalBuildSystem");
            ConfigureBuildSystem(
                buildSystem,
                placedObjectManager,
                mainCamera,
                previewRoot,
                placedRoot,
                preparedStoneAssets,
                preparedStoneAsset != null ? preparedStoneAsset : stoneAsset,
                preparedTreeAsset != null ? preparedTreeAsset : treeAsset);
        }

        private static void ConfigurePlacedObjectManager(
            Component placedObjectManager,
            Camera mainCamera,
            Transform activeRoot,
            Transform poolRoot,
            PlacedObjectPerformanceConfig performanceConfig)
        {
            if (placedObjectManager == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(placedObjectManager);

            SerializedProperty cameraProperty = serializedObject.FindProperty("targetCamera");
            if (cameraProperty != null)
            {
                cameraProperty.objectReferenceValue = mainCamera;
            }

            SerializedProperty activeRootProperty = serializedObject.FindProperty("activeRoot");
            if (activeRootProperty != null)
            {
                activeRootProperty.objectReferenceValue = activeRoot;
            }

            SerializedProperty poolRootProperty = serializedObject.FindProperty("poolRoot");
            if (poolRootProperty != null)
            {
                poolRootProperty.objectReferenceValue = poolRoot;
            }

            SerializedProperty configProperty = serializedObject.FindProperty("config");
            if (configProperty != null)
            {
                configProperty.objectReferenceValue = performanceConfig;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBuildSystem(
            Component buildSystem,
            Component placedObjectManager,
            Camera mainCamera,
            Transform previewRoot,
            Transform placedRoot,
            IReadOnlyList<GameObject> stoneVariants,
            GameObject stoneAsset,
            GameObject treeAsset)
        {
            if (buildSystem == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(buildSystem);

            SerializedProperty cameraProperty = serializedObject.FindProperty("buildCamera");
            if (cameraProperty != null)
            {
                cameraProperty.objectReferenceValue = mainCamera;
            }

            SerializedProperty previewRootProperty = serializedObject.FindProperty("previewRoot");
            if (previewRootProperty != null)
            {
                previewRootProperty.objectReferenceValue = previewRoot;
            }

            SerializedProperty placedRootProperty = serializedObject.FindProperty("placedRoot");
            if (placedRootProperty != null)
            {
                placedRootProperty.objectReferenceValue = placedRoot;
            }

            SerializedProperty placedObjectManagerProperty = serializedObject.FindProperty("placedObjectManager");
            if (placedObjectManagerProperty != null)
            {
                placedObjectManagerProperty.objectReferenceValue = placedObjectManager;
            }

            SerializedProperty placementMaskProperty = serializedObject.FindProperty("placementMask");
            if (placementMaskProperty != null)
            {
                placementMaskProperty.intValue = 1 << BuildSurfaceLayer;
            }

            SerializedProperty catalogProperty = serializedObject.FindProperty("catalog");
            if (catalogProperty != null)
            {
                List<GameObject> additionalBuildables = ResolveOptionalAssetsByFolders(
                    new[]
                    {
                        "Assets/Art/Buildables/Building",
                        "Assets/Art/Buildables/Props",
                        "Assets/Art/Buildables/Utility"
                    },
                    Array.Empty<string>());

                int baseCount = 2;
                int finalCount = baseCount + additionalBuildables.Count;
                catalogProperty.arraySize = finalCount;
                ConfigureCatalogEntry(
                    catalogProperty.GetArrayElementAtIndex(0),
                    "stone01",
                    "Stone 01",
                    stoneAsset,
                    PlacedObjectPerformanceCategory.Stone,
                    Vector3.zero,
                    0.9f,
                    1.1f,
                    stoneVariants,
                    "stone01",
                    "stone");
                ConfigureCatalogEntry(
                    catalogProperty.GetArrayElementAtIndex(1),
                    "tree01",
                    "Tree 01",
                    treeAsset,
                    PlacedObjectPerformanceCategory.Tree,
                    Vector3.zero,
                    0.9f,
                    1.1f,
                    null,
                    "tree01",
                    "tree");

                for (int i = 0; i < additionalBuildables.Count; i++)
                {
                    GameObject buildable = additionalBuildables[i];
                    string normalized = NormalizeCatalogId(buildable != null ? buildable.name : $"buildable_{i + 1}");
                    string displayName = ObjectNames.NicifyVariableName(normalized);
                    ConfigureCatalogEntry(
                        catalogProperty.GetArrayElementAtIndex(baseCount + i),
                        normalized,
                        displayName,
                        buildable,
                        PlacedObjectPerformanceCategory.Generic,
                        Vector3.zero,
                        1f,
                        1f,
                        null,
                        normalized);
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCatalogEntry(
            SerializedProperty entryProperty,
            string id,
            string displayName,
            GameObject prefabOverride,
            PlacedObjectPerformanceCategory performanceCategory,
            Vector3 rotationOffsetEuler,
            float minScaleMultiplier,
            float maxScaleMultiplier,
            IReadOnlyList<GameObject> prefabVariants,
            params string[] searchKeywords)
        {
            if (entryProperty == null)
            {
                return;
            }

            SerializedProperty idProperty = entryProperty.FindPropertyRelative("id");
            if (idProperty != null)
            {
                idProperty.stringValue = id;
            }

            SerializedProperty displayNameProperty = entryProperty.FindPropertyRelative("displayName");
            if (displayNameProperty != null)
            {
                displayNameProperty.stringValue = displayName;
            }

            SerializedProperty prefabOverrideProperty = entryProperty.FindPropertyRelative("prefabOverride");
            if (prefabOverrideProperty != null)
            {
                prefabOverrideProperty.objectReferenceValue = prefabOverride;
            }

            SerializedProperty prefabVariantsProperty = entryProperty.FindPropertyRelative("prefabVariants");
            if (prefabVariantsProperty != null)
            {
                int variantCount = prefabVariants != null ? prefabVariants.Count : 0;
                prefabVariantsProperty.arraySize = variantCount;
                for (int i = 0; i < variantCount; i++)
                {
                    prefabVariantsProperty.GetArrayElementAtIndex(i).objectReferenceValue = prefabVariants[i];
                }
            }

            SerializedProperty rotationOffsetProperty = entryProperty.FindPropertyRelative("rotationOffsetEuler");
            if (rotationOffsetProperty != null)
            {
                rotationOffsetProperty.vector3Value = rotationOffsetEuler;
            }

            SerializedProperty performanceCategoryProperty = entryProperty.FindPropertyRelative("performanceCategory");
            if (performanceCategoryProperty != null)
            {
                performanceCategoryProperty.enumValueIndex = (int)performanceCategory;
            }

            SerializedProperty minScaleProperty = entryProperty.FindPropertyRelative("minScaleMultiplier");
            if (minScaleProperty != null)
            {
                minScaleProperty.floatValue = minScaleMultiplier;
            }

            SerializedProperty maxScaleProperty = entryProperty.FindPropertyRelative("maxScaleMultiplier");
            if (maxScaleProperty != null)
            {
                maxScaleProperty.floatValue = maxScaleMultiplier;
            }

            SerializedProperty searchKeywordsProperty = entryProperty.FindPropertyRelative("searchKeywords");
            if (searchKeywordsProperty != null)
            {
                searchKeywordsProperty.arraySize = searchKeywords.Length;
                for (int i = 0; i < searchKeywords.Length; i++)
                {
                    searchKeywordsProperty.GetArrayElementAtIndex(i).stringValue = searchKeywords[i];
                }
            }
        }

        private static void CreateBuildAnchor(Transform parent, string anchorName, Vector3 worldPosition)
        {
            GameObject anchor = new GameObject(anchorName);
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = worldPosition;
        }

        private static void CreatePreviewMarker(Transform parent, Vector3 worldPosition)
        {
            GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            preview.name = "BuildPreviewMarker";
            preview.transform.SetParent(parent, false);
            preview.transform.position = worldPosition + new Vector3(0f, 0.03f, 0f);
            preview.transform.localScale = new Vector3(2.5f, 0.03f, 2.5f);
            ConfigureStaticMarker(preview, new Color(0.42f, 0.68f, 0.84f, 1f));

            Collider collider = preview.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void CreateResourceNode(
            Transform parent,
            Terrain terrain,
            GameObject sourceAsset,
            string nodeName,
            Vector3 worldPosition,
            float scaleMultiplier)
        {
            if (sourceAsset == null)
            {
                return;
            }

            worldPosition.y = terrain != null ? terrain.SampleHeight(worldPosition) : worldPosition.y;

            GameObject instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (instance == null)
            {
                return;
            }

            instance.name = nodeName;
            instance.transform.SetParent(parent, false);
            instance.transform.position = worldPosition;
            instance.transform.localScale = instance.transform.localScale * scaleMultiplier;
            EnsureColliderHierarchy(instance);

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"{nodeName}_Marker";
            marker.transform.SetParent(instance.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            marker.transform.localScale = new Vector3(1.8f, 0.02f, 1.8f);
            ConfigureStaticMarker(marker, ResourceMarkerColor);

            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                markerCollider.enabled = false;
            }
        }

        private static void PlaceRing(
            GameObject sourceAsset,
            Transform parent,
            Terrain terrain,
            int count,
            float radius,
            float heightOffset,
            float minScale,
            float maxScale,
            float rotationStep)
        {
            if (sourceAsset == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                Vector3 horizontal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 worldPosition = horizontal;
                worldPosition.y = (terrain != null ? terrain.SampleHeight(horizontal) : 0f) + heightOffset;

                GameObject instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
                if (instance == null)
                {
                    continue;
                }

                instance.name = sourceAsset.name;
                instance.transform.SetParent(parent);
                instance.transform.position = worldPosition;
                instance.transform.rotation = Quaternion.Euler(0f, i * rotationStep, 0f);

                float t = (i % 3) / 2f;
                float scale = Mathf.Lerp(minScale, maxScale, t);
                instance.transform.localScale = instance.transform.localScale * scale;
            }
        }

        private static void PlaceLandmarks(
            Transform parent,
            Terrain terrain,
            List<GameObject> landmarks,
            GameObject fallbackSource)
        {
            Vector3[] anchorPositions =
            {
                new Vector3(0f, 0f, 90f),
                new Vector3(95f, 0f, -30f),
                new Vector3(-88f, 0f, -52f)
            };

            if (landmarks != null && landmarks.Count > 0)
            {
                for (int i = 0; i < Mathf.Min(anchorPositions.Length, landmarks.Count); i++)
                {
                    PlaceLandmarkInstance(parent, terrain, landmarks[i], anchorPositions[i], 1.9f + i * 0.2f, i * 27f);
                }

                return;
            }

            PlaceLandmarkInstance(parent, terrain, fallbackSource, anchorPositions[0], 2.6f, 11f);
        }

        private static void PlaceLandmarkInstance(
            Transform parent,
            Terrain terrain,
            GameObject sourceAsset,
            Vector3 worldPosition,
            float scaleMultiplier,
            float yRotation)
        {
            if (sourceAsset == null)
            {
                return;
            }

            worldPosition.y = terrain != null ? terrain.SampleHeight(worldPosition) : worldPosition.y;
            GameObject instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (instance == null)
            {
                return;
            }

            instance.name = $"Landmark_{sourceAsset.name}";
            instance.transform.SetParent(parent, false);
            instance.transform.position = worldPosition;
            instance.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            instance.transform.localScale = instance.transform.localScale * scaleMultiplier;
            EnsureColliderHierarchy(instance);
        }

        private static List<GameObject> ResolveOptionalAssetsByFolders(string[] folders, string[] preferredNameHints)
        {
            var resolved = new List<GameObject>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!seenPaths.Add(path))
                    {
                        continue;
                    }

                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null)
                    {
                        continue;
                    }

                    resolved.Add(asset);
                }
            }

            if (resolved.Count <= 1 || preferredNameHints == null || preferredNameHints.Length == 0)
            {
                return resolved.OrderBy(a => a != null ? a.name : string.Empty, StringComparer.OrdinalIgnoreCase).ToList();
            }

            return resolved
                .OrderByDescending(asset => HasAnyNameHint(asset, preferredNameHints))
                .ThenBy(asset => asset != null ? asset.name : string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool HasAnyNameHint(GameObject asset, string[] hints)
        {
            if (asset == null || hints == null || hints.Length == 0)
            {
                return false;
            }

            string lowered = asset.name.ToLowerInvariant();
            for (int i = 0; i < hints.Length; i++)
            {
                string hint = hints[i];
                if (string.IsNullOrWhiteSpace(hint))
                {
                    continue;
                }

                if (lowered.Contains(hint.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeCatalogId(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                return "buildable";
            }

            var chars = new List<char>(sourceName.Length);
            for (int i = 0; i < sourceName.Length; i++)
            {
                char c = sourceName[i];
                chars.Add(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
            }

            string normalized = new string(chars.ToArray());
            while (normalized.Contains("__", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
            }

            return normalized.Trim('_');
        }

        private static string BuildGeneratedVariantPrefabPath(string folder, string prefix, GameObject sourceAsset)
        {
            string baseName = sourceAsset != null ? NormalizeCatalogId(sourceAsset.name) : "generated";
            string fileName = $"{prefix}{baseName}.prefab";
            return $"{folder}/{fileName}";
        }

        private static void EnsureColliderHierarchy(GameObject root)
        {
            bool hasCollider = root.GetComponentInChildren<Collider>() != null;
            if (hasCollider)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = root.transform.InverseTransformVector(bounds.size);
        }

        private static void RemoveCollidersImmediate(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static void SetStaticRecursively(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.isStatic = true;
            Transform rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                SetStaticRecursively(rootTransform.GetChild(i).gameObject);
            }
        }

        private static void ConfigureStaticMarker(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                material.color = color;
                renderer.sharedMaterial = material;
            }

            target.isStatic = true;
            target.tag = "Untagged";
        }

        private static void ConfigureBuildSurface(GameObject groundPlane)
        {
            if (groundPlane == null)
            {
                return;
            }

            SetLayerRecursively(groundPlane, BuildSurfaceLayer);
            SetTagRecursively(groundPlane, BuildSurfaceTag);
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

        private static void SetTagRecursively(GameObject root, string tag)
        {
            if (root == null || string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            root.tag = tag;
            Transform rootTransform = root.transform;
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                SetTagRecursively(rootTransform.GetChild(i).gameObject, tag);
            }
        }

        private static bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
        {
            if (root == null)
            {
                bounds = default;
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

        private static Vector3 ResolveCatalogRootRotation(string category, GameObject sourceAsset)
        {
            if (sourceAsset == null)
            {
                return Vector3.zero;
            }

            if (string.Equals(category, "tree", StringComparison.OrdinalIgnoreCase))
            {
                return new Vector3(90f, 0f, 0f);
            }

            return Vector3.zero;
        }

        private static GameObject CreateOrUpdateGeneratedPrefab(
            GameObject sourceAsset,
            string prefabAssetPath,
            Vector3 rootRotationEuler,
            Material fallbackMaterial,
            bool forceFallbackMaterial = false,
            Vector3? rootScale = null)
        {
            if (sourceAsset == null || string.IsNullOrWhiteSpace(prefabAssetPath))
            {
                return sourceAsset;
            }

            string folder = Path.GetDirectoryName(prefabAssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(sourceAsset) as GameObject;
            if (instance == null)
            {
                return sourceAsset;
            }

            instance.name = Path.GetFileNameWithoutExtension(prefabAssetPath);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.Euler(rootRotationEuler);
            instance.transform.localScale = rootScale ?? Vector3.one;
            ApplyFallbackMaterialIfNeeded(instance, fallbackMaterial, forceFallbackMaterial);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabAssetPath);
            }

            GameObject generatedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabAssetPath);
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            return generatedPrefab != null ? generatedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
        }

        private static Material EnsureFallbackMaterialAsset(string assetPath, Color baseColor, float smoothness, Shader overrideShader = null)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = overrideShader ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (material.HasProperty("_Tint"))
            {
                material.SetColor("_Tint", Color.white);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Material EnsurePreferredFallbackMaterialAsset(
            GameObject sourceAsset,
            string assetPath,
            Color baseColor,
            float smoothness)
        {
            Material texturedMaterial = EnsureRecoveredTextureMaterialAsset(sourceAsset, assetPath, smoothness);
            if (texturedMaterial != null)
            {
                return texturedMaterial;
            }

            return EnsureFallbackMaterialAsset(
                assetPath,
                baseColor,
                smoothness,
                Shader.Find("SpaceBuild/VertexColorUnlit"));
        }

        private static Material EnsureRecoveredTextureMaterialAsset(GameObject sourceAsset, string assetPath, float smoothness)
        {
            if (sourceAsset == null)
            {
                return null;
            }

            string sourceAssetPath = AssetDatabase.GetAssetPath(sourceAsset);
            if (string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                return null;
            }

            Texture2D[] embeddedTextures = LoadRecoverableTextures(sourceAssetPath, assetPath);
            if (embeddedTextures.Length == 0)
            {
                return null;
            }

            Texture2D baseTexture = FindEmbeddedBaseTexture(embeddedTextures);
            if (baseTexture == null)
            {
                return null;
            }

            Material material = EnsureFallbackMaterialAsset(assetPath, Color.white, smoothness);
            if (material == null)
            {
                return null;
            }

            SetMaterialTexture(material, "_BaseMap", baseTexture);
            SetMaterialTexture(material, "_MainTex", baseTexture);

            Texture2D normalTexture = FindEmbeddedTexture(embeddedTextures, "normal", "nrm");
            SetMaterialTexture(material, "_BumpMap", normalTexture);
            if (normalTexture != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Texture2D[] LoadRecoverableTextures(string sourceAssetPath, string fallbackAssetPath)
        {
            Texture2D[] embeddedTextures = AssetDatabase.LoadAllAssetsAtPath(sourceAssetPath)
                .OfType<Texture2D>()
                .Where(texture => texture != null)
                .ToArray();
            if (embeddedTextures.Length > 0)
            {
                return embeddedTextures;
            }

            string extractedFolder = BuildExtractedTextureFolderPath(sourceAssetPath, fallbackAssetPath);
            Texture2D[] extractedTextures = LoadTexturesFromFolder(extractedFolder);
            if (extractedTextures.Length > 0)
            {
                return extractedTextures;
            }

            if (!TryExtractModelTextures(sourceAssetPath, extractedFolder))
            {
                return Array.Empty<Texture2D>();
            }

            AssetDatabase.Refresh();
            embeddedTextures = AssetDatabase.LoadAllAssetsAtPath(sourceAssetPath)
                .OfType<Texture2D>()
                .Where(texture => texture != null)
                .ToArray();
            if (embeddedTextures.Length > 0)
            {
                return embeddedTextures;
            }

            return LoadTexturesFromFolder(extractedFolder);
        }

        private static Texture2D[] LoadTexturesFromFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return Array.Empty<Texture2D>();
            }

            return AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(texture => texture != null)
                .ToArray();
        }

        private static bool TryExtractModelTextures(string sourceAssetPath, string extractedFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceAssetPath)
                || string.IsNullOrWhiteSpace(extractedFolder)
                || AssetImporter.GetAtPath(sourceAssetPath) is not ModelImporter modelImporter)
            {
                return false;
            }

            EnsureFolder(extractedFolder);
            bool extracted = modelImporter.ExtractTextures(extractedFolder);
            if (!extracted)
            {
                return false;
            }

            modelImporter.SearchAndRemapMaterials(modelImporter.materialName, modelImporter.materialSearch);
            modelImporter.SaveAndReimport();
            return true;
        }

        private static string BuildExtractedTextureFolderPath(string sourceAssetPath, string fallbackAssetPath)
        {
            string fallbackFolder = Path.GetDirectoryName(fallbackAssetPath)?.Replace('\\', '/');
            string sourceName = Path.GetFileNameWithoutExtension(sourceAssetPath);
            if (string.IsNullOrWhiteSpace(fallbackFolder) || string.IsNullOrWhiteSpace(sourceName))
            {
                return fallbackFolder ?? string.Empty;
            }

            return $"{fallbackFolder}/ExtractedTextures/{sourceName}";
        }

        private static void SetMaterialTexture(Material material, string propertyName, Texture texture)
        {
            if (material == null || string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
            {
                return;
            }

            material.SetTexture(propertyName, texture);
        }

        private static Texture2D FindEmbeddedBaseTexture(IEnumerable<Texture2D> textures)
        {
            if (textures == null)
            {
                return null;
            }

            Texture2D namedBaseTexture = textures.FirstOrDefault(texture =>
                texture != null && ContainsAnyToken(texture.name, "albedo", "basecolor", "diffuse", "color", "pbr"));
            if (namedBaseTexture != null)
            {
                return namedBaseTexture;
            }

            return textures.FirstOrDefault(texture =>
                texture != null && !ContainsAnyToken(texture.name, "normal", "nrm", "rough", "metal", "ao", "occlusion", "emiss", "mask"));
        }

        private static Texture2D FindEmbeddedTexture(IEnumerable<Texture2D> textures, params string[] tokens)
        {
            if (textures == null || tokens == null || tokens.Length == 0)
            {
                return null;
            }

            return textures.FirstOrDefault(texture => texture != null && ContainsAnyToken(texture.name, tokens));
        }

        private static bool ContainsAnyToken(string value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyFallbackMaterialIfNeeded(GameObject root, Material fallbackMaterial, bool forceFallbackMaterial = false)
        {
            if (root == null || fallbackMaterial == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!forceFallbackMaterial && !RendererNeedsFallback(renderer, fallbackMaterial))
                {
                    continue;
                }

                Material[] replacements = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int materialIndex = 0; materialIndex < replacements.Length; materialIndex++)
                {
                    replacements[materialIndex] = fallbackMaterial;
                }

                renderer.sharedMaterials = replacements;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static bool RendererNeedsFallback(Renderer renderer, Material fallbackMaterial = null)
        {
            if (renderer == null)
            {
                return true;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return true;
            }

            bool hasVertexColors = RendererHasVertexColors(renderer);
            bool fallbackHasPrimaryTexture = MaterialHasPrimaryTexture(fallbackMaterial);
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    return true;
                }

                if (MaterialClearlyBroken(material))
                {
                    return true;
                }

                Texture mainTexture = GetPrimaryTexture(material);
                if (mainTexture != null)
                {
                    continue;
                }

                if (hasVertexColors && MaterialSupportsVertexColors(material))
                {
                    continue;
                }

                if (fallbackHasPrimaryTexture)
                {
                    return true;
                }

                Color color = Color.white;
                if (material.HasProperty("_BaseColor"))
                {
                    color = material.GetColor("_BaseColor");
                }
                else if (material.HasProperty("_Color"))
                {
                    color = material.GetColor("_Color");
                }

                if (!IsNearlyWhite(color))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool MaterialClearlyBroken(Material material)
        {
            if (material == null || material.shader == null)
            {
                return true;
            }

            string shaderName = material.shader.name ?? string.Empty;
            return shaderName.Contains("InternalErrorShader", StringComparison.OrdinalIgnoreCase);
        }

        private static Texture GetPrimaryTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap"))
            {
                return material.GetTexture("_BaseMap");
            }

            if (material.HasProperty("_MainTex"))
            {
                return material.GetTexture("_MainTex");
            }

            return null;
        }

        private static bool MaterialSupportsVertexColors(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            string shaderName = material.shader.name ?? string.Empty;
            return shaderName.Contains("VertexColor", StringComparison.OrdinalIgnoreCase)
                || shaderName.Contains("Vertex Color", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MaterialHasPrimaryTexture(Material material)
        {
            return GetPrimaryTexture(material) != null;
        }

        private static bool RendererHasVertexColors(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return MeshHasVertexColors(skinnedMeshRenderer.sharedMesh);
            }

            if (renderer.TryGetComponent(out MeshFilter meshFilter))
            {
                return MeshHasVertexColors(meshFilter.sharedMesh);
            }

            return false;
        }

        private static bool MeshHasVertexColors(Mesh mesh)
        {
            return mesh != null && mesh.colors32 != null && mesh.colors32.Length > 0;
        }

        private static bool IsNearlyWhite(Color color)
        {
            return color.r >= 0.92f && color.g >= 0.92f && color.b >= 0.92f;
        }

        private static bool ConfirmSceneOverwrite()
        {
            if (Application.isBatchMode)
            {
                return true;
            }

            string sceneAbsolutePath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                SceneAssetPath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(sceneAbsolutePath))
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                "Rebuild Bootstrap Scene",
                "BootstrapWorld.unity already exists. Continue and overwrite current scene content?",
                "Overwrite",
                "Cancel");
        }

        private static Component AddRuntimeComponent(GameObject target, string typeName)
        {
            Type runtimeType = ResolveRuntimeType(typeName);
            if (runtimeType == null)
            {
                return null;
            }

            Component existing = target.GetComponent(runtimeType);
            if (existing != null)
            {
                return existing;
            }

            return target.AddComponent(runtimeType);
        }

        private static Type ResolveRuntimeType(string typeName)
        {
            Type runtimeType = Type.GetType($"{typeName}, Assembly-CSharp");
            if (runtimeType != null)
            {
                return runtimeType;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);
        }

        private static void EnsureBuildSettingsEntry(string scenePath)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in existing)
            {
                if (string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            var updated = new List<EditorBuildSettingsScene>(existing)
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = updated.ToArray();
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            string[] segments = assetFolderPath.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported path: {assetFolderPath}");
            }

            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
