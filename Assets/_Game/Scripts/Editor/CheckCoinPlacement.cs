#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace DashMasters.Editor
{
    public static class CheckCoinPlacement
    {
        [MenuItem("DashMasters/Audit Coins In Active Scene")]
        public static void AuditCoins()
        {
            AuditSceneCoins(SceneManager.GetActiveScene().name);
        }

        [MenuItem("DashMasters/Audit Coins In All Scenes")]
        public static void AuditAllLevelScenes()
        {
            string[] scenes = new string[]
            {
                "Assets/_Game/Scenes/DashMastersLevel1.unity",
                "Assets/_Game/Scenes/DashMastersLevel2.unity",
                "Assets/_Game/Scenes/DashMastersLevel3.unity",
                "Assets/_Game/Scenes/DashMastersLevel.unity"
            };

            foreach (string sPath in scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(sPath, OpenSceneMode.Single);
                if (scene.IsValid())
                {
                    AuditSceneCoins(scene.name);
                }
            }
        }

        public static void AuditSceneCoins(string sceneName)
        {
            Coin[] coins = Object.FindObjectsByType<Coin>(FindObjectsInactive.Include);
            Debug.Log($"[DashMasters] Auditing scene '{sceneName}': found {coins.Length} coins.");

            TrackPath path = Object.FindAnyObjectByType<TrackPath>();
            if (path == null)
            {
                Debug.LogWarning($"[DashMasters] [{sceneName}] No TrackPath found in scene!");
                return;
            }

            int offTrackCount = 0;
            int abnormalHeightCount = 0;
            int unparentedCount = 0;

            for (int i = 0; i < coins.Length; i++)
            {
                Coin c = coins[i];
                if (c == null) continue;

                if (c.transform.parent == null || c.transform.parent.name != "--- COLLECTIBLES ---")
                {
                    unparentedCount++;
                }

                Vector3 pos = c.transform.position;
                Ray ray = new Ray(pos + Vector3.up * 5f, Vector3.down);
                RaycastHit[] hits = Physics.RaycastAll(ray, 15f);

                bool hitTrack = false;
                float trackSurfaceY = -999f;
                string hitName = "";

                foreach (var h in hits)
                {
                    if (h.collider != null && (h.collider.name.Contains("Track_Top") || h.collider.name.Contains("Slider_Bed") || h.collider.name.Contains("WavyTrack_Top")))
                    {
                        hitTrack = true;
                        trackSurfaceY = h.point.y;
                        hitName = h.collider.name;
                        break;
                    }
                }

                if (!hitTrack)
                {
                    foreach (var h in hits)
                    {
                        if (h.collider != null && (h.collider.name.Contains("Platform") || h.collider.name.Contains("Moving")))
                        {
                            hitTrack = true;
                            trackSurfaceY = h.point.y;
                            hitName = h.collider.name;
                            break;
                        }
                    }
                }

                if (!hitTrack)
                {
                    Debug.LogError($"[DashMasters] [{sceneName}] Coin '{c.name}' at pos {pos} is OFF TRACK! (no track surface detected below)");
                    offTrackCount++;
                }
                else
                {
                    float heightAboveTrack = pos.y - trackSurfaceY;
                    if (heightAboveTrack < -0.1f || heightAboveTrack > 2.5f)
                    {
                        Debug.LogWarning($"[DashMasters] [{sceneName}] Coin '{c.name}' at pos {pos} over '{hitName}' has abnormal height: {heightAboveTrack:F2}m");
                        abnormalHeightCount++;
                    }
                }
            }

            Debug.Log($"[DashMasters] [{sceneName}] Audit complete! Total coins: {coins.Length}, Off-track: {offTrackCount}, Abnormal height: {abnormalHeightCount}, Loose/Unparented: {unparentedCount}");
        }
    }
}
#endif
