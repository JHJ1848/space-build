using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceBuild.Editor
{
    public static class BootstrapProject
    {
        private const string MenuRoot = "Tools/Project Bootstrap/";
        private const string PlayerSourceFolder = "Assets/Art/Characters/Player/Source";
        private const string PlayerAnimationsFolder = "Assets/Art/Characters/Player/Animations";
        private const string PlayerAnimatorControllerPath = "Assets/Art/Characters/Player/PlayerLocomotion.controller";
        private const string BuildablesBuildingSourceFolder = "Assets/Art/Buildables/Building/Source";
        private const string EnvironmentPropsSourceFolder = "Assets/Art/Environment/Props/Source";

        private static readonly (string fileName, string catalogId)[] RequiredBaseBuildings =
        {
            ("powerstation.fbx", "powerstation"),
            ("solar-panel.fbx", "solar_panel"),
            ("water-equipment.fbx", "water_equipment")
        };

        private static readonly string[] RequiredFolders =
        {
            "Assets/Art",
            "Assets/Art/Animals",
            "Assets/Art/Animals/Source",
            "Assets/Art/Characters",
            "Assets/Art/Characters/Player",
            "Assets/Art/Characters/Player/Source",
            "Assets/Art/Characters/Player/Animations",
            "Assets/Art/Environment",
            "Assets/Art/Environment/Rocks",
            "Assets/Art/Environment/Rocks/Source",
            "Assets/Art/Environment/Trees",
            "Assets/Art/Environment/Trees/Source",
            "Assets/Art/Environment/Landmarks",
            "Assets/Art/Environment/Landmarks/Source",
            "Assets/Art/Environment/Terrain",
            "Assets/Art/Environment/Terrain/Source",
            "Assets/Art/Environment/Terrain/Themes",
            "Assets/Art/Environment/Props",
            "Assets/Art/Environment/Props/Source",
            "Assets/Art/Buildables",
            "Assets/Art/Buildables/Building",
            "Assets/Art/Buildables/Building/Source",
            "Assets/Art/Buildables/Props",
            "Assets/Art/Buildables/Props/Source",
            "Assets/Art/Buildables/Utility",
            "Assets/Art/Buildables/Utility/Source",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Settings",
            "Assets/Settings/Rendering"
        };

        [MenuItem(MenuRoot + "Run Full Bootstrap")]
        public static void RunFullBootstrap()
        {
            EnsureRequiredFolders();
            int importedCount = ImportExternalModels();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigurePlayerAnimationAssets();
            ConfigureUrpIfInstalled();
            SceneBootstrap.BuildOrUpdateBootstrapScene();
            ValidateBootstrapState();
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[ProjectBootstrap] Complete. Imported/updated assets: {importedCount}. Scene: {SceneBootstrap.SceneAssetPath}");
        }

        public static void RunBatchBootstrap()
        {
            RunFullBootstrap();
            EditorApplication.Exit(0);
        }

        [MenuItem(MenuRoot + "Import External Models")]
        public static void RunModelImportOnly()
        {
            EnsureRequiredFolders();
            int importedCount = ImportExternalModels();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ProjectBootstrap] Model import complete. Imported/updated assets: {importedCount}.");
        }

        [MenuItem(MenuRoot + "Configure URP If Installed")]
        public static void RunUrpOnly()
        {
            EnsureRequiredFolders();
            ConfigureUrpIfInstalled();
        }

        private static void EnsureRequiredFolders()
        {
            foreach (string folder in RequiredFolders)
            {
                EnsureFolder(folder);
            }
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
                throw new InvalidOperationException($"Unsupported asset path: {assetFolderPath}");
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

        private static int ImportExternalModels()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string repoRoot = Directory.GetParent(projectRoot)?.FullName ?? string.Empty;
            var sourceRoots = new List<string>();
            string modelsRoot = Path.Combine(repoRoot, "models");
            if (Directory.Exists(modelsRoot))
            {
                sourceRoots.Add(modelsRoot);
            }

            string mapRoot = Path.Combine(repoRoot, "map");
            if (Directory.Exists(mapRoot))
            {
                sourceRoots.Add(mapRoot);
            }

            if (sourceRoots.Count == 0)
            {
                Debug.LogWarning($"[ProjectBootstrap] External content directories not found under repo root: {repoRoot}");
                return 0;
            }

            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".fbx",
                ".glb",
                ".gltf",
                ".obj",
                ".png",
                ".jpg",
                ".jpeg",
                ".tga",
                ".tif",
                ".tiff",
                ".psd",
                ".mat"
            };

            var files = new List<(string root, string path)>();
            for (int rootIndex = 0; rootIndex < sourceRoots.Count; rootIndex++)
            {
                string sourceRoot = sourceRoots[rootIndex];
                string[] rootFiles = Directory.GetFiles(sourceRoot, "*.*", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < rootFiles.Length; fileIndex++)
                {
                    files.Add((sourceRoot, rootFiles[fileIndex]));
                }
            }

            int copied = 0;
            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    (string sourceRoot, string sourceFile) = files[i];
                    string extension = Path.GetExtension(sourceFile);
                    if (!supportedExtensions.Contains(extension))
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "Project Bootstrap",
                        $"Importing {Path.GetFileName(sourceFile)}",
                        (i + 1f) / Mathf.Max(1, files.Count));

                    string destinationAssetDirectory = GetDestinationFolderForSource(sourceRoot, sourceFile);
                    EnsureFolder(destinationAssetDirectory);

                    string destinationAbs = Path.Combine(
                        Application.dataPath,
                        destinationAssetDirectory.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar),
                        Path.GetFileName(sourceFile));

                    if (!File.Exists(destinationAbs) || IsSourceNewer(sourceFile, destinationAbs))
                    {
                        File.Copy(sourceFile, destinationAbs, true);
                        copied++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return copied;
        }

        private static string GetDestinationFolderForSource(string sourceRoot, string sourceFile)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourceFile)
                .Replace('\\', '/')
                .ToLowerInvariant();
            string fileName = Path.GetFileName(relativePath);

            if (relativePath.StartsWith("player/anims/", StringComparison.Ordinal) ||
                relativePath.StartsWith("player/animations/", StringComparison.Ordinal))
            {
                return PlayerAnimationsFolder;
            }

            if (LooksLikePlayerAnimation(relativePath))
            {
                return PlayerAnimationsFolder;
            }

            if (relativePath.StartsWith("player/", StringComparison.Ordinal))
            {
                return PlayerSourceFolder;
            }

            if (relativePath.StartsWith("animals/", StringComparison.Ordinal))
            {
                return "Assets/Art/Animals/Source";
            }

            if (relativePath.Contains("/landmark", StringComparison.Ordinal))
            {
                return "Assets/Art/Environment/Landmarks/Source";
            }

            if (relativePath.StartsWith("layers/", StringComparison.Ordinal) &&
                (string.Equals(fileName, "dirt01.png", StringComparison.Ordinal) ||
                 string.Equals(fileName, "grass01.png", StringComparison.Ordinal) ||
                 string.Equals(fileName, "lunar-surface.png", StringComparison.Ordinal) ||
                 string.Equals(fileName, "rocky01.png", StringComparison.Ordinal) ||
                 string.Equals(fileName, "sand01.png", StringComparison.Ordinal)))
            {
                return "Assets/Art/Environment/Terrain/Themes";
            }

            if (relativePath.Contains("/terrain", StringComparison.Ordinal) ||
                relativePath.Contains("/ground", StringComparison.Ordinal))
            {
                return "Assets/Art/Environment/Terrain/Source";
            }

            if (relativePath.Contains("/tree", StringComparison.Ordinal))
            {
                return "Assets/Art/Environment/Trees/Source";
            }

            if (relativePath.Contains("/stone", StringComparison.Ordinal) || relativePath.Contains("/rock", StringComparison.Ordinal))
            {
                return "Assets/Art/Environment/Rocks/Source";
            }

            if (relativePath.StartsWith("building/base-building/", StringComparison.Ordinal) ||
                relativePath.StartsWith("building/floor/", StringComparison.Ordinal) ||
                relativePath.StartsWith("building/wall/", StringComparison.Ordinal) ||
                relativePath.StartsWith("building/stairs/", StringComparison.Ordinal) ||
                relativePath.StartsWith("building/pillar/", StringComparison.Ordinal) ||
                relativePath.StartsWith("building/roof/", StringComparison.Ordinal))
            {
                return "Assets/Art/Buildables/Building/Source";
            }

            if (relativePath.StartsWith("building/props/", StringComparison.Ordinal) ||
                relativePath.StartsWith("props/", StringComparison.Ordinal))
            {
                return "Assets/Art/Buildables/Props/Source";
            }

            if (relativePath.StartsWith("building/utility/", StringComparison.Ordinal) ||
                relativePath.StartsWith("utility/", StringComparison.Ordinal))
            {
                return "Assets/Art/Buildables/Utility/Source";
            }

            if (relativePath.StartsWith("scene/", StringComparison.Ordinal))
            {
                return "Assets/Art/Environment/Props/Source";
            }

            return "Assets/Art/Environment/Props/Source";
        }

        private static bool LooksLikePlayerAnimation(string relativePath)
        {
            if (!relativePath.StartsWith("player/", StringComparison.Ordinal))
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(relativePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string[] animationKeywords =
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

            for (int i = 0; i < animationKeywords.Length; i++)
            {
                if (fileName.Contains(animationKeywords[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSourceNewer(string sourceFile, string destinationFile)
        {
            return File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destinationFile);
        }

        private static void ConfigurePlayerAnimationAssets()
        {
            string playerModelPath = FindPrimaryPlayerModelPath();
            if (string.IsNullOrWhiteSpace(playerModelPath))
            {
                return;
            }

            ConfigurePrimaryPlayerModelImporter(playerModelPath);
            Avatar playerAvatar = LoadValidAvatar(playerModelPath);
            ConfigurePlayerAnimationImporters(playerAvatar);
            CreateOrUpdatePlayerAnimatorController();
        }

        private static string FindPrimaryPlayerModelPath()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { PlayerSourceFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (LooksLikePlayerAnimation($"player/{Path.GetFileName(path)}"))
                {
                    continue;
                }

                return path;
            }

            return string.Empty;
        }

        private static void ConfigurePrimaryPlayerModelImporter(string playerModelPath)
        {
            if (AssetImporter.GetAtPath(playerModelPath) is not ModelImporter importer)
            {
                return;
            }

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void ConfigurePlayerAnimationImporters(Avatar playerAvatar)
        {
            if (playerAvatar == null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { PlayerAnimationsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    continue;
                }

                bool changed = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    changed = true;
                }

                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    changed = true;
                }

                if (importer.sourceAvatar != playerAvatar)
                {
                    importer.sourceAvatar = playerAvatar;
                    changed = true;
                }

                if (!importer.importAnimation)
                {
                    importer.importAnimation = true;
                    changed = true;
                }

                if (ConfigurePlayerAnimationClips(importer, path))
                {
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static Avatar LoadValidAvatar(string assetPath)
        {
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is Avatar avatar && avatar.isValid)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static bool ConfigurePlayerAnimationClips(ModelImporter importer, string assetPath)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                bool shouldLoop =
                    MatchesAnyKeyword(assetName, "standing", "stand", "idle", "running", "run", "walk") ||
                    MatchesAnyKeyword(clip.name, "standing", "stand", "idle", "running", "run", "walk");

                if (!shouldLoop)
                {
                    continue;
                }

                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }

                if (!clip.loopPose)
                {
                    clip.loopPose = true;
                    changed = true;
                }

                clips[i] = clip;
            }

            if (changed)
            {
                importer.clipAnimations = clips;
            }

            return changed;
        }

        private static void CreateOrUpdatePlayerAnimatorController()
        {
            AnimationClip idleClip = FindPlayerAnimationClip(new[] { "standing", "stand", "idle" });
            AnimationClip runClip = FindPlayerAnimationClip(new[] { "running", "run", "walk" });
            if (idleClip == null && runClip == null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerAnimatorControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(PlayerAnimatorControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerAnimatorControllerPath);
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = null;
            AnimatorState runState = null;

            if (idleClip != null)
            {
                idleState = stateMachine.AddState("StandingBy");
                idleState.motion = idleClip;
                stateMachine.defaultState = idleState;
            }

            if (runClip != null)
            {
                runState = stateMachine.AddState("Running");
                runState.motion = runClip;
                if (stateMachine.defaultState == null)
                {
                    stateMachine.defaultState = runState;
                }
            }

            if (idleState != null && runState != null)
            {
                AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
                idleToRun.hasExitTime = false;
                idleToRun.duration = 0.12f;
                idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.15f, "MoveSpeed");

                AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
                runToIdle.hasExitTime = false;
                runToIdle.duration = 0.12f;
                runToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "MoveSpeed");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip FindPlayerAnimationClip(string[] keywords)
        {
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { PlayerAnimationsFolder });
            foreach (string guid in modelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                bool matches = false;
                for (int i = 0; i < keywords.Length; i++)
                {
                    if (fileName.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                    {
                        matches = true;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is AnimationClip clip &&
                        !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) &&
                        MatchesAnyKeyword(clip.name, keywords))
                    {
                        return clip;
                    }
                }

                for (int i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static bool MatchesAnyKeyword(string value, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value) || keywords == null || keywords.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    continue;
                }

                if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureUrpIfInstalled()
        {
            if (!ManifestContains("com.unity.render-pipelines.universal"))
            {
                Debug.Log("[ProjectBootstrap] URP is not present in manifest. Skipping pipeline configuration.");
                return;
            }

            RenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset") ??
                AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Rendering/UniversalRenderPipelineAsset.asset");

            if (pipelineAsset == null)
            {
                Debug.LogWarning(
                    "[ProjectBootstrap] URP is installed but no pipeline asset was found. Import the URP template assets or create a pipeline asset, then rerun bootstrap.");
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            QualitySettings.SetQualityLevel(originalQuality, false);
            QualitySettings.renderPipeline = pipelineAsset;
            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ProjectBootstrap] URP configured with pipeline asset: {AssetDatabase.GetAssetPath(pipelineAsset)}");
        }

        private static void ValidateBootstrapState()
        {
            int playerAssetCount = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art/Characters" }).Length;
            if (playerAssetCount == 0)
            {
                Debug.LogWarning("[ProjectBootstrap] No importable player model was found under Assets/Art/Characters.");
            }

            int environmentAssetCount = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Art/Environment" }).Length;
            string environmentRoot = Path.Combine(Application.dataPath, "Art", "Environment");
            int glbSourceCount = Directory.Exists(environmentRoot)
                ? Directory.GetFiles(environmentRoot, "*.glb", SearchOption.AllDirectories).Length
                : 0;
            if (environmentAssetCount == 0 && glbSourceCount > 0)
            {
                throw new InvalidOperationException(
                    "[ProjectBootstrap] GLB source files were copied, but no environment GameObject assets were imported. Install a glTF importer or convert GLB to FBX/Prefab through Blender.");
            }

            ValidateMvpEnvironmentModels();
            ValidateBaseBuildingIntegration();

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                Debug.LogWarning("[ProjectBootstrap] No active render pipeline is assigned after bootstrap.");
            }
        }

        private static void ValidateMvpEnvironmentModels()
        {
            string[] requiredModels =
            {
                "stone01",
                "tree01"
            };

            var missing = new List<string>();
            foreach (string modelName in requiredModels)
            {
                if (!HasEnvironmentGameObject(modelName))
                {
                    missing.Add(modelName);
                }
            }

            if (missing.Count == 0)
            {
                return;
            }

            string modelsRoot = Path.Combine(Directory.GetParent(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty)?.FullName ?? string.Empty, "models", "scene");
            string treeSourceHint = Path.Combine(modelsRoot, "tree", "tree01.glb");
            bool treeGlbExists = File.Exists(treeSourceHint);

            string guidance = treeGlbExists
                ? "Detected models/scene/tree/tree01.glb source. If tree01 is missing as GameObject, install a glTF importer or convert via Blender to FBX/Prefab."
                : "Ensure models/scene/stone/stone01.fbx and models/scene/tree/tree01.glb exist before bootstrap.";

            throw new InvalidOperationException(
                $"[ProjectBootstrap] MVP environment validation failed. Missing imported GameObject model(s): {string.Join(", ", missing)}. {guidance}");
        }

        private static bool HasEnvironmentGameObject(string modelName)
        {
            string[] guids = AssetDatabase.FindAssets($"{modelName} t:GameObject", new[] { "Assets/Art/Environment" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    continue;
                }

                if (asset.name.Contains(modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateBaseBuildingIntegration()
        {
            string repoRoot = Directory.GetParent(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty)?.FullName ?? string.Empty;
            string sourceRoot = Path.Combine(repoRoot, "models", "building", "base-building");
            if (!Directory.Exists(sourceRoot))
            {
                return;
            }

            var missingImportedFiles = new List<string>();
            var wrongRoutedFiles = new List<string>();
            var nonGameObjectImports = new List<string>();
            var expectedCatalogIds = new List<string> { "stone01", "tree01" };

            for (int i = 0; i < RequiredBaseBuildings.Length; i++)
            {
                (string fileName, string catalogId) = RequiredBaseBuildings[i];
                string sourceFile = Path.Combine(sourceRoot, fileName);
                if (!File.Exists(sourceFile))
                {
                    throw new InvalidOperationException(
                        $"[ProjectBootstrap] Missing required base-building source file: {sourceFile}");
                }

                string importedAssetPath = $"{BuildablesBuildingSourceFolder}/{fileName}";
                string importedAbsolutePath = ToAbsoluteAssetPath(importedAssetPath);
                if (!File.Exists(importedAbsolutePath))
                {
                    missingImportedFiles.Add(importedAssetPath);
                }
                else if (AssetDatabase.LoadAssetAtPath<GameObject>(importedAssetPath) == null)
                {
                    nonGameObjectImports.Add(importedAssetPath);
                }

                string legacyPropsPath = $"{EnvironmentPropsSourceFolder}/{fileName}";
                if (File.Exists(ToAbsoluteAssetPath(legacyPropsPath)))
                {
                    wrongRoutedFiles.Add(legacyPropsPath);
                }

                expectedCatalogIds.Add(catalogId);
            }

            if (missingImportedFiles.Count > 0 || wrongRoutedFiles.Count > 0 || nonGameObjectImports.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[ProjectBootstrap] Base-building bootstrap validation failed. " +
                    $"Missing imported files: {FormatListOrNone(missingImportedFiles)}. " +
                    $"Wrong-routed files: {FormatListOrNone(wrongRoutedFiles)}. " +
                    $"Non-GameObject imports: {FormatListOrNone(nonGameObjectImports)}.");
            }

            ValidateBuildCatalog(expectedCatalogIds);
        }

        private static void ValidateBuildCatalog(IReadOnlyList<string> expectedCatalogIds)
        {
            MinimalBuildSystem buildSystem = UnityEngine.Object.FindFirstObjectByType<MinimalBuildSystem>();
            if (buildSystem == null)
            {
                throw new InvalidOperationException(
                    "[ProjectBootstrap] Base-building bootstrap validation failed. Missing MinimalBuildSystem in the active bootstrap scene.");
            }

            var serializedObject = new SerializedObject(buildSystem);
            SerializedProperty catalogProperty = serializedObject.FindProperty("catalog");
            if (catalogProperty == null)
            {
                throw new InvalidOperationException(
                    "[ProjectBootstrap] Base-building bootstrap validation failed. MinimalBuildSystem.catalog is unavailable.");
            }

            if (catalogProperty.arraySize != expectedCatalogIds.Count)
            {
                throw new InvalidOperationException(
                    $"[ProjectBootstrap] Base-building bootstrap validation failed. " +
                    $"Expected catalog size {expectedCatalogIds.Count}, got {catalogProperty.arraySize}.");
            }

            var actualIds = new List<string>(catalogProperty.arraySize);
            for (int i = 0; i < catalogProperty.arraySize; i++)
            {
                SerializedProperty entryProperty = catalogProperty.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = entryProperty.FindPropertyRelative("id");
                string id = idProperty != null ? idProperty.stringValue : string.Empty;
                actualIds.Add(id);
            }

            var expectedSet = new HashSet<string>(expectedCatalogIds, StringComparer.OrdinalIgnoreCase);
            var actualSet = new HashSet<string>(actualIds, StringComparer.OrdinalIgnoreCase);
            if (!expectedSet.SetEquals(actualSet))
            {
                throw new InvalidOperationException(
                    $"[ProjectBootstrap] Base-building bootstrap validation failed. " +
                    $"Expected catalog ids: {string.Join(", ", expectedCatalogIds)}. " +
                    $"Actual catalog ids: {string.Join(", ", actualIds)}.");
            }
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            return Path.Combine(
                Application.dataPath,
                assetPath.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string FormatListOrNone(IReadOnlyList<string> values)
        {
            return values.Count > 0 ? string.Join(", ", values) : "<none>";
        }

        private static bool ManifestContains(string packageName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            return File.Exists(manifestPath) &&
                File.ReadAllText(manifestPath).Contains(packageName, StringComparison.Ordinal);
        }
    }
}
