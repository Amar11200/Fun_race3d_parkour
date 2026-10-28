using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DashMasters.Editor
{
    [InitializeOnLoad]
    public static class DashMastersLevelBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/DashMastersLevel.unity";
        public const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        public const string Level1ScenePath = "Assets/_Game/Scenes/DashMastersLevel1.unity";
        public const string Level2ScenePath = "Assets/_Game/Scenes/DashMastersLevel2.unity";
        public const string Level3ScenePath = "Assets/_Game/Scenes/DashMastersLevel3.unity";

        private static bool isBuilding = false;

        static DashMastersLevelBuilder()
        {
            EditorSceneManager.sceneOpened += (scene, mode) =>
            {
                DashMastersRuntimeLevelBuilder.PurgeUnwantedPlatforms();
            };

            EditorApplication.delayCall += () =>
            {
                TriggerAutoBuild();
            };
        }

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            EditorApplication.delayCall += () =>
            {
                TriggerAutoBuild();
            };
        }

        private static void TriggerAutoBuild()
        {
            if (isBuilding || EditorApplication.isPlayingOrWillChangePlaymode) return;

            DashMastersRuntimeLevelBuilder.PurgeUnwantedPlatforms();
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.isLoaded && !string.IsNullOrEmpty(active.path))
            {
                EditorSceneManager.MarkSceneDirty(active);
            }

            Debug.Log("[DashMasters] Updating all dedicated level scenes with latest obstacle designs (NO green platform under C-ring)...");
            BuildAllLevels();
        }

        [MenuItem("Tools/Dash Masters/Build All Levels", false, 1)]
        public static void BuildAllLevelsFromMenu()
        {
            BuildAllLevels();
            EditorUtility.DisplayDialog("Dash Masters 3D",
                "All 3 Levels and Dedicated Scenes built successfully!\n\n" +
                "• Level 1: Lagoon Sprint (~280m Beginner)\n" +
                "• Level 2: Coral Rapids (~420m Intermediate)\n" +
                "• Level 3: Titan's Trench (~530m Expert)\n\n" +
                "Generated Scenes in Assets/_Game/Scenes/:\n" +
                "- DashMastersLevel.unity (Master Starting Scene)\n" +
                "- DashMastersLevel1.unity\n" +
                "- DashMastersLevel2.unity\n" +
                "- DashMastersLevel3.unity\n" +
                "- Synchronized Assets/Scenes/SampleScene.unity\n\n" +
                "All scenes registered in Build Settings.\n" +
                "Press PLAY in Unity Editor to test!", "Awesome!");
        }

        [MenuItem("Tools/Dash Masters/Build Current Level Scene", false, 2)]
        public static void BuildLevelFromMenu()
        {
            BuildCompleteScene();
            EditorUtility.DisplayDialog("Dash Masters 3D",
                "Dash Masters 3D Scene built successfully!\n\n" +
                "Features:\n" +
                "- 3 Stylized Humanoid Racers: Player (Pink) + 3 AI Racers (Cobalt, Emerald, Amber)\n" +
                "- Controls: Hold [W] to run, Release [W] to stop, [Space] jump, [S] crawl/slide\n" +
                "- Physical obstacle collisions: tangential sweeps, pendulum physics, splash & stumble\n" +
                "- 100% Path-anchored coins placed strictly on the walkable road\n" +
                "- Interactive Level Selection UI with 3 selectable tracks\n\n" +
                "Saved to: Assets/_Game/Scenes/DashMastersLevel.unity\n" +
                "Press PLAY in Unity Editor to test!", "Awesome!");
        }

        [MenuItem("Tools/Dash Masters/Remove Unwanted Green Platforms", false, 3)]
        public static void PurgePlatformsFromMenu()
        {
            DashMastersRuntimeLevelBuilder.PurgeUnwantedPlatforms();
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }
            EditorUtility.DisplayDialog("Dash Masters 3D",
                "Unwanted green platforms have been completely removed from the scene and saved!\n\n" +
                "The white road remains the only walking surface beneath the C-shaped ring.", "Awesome!");
        }

        [MenuItem("Tools/Dash Masters/Build Level 1 (Lagoon Sprint - Beginner)", false, 11)]
        public static void BuildLevel1FromMenu()
        {
            BuildSingleLevelScene(1, Level1ScenePath);
            EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Dash Masters 3D", "Level 1 (Lagoon Sprint) built and loaded successfully!", "OK");
        }

        [MenuItem("Tools/Dash Masters/Build Level 2 (Coral Rapids - Intermediate)", false, 12)]
        public static void BuildLevel2FromMenu()
        {
            BuildSingleLevelScene(2, Level2ScenePath);
            EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Dash Masters 3D", "Level 2 (Coral Rapids) built and loaded successfully!", "OK");
        }

        [MenuItem("Tools/Dash Masters/Build Level 3 (Titan's Trench - Expert)", false, 13)]
        public static void BuildLevel3FromMenu()
        {
            BuildSingleLevelScene(3, Level3ScenePath);
            EditorSceneManager.OpenScene(Level3ScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("Dash Masters 3D", "Level 3 (Titan's Trench) built and loaded successfully!", "OK");
        }

        public static void BuildSceneCommandLine()
        {
            Debug.Log("[DashMasters] Starting command line multi-level scene build...");
            BuildAllLevels();
            Debug.Log("[DashMasters] Command line scene build completed successfully!");
        }

        public static void BuildAllLevels()
        {
            if (isBuilding) return;
            isBuilding = true;

            try
            {
                EnsureDirectories();

                // Save active scene if already valid
                Scene initialScene = EditorSceneManager.GetActiveScene();
                if (initialScene.isDirty && !string.IsNullOrEmpty(initialScene.path))
                {
                    EditorSceneManager.SaveScene(initialScene);
                }

                // Build Level 1 Scene
                BuildSingleLevelScene(1, Level1ScenePath);

                // Build Level 2 Scene
                BuildSingleLevelScene(2, Level2ScenePath);

                // Build Level 3 Scene
                BuildSingleLevelScene(3, Level3ScenePath);

                // Build Master starting scene (Level 1 default with dynamic Level Select system)
                BuildSingleLevelScene(1, ScenePath);

                // Build & Synchronize SampleScene.unity directly through Unity SceneManager
                BuildSingleLevelScene(1, SampleScenePath);

                // Reopen Master Scene in Editor
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                // Register all scenes in Build Settings
                ConfigureBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[DashMasters] All 3 Levels built and registered successfully!");
            }
            finally
            {
                isBuilding = false;
            }
        }

        public static void BuildSingleLevelScene(int levelIndex, string targetScenePath)
        {
            EnsureDirectories();

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DashMastersRuntimeLevelBuilder.BuildLevel(levelIndex);

            EditorSceneManager.MarkSceneDirty(newScene);
            EditorSceneManager.SaveScene(newScene, targetScenePath);
            Debug.Log($"[DashMasters] Successfully built and saved Level {levelIndex} to {targetScenePath}");
        }

        public static void BuildCompleteScene()
        {
            if (isBuilding) return;
            isBuilding = true;

            try
            {
                EnsureDirectories();

                // Build all game objects using the authoritative builder
                DashMastersRuntimeLevelBuilder.BuildCompleteLevel();

                // Mark active scene dirty
                Scene activeScene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(activeScene);

                // Save to DashMastersLevel.unity
                EditorSceneManager.SaveScene(activeScene, ScenePath);
                Debug.Log($"[DashMasters] Scene successfully populated and saved to {ScenePath}");

                // Also synchronize SampleScene.unity so playing either scene works identically
                try
                {
                    EditorSceneManager.SaveScene(activeScene, SampleScenePath);
                    Debug.Log($"[DashMasters] Synchronized SampleScene to {SampleScenePath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[DashMasters] Could not sync SampleScene: {ex.Message}");
                }

                // Ensure Build Settings has all scenes configured
                ConfigureBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                isBuilding = false;
            }
        }

        private static void EnsureDirectories()
        {
            string[] dirs = new string[]
            {
                "Assets/_Game",
                "Assets/_Game/Scenes",
                "Assets/_Game/Scripts",
                "Assets/_Game/Materials",
                "Assets/_Game/Prefabs",
                "Assets/_Game/Editor"
            };

            foreach (var d in dirs)
            {
                if (!Directory.Exists(d))
                {
                    Directory.CreateDirectory(d);
                }
            }
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();

            // 0: Master Scene
            if (File.Exists(ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            // 1: Level 1
            if (File.Exists(Level1ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(Level1ScenePath, true));
            }

            // 2: Level 2
            if (File.Exists(Level2ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(Level2ScenePath, true));
            }

            // 3: Level 3
            if (File.Exists(Level3ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(Level3ScenePath, true));
            }

            // 4: SampleScene fallback
            if (File.Exists(SampleScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[DashMasters] Configured EditorBuildSettings with {scenes.Count} scenes.");
        }
    }
}
