#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DashMasters.Editor
{
    public static class DashMastersSceneBuilder
    {
        [MenuItem("DashMasters/Rebuild All Levels & Save Scenes")]
        public static void RebuildAllScenes()
        {
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel1.unity", 1);
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel2.unity", 2);
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel3.unity", 3);
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel.unity", 1);
            RebuildScene("Assets/_Game/Scenes/DashMastersTrack.unity", 1);

            Debug.Log("[DashMasters] All levels successfully rebuilt and scenes saved!");
        }

        [MenuItem("DashMasters/Rebuild Level 1 (Lagoon Sprint)")]
        public static void RebuildLevel1()
        {
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel1.unity", 1);
        }

        [MenuItem("DashMasters/Rebuild Level 2 (Coral Rapids)")]
        public static void RebuildLevel2()
        {
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel2.unity", 2);
        }

        [MenuItem("DashMasters/Rebuild Level 3 (Titan's Trench)")]
        public static void RebuildLevel3()
        {
            RebuildScene("Assets/_Game/Scenes/DashMastersLevel3.unity", 3);
        }

        private static void RebuildScene(string scenePath, int levelIndex)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogWarning($"[DashMasters] Could not open scene at {scenePath}");
                return;
            }

            Debug.Log($"[DashMasters] Building Level {levelIndex} for scene {scenePath}...");
            DashMastersRuntimeLevelBuilder.BuildLevel(levelIndex);
            DashMastersRuntimeLevelBuilder.PurgeUnwantedPlatforms();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DashMasters] Successfully saved {scenePath}");
        }
    }
}
#endif
