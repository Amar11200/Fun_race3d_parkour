using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DashMasters
{
    public static class DashMastersRuntimeLevelBuilder
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoaded()
        {
            PurgeUnwantedPlatforms();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureLevelLoaded()
        {
            // 1. Instantly purge any unwanted green platform from the scene
            PurgeUnwantedPlatforms();

            // 2. Check if the scene is missing key elements, contains outdated hoops, or lacks C-rings
            bool hasGameManager = Object.FindAnyObjectByType<GameManager>() != null;
            bool hasTrackPath = Object.FindAnyObjectByType<TrackPath>() != null;
            bool hasOldHoop = Object.FindObjectsByType<PinkSegmentedHoop>().Length > 0;
            bool hasCRing = Object.FindObjectsByType<PinkSegmentedCRing>().Length > 0;

            int lvl = 1;
            if (GameManager.Instance != null && GameManager.Instance.CurrentLevelIndex >= 1 && GameManager.Instance.CurrentLevelIndex <= 3)
            {
                lvl = GameManager.Instance.CurrentLevelIndex;
            }
            else
            {
                var gm = Object.FindAnyObjectByType<GameManager>();
                if (gm != null && gm.CurrentLevelIndex >= 1 && gm.CurrentLevelIndex <= 3)
                {
                    lvl = gm.CurrentLevelIndex;
                }
                else
                {
                    string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    if (sceneName.Contains("2")) lvl = 2;
                    else if (sceneName.Contains("3")) lvl = 3;
                }
            }

            bool hasLadderTower = Object.FindObjectsByType<LadderRotatingObstacle>().Length > 0;
            bool hasSweeperHazard = Object.FindObjectsByType<LadderSweeperHazard>().Length > 0;
            bool outdatedLadderInLevel1 = (lvl == 1 && (!hasLadderTower || !hasSweeperHazard));
            bool missingSliderInLevel2 = (lvl == 2 && (TrackPath.Instance == null || !TrackPath.Instance.IsSlider(235f)));
            bool hasScenery = GameObject.Find("--- SCENERY ---") != null;
            bool hasCollectibles = GameObject.Find("--- COLLECTIBLES ---") != null;

            if (!hasGameManager || !hasTrackPath || hasOldHoop || !hasCRing || outdatedLadderInLevel1 || missingSliderInLevel2 || !hasScenery || !hasCollectibles)
            {
                Debug.Log($"[DashMasters] Outdated scene elements detected (OldHoop={hasOldHoop}, HasCRing={hasCRing}, OutdatedLadder={outdatedLadderInLevel1}, MissingSlider={missingSliderInLevel2}, HasScenery={hasScenery}, HasCollectibles={hasCollectibles}). Rebuilding Level {lvl} fresh...");
                BuildLevel(lvl);
                PurgeUnwantedPlatforms();
            }
        }

        public static void BuildCompleteLevel()
        {
            int lvl = 1;
            if (GameManager.Instance != null && GameManager.Instance.CurrentLevelIndex >= 1 && GameManager.Instance.CurrentLevelIndex <= 3)
            {
                lvl = GameManager.Instance.CurrentLevelIndex;
            }
            BuildLevel(lvl);
        }

        public static void BuildLevel(int levelIndex)
        {
            levelIndex = Mathf.Clamp(levelIndex, 1, 3);
            ClearExistingObjects();

            // 1. Setup Materials
            MaterialDict mats = CreateRuntimeMaterials(levelIndex);

            // 2. Setup Lighting & Environment
            SetupEnvironment(levelIndex, mats);

            // 2.5 Build Distant Background Scenery
            BuildDistantScenery(levelIndex, mats);

            // 3. Build Spline Track & Waypoints
            TrackPath trackPath = BuildTrackPath(levelIndex);

            // 4. Build Exposed Elevated Track Geometry, Water & Obstacles
            List<Checkpoint> checkpoints = new List<Checkpoint>();
            FinishLine finishLine;
            BuildTrackGeometryAndHazards(levelIndex, trackPath, mats, checkpoints, out finishLine);

            // 5. Build Racers (1 Player + EXACTLY 3 AI Racers = 4 Total)
            PathPoint startPt = trackPath.EvaluateDistance(0f);
            Vector3 playerSpawn = startPt.position - (startPt.right * 0.35f) + Vector3.up * 0.35f;
            Vector3 ai1Spawn = startPt.position - (startPt.right * 1.15f) + Vector3.up * 0.35f;
            Vector3 ai2Spawn = startPt.position + (startPt.right * 1.15f) + Vector3.up * 0.35f;
            Vector3 ai3Spawn = startPt.position + (startPt.right * 0.35f) + Vector3.up * 0.35f;

            // Speed balance based on level difficulty
            float pSpeed = (levelIndex == 1) ? 7.2f : ((levelIndex == 2) ? 7.6f : 8.1f);
            float ai1Speed = (levelIndex == 1) ? 6.85f : ((levelIndex == 2) ? 7.35f : 7.95f);
            float ai2Speed = (levelIndex == 1) ? 7.05f : ((levelIndex == 2) ? 7.55f : 8.15f);
            float ai3Speed = (levelIndex == 1) ? 6.95f : ((levelIndex == 2) ? 7.45f : 8.05f);

            PlayerController player = BuildPlayer(playerSpawn, startPt.forward, mats, pSpeed);
            AIRacerController ai1 = BuildAIRacer("Racer Cobalt", mats.ai1Color, -1.15f, ai1Spawn, mats.ai1Mat, mats, ai1Speed);
            AIRacerController ai2 = BuildAIRacer("Racer Emerald", mats.ai2Color, 1.15f, ai2Spawn, mats.ai2Mat, mats, ai2Speed);
            AIRacerController ai3 = BuildAIRacer("Racer Amber", mats.ai3Color, 0.35f, ai3Spawn, mats.ai3Mat, mats, ai3Speed);
            List<AIRacerController> aiList = new List<AIRacerController> { ai1, ai2, ai3 };

            // 6. Build Camera
            BuildCamera(player.transform, levelIndex);

            // 7. Build Audio, FX & GameManager
            GameObject managersObj = GameObject.Find("--- MANAGERS ---");
            if (managersObj == null) managersObj = new GameObject("--- MANAGERS ---");
            if (managersObj.GetComponent<AudioManager>() == null) managersObj.AddComponent<AudioManager>();
            if (managersObj.GetComponent<ProceduralFX>() == null) managersObj.AddComponent<ProceduralFX>();

            GameManager gameMgr = managersObj.GetComponent<GameManager>() ?? managersObj.AddComponent<GameManager>();
            gameMgr.Configure(player, aiList, finishLine, checkpoints.ToArray(), trackPath.TotalLength, playerSpawn,
                new Vector3[] { ai1Spawn, ai2Spawn, ai3Spawn }, levelIndex);

            // 8. Build Complete UI Canvas & UIManager
            BuildUI();

            Debug.Log($"[DashMasters] Level {levelIndex} built successfully! Total length: {trackPath.TotalLength:F1}m | 3 AI Racers active.");
        }

        private static void ClearExistingObjects()
        {
            string[] tagsToClean = new string[]
            {
                "--- TRACK ---",
                "--- WATER ---",
                "--- SCENERY ---",
                "--- COLLECTIBLES ---",
                "Player",
                "Racer Cobalt",
                "Racer Emerald",
                "Racer Amber",
                "Canvas",
                "EventSystem",
                "Directional Light",
                "Main Camera",
                "TrackPath"
            };

            foreach (string name in tagsToClean)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            // Thoroughly wipe ANY loose or orphaned Coin components anywhere in the scene
            Coin[] looseCoins = Object.FindObjectsByType<Coin>(FindObjectsInactive.Include);
            for (int i = looseCoins.Length - 1; i >= 0; i--)
            {
                if (looseCoins[i] != null && looseCoins[i].gameObject != null)
                {
                    Object.DestroyImmediate(looseCoins[i].gameObject);
                }
            }

            // Thoroughly wipe ANY loose or orphaned PowerUpItem components anywhere in the scene
            PowerUpItem[] loosePowerUps = Object.FindObjectsByType<PowerUpItem>(FindObjectsInactive.Include);
            for (int i = loosePowerUps.Length - 1; i >= 0; i--)
            {
                if (loosePowerUps[i] != null && loosePowerUps[i].gameObject != null)
                {
                    Object.DestroyImmediate(loosePowerUps[i].gameObject);
                }
            }

            // Also check all objects by name prefix
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = allObjects.Length - 1; i >= 0; i--)
            {
                if (allObjects[i] != null && (allObjects[i].name.StartsWith("Coin_") || allObjects[i].name.StartsWith("PowerUp_")))
                {
                    Object.DestroyImmediate(allObjects[i]);
                }
            }

            PurgeUnwantedPlatforms();
        }

        public static void PurgeUnwantedPlatforms()
        {
            // 1. Find and destroy ALL UsableGreenPlatform or blue platform GameObjects anywhere in the hierarchy
            GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject go in allObjects)
            {
                if (go == null) continue;

                string n = go.name;
                if (n.Contains("UsableGreenPlatform") ||
                    (n.StartsWith("Platform_") && go.transform.parent != null && go.transform.parent.name.Contains("UsableGreenPlatform")) ||
                    n.Contains("BlueCircularFloor") || n.Contains("BlueFloorRim") || n.Contains("BlueFloorUnderside") ||
                    n.Contains("CenterPivotHub") || n.Contains("CenterBearingSphere"))
                {
                    Debug.Log($"[DashMasters] Purged unwanted platform object: {go.name}");
                    Object.DestroyImmediate(go);
                }
                else if (n == "Obstacle_LargePinkSegmentedCRing")
                {
                    Transform parent = go.transform.parent;
                    Object.DestroyImmediate(go);

                    TrackPath path = Object.FindAnyObjectByType<TrackPath>();
                    if (path != null && parent != null)
                    {
                        MaterialDict mats = CreateRuntimeMaterials();
                        PathPoint pt = path.EvaluateDistance(145f);
                        CreatePinkSegmentedCRingObstacle(parent, pt, 4.2f, 48f, true, mats);
                        Debug.Log("[DashMasters] Replaced Obstacle_LargePinkSegmentedCRing with Level 1 PinkSegmentedCRing");
                    }
                }
            }

            // 2. Remove any unwanted platform child from any obstacle (e.g. child of hoop or c-ring)
            ObstacleBase[] allObs = Object.FindObjectsByType<ObstacleBase>(FindObjectsInactive.Include);
            foreach (ObstacleBase obs in allObs)
            {
                if (obs == null) continue;

                for (int i = obs.transform.childCount - 1; i >= 0; i--)
                {
                    Transform ch = obs.transform.GetChild(i);
                    if (ch == null) continue;
                    if (ch.name.Contains("GreenPlatform") || ch.name.Contains("UsablePlatform") || ch.name.Contains("Platform_") ||
                        ch.name.Contains("BlueCircularFloor") || ch.name.Contains("BlueFloorRim") || ch.name.Contains("BlueFloorUnderside"))
                    {
                        if (!ch.name.Contains("MovingGreenPlatform"))
                        {
                            Debug.Log($"[DashMasters] Purged obstacle platform child: {ch.name} from {obs.name}");
                            Object.DestroyImmediate(ch.gameObject);
                        }
                    }
                }
            }
        }

        public class MaterialDict
        {
            public Material trackTopMat;
            public Material trackSideMat;
            public Material waterMat;

            // Racers
            public Color playerColor = new Color(1.0f, 0.12f, 0.55f); // Pink
            public Color ai1Color = new Color(0.0f, 0.72f, 1.0f);     // Cyan
            public Color ai2Color = new Color(0.12f, 0.90f, 0.25f);    // Green
            public Color ai3Color = new Color(1.0f, 0.62f, 0.05f);    // Amber/Gold

            public Material playerMat;
            public Material ai1Mat;
            public Material ai2Mat;
            public Material ai3Mat;
            public Material visorMat;
            public Material visorHighlightMat;
            public Material gloveMat;

            // Obstacles
            public Material redSweeperMat;
            public Material cyanHubMat;
            public Material sweeperOrangeCapMat;
            public Material pinkStripeMat;
            public Material whiteStripeMat;
            public Material gantryYellowMat;
            public Material greenPlatformMat;
            public Material greenPlatformTopMat;
            public Material greenPlatformUnderMat;
            public Material coinMat;
            public Material boostMat;
            public Material shieldPickupMat;
            public Material shieldOrbMat;
            public Material speedPickupMat;
            public Material checkpointMat;
            public Material finishArchMat;

            // Pink Segmented Hoops
            public Material hoopPinkMat;
            public Material hoopMagentaMat;
            public Material hoopPurpleMat;
            public Material hoopJointYellowMat;
            public Material hoopAxleDarkMat;

            // Swinging & Rotating Hammers
            public Material hammerSupportYellowMat;
            public Material hammerSteelMat;
            public Material hammerStripeMat;

            // Wavy Climbing & Sliding Track
            public Material wavyRoadGreenMat;
            public Material wavyRoadOrangeMat;
            public Material blueHubMat;

            // Blue Circular Floor / Platform (Large Pink Segmented Ring)
            public Material blueCircularFloorMat;
            public Material blueCircularFloorRimMat;

            // Podium
            public Material goldMat;
            public Material silverMat;
            public Material bronzeMat;
        }

        public static MaterialDict CreateRuntimeMaterials(int levelIndex = 1)
        {
            MaterialDict dict = new MaterialDict();

            dict.trackTopMat = CreateMat(new Color(0.96f, 0.97f, 0.98f), 0.45f);
            dict.trackSideMat = CreateMat(new Color(0.20f, 0.23f, 0.28f), 0.25f);
            
            if (levelIndex == 1)
            {
                // Level 1: Calm turquoise ocean water
                dict.waterMat = CreateMat(new Color(0.02f, 0.68f, 0.88f), 0.95f);
            }
            else if (levelIndex == 2)
            {
                // Level 2: Soft pastel aqua/mint water tint
                dict.waterMat = CreateMat(new Color(0.12f, 0.72f, 0.82f), 0.92f);
            }
            else
            {
                // Level 3: Existing signature blue water beneath and around track with sunset ocean sheen
                dict.waterMat = CreateMat(new Color(0.08f, 0.44f, 0.70f), 0.90f);
            }

            dict.playerMat = CreateMat(dict.playerColor, 0.65f);
            dict.ai1Mat = CreateMat(dict.ai1Color, 0.65f);
            dict.ai2Mat = CreateMat(dict.ai2Color, 0.65f);
            dict.ai3Mat = CreateMat(dict.ai3Color, 0.65f);

            dict.visorMat = CreateMat(new Color(0.08f, 0.09f, 0.12f), 0.98f);
            dict.visorHighlightMat = CreateMat(new Color(1f, 1f, 1f, 0.9f), 0.95f);
            dict.gloveMat = CreateMat(new Color(0.98f, 0.98f, 0.98f), 0.5f);

            dict.redSweeperMat = CreateMat(new Color(0.96f, 0.18f, 0.18f), 0.55f);
            dict.cyanHubMat = CreateMat(new Color(0.0f, 0.82f, 0.86f), 0.7f);
            dict.sweeperOrangeCapMat = CreateMat(new Color(1.0f, 0.55f, 0.05f), 0.65f);
            dict.pinkStripeMat = CreateMat(new Color(1.0f, 0.22f, 0.65f), 0.55f);
            dict.whiteStripeMat = CreateMat(new Color(0.98f, 0.98f, 0.98f), 0.55f);
            dict.gantryYellowMat = CreateMat(new Color(1.0f, 0.75f, 0.05f), 0.55f);
            dict.greenPlatformMat = CreateMat(new Color(0.0f, 0.88f, 0.42f), 0.55f);
            dict.greenPlatformTopMat = CreateMat(new Color(0.10f, 0.85f, 0.28f), 0.6f);
            dict.greenPlatformUnderMat = CreateMat(new Color(0.04f, 0.38f, 0.12f), 0.35f);

            dict.hoopPinkMat = CreateMat(new Color(1.0f, 0.18f, 0.65f), 0.6f);
            dict.hoopMagentaMat = CreateMat(new Color(0.85f, 0.10f, 0.45f), 0.6f);
            dict.hoopPurpleMat = CreateMat(new Color(0.52f, 0.12f, 0.78f), 0.6f);
            dict.hoopJointYellowMat = CreateMat(new Color(1.0f, 0.72f, 0.02f), 0.75f);
            dict.hoopAxleDarkMat = CreateMat(new Color(0.12f, 0.14f, 0.18f), 0.35f);

            dict.hammerSupportYellowMat = CreateMat(new Color(1.0f, 0.62f, 0.05f), 0.55f);
            dict.hammerSteelMat = CreateMat(new Color(0.24f, 0.26f, 0.32f), 0.65f);
            dict.hammerStripeMat = CreateMat(new Color(1.0f, 0.82f, 0.15f), 0.7f);

            dict.wavyRoadGreenMat = CreateMat(new Color(0.12f, 0.84f, 0.32f), 0.5f);
            dict.wavyRoadOrangeMat = CreateMat(new Color(1.0f, 0.58f, 0.02f), 0.5f);
            dict.blueHubMat = CreateMat(new Color(0.0f, 0.52f, 0.85f), 0.65f);
            dict.blueCircularFloorMat = CreateMat(new Color(0.0f, 0.47f, 0.84f), 0.65f);
            dict.blueCircularFloorRimMat = CreateMat(new Color(0.01f, 0.22f, 0.48f), 0.7f);

            dict.coinMat = CreateMat(new Color(1.0f, 0.84f, 0.12f), 0.85f);
            dict.boostMat = CreateMat(new Color(0.05f, 0.98f, 0.48f), 0.75f);
            dict.shieldPickupMat = CreateMat(new Color(0.0f, 0.85f, 1.0f), 0.85f);
            dict.shieldOrbMat = CreateMat(new Color(0.2f, 0.95f, 1.0f), 0.95f);
            dict.speedPickupMat = CreateMat(new Color(1.0f, 0.92f, 0.15f), 0.9f);
            dict.checkpointMat = CreateMat(new Color(0.72f, 0.20f, 0.95f), 0.65f);
            dict.finishArchMat = CreateMat(new Color(0.98f, 0.98f, 0.98f), 0.65f);

            dict.goldMat = CreateMat(new Color(1.0f, 0.82f, 0.10f), 0.85f);
            dict.silverMat = CreateMat(new Color(0.82f, 0.85f, 0.88f), 0.85f);
            dict.bronzeMat = CreateMat(new Color(0.80f, 0.50f, 0.25f), 0.85f);

            return dict;
        }

        public static Material CreateMat(Color color, float smoothness = 0.5f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        private static void SetupEnvironment(int levelIndex, MaterialDict mats)
        {
            Light dirLight = null;
            Light[] existingLights = Object.FindObjectsByType<Light>();
            if (existingLights != null)
            {
                foreach (Light l in existingLights)
                {
                    if (l != null && l.type == LightType.Directional)
                    {
                        dirLight = l;
                        break;
                    }
                }
            }

            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.shadows = LightShadows.Soft;

            if (levelIndex == 1)
            {
                // LEVEL 1: Ocean Environment
                // Bright, natural daytime lighting
                dirLight.color = new Color(1.0f, 0.98f, 0.94f);
                dirLight.intensity = 1.35f;
                dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.68f, 0.82f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.58f, 0.82f, 0.98f);
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.0012f;
            }
            else if (levelIndex == 2)
            {
                // LEVEL 2: Pastel Environment
                // Soft, balanced lighting with warm peach/lavender ambient
                dirLight.color = new Color(1.0f, 0.94f, 0.92f);
                dirLight.intensity = 1.22f;
                dirLight.transform.rotation = Quaternion.Euler(42f, -25f, 0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.68f, 0.65f, 0.78f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.84f, 0.80f, 0.92f);
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.0014f;
            }
            else
            {
                // LEVEL 3: Sunset Environment
                // Warm golden-orange sunset light, angled low at the horizon, warm subtle ambient
                dirLight.color = new Color(1.0f, 0.68f, 0.38f);
                dirLight.intensity = 1.30f;
                dirLight.transform.rotation = Quaternion.Euler(18f, -48f, 0f);

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.44f, 0.40f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.95f, 0.55f, 0.35f);
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.0015f;
            }

            // Procedural Skybox Configuration
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material skyMat = new Material(skyShader);
                if (levelIndex == 1)
                {
                    // Level 1: Clear vibrant blue daytime sky
                    skyMat.SetColor("_SkyTint", new Color(0.42f, 0.68f, 0.98f));
                    skyMat.SetColor("_GroundColor", new Color(0.35f, 0.60f, 0.80f));
                    skyMat.SetFloat("_AtmosphereThickness", 0.90f);
                    skyMat.SetFloat("_Exposure", 1.25f);
                    skyMat.SetFloat("_SunSize", 0.04f);
                }
                else if (levelIndex == 2)
                {
                    // Level 2: Soft pastel blue / lavender-pink sky
                    skyMat.SetColor("_SkyTint", new Color(0.72f, 0.76f, 0.95f));
                    skyMat.SetColor("_GroundColor", new Color(0.88f, 0.80f, 0.88f));
                    skyMat.SetFloat("_AtmosphereThickness", 0.72f);
                    skyMat.SetFloat("_Exposure", 1.18f);
                    skyMat.SetFloat("_SunSize", 0.035f);
                }
                else
                {
                    // Level 3: Warm golden-orange sunset sky
                    skyMat.SetColor("_SkyTint", new Color(0.95f, 0.44f, 0.20f));
                    skyMat.SetColor("_GroundColor", new Color(0.48f, 0.22f, 0.14f));
                    skyMat.SetFloat("_AtmosphereThickness", 1.45f);
                    skyMat.SetFloat("_Exposure", 1.15f);
                    skyMat.SetFloat("_SunSize", 0.055f);
                }
                RenderSettings.skybox = skyMat;
            }
        }

        private static void BuildDistantScenery(int levelIndex, MaterialDict mats)
        {
            GameObject sceneryRoot = new GameObject("--- SCENERY ---");

            if (levelIndex == 1)
            {
                // LEVEL 1: Ocean Environment - A few small, distant islands
                Material sandMat = CreateMat(new Color(0.88f, 0.83f, 0.68f), 0.2f);
                Material grassMat = CreateMat(new Color(0.20f, 0.65f, 0.30f), 0.3f);
                Material trunkMat = CreateMat(new Color(0.45f, 0.30f, 0.18f), 0.2f);
                Material leafMat = CreateMat(new Color(0.15f, 0.70f, 0.25f), 0.35f);

                Vector3[] islandPositions = new Vector3[]
                {
                    new Vector3(-280f, -5.5f, 100f),
                    new Vector3(290f, -5.5f, 180f),
                    new Vector3(-310f, -5.5f, 320f),
                    new Vector3(320f, -5.5f, 420f),
                    new Vector3(0f, -5.5f, 620f),
                    new Vector3(-180f, -5.5f, 550f)
                };

                float[] islandRadii = new float[] { 35f, 42f, 38f, 45f, 50f, 32f };
                float[] islandHeights = new float[] { 8f, 10f, 9f, 11f, 12f, 7f };

                for (int i = 0; i < islandPositions.Length; i++)
                {
                    BuildIsland(sceneryRoot.transform, islandPositions[i], islandRadii[i], islandHeights[i], sandMat, grassMat, trunkMat, leafMat);
                }
            }
            else if (levelIndex == 2)
            {
                // LEVEL 2: Enhanced Pastel Environment
                // Palette: soft lavender, mint, pastel aqua, peach, and blush pink
                Material cloudLowMat = CreateMat(new Color(1.0f, 0.96f, 0.93f), 0.1f);
                Material cloudMidMat = CreateMat(new Color(0.98f, 0.94f, 0.98f), 0.1f);
                Material cloudHighMat = CreateMat(new Color(0.92f, 0.90f, 0.98f), 0.1f);

                Material islandGrassMat = CreateMat(new Color(0.56f, 0.84f, 0.72f), 0.35f);
                Material islandRockMat = CreateMat(new Color(0.72f, 0.65f, 0.78f), 0.2f);
                Material crystalMat = CreateMat(new Color(0.98f, 0.80f, 0.92f), 0.7f);

                Material hillNearMat = CreateMat(new Color(0.48f, 0.76f, 0.68f), 0.25f);
                Material hillMidMat = CreateMat(new Color(0.42f, 0.70f, 0.68f), 0.25f);
                Material hillFarMat = CreateMat(new Color(0.55f, 0.68f, 0.75f), 0.25f);

                Material orbMat1 = CreateMat(new Color(1.0f, 0.90f, 0.65f), 0.8f);
                Material orbMat2 = CreateMat(new Color(0.96f, 0.75f, 0.88f), 0.8f);

                // 1. Distant Floating Islands (Arranged at varied distances and heights)
                BuildFloatingIsland(sceneryRoot.transform, new Vector3(-190f, 20f, 100f), 18f, 13f, islandGrassMat, islandRockMat, crystalMat);
                BuildFloatingIsland(sceneryRoot.transform, new Vector3(210f, 26f, 190f), 22f, 16f, islandGrassMat, islandRockMat, crystalMat);
                BuildFloatingIsland(sceneryRoot.transform, new Vector3(-225f, 32f, 300f), 24f, 17f, islandGrassMat, islandRockMat, crystalMat);
                BuildFloatingIsland(sceneryRoot.transform, new Vector3(200f, 22f, 410f), 20f, 14f, islandGrassMat, islandRockMat, crystalMat);
                BuildFloatingIsland(sceneryRoot.transform, new Vector3(15f, 34f, 590f), 26f, 19f, islandGrassMat, islandRockMat, crystalMat);

                // 2. Layered Clouds (Low wispy puffs, mid billowy clouds, high strata sheets)
                // Low wispy clouds (y ~ 28m - 35m)
                BuildCloudCluster(sceneryRoot.transform, new Vector3(-120f, 30f, 70f), cloudLowMat, 0.75f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(130f, 33f, 220f), cloudLowMat, 0.80f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(-100f, 28f, 360f), cloudLowMat, 0.75f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(110f, 35f, 480f), cloudLowMat, 0.85f);

                // Mid billowy clouds (y ~ 48m - 58m)
                BuildCloudCluster(sceneryRoot.transform, new Vector3(-150f, 50f, 140f), cloudMidMat, 1.0f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(160f, 54f, 270f), cloudMidMat, 1.15f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(-120f, 52f, 390f), cloudMidMat, 1.05f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(140f, 58f, 500f), cloudMidMat, 1.2f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(0f, 55f, 620f), cloudMidMat, 1.3f);

                // High strata clouds (y ~ 75m - 90m)
                BuildCloudCluster(sceneryRoot.transform, new Vector3(-80f, 82f, 180f), cloudHighMat, 1.6f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(90f, 88f, 340f), cloudHighMat, 1.8f);
                BuildCloudCluster(sceneryRoot.transform, new Vector3(-40f, 78f, 530f), cloudHighMat, 1.7f);

                // 3. Layered Rolling Hills on Water (Near foothills, midground rolling hills, distant horizon domes)
                // Near foothills (190m - 230m away)
                BuildPastelHill(sceneryRoot.transform, new Vector3(-200f, -5.5f, 80f), 32f, 9f, hillNearMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(210f, -5.5f, 160f), 36f, 11f, hillNearMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(-210f, -5.5f, 320f), 34f, 10f, hillNearMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(200f, -5.5f, 440f), 38f, 12f, hillNearMat);

                // Midground rolling hills (290m - 370m away)
                BuildPastelHill(sceneryRoot.transform, new Vector3(-310f, -5.5f, 150f), 65f, 18f, hillMidMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(320f, -5.5f, 250f), 70f, 20f, hillMidMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(-330f, -5.5f, 410f), 68f, 19f, hillMidMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(310f, -5.5f, 520f), 75f, 22f, hillMidMat);

                // Distant horizon domes (450m - 580m away)
                BuildPastelHill(sceneryRoot.transform, new Vector3(-450f, -5.5f, 220f), 100f, 28f, hillFarMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(460f, -5.5f, 360f), 110f, 30f, hillFarMat);
                BuildPastelHill(sceneryRoot.transform, new Vector3(40f, -5.5f, 640f), 120f, 34f, hillFarMat);

                // 4. Subtle Atmospheric Floating Pastel Orbs
                BuildFloatingOrb(sceneryRoot.transform, new Vector3(-185f, 25f, 120f), 2.8f, orbMat1);
                BuildFloatingOrb(sceneryRoot.transform, new Vector3(205f, 32f, 215f), 3.2f, orbMat2);
                BuildFloatingOrb(sceneryRoot.transform, new Vector3(-220f, 38f, 325f), 3.5f, orbMat1);
                BuildFloatingOrb(sceneryRoot.transform, new Vector3(190f, 26f, 425f), 2.6f, orbMat2);
                BuildFloatingOrb(sceneryRoot.transform, new Vector3(25f, 40f, 575f), 3.6f, orbMat1);
            }
            else
            {
                // LEVEL 3: Realistic Layered Sunset Mountains with Atmospheric Depth
                // Multi-tier aerial perspective: Near (dark twilight plum) -> Mid (sunset mauve) -> Far (hazy rose-bronze) -> Backdrop (soft sunset mist)
                Material mtnNearMat = CreateMat(new Color(0.20f, 0.14f, 0.26f), 0.20f);
                Material mtnMidMat = CreateMat(new Color(0.38f, 0.22f, 0.32f), 0.15f);
                Material mtnFarMat = CreateMat(new Color(0.62f, 0.35f, 0.34f), 0.10f);
                Material mtnBackdropMat = CreateMat(new Color(0.76f, 0.44f, 0.36f), 0.05f);

                Material sunMat = CreateMat(new Color(1.0f, 0.85f, 0.40f), 0.0f);
                Material sunGlowMat = CreateMat(new Color(1.0f, 0.65f, 0.25f, 0.4f), 0.0f);

                // --- LEFT FLANK LAYERED RIDGES ---
                // Layer 1: Near Crags & Foothills (~280m distance)
                BuildMountainRidge(sceneryRoot.transform, new Vector3(-280f, -5.5f, 30f), new Vector3(0.2f, 0f, 1f), 220f, 48f, 75f, mtnNearMat, 101);
                // Layer 2: Intermediate Layered Ridge (~360m distance)
                BuildMountainRidge(sceneryRoot.transform, new Vector3(-360f, -5.5f, 110f), new Vector3(0.1f, 0f, 1f), 280f, 82f, 105f, mtnMidMat, 102);
                // Layer 3: Towering Distant Peaks (~450m distance)
                BuildMountainRidge(sceneryRoot.transform, new Vector3(-450f, -5.5f, 60f), new Vector3(0.15f, 0f, 1f), 380f, 122f, 145f, mtnFarMat, 103);

                // --- RIGHT FLANK LAYERED RIDGES ---
                // Layer 1: Near Crags & Foothills (~290m distance)
                BuildMountainRidge(sceneryRoot.transform, new Vector3(290f, -5.5f, 70f), new Vector3(-0.2f, 0f, 1f), 230f, 52f, 80f, mtnNearMat, 201);
                // Layer 2: Intermediate Layered Ridge (~370m distance)
                BuildMountainRidge(sceneryRoot.transform, new Vector3(370f, -5.5f, 150f), new Vector3(-0.1f, 0f, 1f), 290f, 86f, 110f, mtnMidMat, 202);
                // Layer 3: Towering Distant Peaks (~460m distance)
                BuildMountainRidge(sceneryRoot.transform, new Vector3(460f, -5.5f, 100f), new Vector3(-0.15f, 0f, 1f), 390f, 126f, 150f, mtnFarMat, 203);

                // --- HORIZON BACKDROP (Behind Finish Line, z = 590m - 750m) ---
                // Layer 2: Mid Horizon Range across horizon
                BuildMountainRidge(sceneryRoot.transform, new Vector3(-290f, -5.5f, 600f), new Vector3(1f, 0f, 0.15f), 580f, 96f, 125f, mtnMidMat, 301);
                // Layer 4: Colossal Distant Alpine Backdrop Range
                BuildMountainRidge(sceneryRoot.transform, new Vector3(-390f, -5.5f, 710f), new Vector3(1f, 0f, 0.05f), 780f, 145f, 190f, mtnBackdropMat, 302);

                // Golden Sunset Sun Disc & Glowing Corona on Horizon
                GameObject sunObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sunObj.name = "SunsetSunDisc";
                sunObj.transform.parent = sceneryRoot.transform;
                sunObj.transform.position = new Vector3(180f, 25f, 740f);
                sunObj.transform.localScale = new Vector3(46f, 46f, 5f);
                sunObj.GetComponent<MeshRenderer>().material = sunMat;
                Collider sunCol = sunObj.GetComponent<Collider>();
                if (sunCol != null) Object.DestroyImmediate(sunCol);

                GameObject coronaObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coronaObj.name = "SunsetSunCorona";
                coronaObj.transform.parent = sceneryRoot.transform;
                coronaObj.transform.position = new Vector3(180f, 25f, 742f);
                coronaObj.transform.localScale = new Vector3(78f, 78f, 4f);
                coronaObj.GetComponent<MeshRenderer>().material = sunGlowMat;
                Collider coronaCol = coronaObj.GetComponent<Collider>();
                if (coronaCol != null) Object.DestroyImmediate(coronaCol);
            }
        }

        private static void BuildIsland(Transform parent, Vector3 centerPos, float radius, float height, Material sandMat, Material grassMat, Material trunkMat, Material leafMat)
        {
            GameObject islandObj = new GameObject("DistantIsland");
            islandObj.transform.parent = parent;
            islandObj.transform.position = centerPos;

            // Sand base
            GameObject sand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sand.name = "SandBase";
            sand.transform.parent = islandObj.transform;
            sand.transform.localPosition = new Vector3(0f, height * 0.2f, 0f);
            sand.transform.localScale = new Vector3(radius * 2f, height * 0.7f, radius * 2f);
            sand.GetComponent<MeshRenderer>().material = sandMat;
            Collider sandCol = sand.GetComponent<Collider>();
            if (sandCol != null) Object.DestroyImmediate(sandCol);

            // Grass mound
            GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grass.name = "GrassMound";
            grass.transform.parent = islandObj.transform;
            grass.transform.localPosition = new Vector3(0f, height * 0.45f, 0f);
            grass.transform.localScale = new Vector3(radius * 1.5f, height * 0.9f, radius * 1.5f);
            grass.GetComponent<MeshRenderer>().material = grassMat;
            Collider grassCol = grass.GetComponent<Collider>();
            if (grassCol != null) Object.DestroyImmediate(grassCol);

            // Stylized Palm Trees
            Vector3[] palmOffsets = new Vector3[] { new Vector3(radius * 0.2f, height * 0.6f, radius * 0.15f), new Vector3(-radius * 0.15f, height * 0.65f, -radius * 0.1f) };
            foreach (Vector3 pOff in palmOffsets)
            {
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "PalmTrunk";
                trunk.transform.parent = islandObj.transform;
                trunk.transform.localPosition = pOff + Vector3.up * 3.5f;
                trunk.transform.localScale = new Vector3(0.7f, 3.5f, 0.7f);
                trunk.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-5f, 5f), 0f, UnityEngine.Random.Range(-5f, 5f));
                trunk.GetComponent<MeshRenderer>().material = trunkMat;
                Collider tCol = trunk.GetComponent<Collider>();
                if (tCol != null) Object.DestroyImmediate(tCol);

                Vector3 canopyTop = trunk.transform.localPosition + Vector3.up * 3.5f;
                for (int k = 0; k < 3; k++)
                {
                    GameObject frond = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    frond.name = "PalmFrond";
                    frond.transform.parent = islandObj.transform;
                    frond.transform.localPosition = canopyTop;
                    frond.transform.localRotation = Quaternion.Euler(20f, k * 120f, 0f);
                    frond.transform.localScale = new Vector3(1.2f, 0.2f, 3.0f);
                    frond.GetComponent<MeshRenderer>().material = leafMat;
                    Collider fCol = frond.GetComponent<Collider>();
                    if (fCol != null) Object.DestroyImmediate(fCol);
                }
            }
        }

        private static void BuildCloudCluster(Transform parent, Vector3 centerPos, Material cloudMat, float scaleMult = 1.0f)
        {
            GameObject cloudObj = new GameObject("DistantCloud");
            cloudObj.transform.parent = parent;
            cloudObj.transform.position = centerPos;

            Vector3[] offsets = new Vector3[]
            {
                Vector3.zero,
                new Vector3(-12f * scaleMult, 2f * scaleMult, 0f),
                new Vector3(14f * scaleMult, 1f * scaleMult, 2f * scaleMult),
                new Vector3(2f * scaleMult, -1f * scaleMult, -6f * scaleMult)
            };
            Vector3[] scales = new Vector3[]
            {
                new Vector3(28f, 8f, 18f) * scaleMult,
                new Vector3(20f, 9f, 15f) * scaleMult,
                new Vector3(22f, 8f, 16f) * scaleMult,
                new Vector3(18f, 7f, 14f) * scaleMult
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = $"Puff_{i}";
                puff.transform.parent = cloudObj.transform;
                puff.transform.localPosition = offsets[i];
                puff.transform.localScale = scales[i];
                puff.GetComponent<MeshRenderer>().material = cloudMat;
                Collider col = puff.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
        }

        private static void BuildPastelHill(Transform parent, Vector3 centerPos, float radius, float height, Material hillMat)
        {
            GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hill.name = "PastelHill";
            hill.transform.parent = parent;
            hill.transform.position = centerPos;
            hill.transform.localScale = new Vector3(radius * 2f, height * 2f, radius * 2f);
            hill.GetComponent<MeshRenderer>().material = hillMat;
            Collider col = hill.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void BuildFloatingIsland(Transform parent, Vector3 centerPos, float radius, float depth, Material grassMat, Material rockMat, Material crystalMat)
        {
            GameObject island = new GameObject("PastelFloatingIsland");
            island.transform.parent = parent;
            island.transform.position = centerPos;

            // 1. Lush Top Plateau (Smooth pastel green/mint mound)
            GameObject plateau = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plateau.name = "IslandPlateau";
            plateau.transform.parent = island.transform;
            plateau.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            plateau.transform.localScale = new Vector3(radius * 2f, 2.2f, radius * 2f);
            plateau.GetComponent<MeshRenderer>().material = grassMat;
            Collider pCol = plateau.GetComponent<Collider>();
            if (pCol != null) Object.DestroyImmediate(pCol);

            // Gentle top dome
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "IslandDome";
            dome.transform.parent = island.transform;
            dome.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            dome.transform.localScale = new Vector3(radius * 1.7f, 3.0f, radius * 1.7f);
            dome.GetComponent<MeshRenderer>().material = grassMat;
            Collider dCol = dome.GetComponent<Collider>();
            if (dCol != null) Object.DestroyImmediate(dCol);

            // 2. Tapered Earthen/Rock Underside (Inverted cone hanging below)
            GameObject rockBase = new GameObject("IslandUnderside");
            rockBase.transform.parent = island.transform;
            rockBase.transform.localPosition = Vector3.zero;

            MeshFilter mf = rockBase.AddComponent<MeshFilter>();
            MeshRenderer mr = rockBase.AddComponent<MeshRenderer>();
            mr.material = rockMat;

            Mesh coneMesh = new Mesh();
            coneMesh.name = "InvertedRockCone";

            int segments = 8;
            Vector3[] verts = new Vector3[segments + 2];
            verts[0] = new Vector3(0f, -depth, 0f); // tip at bottom
            verts[1] = Vector3.zero;                // center top

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float r = radius * (0.88f + 0.12f * Mathf.Sin(angle * 3f));
                verts[2 + i] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            }

            int[] tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = 2 + next;
                tris[i * 3 + 2] = 2 + i;
            }

            coneMesh.vertices = verts;
            coneMesh.triangles = tris;
            coneMesh.RecalculateNormals();
            mf.mesh = coneMesh;

            // 3. Cute stylized pastel crystal / decorative spire on top
            if (crystalMat != null)
            {
                GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crystal.name = "Spire";
                crystal.transform.parent = island.transform;
                crystal.transform.localPosition = new Vector3(radius * 0.15f, 4.5f, radius * 0.1f);
                crystal.transform.localScale = new Vector3(1.2f, 3.5f, 1.2f);
                crystal.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-8f, 8f), 0f, UnityEngine.Random.Range(-8f, 8f));
                crystal.GetComponent<MeshRenderer>().material = crystalMat;
                Collider cCol = crystal.GetComponent<Collider>();
                if (cCol != null) Object.DestroyImmediate(cCol);

                GameObject topper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                topper.name = "Topper";
                topper.transform.parent = island.transform;
                topper.transform.localPosition = crystal.transform.localPosition + Vector3.up * 2.2f;
                topper.transform.localScale = new Vector3(2.2f, 3.2f, 2.2f);
                topper.GetComponent<MeshRenderer>().material = crystalMat;
                Collider topCol = topper.GetComponent<Collider>();
                if (topCol != null) Object.DestroyImmediate(topCol);
            }
        }

        private static void BuildFloatingOrb(Transform parent, Vector3 pos, float radius, Material orbMat)
        {
            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "PastelAtmosphericOrb";
            orb.transform.parent = parent;
            orb.transform.position = pos;
            orb.transform.localScale = Vector3.one * (radius * 2f);
            orb.GetComponent<MeshRenderer>().material = orbMat;
            Collider col = orb.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static void BuildMountainRidge(Transform parent, Vector3 basePos, Vector3 dir, float length, float mainHeight, float baseWidth, Material mat, int seed)
        {
            GameObject ridgeObj = new GameObject("MountainRidge");
            ridgeObj.transform.parent = parent;

            MeshFilter mf = ridgeObj.AddComponent<MeshFilter>();
            MeshRenderer mr = ridgeObj.AddComponent<MeshRenderer>();
            mr.material = mat;

            Mesh mesh = new Mesh();
            mesh.name = "MountainRidgeMesh";

            dir = dir.normalized;
            Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;

            int spineSegments = 10;
            int vertCount = (spineSegments + 1) * 3;
            Vector3[] vertices = new Vector3[vertCount];

            for (int i = 0; i <= spineSegments; i++)
            {
                float u = (float)i / spineSegments;

                // Natural mountain envelope: broad shoulder with peak harmonics
                float envelope = Mathf.Sin(Mathf.PI * u);
                envelope = Mathf.Pow(envelope, 0.75f);

                // Multi-frequency harmonic variation for realistic serrated ridge
                float hVar = 0.65f 
                    + 0.28f * Mathf.Sin(u * 11.0f + seed * 1.7f) 
                    + 0.15f * Mathf.Cos(u * 23.0f + seed * 3.1f);
                float h = mainHeight * envelope * Mathf.Clamp(hVar, 0.15f, 1.25f);

                // Slight lateral meandering along spine
                float lateralOffset = Mathf.Sin(u * 8.0f + seed) * (baseWidth * 0.12f);
                Vector3 spinePt = basePos + dir * (u * length) + perp * lateralOffset + Vector3.up * h;

                // Front and back base spread
                float fSpread = baseWidth * 0.5f * (0.85f + 0.15f * Mathf.Sin(u * 6.5f + seed * 0.9f));
                float bSpread = baseWidth * 0.5f * (0.85f + 0.15f * Mathf.Cos(u * 5.5f + seed * 1.3f));

                Vector3 frontBase = basePos + dir * (u * length) + perp * fSpread + Vector3.up * 0f;
                Vector3 backBase = basePos + dir * (u * length) - perp * bSpread + Vector3.up * 0f;

                int idx = i * 3;
                vertices[idx + 0] = spinePt;
                vertices[idx + 1] = frontBase;
                vertices[idx + 2] = backBase;
            }

            int quadCount = spineSegments;
            int triCount = quadCount * 4 + 2;
            int[] triangles = new int[triCount * 3];
            int t = 0;

            for (int i = 0; i < spineSegments; i++)
            {
                int currSpine = i * 3 + 0;
                int currFront = i * 3 + 1;
                int currBack  = i * 3 + 2;

                int nextSpine = (i + 1) * 3 + 0;
                int nextFront = (i + 1) * 3 + 1;
                int nextBack  = (i + 1) * 3 + 2;

                // Front slope (2 triangles)
                triangles[t++] = currFront;
                triangles[t++] = nextSpine;
                triangles[t++] = currSpine;

                triangles[t++] = currFront;
                triangles[t++] = nextFront;
                triangles[t++] = nextSpine;

                // Back slope (2 triangles)
                triangles[t++] = currSpine;
                triangles[t++] = nextSpine;
                triangles[t++] = currBack;

                triangles[t++] = currBack;
                triangles[t++] = nextSpine;
                triangles[t++] = nextBack;
            }

            // Start endcap
            triangles[t++] = 0;
            triangles[t++] = 2;
            triangles[t++] = 1;

            // End endcap
            int lastSpine = spineSegments * 3 + 0;
            int lastFront = spineSegments * 3 + 1;
            int lastBack  = spineSegments * 3 + 2;

            triangles[t++] = lastSpine;
            triangles[t++] = lastFront;
            triangles[t++] = lastBack;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mf.mesh = mesh;
        }

        private static TrackPath BuildTrackPath(int levelIndex)
        {
            GameObject pathObj = new GameObject("TrackPath");
            TrackPath path = pathObj.AddComponent<TrackPath>();

            List<Vector3> waypoints = new List<Vector3>();

            if (levelIndex == 1)
            {
                // LEVEL 1: Lagoon Sprint (~365m Beginner)
                // Features proper vertical climbing ladder tower with rotating obstacles (matching Image 1)
                waypoints = new List<Vector3>
                {
                    new Vector3(0f, 0f, -15f),
                    new Vector3(0f, 0f, 25f),
                    new Vector3(10f, 0f, 65f),      // Gentle wide curve right
                    new Vector3(26f, 0.2f, 105f),
                    new Vector3(32f, 0.4f, 145f),   // Slight elevation
                    new Vector3(26f, 0.5f, 185f),   // Checkpoint 2 safe staging ground
                    new Vector3(22f, 0.5f, 196f),   // Approach to tower base
                    new Vector3(22f, 0.5f, 200f),   // Base of vertical climbing ladder
                    new Vector3(22f, 1.5f, 201f),   // Curve up onto vertical wall
                    new Vector3(22f, 7.5f, 201f),   // Climb vertical wall near top
                    new Vector3(22f, 8.5f, 202f),   // Lip of top landing deck
                    new Vector3(22f, 8.5f, 208f),   // Straight elevated landing platform deck
                    new Vector3(22f, 8.5f, 216f),   // Straight elevated road runway
                    new Vector3(20f, 8.5f, 228f),   // Gentle curve continuing elevated road
                    new Vector3(14f, 8.5f, 244f),   // Elevated track with obstacle
                    new Vector3(6f, 4.2f, 268f),    // Smooth descent ramp
                    new Vector3(-4f, 0f, 292f),     // Ground stretch into speed boost pad
                    new Vector3(-12f, 0f, 316f),    // Finish stretch
                    new Vector3(-12f, 0f, 336f)     // Podium stretch
                };
            }
            else if (levelIndex == 2)
            {
                // LEVEL 2: Coral Rapids (~520m Intermediate)
                // Features steep slider chutes attached directly to the road (matching Image 2)
                waypoints = new List<Vector3>
                {
                    new Vector3(0f, 0f, -15f),
                    new Vector3(0f, 0f, 20f),
                    new Vector3(0f, 0f, 55f),
                    new Vector3(18f, 0.4f, 95f),    // Sweeping right bend
                    new Vector3(35f, 2.5f, 130f),
                    new Vector3(48f, 4.8f, 160f),
                    new Vector3(52f, 6.2f, 185f),   // Approach deck before Slider 1
                    new Vector3(50f, 6.2f, 195f),   // Slider 1 entry mouth
                    new Vector3(40f, 3.5f, 212f),   // Slider 1 steep downward chute
                    new Vector3(28f, 1.2f, 226f),   // Slider 1 curve out
                    new Vector3(18f, 1.0f, 238f),   // Slider 1 exit into white road
                    new Vector3(6f, 2.0f, 268f),    // Sweeping curve
                    new Vector3(-8f, 4.2f, 298f),   // Rise to crest 2
                    new Vector3(-20f, 5.8f, 328f),  // Approach deck before Slider 2
                    new Vector3(-25f, 5.8f, 340f),  // Slider 2 entry mouth
                    new Vector3(-32f, 3.2f, 356f),  // Slider 2 steep downward chute
                    new Vector3(-36f, 1.2f, 372f),  // Slider 2 curve out
                    new Vector3(-34f, 1.0f, 385f),  // Slider 2 exit into white road
                    new Vector3(-20f, 0.5f, 415f),  // Finish approach
                    new Vector3(0f, 0f, 445f),      // Finish line
                    new Vector3(0f, 0f, 465f)       // Podium stretch
                };
            }
            else
            {
                // LEVEL 3: Titan's Trench (~530m Expert)
                waypoints = new List<Vector3>
                {
                    new Vector3(0f, 0f, -15f),
                    new Vector3(0f, 0f, 30f),
                    new Vector3(20f, 0.8f, 75f),    // Climbing elevated sweep right
                    new Vector3(48f, 1.8f, 120f),
                    new Vector3(68f, 2.8f, 170f),
                    new Vector3(72f, 3.6f, 220f),   // High elevated crest over water
                    new Vector3(54f, 4.8f, 260f),   // HIGH WAVY CREST (Climb to 4.8m!)
                    new Vector3(26f, 1.8f, 295f),   // DEEP WAVY DIP (Steep slide!)
                    new Vector3(-6f, 4.2f, 330f),   // WAVY CREST 2 (Rise)
                    new Vector3(-32f, 2.4f, 365f),  // S-curve descent
                    new Vector3(-58f, 1.5f, 410f),
                    new Vector3(-62f, 0.8f, 455f),
                    new Vector3(-35f, 0.3f, 495f),  // Hairpin alignment to finish
                    new Vector3(-5f, 0f, 530f),
                    new Vector3(0f, 0f, 560f)       // Finish stretch
                };
            }

            path.SetWaypoints(waypoints);
            path.ClearZones();

            if (levelIndex == 1)
            {
                // Register Vertical Climbing Ladder Zone (221m - 230.8m)
                path.AddZone(221.0f, 230.8f, TrackZoneType.Ladder);
            }
            else if (levelIndex == 2)
            {
                // Register Slider Chute Zones (Slider 1: 220m-266m, Slider 2: 385m-428m)
                path.AddZone(220.0f, 266.0f, TrackZoneType.Slider);
                path.AddZone(385.0f, 428.0f, TrackZoneType.Slider);
            }

            return path;
        }

        private static void BuildTrackGeometryAndHazards(int levelIndex, TrackPath path, MaterialDict mats, List<Checkpoint> checkpoints, out FinishLine finishLine)
        {
            GameObject trackRoot = new GameObject("--- TRACK ---");

            // 1. Water Plane (Huge 1200m x 1200m continuous turquoise swimming-pool surface at y = -5.5f)
            GameObject waterObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterObj.name = "--- WATER ---";
            waterObj.transform.position = new Vector3(0f, -5.5f, 240f);
            waterObj.transform.localScale = new Vector3(1200f, 1.0f, 1200f);
            waterObj.GetComponent<MeshRenderer>().material = mats.waterMat;
            waterObj.GetComponent<BoxCollider>().isTrigger = true;
            WaterSurface ws = waterObj.GetComponent<WaterSurface>() ?? waterObj.AddComponent<WaterSurface>();
            if (levelIndex == 1)
            {
                ws.SetColors(new Color(0.02f, 0.62f, 0.88f), new Color(0.08f, 0.82f, 0.96f));
            }
            else if (levelIndex == 2)
            {
                ws.SetColors(new Color(0.12f, 0.68f, 0.80f), new Color(0.24f, 0.84f, 0.88f));
            }
            else
            {
                ws.SetColors(new Color(0.06f, 0.40f, 0.68f), new Color(0.14f, 0.54f, 0.80f));
            }

            // 2. Build Elevated Exposed Ribbon Track (NO Guardrails!)
            float totalLen = path.TotalLength;
            float step = 2.4f;
            int segmentCount = Mathf.CeilToInt(totalLen / step);

            float trackWidth = 4.2f;
            float slabThickness = 0.35f;
            float baseUnderHeight = 3.6f;

            for (int i = 0; i <= segmentCount; i++)
            {
                float dist = Mathf.Min(i * step, totalLen);
                PathPoint pt = path.EvaluateDistance(dist);

                // 1. Skip standard road slabs across vertical ladder face in Level 1
                // The 3D Climbing Ladder Tower, rungs, and rotating obstacles form the vertical climbing section!
                if (levelIndex == 1 && path.IsLadder(dist))
                {
                    continue;
                }

                // 2. Slider Chute section in Level 2 (matching Image 2)
                bool isSliderSection = (levelIndex == 2 && path.IsSlider(dist));
                bool isWavySection = (levelIndex == 3 && dist >= 245f && dist <= 345f);

                if (isSliderSection)
                {
                    float sliderWidth = 3.6f;
                    GameObject slideBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slideBed.name = $"Slider_Bed_{i}";
                    slideBed.transform.parent = trackRoot.transform;
                    slideBed.transform.position = pt.position - Vector3.up * (slabThickness * 0.5f);
                    slideBed.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
                    slideBed.transform.localScale = new Vector3(sliderWidth, slabThickness, step * 1.05f);
                    slideBed.GetComponent<MeshRenderer>().material = mats.wavyRoadGreenMat;

                    // Thick, rounded, raised orange/yellow rims along both edges (matching Image 2)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rim.name = (s < 0) ? $"Slider_Rim_Left_{i}" : $"Slider_Rim_Right_{i}";
                        rim.transform.parent = trackRoot.transform;
                        rim.transform.position = pt.position + (pt.right * (s * (sliderWidth * 0.5f + 0.18f))) + (pt.up * 0.24f);
                        rim.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
                        rim.transform.localScale = new Vector3(0.42f, 0.48f, step * 1.05f);
                        rim.GetComponent<MeshRenderer>().material = mats.wavyRoadOrangeMat;
                        Object.DestroyImmediate(rim.GetComponent<BoxCollider>());
                    }

                    // Underside vertical wall
                    GameObject underSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    underSlab.name = $"Slider_Under_{i}";
                    underSlab.transform.parent = trackRoot.transform;
                    underSlab.transform.position = pt.position - Vector3.up * (slabThickness + baseUnderHeight * 0.5f);
                    underSlab.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
                    underSlab.transform.localScale = new Vector3(sliderWidth * 0.92f, baseUnderHeight, step * 1.05f);
                    underSlab.GetComponent<MeshRenderer>().material = mats.trackSideMat;
                    Object.DestroyImmediate(underSlab.GetComponent<BoxCollider>());

                    continue;
                }

                float curTrackWidth = isWavySection ? 2.8f : trackWidth;

                // Top white walking surface or green wavy road
                GameObject topSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                topSlab.name = isWavySection ? $"WavyTrack_Top_{i}" : $"Track_Top_{i}";
                topSlab.transform.parent = trackRoot.transform;
                topSlab.transform.position = pt.position - Vector3.up * (slabThickness * 0.5f);
                topSlab.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
                topSlab.transform.localScale = new Vector3(curTrackWidth, slabThickness, step * 1.05f);
                topSlab.GetComponent<MeshRenderer>().material = isWavySection ? mats.wavyRoadGreenMat : mats.trackTopMat;

                if (isWavySection)
                {
                    // Add distinct thick orange/yellow curved side detailing along the wavy road
                    for (int s = -1; s <= 1; s += 2)
                    {
                        GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        curb.name = (s < 0) ? $"Wavy_Curb_Left_{i}" : $"Wavy_Curb_Right_{i}";
                        curb.transform.parent = trackRoot.transform;
                        curb.transform.position = pt.position + (pt.right * (s * (curTrackWidth * 0.5f + 0.16f))) + (pt.up * 0.08f);
                        curb.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
                        curb.transform.localScale = new Vector3(0.36f, 0.24f, step * 1.05f);
                        curb.GetComponent<MeshRenderer>().material = mats.wavyRoadOrangeMat;
                        Object.DestroyImmediate(curb.GetComponent<BoxCollider>());
                    }
                }

                // Thick dark underside vertical walls
                GameObject sideSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sideSlab.name = $"Track_Side_{i}";
                sideSlab.transform.parent = trackRoot.transform;
                sideSlab.transform.position = pt.position - Vector3.up * (slabThickness + baseUnderHeight * 0.5f);
                sideSlab.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
                sideSlab.transform.localScale = new Vector3(curTrackWidth * 0.94f, baseUnderHeight, step * 1.05f);
                sideSlab.GetComponent<MeshRenderer>().material = mats.trackSideMat;
                Object.DestroyImmediate(sideSlab.GetComponent<BoxCollider>());
            }

            // 3. Start Arch
            CreateArch(trackRoot.transform, path.EvaluateDistance(0f), mats.finishArchMat, mats.trackSideMat, trackWidth, "START");

            // 4. LEVEL-SPECIFIC OBSTACLES & COINS
            if (levelIndex == 1)
            {
                // === LEVEL 1: Lagoon Sprint (Beginner, ~365m) ===
                SpawnGuaranteedCoins(path, 10f, 26f, 5, CoinPattern.Center, mats.coinMat);

                // Obs 1: Red Sweeper with blue central hub and orange endcaps
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(36f), trackWidth, 70f, mats);

                // Speed Boost Pickup before hurdle
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(52f), PowerUpType.SpeedBoost, mats, 0f);

                // Obs 2: Jump hurdle + coin arc
                CreateJumpHurdle(trackRoot.transform, path.EvaluateDistance(68f), trackWidth, mats.redSweeperMat);
                SpawnGuaranteedCoins(path, 64f, 72f, 5, CoinPattern.JumpArc, mats.coinMat, 1.8f);

                // Checkpoint 1
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 1, 95f, path.EvaluateDistance(95f), mats.checkpointMat, trackWidth));
                SpawnGuaranteedCoins(path, 102f, 122f, 6, CoinPattern.DualRows, mats.coinMat);

                // Obs 3: Realistic Overhead Swinging Hammer with yellow supports & steel head
                CreateOverheadSwingingHammer(trackRoot.transform, path.EvaluateDistance(134f), trackWidth, 50f, 1.8f, 0f, mats);

                // Shield Pickup before C-Ring and Tower
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(150f), PowerUpType.Shield, mats, -0.4f);

                // Obs 4: Thick Pink Segmented C-Ring Obstacle (NO green platform! Ribbon track over blue water)
                CreatePinkSegmentedCRingObstacle(trackRoot.transform, path.EvaluateDistance(168f), trackWidth, 42f, true, mats);

                // Checkpoint 2 (Safe staging platform directly in front of the Climbing Ladder Tower!)
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 2, 218f, path.EvaluateDistance(218f), mats.checkpointMat, trackWidth));

                // Obs 5: PROPER VERTICAL CLIMBING LADDER TOWER WITH ROTATING OBSTACLES (matching Image 1)
                // Climbs vertically from y = 0.5m to y = 8.5m with two rotating obstacles!
                CreateVerticalClimbingLadderTower(trackRoot.transform, path, 221.0f, 230.8f, mats);

                // Checkpoint 3 (On straight elevated road runway past tower at y = 8.5m)
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 3, 242.0f, path.EvaluateDistance(242.0f), mats.checkpointMat, trackWidth));

                // Continuing Road after climbing ladder:
                // Straight elevated road runway past ladder top at y = 8.5m leading into Checkpoint 3
                SpawnGuaranteedCoins(path, 233f, 245f, 4, CoinPattern.Center, mats.coinMat);

                // Speed Boost on elevated runway after ladder
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(248f), PowerUpType.SpeedBoost, mats, 0.4f);

                // Obs 6: Moving Green Platform shuttle on elevated stretch
                CreateMovingGreenPlatform(trackRoot.transform, path.EvaluateDistance(254f), new Vector3(2.6f, 0.35f, 3.2f),
                    MovingPlatformType.Horizontal, Vector3.right, 1.2f, 1.6f, 0f, mats);

                // Obs 7: Water-Rising Rotating Bar sweeping road on elevated crest
                CreateWaterRisingRotatingBar(trackRoot.transform, path.EvaluateDistance(270f), trackWidth, 75f, true, mats, 4.0f);

                // Speed pad on descent into finish stretch
                CreateSpeedPad(trackRoot.transform, path.EvaluateDistance(310f), mats.boostMat);
                SpawnGuaranteedCoins(path, 312f, 326f, 5, CoinPattern.Center, mats.coinMat);
            }
            else if (levelIndex == 2)
            {
                // === LEVEL 2: Coral Rapids (Intermediate, ~520m) ===
                SpawnGuaranteedCoins(path, 12f, 28f, 5, CoinPattern.Center, mats.coinMat);

                // Obs 1: Dual counter-rotating red sweepers
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(38f), trackWidth, 85f, mats);
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(46f), trackWidth, -85f, mats);

                // Speed Boost Pickup before Water Bar
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(56f), PowerUpType.SpeedBoost, mats, 0f);

                // Obs 2: Water-Rising Rotating Bar
                CreateWaterRisingRotatingBar(trackRoot.transform, path.EvaluateDistance(68f), trackWidth, 80f, true, mats, 4.0f);

                // Checkpoint 1
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 1, 85f, path.EvaluateDistance(85f), mats.checkpointMat, trackWidth));
                SpawnGuaranteedCoins(path, 88f, 102f, 4, CoinPattern.Center, mats.coinMat);

                // Obs 3: Moving Green Platform
                CreateMovingGreenPlatform(trackRoot.transform, path.EvaluateDistance(120f), new Vector3(2.8f, 0.35f, 3.4f),
                    MovingPlatformType.Horizontal, Vector3.right, 1.4f, 1.8f, 0f, mats);

                // Shield Pickup before Overhead Hammer
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(136f), PowerUpType.Shield, mats, -0.4f);

                // Obs 4: Overhead Swinging Hammer
                CreateOverheadSwingingHammer(trackRoot.transform, path.EvaluateDistance(150f), trackWidth, 52f, 2.0f, 0f, mats);

                // Checkpoint 2 (On elevated approach deck before Slider 1)
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 2, 214f, path.EvaluateDistance(214f), mats.checkpointMat, trackWidth));

                // Obs 5: SLIDER 1 (220m - 266m) - Steep chute attached directly to road dropping from 6.2m to 1.0m (matching Image 2)
                // Guaranteed coins along center of the slide chute
                SpawnGuaranteedCoins(path, 226f, 258f, 6, CoinPattern.Center, mats.coinMat);

                // Obs 6: IMMEDIATE OBSTACLE AFTER SLIDER 1 (at 272m, just 6m after slide exit!)
                // Pink Segmented C-Ring Obstacle (NO green platform! Ribbon track over blue water)
                CreatePinkSegmentedCRingObstacle(trackRoot.transform, path.EvaluateDistance(272f), trackWidth, 48f, true, mats);

                // Checkpoint 3 (Staging platform after C-Ring)
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 3, 290f, path.EvaluateDistance(290f), mats.checkpointMat, trackWidth));

                // Shield Pickup before Hammer Corridor
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(300f), PowerUpType.Shield, mats, 0.4f);

                // Obs 7: Overhead Hammer Corridor (2 timed swinging hammers)
                CreateOverheadHammerCorridor(trackRoot.transform, path, 310f, 8.5f, 2, 2.4f, mats, trackWidth);
                SpawnGuaranteedCoins(path, 312f, 330f, 5, CoinPattern.Slalom, mats.coinMat);

                // Speed Boost Pickup before Slider 2
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(372f), PowerUpType.SpeedBoost, mats, 0f);

                // Checkpoint 4 (On elevated crest before Slider 2)
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 4, 380f, path.EvaluateDistance(380f), mats.checkpointMat, trackWidth));

                // Obs 8: SLIDER 2 (385m - 428m) - Second steep chute dropping from 5.8m to 1.0m
                SpawnGuaranteedCoins(path, 390f, 420f, 6, CoinPattern.Center, mats.coinMat);

                // Obs 9: IMMEDIATE OBSTACLE AFTER SLIDER 2 (at 434m, right at slide exit!)
                // Water-Rising Rotating Bar Obstacle!
                CreateWaterRisingRotatingBar(trackRoot.transform, path.EvaluateDistance(434f), trackWidth, -85f, false, mats, 4.0f);

                // Speed pad into finish stretch
                CreateSpeedPad(trackRoot.transform, path.EvaluateDistance(448f), mats.boostMat);
                SpawnGuaranteedCoins(path, 450f, 460f, 4, CoinPattern.Center, mats.coinMat);
            }
            else
            {
                // === LEVEL 3: Titan's Trench (Expert, ~530m) ===
                SpawnGuaranteedCoins(path, 12f, 32f, 6, CoinPattern.Center, mats.coinMat);

                // Obs 1: Fast dual red sweepers
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(42f), trackWidth, 110f, mats);
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(50f), trackWidth, -110f, mats);

                // Checkpoint 1
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 1, 75f, path.EvaluateDistance(75f), mats.checkpointMat, trackWidth));

                // Speed Boost Pickup before Hammer Corridor
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(82f), PowerUpType.SpeedBoost, mats, 0f);

                // Obs 2: Overhead Hammer Corridor (3 timed swinging hammers with staggered phases)
                CreateOverheadHammerCorridor(trackRoot.transform, path, 90f, 7.5f, 3, 2.6f, mats, trackWidth);
                SpawnGuaranteedCoins(path, 88f, 112f, 6, CoinPattern.Slalom, mats.coinMat);

                // Shield Pickup before C-Ring and Water Bar
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(132f), PowerUpType.Shield, mats, -0.4f);

                // Obs 3: Pink Segmented C-Ring Obstacle (matching Level 1 - NO blue platform! Ribbon track over blue water)
                CreatePinkSegmentedCRingObstacle(trackRoot.transform, path.EvaluateDistance(145f), trackWidth, 48f, true, mats);
                SpawnGuaranteedCoins(path, 138f, 152f, 5, CoinPattern.Center, mats.coinMat);

                // Obs 4: Water-Rising Rotating Bar (at 165m)
                CreateWaterRisingRotatingBar(trackRoot.transform, path.EvaluateDistance(165f), trackWidth, 90f, true, mats, 4.2f);

                // Checkpoint 2
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 2, 182f, path.EvaluateDistance(182f), mats.checkpointMat, trackWidth));

                // Clear runway past Checkpoint 2 before hazards
                SpawnGuaranteedCoins(path, 184f, 196f, 5, CoinPattern.Center, mats.coinMat);

                // Obs 5: Rotating Hammer Arm + Low Rotating Rod combo
                CreateRotatingHammerArmObstacle(trackRoot.transform, path.EvaluateDistance(198f), trackWidth, 75f, false, mats);
                CreateGreenPlatformHazard(trackRoot.transform, path.EvaluateDistance(208f), trackWidth, 220f, mats);

                // Shield Pickup before Moving Platforms
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(218f), PowerUpType.Shield, mats, 0.4f);

                // Obs 6: Dual horizontal moving green platforms over open water
                CreateMovingGreenPlatform(trackRoot.transform, path.EvaluateDistance(225f), new Vector3(2.6f, 0.35f, 3.0f),
                    MovingPlatformType.Horizontal, Vector3.right, 1.5f, 2.4f, 0f, mats);
                CreateMovingGreenPlatform(trackRoot.transform, path.EvaluateDistance(235f), new Vector3(2.6f, 0.35f, 3.0f),
                    MovingPlatformType.Horizontal, Vector3.left, 1.5f, 2.4f, Mathf.PI * 0.5f, mats);

                // Obs 7: High-Altitude Wavy Climbing & Sliding Track (245m - 345m)
                // Climb to y=4.8m crest, slide descent under sweeper
                SpawnGuaranteedCoins(path, 252f, 278f, 6, CoinPattern.Center, mats.coinMat);
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(285f), 2.8f, 95f, mats);
                SpawnGuaranteedCoins(path, 292f, 318f, 6, CoinPattern.Center, mats.coinMat);

                // Checkpoint 3
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 3, 348f, path.EvaluateDistance(348f), mats.checkpointMat, trackWidth));

                // Speed Boost Pickup before Crusher Gauntlet
                CreatePowerUpPickup(trackRoot.transform, path.EvaluateDistance(358f), PowerUpType.SpeedBoost, mats, 0f);

                // Obs 8: Crusher Gate Gauntlet (2 timed descending slabs)
                CreateCrusherGate(trackRoot.transform, path.EvaluateDistance(368f), trackWidth, mats, 1.9f, 0f);
                CreateCrusherGate(trackRoot.transform, path.EvaluateDistance(378f), trackWidth, mats, 1.9f, 0.95f);

                // Checkpoint 4
                checkpoints.Add(CreateCheckpointTrigger(trackRoot.transform, 4, 412f, path.EvaluateDistance(412f), mats.checkpointMat, trackWidth));
                SpawnGuaranteedCoins(path, 414f, 426f, 5, CoinPattern.Center, mats.coinMat);

                // Obs 9: Triple rotating sweepers in sequence on hairpin bend
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(430f), trackWidth, 115f, mats);
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(438f), trackWidth, -115f, mats);
                CreateRedSweeperObstacle(trackRoot.transform, path.EvaluateDistance(446f), trackWidth, 125f, mats);

                // Obs 10: Water-Rising Rotating Bar rising out of the water
                CreateWaterRisingRotatingBar(trackRoot.transform, path.EvaluateDistance(458f), trackWidth, -95f, false, mats, 4.2f);

                // Obs 11: Rotating Green Turntable Platform over water gap
                CreateMovingGreenPlatform(trackRoot.transform, path.EvaluateDistance(475f), new Vector3(3.2f, 0.35f, 3.2f),
                    MovingPlatformType.Rotating, Vector3.up, 0f, 0f, 0f, mats, 45f);

                // Finale sprint with boost pads into finish
                CreateSpeedPad(trackRoot.transform, path.EvaluateDistance(498f), mats.boostMat);
                SpawnGuaranteedCoins(path, 492f, 510f, 5, CoinPattern.Center, mats.coinMat);
                CreateOverheadSwingingHammer(trackRoot.transform, path.EvaluateDistance(514f), trackWidth, 75f, 3.0f, 0f, mats);
            }

            // 5. Finish Line & Winner Podium
            float finishDist = totalLen - 12f;
            PathPoint finishPt = path.EvaluateDistance(finishDist);
            finishLine = CreateFinishArea(trackRoot.transform, finishDist, finishPt, trackWidth, mats);
        }

        public enum CoinPattern { Center, DualRows, JumpArc, Slalom }

        private static Transform GetOrCreateCollectiblesRoot()
        {
            GameObject root = GameObject.Find("--- COLLECTIBLES ---");
            if (root == null)
            {
                root = new GameObject("--- COLLECTIBLES ---");
            }
            return root.transform;
        }

        private static void SpawnGuaranteedCoins(TrackPath path, float startDist, float endDist, int count, CoinPattern pattern, Material coinMat, float arcHeight = 1.8f)
        {
            if (count <= 0) return;
            Transform parent = GetOrCreateCollectiblesRoot();
            float len = endDist - startDist;

            for (int i = 0; i < count; i++)
            {
                float t = (count > 1) ? (float)i / (count - 1) : 0.5f;
                float d = startDist + (t * len);
                PathPoint pt = path.EvaluateDistance(d);

                if (pattern == CoinPattern.DualRows)
                {
                    // Spawn pair: left and right within 0.85m of center
                    SpawnSingleCoin(parent, pt, -0.85f, 0.50f, coinMat, $"Coin_Dual_L_{i}");
                    SpawnSingleCoin(parent, pt, 0.85f, 0.50f, coinMat, $"Coin_Dual_R_{i}");
                }
                else if (pattern == CoinPattern.JumpArc)
                {
                    float yOffset = Mathf.Sin(t * Mathf.PI) * arcHeight + 0.50f;
                    SpawnSingleCoin(parent, pt, 0f, yOffset, coinMat, $"Coin_Arc_{i}");
                }
                else if (pattern == CoinPattern.Slalom)
                {
                    float lat = Mathf.Sin(t * Mathf.PI * 2.5f) * 0.9f;
                    SpawnSingleCoin(parent, pt, lat, 0.50f, coinMat, $"Coin_Slalom_{i}");
                }
                else
                {
                    // Center
                    SpawnSingleCoin(parent, pt, 0f, 0.50f, coinMat, $"Coin_Center_{i}");
                }
            }
        }

        private static void SpawnSingleCoin(Transform parent, PathPoint pt, float lateral, float hoverHeight, Material coinMat, string name)
        {
            float slabThickness = 0.35f;

            // Surface anchor: start at slab top surface along pt.up and lateral offset
            Vector3 surfacePos = pt.position - (Vector3.up * (slabThickness * 0.5f)) + (pt.up * (slabThickness * 0.5f)) + (pt.right * lateral);

            // Raycast downward towards track surface to snap with 100% precision to actual colliders if available
            RaycastHit hit;
            Vector3 rayStart = surfacePos + (pt.up * 1.5f);
            if (Physics.Raycast(rayStart, -pt.up, out hit, 3.5f))
            {
                if (hit.collider != null && (hit.collider.name.Contains("Track_Top") || hit.collider.name.Contains("Slider_Bed") || hit.collider.name.Contains("WavyTrack_Top") || hit.collider.name.Contains("Platform")))
                {
                    surfacePos = hit.point;
                }
            }
            else if (Physics.Raycast(rayStart, Vector3.down, out hit, 3.5f))
            {
                if (hit.collider != null && (hit.collider.name.Contains("Track_Top") || hit.collider.name.Contains("Slider_Bed") || hit.collider.name.Contains("WavyTrack_Top") || hit.collider.name.Contains("Platform")))
                {
                    surfacePos = hit.point;
                }
            }

            Vector3 pos = surfacePos + (pt.up * hoverHeight);

            GameObject coinObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coinObj.name = name;
            coinObj.transform.parent = parent;
            coinObj.transform.position = pos;
            coinObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coinObj.transform.localScale = new Vector3(0.48f, 0.08f, 0.48f);
            coinObj.GetComponent<MeshRenderer>().material = coinMat;

            CapsuleCollider cc = coinObj.GetComponent<CapsuleCollider>();
            cc.isTrigger = true;
            cc.radius = 0.55f;
            cc.height = 1.2f;

            Coin coinComp = coinObj.AddComponent<Coin>();
            coinComp.SetStartPosition(pos);
        }

        private static void CreateArch(Transform parent, PathPoint pt, Material archMat, Material bannerMat, float trackWidth, string title)
        {
            GameObject arch = new GameObject($"Arch_{title}");
            arch.transform.parent = parent;
            arch.transform.position = pt.position;
            arch.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            float pillarHeight = 4.2f;
            float pillarRadius = 0.35f;

            GameObject leftCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftCol.transform.parent = arch.transform;
            leftCol.transform.localPosition = new Vector3(-trackWidth * 0.5f - 0.4f, pillarHeight * 0.5f, 0f);
            leftCol.transform.localScale = new Vector3(pillarRadius * 2f, pillarHeight * 0.5f, pillarRadius * 2f);
            leftCol.GetComponent<MeshRenderer>().material = archMat;
            Object.DestroyImmediate(leftCol.GetComponent<CapsuleCollider>());

            GameObject rightCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightCol.transform.parent = arch.transform;
            rightCol.transform.localPosition = new Vector3(trackWidth * 0.5f + 0.4f, pillarHeight * 0.5f, 0f);
            rightCol.transform.localScale = new Vector3(pillarRadius * 2f, pillarHeight * 0.5f, pillarRadius * 2f);
            rightCol.GetComponent<MeshRenderer>().material = archMat;
            Object.DestroyImmediate(rightCol.GetComponent<CapsuleCollider>());

            GameObject cross = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cross.transform.parent = arch.transform;
            cross.transform.localPosition = new Vector3(0f, pillarHeight, 0f);
            cross.transform.localScale = new Vector3(trackWidth + 1.6f, 0.7f, 0.6f);
            cross.GetComponent<MeshRenderer>().material = bannerMat;
            Object.DestroyImmediate(cross.GetComponent<BoxCollider>());
        }

        private static void CreateRedSweeperObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, MaterialDict mats)
        {
            GameObject obsObj = new GameObject("Obstacle_RedSweeper");
            obsObj.transform.parent = parent;
            obsObj.transform.position = pt.position;
            obsObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            // Blue central hub
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.transform.parent = obsObj.transform;
            hub.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            hub.transform.localScale = new Vector3(0.85f, 0.55f, 0.85f);
            hub.GetComponent<MeshRenderer>().material = mats.blueHubMat;

            // Sweeper Pivot & Bar
            GameObject pivot = new GameObject("Pivot");
            pivot.transform.parent = obsObj.transform;
            pivot.transform.localPosition = new Vector3(0f, 0.65f, 0f);

            float barLen = trackWidth * 0.94f;
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.parent = pivot.transform;
            bar.transform.localPosition = Vector3.zero;
            bar.transform.localScale = new Vector3(barLen, 0.35f, 0.35f);
            bar.GetComponent<MeshRenderer>().material = mats.redSweeperMat;

            // Orange connection caps at both ends
            for (int e = -1; e <= 1; e += 2)
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cap.transform.parent = pivot.transform;
                cap.transform.localPosition = new Vector3(e * (barLen * 0.5f), 0f, 0f);
                cap.transform.localScale = new Vector3(0.25f, 0.42f, 0.42f);
                cap.GetComponent<MeshRenderer>().material = mats.sweeperOrangeCapMat;
                Object.DestroyImmediate(cap.GetComponent<BoxCollider>());
            }

            RotatingHazard rot = pivot.AddComponent<RotatingHazard>();
            rot.Configure(Vector3.up, speed);
        }

        public static GameObject CreatePinkSegmentedCRingObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, bool clockwise, MaterialDict mats)
        {
            GameObject hoopObj = new GameObject("Obstacle_PinkSegmentedCRing");
            hoopObj.transform.parent = parent;
            hoopObj.transform.position = pt.position;
            hoopObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            Rigidbody rootRb = hoopObj.AddComponent<Rigidbody>();
            rootRb.isKinematic = true;
            rootRb.useGravity = false;

            float radius = 2.45f;
            float hoopCenterY = 0.38f; // Low horizontal ring resting directly over ribbon track

            // Outer side support stanchions placed outside track ribbon
            GameObject frame = new GameObject("SupportFrame");
            frame.transform.parent = hoopObj.transform;
            frame.transform.localPosition = Vector3.zero;

            float postRadius = 0.22f;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = side < 0 ? "SupportPost_L" : "SupportPost_R";
                post.transform.parent = frame.transform;
                post.transform.localPosition = new Vector3(side * (radius + 0.35f), hoopCenterY * 0.5f, 0f);
                post.transform.localScale = new Vector3(postRadius * 2f, hoopCenterY * 0.5f, postRadius * 2f);
                post.GetComponent<MeshRenderer>().material = mats.hoopAxleDarkMat;
                Object.DestroyImmediate(post.GetComponent<CapsuleCollider>());

                GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.transform.parent = frame.transform;
                joint.transform.localPosition = new Vector3(side * (radius + 0.35f), hoopCenterY, 0f);
                joint.transform.localScale = Vector3.one * (postRadius * 2.6f);
                joint.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
                Object.DestroyImmediate(joint.GetComponent<SphereCollider>());
            }

            // Central rotating ring lying flat horizontally
            GameObject rotatingRing = new GameObject("RotatingRing");
            rotatingRing.transform.parent = hoopObj.transform;
            rotatingRing.transform.localPosition = new Vector3(0f, hoopCenterY, 0f);

            PinkSegmentedCRing cRingScript = hoopObj.AddComponent<PinkSegmentedCRing>();
            cRingScript.Configure(rotatingRing.transform, speed, clockwise);
            cRingScript.PushbackForce = 8.0f;

            // C-shaped arc: 260 degrees total, leaving a deliberate 100-degree opening gap!
            int segmentCount = 10;
            float startAngleDeg = 50f;
            float totalArcDeg = 260f;
            float segArc = totalArcDeg / segmentCount;
            float segLength = (2f * Mathf.PI * radius * (totalArcDeg / 360f) / segmentCount) * 1.04f;
            float tubeRadius = 0.28f;

            for (int i = 0; i < segmentCount; i++)
            {
                float angleDeg = startAngleDeg + (i + 0.5f) * segArc;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Position in XZ plane
                Vector3 segPos = new Vector3(Mathf.Sin(angleRad) * radius, 0f, Mathf.Cos(angleRad) * radius);
                Vector3 tangentDir = new Vector3(Mathf.Cos(angleRad), 0f, -Mathf.Sin(angleRad));
                Quaternion segRot = Quaternion.FromToRotation(Vector3.up, tangentDir);

                GameObject segObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                segObj.name = $"CRingSeg_{i}_{(i % 2 == 0 ? "Pink" : "Magenta")}";
                segObj.transform.parent = rotatingRing.transform;
                segObj.transform.localPosition = segPos;
                segObj.transform.localRotation = segRot;
                segObj.transform.localScale = new Vector3(tubeRadius * 2f, segLength * 0.5f, tubeRadius * 2f);

                // Alternating bright pink and darker magenta segments
                Material segMat = (i % 2 == 0) ? mats.hoopPinkMat : mats.hoopMagentaMat;
                segObj.GetComponent<MeshRenderer>().material = segMat;

                CapsuleCollider cc = segObj.GetComponent<CapsuleCollider>();
                cc.isTrigger = true;

                LargeRingSegmentCollider segHelper = segObj.AddComponent<LargeRingSegmentCollider>();
                segHelper.parentObstacle = cRingScript;

                // Golden spherical joints between segments
                if (i < segmentCount - 1)
                {
                    GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    node.name = $"Joint_{i}";
                    node.transform.parent = rotatingRing.transform;
                    float nodeAngle = (startAngleDeg + (i + 1) * segArc) * Mathf.Deg2Rad;
                    node.transform.localPosition = new Vector3(Mathf.Sin(nodeAngle) * radius, 0f, Mathf.Cos(nodeAngle) * radius);
                    node.transform.localScale = Vector3.one * (tubeRadius * 2.2f);
                    node.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
                    Object.DestroyImmediate(node.GetComponent<SphereCollider>());
                }
            }

            // Rounded spherical endcaps at both ends of the C-shaped opening
            float[] endAngles = new float[] { startAngleDeg, startAngleDeg + totalArcDeg };
            for (int e = 0; e < endAngles.Length; e++)
            {
                float endRad = endAngles[e] * Mathf.Deg2Rad;
                GameObject endCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                endCap.name = $"CEndCap_{e}";
                endCap.transform.parent = rotatingRing.transform;
                endCap.transform.localPosition = new Vector3(Mathf.Sin(endRad) * radius, 0f, Mathf.Cos(endRad) * radius);
                endCap.transform.localScale = Vector3.one * (tubeRadius * 2.1f);
                endCap.GetComponent<MeshRenderer>().material = (e == 0) ? mats.hoopPinkMat : mats.hoopMagentaMat;

                SphereCollider sc = endCap.GetComponent<SphereCollider>();
                sc.isTrigger = true;
                LargeRingSegmentCollider capHelper = endCap.AddComponent<LargeRingSegmentCollider>();
                capHelper.parentObstacle = cRingScript;
            }

            // NOTE: Bright green rectangular platform is COMPLETELY REMOVED!
            // The white track ribbon runs cleanly underneath with open blue water visible around it.

            return hoopObj;
        }

        private static GameObject CreatePinkSegmentedHoopObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, bool clockwise, MaterialDict mats, bool addCenterPlatform = true)
        {
            // Backwards-compatible forwarder: Always builds the C-shaped ring with NO green platform
            return CreatePinkSegmentedCRingObstacle(parent, pt, trackWidth, speed, clockwise, mats);
        }

        public static GameObject CreateLargePinkSegmentedCRingObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, bool clockwise, MaterialDict mats)
        {
            // Exact same pink segmented C-ring as Level 1 (NO blue platform!)
            return CreatePinkSegmentedCRingObstacle(parent, pt, trackWidth, speed, clockwise, mats);
        }

        private static GameObject CreateLargePinkSegmentedRingObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, bool clockwise, MaterialDict mats)
        {
            return CreateLargePinkSegmentedCRingObstacle(parent, pt, trackWidth, speed, clockwise, mats);
        }

        public static GameObject CreateWaterRisingRotatingBar(Transform parent, PathPoint pt, float trackWidth, float speed, bool clockwise, MaterialDict mats, float barLength = 4.0f)
        {
            GameObject barObj = new GameObject("Obstacle_WaterRisingRotatingBar");
            barObj.transform.parent = parent;
            barObj.transform.position = pt.position;
            barObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            Rigidbody rb = barObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // 1. Heavy Pylon rising from deep underwater (-5.8m up to track height +0.45m)
            float pylonBottom = -5.8f;
            float pylonTop = 0.45f;
            float pylonHeight = pylonTop - pylonBottom;
            GameObject pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pylon.name = "UnderwaterPylon";
            pylon.transform.parent = barObj.transform;
            pylon.transform.localPosition = new Vector3(0f, pylonBottom + pylonHeight * 0.5f, 0f);
            pylon.transform.localScale = new Vector3(0.65f, pylonHeight * 0.5f, 0.65f);
            pylon.GetComponent<MeshRenderer>().material = mats.trackSideMat;
            Object.DestroyImmediate(pylon.GetComponent<CapsuleCollider>());

            // 2. Waterline Flotation Collar at y = -5.5m (water surface)
            GameObject waterCollar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waterCollar.name = "WaterlineCollar";
            waterCollar.transform.parent = barObj.transform;
            waterCollar.transform.localPosition = new Vector3(0f, -5.5f - pt.position.y, 0f);
            waterCollar.transform.localScale = new Vector3(1.35f, 0.35f, 1.35f);
            waterCollar.GetComponent<MeshRenderer>().material = mats.sweeperOrangeCapMat;
            Object.DestroyImmediate(waterCollar.GetComponent<CapsuleCollider>());

            // 3. Track Hub (Cyan/Blue cylindrical hub with orange accents)
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "TrackPivotHub";
            hub.transform.parent = barObj.transform;
            hub.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            hub.transform.localScale = new Vector3(1.05f, 0.35f, 1.05f);
            hub.GetComponent<MeshRenderer>().material = mats.blueHubMat;

            GameObject hubRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hubRing.name = "HubOrangeCollar";
            hubRing.transform.parent = barObj.transform;
            hubRing.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            hubRing.transform.localScale = new Vector3(1.15f, 0.10f, 1.15f);
            hubRing.GetComponent<MeshRenderer>().material = mats.sweeperOrangeCapMat;
            Object.DestroyImmediate(hubRing.GetComponent<CapsuleCollider>());

            GameObject hubTopSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hubTopSphere.name = "HubTopSphere";
            hubTopSphere.transform.parent = barObj.transform;
            hubTopSphere.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            hubTopSphere.transform.localScale = Vector3.one * 0.95f;
            hubTopSphere.GetComponent<MeshRenderer>().material = mats.blueHubMat;
            Object.DestroyImmediate(hubTopSphere.GetComponent<SphereCollider>());

            // 4. Rotating Bar Assembly
            GameObject pivot = new GameObject("Pivot");
            pivot.transform.parent = barObj.transform;
            pivot.transform.localPosition = new Vector3(0f, 0.42f, 0f);

            WaterRisingRotatingBar barScript = barObj.AddComponent<WaterRisingRotatingBar>();
            barScript.Configure(pivot.transform, speed, clockwise, barLength);
            barScript.PushbackForce = 8.5f;

            // Long red sweeping bar extending across the track
            float halfLen = barLength * 0.5f;
            GameObject barArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            barArm.name = "SweepingBar_Red";
            barArm.transform.parent = pivot.transform;
            barArm.transform.localPosition = new Vector3(0f, 0f, 0f);
            barArm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            barArm.transform.localScale = new Vector3(0.32f, halfLen, 0.32f);
            barArm.GetComponent<MeshRenderer>().material = mats.redSweeperMat;

            CapsuleCollider cc = barArm.GetComponent<CapsuleCollider>();
            cc.isTrigger = true;

            LargeRingSegmentCollider colHelper = barArm.AddComponent<LargeRingSegmentCollider>();
            colHelper.parentObstacle = barScript;

            // Bright orange protective endcaps on both ends of the sweeping bar
            for (int e = -1; e <= 1; e += 2)
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = $"BarCap_{(e < 0 ? "L" : "R")}";
                cap.transform.parent = pivot.transform;
                cap.transform.localPosition = new Vector3(e * halfLen, 0f, 0f);
                cap.transform.localScale = Vector3.one * 0.44f;
                cap.GetComponent<MeshRenderer>().material = mats.sweeperOrangeCapMat;
                Object.DestroyImmediate(cap.GetComponent<SphereCollider>());
            }

            return barObj;
        }

        public static void CreateVerticalClimbingLadderTower(Transform parent, TrackPath path, float startDist, float endDist, MaterialDict mats)
        {
            GameObject towerGroup = new GameObject("Section_VerticalClimbingLadderTower");
            towerGroup.transform.parent = parent;

            PathPoint pStart = path.EvaluateDistance(startDist);
            PathPoint pEnd = path.EvaluateDistance(endDist);

            float baseHeight = pStart.position.y;
            float topHeight = pEnd.position.y;
            float totalClimbHeight = topHeight - baseHeight;
            float ladderWidth = 2.6f;

            Vector3 centerPos = (pStart.position + pEnd.position) * 0.5f;
            Vector3 trackFwd = new Vector3(pEnd.position.x - pStart.position.x, 0f, pEnd.position.z - pStart.position.z).normalized;
            if (trackFwd == Vector3.zero) trackFwd = Vector3.forward;
            Vector3 trackRight = Vector3.Cross(Vector3.up, trackFwd).normalized;

            // 1. Tall Concrete Tower Pillar Block (matching Image 1)
            // Positioned behind the ladder face, descending into the turquoise water
            float deckWidth = 5.4f;
            float deckLength = 8.5f;
            float pillarHeight = totalClimbHeight + 14.0f; // Extends down to water foundation

            GameObject towerPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            towerPillar.name = "LadderTowerPillar";
            towerPillar.transform.parent = towerGroup.transform;
            Vector3 pillarCenter = new Vector3(centerPos.x, topHeight - pillarHeight * 0.5f, centerPos.z) + trackFwd * (deckLength * 0.5f);
            towerPillar.transform.position = pillarCenter;
            towerPillar.transform.rotation = Quaternion.LookRotation(trackFwd, Vector3.up);
            towerPillar.transform.localScale = new Vector3(deckWidth, pillarHeight, deckLength);
            towerPillar.GetComponent<MeshRenderer>().material = mats.trackSideMat;
            // Remove box collider so character controller never snags or pushes against tower face
            Object.DestroyImmediate(towerPillar.GetComponent<BoxCollider>());

            // 2. Ladder Rungs (matching Image 1: horizontal orange/yellow rungs up the front face)
            int rungCount = Mathf.RoundToInt(totalClimbHeight / 0.35f);
            float rungZOffset = 0.0f;

            for (int r = 0; r <= rungCount; r++)
            {
                float t = r / (float)rungCount;
                float currentY = Mathf.Lerp(baseHeight + 0.2f, topHeight - 0.2f, t);

                GameObject rung = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rung.name = $"LadderRung_{r}";
                rung.transform.parent = towerGroup.transform;
                rung.transform.position = new Vector3(centerPos.x, currentY, centerPos.z) + trackFwd * rungZOffset;
                rung.transform.rotation = Quaternion.LookRotation(trackFwd, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
                rung.transform.localScale = new Vector3(0.12f, ladderWidth * 0.48f, 0.12f);
                rung.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
                Object.DestroyImmediate(rung.GetComponent<CapsuleCollider>());
            }

            // 3. Side Vertical Framing Rails along the ladder
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject sideRail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sideRail.name = (s < 0) ? "Ladder_SideRail_Left" : "Ladder_SideRail_Right";
                sideRail.transform.parent = towerGroup.transform;
                Vector3 railPos = new Vector3(centerPos.x, baseHeight + totalClimbHeight * 0.5f, centerPos.z) + (trackRight * (s * (ladderWidth * 0.5f + 0.08f))) + trackFwd * rungZOffset;
                sideRail.transform.position = railPos;
                sideRail.transform.localScale = new Vector3(0.14f, (totalClimbHeight + 0.6f) * 0.5f, 0.14f);
                sideRail.GetComponent<MeshRenderer>().material = mats.blueHubMat;
                Object.DestroyImmediate(sideRail.GetComponent<CapsuleCollider>());
            }

            // 4. Top Landing Deck Platform & Yellow Curved Handrails (matching Image 1)
            GameObject topDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topDeck.name = "Tower_TopDeck";
            topDeck.transform.parent = towerGroup.transform;
            topDeck.transform.position = new Vector3(centerPos.x, topHeight - 0.175f, centerPos.z) + trackFwd * (deckLength * 0.5f);
            topDeck.transform.rotation = Quaternion.LookRotation(trackFwd, Vector3.up);
            topDeck.transform.localScale = new Vector3(deckWidth, 0.35f, deckLength);
            topDeck.GetComponent<MeshRenderer>().material = mats.trackTopMat;

            // Ensure top deck has a solid BoxCollider for firm walking surface
            BoxCollider topDeckCol = topDeck.GetComponent<BoxCollider>();
            if (topDeckCol == null) topDeckCol = topDeck.AddComponent<BoxCollider>();
            topDeckCol.isTrigger = false;

            // Yellow safety railing on top of tower (Image 1)
            for (int s = -1; s <= 1; s += 2)
            {
                GameObject handrail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                handrail.name = (s < 0) ? "Tower_Handrail_Left" : "Tower_Handrail_Right";
                handrail.transform.parent = towerGroup.transform;
                handrail.transform.position = new Vector3(centerPos.x, topHeight + 0.45f, centerPos.z) + (trackRight * (s * (deckWidth * 0.5f - 0.12f))) + trackFwd * (deckLength * 0.5f);
                handrail.transform.rotation = Quaternion.LookRotation(trackFwd, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                handrail.transform.localScale = new Vector3(0.12f, deckLength * 0.48f, 0.12f);
                handrail.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
                Object.DestroyImmediate(handrail.GetComponent<CapsuleCollider>());
            }

            // Top curved arch handrail high above the ladder exit (Image 1)
            GameObject topArch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            topArch.name = "Tower_TopArch";
            topArch.transform.parent = towerGroup.transform;
            topArch.transform.position = new Vector3(centerPos.x, topHeight + 2.5f, centerPos.z) + trackFwd * (rungZOffset + 0.2f);
            topArch.transform.rotation = Quaternion.LookRotation(trackFwd, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
            topArch.transform.localScale = new Vector3(0.14f, ladderWidth * 0.55f, 0.14f);
            topArch.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
            Object.DestroyImmediate(topArch.GetComponent<CapsuleCollider>());

            // 5. TWO PROMINENT ROTATING OBSTACLES MOUNTED IN FRONT OF LADDER (matching Image 1)
            // Axle stands off 0.75m forward from ladder rungs into open air toward camera
            float standoff = 0.75f;

            // Obstacle 1: Lower obstacle at ~35% climb height
            float obs1Height = baseHeight + totalClimbHeight * 0.35f;
            Vector3 obs1Pos = new Vector3(centerPos.x, obs1Height, centerPos.z) - trackFwd * standoff;
            CreateLadderRotatingObstacleUnit(towerGroup.transform, obs1Pos, trackFwd, trackRight, ladderWidth, 62f, true, mats, "Obs_LadderRotating_1");

            // Obstacle 2: Upper obstacle at ~70% climb height
            float obs2Height = baseHeight + totalClimbHeight * 0.70f;
            Vector3 obs2Pos = new Vector3(centerPos.x, obs2Height, centerPos.z) - trackFwd * standoff;
            CreateLadderRotatingObstacleUnit(towerGroup.transform, obs2Pos, trackFwd, trackRight, ladderWidth, 68f, false, mats, "Obs_LadderRotating_2");
        }

        private static GameObject CreateLadderRotatingObstacleUnit(Transform parent, Vector3 centerPos, Vector3 trackFwd, Vector3 trackRight, float ladderWidth, float rotSpeed, bool clockwise, MaterialDict mats, string name)
        {
            GameObject obsRoot = new GameObject(name);
            obsRoot.transform.parent = parent;
            obsRoot.transform.position = centerPos;
            obsRoot.transform.rotation = Quaternion.LookRotation(trackFwd, Vector3.up);

            LadderRotatingObstacle obsScript = obsRoot.AddComponent<LadderRotatingObstacle>();
            obsScript.RotationSpeed = Mathf.Abs(rotSpeed);
            obsScript.RotateClockwise = clockwise;
            obsScript.PushbackForce = 8.5f;

            // In obsRoot local space:
            // Local +Z points toward the ladder/tower (standoff is 0.75m)
            // Local -Z points toward camera
            // Local +X points right
            // Local -X points left
            // Local +Y points up

            float wallStandoff = 0.75f;

            // 1. Structural Wall Mounts (Yellow Spheres and standoffs)
            Vector3 rightMountPos = new Vector3(ladderWidth * 0.5f + 0.35f, 0f, 0f);
            Vector3 midMountPos = new Vector3(-(ladderWidth * 0.5f + 0.15f), 0f, 0f);
            Vector3 leftTipPos = new Vector3(-(ladderWidth * 0.5f + 1.85f), 0f, 0f);

            // Right Mount Sphere
            GameObject rightSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightSphere.name = "Mount_Sphere_Right";
            rightSphere.transform.parent = obsRoot.transform;
            rightSphere.transform.localPosition = rightMountPos;
            rightSphere.transform.localScale = Vector3.one * 0.52f;
            rightSphere.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
            Object.DestroyImmediate(rightSphere.GetComponent<SphereCollider>());

            // Right Standoff Peg extending back into the tower face
            GameObject rightPeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPeg.name = "Mount_Peg_Right";
            rightPeg.transform.parent = obsRoot.transform;
            rightPeg.transform.localPosition = rightMountPos + new Vector3(0f, 0f, wallStandoff * 0.5f);
            rightPeg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rightPeg.transform.localScale = new Vector3(0.12f, wallStandoff * 0.5f, 0.12f);
            rightPeg.GetComponent<MeshRenderer>().material = mats.hoopAxleDarkMat;
            Object.DestroyImmediate(rightPeg.GetComponent<CapsuleCollider>());

            // Middle Mount Sphere (between climbing lane and left roller)
            GameObject midSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            midSphere.name = "Mount_Sphere_Middle";
            midSphere.transform.parent = obsRoot.transform;
            midSphere.transform.localPosition = midMountPos;
            midSphere.transform.localScale = Vector3.one * 0.52f;
            midSphere.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
            Object.DestroyImmediate(midSphere.GetComponent<SphereCollider>());

            // Middle Standoff Peg extending back into the tower face
            GameObject midPeg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            midPeg.name = "Mount_Peg_Middle";
            midPeg.transform.parent = obsRoot.transform;
            midPeg.transform.localPosition = midMountPos + new Vector3(0f, 0f, wallStandoff * 0.5f);
            midPeg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            midPeg.transform.localScale = new Vector3(0.12f, wallStandoff * 0.5f, 0.12f);
            midPeg.GetComponent<MeshRenderer>().material = mats.hoopAxleDarkMat;
            Object.DestroyImmediate(midPeg.GetComponent<CapsuleCollider>());

            // Left Tip Sphere (at far end of roller drum)
            GameObject leftSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftSphere.name = "Mount_Sphere_LeftTip";
            leftSphere.transform.parent = obsRoot.transform;
            leftSphere.transform.localPosition = leftTipPos;
            leftSphere.transform.localScale = Vector3.one * 0.48f;
            leftSphere.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
            Object.DestroyImmediate(leftSphere.GetComponent<SphereCollider>());

            // 2. Rotating Assembly (Children rotate together around local X axis)
            GameObject hub = new GameObject("RotatingAssembly");
            hub.transform.parent = obsRoot.transform;
            hub.transform.localPosition = Vector3.zero;
            hub.transform.localRotation = Quaternion.identity;
            obsScript.RotatingHub = hub.transform;

            // 3. Central Horizontal Axle connecting through the obstacle
            float totalSpan = Vector3.Distance(rightMountPos, leftTipPos);
            Vector3 axleMid = (rightMountPos + leftTipPos) * 0.5f;
            GameObject axle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axle.name = "Axle_Rod";
            axle.transform.parent = hub.transform;
            axle.transform.localPosition = axleMid;
            axle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            axle.transform.localScale = new Vector3(0.14f, totalSpan * 0.5f, 0.14f);
            axle.GetComponent<MeshRenderer>().material = mats.hoopAxleDarkMat;
            Object.DestroyImmediate(axle.GetComponent<CapsuleCollider>());

            // 4. Crossing Sweeper Hazard Bar across the ladder (between middle sphere and right sphere)
            // Radial offset: 0.55m. When theta = 0, points toward ladder rungs (+Z).
            float ladderCrossingWidth = Vector3.Distance(rightMountPos, midMountPos);
            Vector3 crossingMid = (rightMountPos + midMountPos) * 0.5f;
            float radialArmRadius = 0.55f;

            GameObject sweeperBar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sweeperBar.name = "SweeperHazardBar";
            sweeperBar.transform.parent = hub.transform;
            sweeperBar.transform.localPosition = crossingMid + new Vector3(0f, 0f, radialArmRadius);
            sweeperBar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            sweeperBar.transform.localScale = new Vector3(0.24f, ladderCrossingWidth * 0.48f, 0.24f);
            sweeperBar.GetComponent<MeshRenderer>().material = mats.sweeperOrangeCapMat;

            // Accurate physical trigger collider matching the sweeper cylinder
            CapsuleCollider barCol = sweeperBar.GetComponent<CapsuleCollider>();
            if (barCol != null)
            {
                barCol.isTrigger = true;
                barCol.radius = 0.55f; // In cylinder local space (0.24 scale * 0.55 = 0.132m radius)
            }

            Rigidbody barRb = sweeperBar.AddComponent<Rigidbody>();
            barRb.isKinematic = true;
            barRb.useGravity = false;

            LadderSweeperHazard sweeperHazard = sweeperBar.AddComponent<LadderSweeperHazard>();
            sweeperHazard.parentObstacle = obsScript;

            // Protective spherical endcaps on sweeper bar
            for (int e = -1; e <= 1; e += 2)
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = $"SweeperCap_{(e < 0 ? "L" : "R")}";
                cap.transform.parent = sweeperBar.transform;
                cap.transform.localPosition = new Vector3(0f, e * 0.96f, 0f);
                cap.transform.localScale = Vector3.one * 1.35f;
                cap.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
                Object.DestroyImmediate(cap.GetComponent<SphereCollider>());
            }

            // Radial connector arms holding the sweeper bar to the axle
            for (int a = 0; a <= 2; a++)
            {
                float t = a / 2f;
                Vector3 armX = Vector3.Lerp(midMountPos, rightMountPos, t);
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arm.name = $"RadialArm_{a}";
                arm.transform.parent = hub.transform;
                arm.transform.localPosition = armX + new Vector3(0f, 0f, radialArmRadius * 0.5f);
                arm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                arm.transform.localScale = new Vector3(0.08f, radialArmRadius * 0.5f, 0.08f);
                arm.GetComponent<MeshRenderer>().material = mats.hoopAxleDarkMat;
                Object.DestroyImmediate(arm.GetComponent<CapsuleCollider>());
            }

            // 5. Studded Roller Drum on Left Side (matching Image 1)
            Vector3 drumMid = (midMountPos + leftTipPos) * 0.5f;
            float drumLength = Vector3.Distance(midMountPos, leftTipPos) - 0.35f;

            GameObject drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "StuddedRollerDrum";
            drum.transform.parent = hub.transform;
            drum.transform.localPosition = drumMid;
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            drum.transform.localScale = new Vector3(0.68f, drumLength * 0.5f, 0.68f);
            drum.GetComponent<MeshRenderer>().material = mats.redSweeperMat;
            Object.DestroyImmediate(drum.GetComponent<CapsuleCollider>());

            // Yellow end rings on the drum
            for (int e = -1; e <= 1; e += 2)
            {
                GameObject drumRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                drumRing.name = $"DrumRing_{(e < 0 ? "L" : "R")}";
                drumRing.transform.parent = hub.transform;
                drumRing.transform.localPosition = drumMid + new Vector3(e * (drumLength * 0.48f), 0f, 0f);
                drumRing.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                drumRing.transform.localScale = new Vector3(0.74f, 0.08f, 0.74f);
                drumRing.GetComponent<MeshRenderer>().material = mats.hoopJointYellowMat;
                Object.DestroyImmediate(drumRing.GetComponent<CapsuleCollider>());
            }

            // Bright Green Rectangular Studs around the drum circumference (Image 1)
            int studRingCount = 4;
            int studsPerRing = 4;
            for (int ring = 0; ring < studRingCount; ring++)
            {
                float t = (ring + 0.5f) / (float)studRingCount;
                float studX = Mathf.Lerp(-drumLength * 0.45f, drumLength * 0.45f, t);

                for (int s = 0; s < studsPerRing; s++)
                {
                    float angle = (s * (360f / studsPerRing)) + (ring * 25f);
                    float rad = angle * Mathf.Deg2Rad;
                    float radius = 0.36f;
                    Vector3 studLocalPos = drumMid + new Vector3(studX, Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);

                    GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stud.name = $"GreenStud_{ring}_{s}";
                    stud.transform.parent = hub.transform;
                    stud.transform.localPosition = studLocalPos;
                    stud.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
                    stud.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                    stud.GetComponent<MeshRenderer>().material = mats.greenPlatformMat;
                    Object.DestroyImmediate(stud.GetComponent<BoxCollider>());
                }
            }

            return obsRoot;
        }

        private static GameObject CreateMovingGreenPlatform(Transform parent, PathPoint pt, Vector3 size, MovingPlatformType type, Vector3 moveAxis, float distance, float speed, float phase, MaterialDict mats, float rotSpeed = 35f)
        {
            GameObject platformRoot = new GameObject($"MovingGreenPlatform_{type}");
            platformRoot.transform.parent = parent;
            platformRoot.transform.position = pt.position;
            platformRoot.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            GameObject slabTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slabTop.name = "Platform_Top";
            slabTop.transform.parent = platformRoot.transform;
            slabTop.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            slabTop.transform.localScale = size;
            slabTop.GetComponent<MeshRenderer>().material = mats.greenPlatformTopMat;

            GameObject slabUnder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slabUnder.name = "Platform_Under";
            slabUnder.transform.parent = platformRoot.transform;
            slabUnder.transform.localPosition = new Vector3(0f, -size.y * 0.35f, 0f);
            slabUnder.transform.localScale = new Vector3(size.x * 0.94f, size.y * 0.7f, size.z * 0.94f);
            slabUnder.GetComponent<MeshRenderer>().material = mats.greenPlatformUnderMat;
            Object.DestroyImmediate(slabUnder.GetComponent<BoxCollider>());

            MovingPlatform mp = platformRoot.AddComponent<MovingPlatform>();
            mp.Configure(type, moveAxis, distance, speed, phase, rotSpeed);

            return platformRoot;
        }



        private static GameObject CreateOverheadSwingingHammer(Transform parent, PathPoint pt, float trackWidth, float maxAngle, float swingSpeed, float phase, MaterialDict mats)
        {
            GameObject hammerObj = new GameObject("Obstacle_OverheadSwingingHammer");
            hammerObj.transform.parent = parent;
            hammerObj.transform.position = pt.position;
            hammerObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            float gantryHeight = 5.6f;
            float postRadius = 0.28f;

            // A-frame yellow supports on left and right
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (trackWidth * 0.5f + 0.65f);
                GameObject post1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post1.transform.parent = hammerObj.transform;
                post1.transform.localPosition = new Vector3(x, gantryHeight * 0.5f, -0.6f);
                post1.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                post1.transform.localScale = new Vector3(postRadius * 2f, gantryHeight * 0.52f, postRadius * 2f);
                post1.GetComponent<MeshRenderer>().material = mats.hammerSupportYellowMat;

                GameObject post2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post2.transform.parent = hammerObj.transform;
                post2.transform.localPosition = new Vector3(x, gantryHeight * 0.5f, 0.6f);
                post2.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                post2.transform.localScale = new Vector3(postRadius * 2f, gantryHeight * 0.52f, postRadius * 2f);
                post2.GetComponent<MeshRenderer>().material = mats.hammerSupportYellowMat;
            }

            // Overhead steel crossbeam
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.transform.parent = hammerObj.transform;
            beam.transform.localPosition = new Vector3(0f, gantryHeight, 0f);
            beam.transform.localScale = new Vector3(trackWidth + 2.0f, 0.55f, 0.7f);
            beam.GetComponent<MeshRenderer>().material = mats.hammerSteelMat;

            // Pivot transform for pendulum
            GameObject pivot = new GameObject("Pivot");
            pivot.transform.parent = hammerObj.transform;
            pivot.transform.localPosition = new Vector3(0f, gantryHeight - 0.2f, 0f);

            // Suspension rod
            float rodLength = 4.2f;
            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.transform.parent = pivot.transform;
            rod.transform.localPosition = new Vector3(0f, -rodLength * 0.5f, 0f);
            rod.transform.localScale = new Vector3(0.18f, rodLength * 0.5f, 0.18f);
            rod.GetComponent<MeshRenderer>().material = mats.hammerSteelMat;
            Object.DestroyImmediate(rod.GetComponent<CapsuleCollider>());

            // Massive hammer head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "HammerHead";
            head.transform.parent = pivot.transform;
            head.transform.localPosition = new Vector3(0f, -rodLength, 0f);
            head.transform.localScale = new Vector3(1.6f, 1.1f, 1.1f);
            head.GetComponent<MeshRenderer>().material = mats.hammerSteelMat;

            BoxCollider bc = head.GetComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(1.6f, 1.1f, 1.1f);

            // Yellow warning stripes on hammer face
            for (int f = -1; f <= 1; f += 2)
            {
                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.transform.parent = head.transform;
                stripe.transform.localPosition = new Vector3(f * 0.51f, 0f, 0f);
                stripe.transform.localScale = new Vector3(0.08f, 0.9f, 0.9f);
                stripe.GetComponent<MeshRenderer>().material = mats.hammerStripeMat;
                Object.DestroyImmediate(stripe.GetComponent<BoxCollider>());
            }

            PendulumHammer ph = hammerObj.AddComponent<PendulumHammer>();
            ph.Configure(pivot.transform, maxAngle, swingSpeed, phase);

            return hammerObj;
        }

        private static void CreateOverheadHammerCorridor(Transform parent, TrackPath path, float startDist, float spacing, int count, float swingSpeed, MaterialDict mats, float trackWidth)
        {
            for (int i = 0; i < count; i++)
            {
                float d = startDist + (i * spacing);
                PathPoint pt = path.EvaluateDistance(d);
                float phase = i * (Mathf.PI / count);
                float angle = (i % 2 == 0) ? 65f : 70f;
                CreateOverheadSwingingHammer(parent, pt, trackWidth, angle, swingSpeed, phase, mats);
            }
        }

        private static GameObject CreateRotatingHammerArmObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, bool clockwise, MaterialDict mats)
        {
            GameObject armObj = new GameObject("Obstacle_RotatingHammerArm");
            armObj.transform.parent = parent;
            armObj.transform.position = pt.position;
            armObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            // Central base pedestal
            GameObject basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePedestal.transform.parent = armObj.transform;
            basePedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            basePedestal.transform.localScale = new Vector3(0.9f, 0.35f, 0.9f);
            basePedestal.GetComponent<MeshRenderer>().material = mats.blueHubMat;

            // Rotating arm root
            GameObject rotRoot = new GameObject("RotatingArmRoot");
            rotRoot.transform.parent = armObj.transform;
            rotRoot.transform.localPosition = new Vector3(0f, 0.65f, 0f);

            float armLength = 2.4f;
            GameObject armRod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            armRod.transform.parent = rotRoot.transform;
            armRod.transform.localPosition = new Vector3(armLength * 0.5f, 0f, 0f);
            armRod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            armRod.transform.localScale = new Vector3(0.18f, armLength * 0.5f, 0.18f);
            armRod.GetComponent<MeshRenderer>().material = mats.hammerSteelMat;
            Object.DestroyImmediate(armRod.GetComponent<CapsuleCollider>());

            // Heavy end piece / hammer head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "HammerHeadEnd";
            head.transform.parent = rotRoot.transform;
            head.transform.localPosition = new Vector3(armLength, 0f, 0f);
            head.transform.localScale = new Vector3(1.1f, 0.95f, 0.95f);
            head.GetComponent<MeshRenderer>().material = mats.hammerSteelMat;

            BoxCollider bc = head.GetComponent<BoxCollider>();
            bc.isTrigger = true;

            // Yellow warning stripes
            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.transform.parent = head.transform;
            stripe.transform.localPosition = Vector3.zero;
            stripe.transform.localScale = new Vector3(1.15f, 0.45f, 1.05f);
            stripe.GetComponent<MeshRenderer>().material = mats.hammerStripeMat;
            Object.DestroyImmediate(stripe.GetComponent<BoxCollider>());

            RotatingHammerArm rha = rotRoot.AddComponent<RotatingHammerArm>();
            rha.Configure(speed, clockwise, armLength);

            return armObj;
        }

        private static void CreatePinkWhiteCurvedObstacle(Transform parent, PathPoint pt, float trackWidth, float speed, MaterialDict mats, float arcAngle = 270f)
        {
            GameObject obsObj = new GameObject("Obstacle_PinkWhiteCurvedRing");
            obsObj.transform.parent = parent;
            obsObj.transform.position = pt.position + Vector3.up * 1.8f;
            obsObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            GameObject ringPivot = new GameObject("RingPivot");
            ringPivot.transform.parent = obsObj.transform;
            ringPivot.transform.localPosition = Vector3.zero;

            int segments = 8;
            float radius = trackWidth * 0.45f;

            for (int s = 0; s < segments; s++)
            {
                float angle = (arcAngle / segments) * s;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 segPos = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, 0f);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.transform.parent = ringPivot.transform;
                seg.transform.localPosition = segPos;
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                seg.transform.localScale = new Vector3(0.75f, 0.28f, 0.35f);

                Material segMat = (s % 2 == 0) ? mats.pinkStripeMat : mats.whiteStripeMat;
                seg.GetComponent<MeshRenderer>().material = segMat;

                seg.AddComponent<ObstacleBase>();
            }

            PinkWhiteCurvedObstacle curvedComp = ringPivot.AddComponent<PinkWhiteCurvedObstacle>();
            curvedComp.Configure(ringPivot.transform, speed, Vector3.forward);
        }

        private static void CreateGreenPlatformHazard(Transform parent, PathPoint pt, float trackWidth, float rodSpeed, MaterialDict mats)
        {
            GameObject obsObj = new GameObject("Obstacle_GreenPlatformHazard");
            obsObj.transform.parent = parent;
            obsObj.transform.position = pt.position;
            obsObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            // Green floor slab
            GameObject greenSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            greenSlab.name = "GreenFloor";
            greenSlab.transform.parent = obsObj.transform;
            greenSlab.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            greenSlab.transform.localScale = new Vector3(trackWidth, 0.38f, 6.0f);
            greenSlab.GetComponent<MeshRenderer>().material = mats.greenPlatformMat;

            // Low spinning rod
            GameObject rodPivot = new GameObject("RodPivot");
            rodPivot.transform.parent = obsObj.transform;
            rodPivot.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.transform.parent = rodPivot.transform;
            rod.transform.localPosition = Vector3.zero;
            rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rod.transform.localScale = new Vector3(0.24f, trackWidth * 0.46f, 0.24f);
            rod.GetComponent<MeshRenderer>().material = mats.redSweeperMat;

            GreenPlatformHazard hazardComp = rodPivot.AddComponent<GreenPlatformHazard>();
            hazardComp.Configure(rodPivot.transform, rodSpeed, Vector3.up);
        }

        private static void CreateSwingingMallet(Transform parent, PathPoint pt, float trackWidth, float speed, float maxAngle, MaterialDict mats, float phase = 0f)
        {
            GameObject gantryObj = new GameObject("Obstacle_SwingingMallet");
            gantryObj.transform.parent = parent;
            gantryObj.transform.position = pt.position;
            gantryObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            float gantryHeight = 5.2f;

            // Support pillars
            GameObject lCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lCol.transform.parent = gantryObj.transform;
            lCol.transform.localPosition = new Vector3(-trackWidth * 0.5f - 0.2f, gantryHeight * 0.5f, 0f);
            lCol.transform.localScale = new Vector3(0.4f, gantryHeight * 0.5f, 0.4f);
            lCol.GetComponent<MeshRenderer>().material = mats.gantryYellowMat;

            GameObject rCol = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rCol.transform.parent = gantryObj.transform;
            rCol.transform.localPosition = new Vector3(trackWidth * 0.5f + 0.2f, gantryHeight * 0.5f, 0f);
            rCol.transform.localScale = new Vector3(0.4f, gantryHeight * 0.5f, 0.4f);
            rCol.GetComponent<MeshRenderer>().material = mats.gantryYellowMat;

            GameObject topBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBeam.transform.parent = gantryObj.transform;
            topBeam.transform.localPosition = new Vector3(0f, gantryHeight, 0f);
            topBeam.transform.localScale = new Vector3(trackWidth + 1.2f, 0.5f, 0.5f);
            topBeam.GetComponent<MeshRenderer>().material = mats.gantryYellowMat;

            // Swinging Pivot & Hammer
            GameObject pivot = new GameObject("Pivot");
            pivot.transform.parent = gantryObj.transform;
            pivot.transform.localPosition = new Vector3(0f, gantryHeight - 0.2f, 0f);

            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.transform.parent = pivot.transform;
            rod.transform.localPosition = new Vector3(0f, -2.1f, 0f);
            rod.transform.localScale = new Vector3(0.12f, 2.0f, 0.12f);
            rod.GetComponent<MeshRenderer>().material = mats.gantryYellowMat;

            GameObject mallet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mallet.transform.parent = pivot.transform;
            mallet.transform.localPosition = new Vector3(0f, -4.2f, 0f);
            mallet.transform.localScale = new Vector3(1.1f, 0.75f, 0.8f);
            mallet.GetComponent<MeshRenderer>().material = mats.redSweeperMat;

            PendulumHammer hammerComp = gantryObj.AddComponent<PendulumHammer>();
            hammerComp.Configure(pivot.transform, maxAngle, speed, phase);
        }

        private static void CreateRotatingLog(Transform parent, PathPoint pt, float trackWidth, float speed, MaterialDict mats)
        {
            GameObject logObj = new GameObject("Obstacle_RotatingLog");
            logObj.transform.parent = parent;
            logObj.transform.position = pt.position + Vector3.up * 0.35f;
            logObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            GameObject roller = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            roller.transform.parent = logObj.transform;
            roller.transform.localPosition = Vector3.zero;
            roller.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            roller.transform.localScale = new Vector3(0.45f, trackWidth * 0.48f, 0.45f);
            roller.GetComponent<MeshRenderer>().material = mats.pinkStripeMat;

            RotatingLog logComp = logObj.AddComponent<RotatingLog>();
            logComp.Configure(speed, Vector3.right);
        }

        private static void CreateCrusherGate(Transform parent, PathPoint pt, float trackWidth, MaterialDict mats, float cycleDur = 2.4f, float delay = 0f)
        {
            GameObject gateObj = new GameObject("Obstacle_CrusherGate");
            gateObj.transform.parent = parent;
            gateObj.transform.position = pt.position;
            gateObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            float frameHeight = 4.0f;

            GameObject lCol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lCol.transform.parent = gateObj.transform;
            lCol.transform.localPosition = new Vector3(-trackWidth * 0.5f - 0.15f, frameHeight * 0.5f, 0f);
            lCol.transform.localScale = new Vector3(0.4f, frameHeight, 0.5f);
            lCol.GetComponent<MeshRenderer>().material = mats.gantryYellowMat;

            GameObject rCol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rCol.transform.parent = gateObj.transform;
            rCol.transform.localPosition = new Vector3(trackWidth * 0.5f + 0.15f, frameHeight * 0.5f, 0f);
            rCol.transform.localScale = new Vector3(0.4f, frameHeight, 0.5f);
            rCol.GetComponent<MeshRenderer>().material = mats.gantryYellowMat;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.parent = gateObj.transform;
            block.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            block.transform.localScale = new Vector3(trackWidth * 0.92f, 1.2f, 0.6f);
            block.GetComponent<MeshRenderer>().material = mats.redSweeperMat;

            CrusherGate crusherComp = block.AddComponent<CrusherGate>();
            crusherComp.Configure(0.55f, 3.2f, cycleDur, delay);
        }

        private static void CreateJumpHurdle(Transform parent, PathPoint pt, float trackWidth, Material mat)
        {
            GameObject hurdle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hurdle.name = "Obstacle_JumpHurdle";
            hurdle.transform.parent = parent;
            hurdle.transform.position = pt.position + Vector3.up * 0.35f;
            hurdle.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
            hurdle.transform.localScale = new Vector3(trackWidth * 0.88f, 0.7f, 0.3f);
            hurdle.GetComponent<MeshRenderer>().material = mat;
            hurdle.AddComponent<ObstacleBase>();
        }

        private static void CreateSpeedPad(Transform parent, PathPoint pt, Material boostMat)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "SpeedBoostPad";
            pad.transform.parent = parent;
            pad.transform.position = pt.position + Vector3.up * 0.05f;
            pad.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);
            pad.transform.localScale = new Vector3(2.4f, 0.1f, 3.5f);
            pad.GetComponent<MeshRenderer>().material = boostMat;
            pad.GetComponent<BoxCollider>().isTrigger = true;

            SpeedBoostPad boostComp = pad.AddComponent<SpeedBoostPad>();
            boostComp.Configure(1.6f, 2.5f);
        }

        public static GameObject CreatePowerUpPickup(Transform parent, PathPoint pt, PowerUpType type, MaterialDict mats, float lateralOffset = 0f)
        {
            GameObject root = new GameObject($"PowerUp_{type}");
            root.transform.parent = parent;
            Vector3 trackSurface = pt.position + (pt.right * lateralOffset);
            root.transform.position = trackSurface + Vector3.up * 0.45f;
            root.transform.rotation = Quaternion.LookRotation(pt.forward, Vector3.up);

            SphereCollider col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.85f;

            PowerUpItem item = root.AddComponent<PowerUpItem>();
            item.Configure(type, type == PowerUpType.SpeedBoost ? 3.5f : 0f, 1.6f);

            GameObject model = new GameObject("VisualModel");
            model.transform.parent = root.transform;
            model.transform.localPosition = Vector3.zero;

            if (type == PowerUpType.SpeedBoost)
            {
                // Core Diamond / Lightning Prism
                GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = "SpeedGem";
                core.transform.parent = model.transform;
                core.transform.localPosition = Vector3.zero;
                core.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                core.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
                Object.DestroyImmediate(core.GetComponent<Collider>());
                core.GetComponent<MeshRenderer>().material = mats.speedPickupMat ?? mats.boostMat;

                // Orbiting Ring
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "AuraRing";
                ring.transform.parent = model.transform;
                ring.transform.localPosition = Vector3.zero;
                ring.transform.localRotation = Quaternion.Euler(20f, 0f, 35f);
                ring.transform.localScale = new Vector3(0.72f, 0.04f, 0.72f);
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.GetComponent<MeshRenderer>().material = mats.coinMat;
            }
            else // Shield
            {
                // Energy Core Sphere
                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "ShieldOrb";
                orb.transform.parent = model.transform;
                orb.transform.localPosition = Vector3.zero;
                orb.transform.localScale = new Vector3(0.52f, 0.52f, 0.52f);
                Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.GetComponent<MeshRenderer>().material = mats.shieldOrbMat ?? mats.cyanHubMat;

                // Protective Crest Ring
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "ShieldRing";
                ring.transform.parent = model.transform;
                ring.transform.localPosition = Vector3.zero;
                ring.transform.localRotation = Quaternion.Euler(30f, 45f, 0f);
                ring.transform.localScale = new Vector3(0.85f, 0.05f, 0.85f);
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.GetComponent<MeshRenderer>().material = mats.shieldPickupMat ?? mats.cyanHubMat;
            }

            return root;
        }

        private static Checkpoint CreateCheckpointTrigger(Transform parent, int index, float distance, PathPoint pt, Material cpMat, float trackWidth)
        {
            GameObject cpObj = new GameObject($"Checkpoint_{index}");
            cpObj.transform.parent = parent;
            cpObj.transform.position = pt.position;
            cpObj.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            float h = 4.0f;
            GameObject lPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lPost.transform.parent = cpObj.transform;
            lPost.transform.localPosition = new Vector3(-trackWidth * 0.5f - 0.35f, h * 0.5f, 0f);
            lPost.transform.localScale = new Vector3(0.3f, h * 0.5f, 0.3f);
            lPost.GetComponent<MeshRenderer>().material = cpMat;
            Object.DestroyImmediate(lPost.GetComponent<CapsuleCollider>());

            GameObject rPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rPost.transform.parent = cpObj.transform;
            rPost.transform.localPosition = new Vector3(trackWidth * 0.5f + 0.35f, h * 0.5f, 0f);
            rPost.transform.localScale = new Vector3(0.3f, h * 0.5f, 0.3f);
            rPost.GetComponent<MeshRenderer>().material = cpMat;
            Object.DestroyImmediate(rPost.GetComponent<CapsuleCollider>());

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.transform.parent = cpObj.transform;
            beam.transform.localPosition = new Vector3(0f, h, 0f);
            beam.transform.localScale = new Vector3(trackWidth + 1.4f, 0.4f, 0.4f);
            beam.GetComponent<MeshRenderer>().material = cpMat;
            Object.DestroyImmediate(beam.GetComponent<BoxCollider>());

            BoxCollider trigger = cpObj.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(trackWidth + 1.5f, 4.0f, 2.0f);
            trigger.center = new Vector3(0f, 2.0f, 0f);

            Checkpoint cpComp = cpObj.AddComponent<Checkpoint>();
            cpComp.Configure(index, distance);
            return cpComp;
        }

        private static FinishLine CreateFinishArea(Transform parent, float distance, PathPoint pt, float trackWidth, MaterialDict mats)
        {
            GameObject finishRoot = new GameObject("--- FINISH_AREA ---");
            finishRoot.transform.parent = parent;
            finishRoot.transform.position = pt.position;
            finishRoot.transform.rotation = Quaternion.LookRotation(pt.forward, pt.up);

            CreateArch(finishRoot.transform, pt, mats.finishArchMat, mats.trackSideMat, trackWidth, "FINISH");

            BoxCollider trigger = finishRoot.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(trackWidth + 1.5f, 3.5f, 2.5f);
            trigger.center = new Vector3(0f, 1.75f, 0f);

            FinishLine flComp = finishRoot.AddComponent<FinishLine>();

            // Winner Podium (Placed 9m past finish line)
            Vector3 podiumPos = new Vector3(0f, 0f, 9.0f);
            GameObject podium = new GameObject("Winner_Podium");
            podium.transform.parent = finishRoot.transform;
            podium.transform.localPosition = podiumPos;
            podium.transform.localRotation = Quaternion.identity;

            // 1st Place Step (Center, Gold)
            GameObject step1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            step1.name = "Podium_1st";
            step1.transform.parent = podium.transform;
            step1.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            step1.transform.localScale = new Vector3(1.6f, 0.65f, 1.6f);
            step1.GetComponent<MeshRenderer>().material = mats.goldMat;

            // 2nd Place Step (Left, Silver)
            GameObject step2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            step2.name = "Podium_2nd";
            step2.transform.parent = podium.transform;
            step2.transform.localPosition = new Vector3(-2.2f, 0.45f, 0f);
            step2.transform.localScale = new Vector3(1.5f, 0.45f, 1.5f);
            step2.GetComponent<MeshRenderer>().material = mats.silverMat;

            // 3rd Place Step (Right, Bronze)
            GameObject step3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            step3.name = "Podium_3rd";
            step3.transform.parent = podium.transform;
            step3.transform.localPosition = new Vector3(2.2f, 0.3f, 0f);
            step3.transform.localScale = new Vector3(1.5f, 0.3f, 1.5f);
            step3.GetComponent<MeshRenderer>().material = mats.bronzeMat;

            // 4th Place Position (Right of Bronze)
            GameObject step4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            step4.name = "Podium_4th";
            step4.transform.parent = podium.transform;
            step4.transform.localPosition = new Vector3(4.0f, 0.12f, 0f);
            step4.transform.localScale = new Vector3(1.4f, 0.12f, 1.4f);
            step4.GetComponent<MeshRenderer>().material = mats.trackSideMat;

            return flComp;
        }

        public static PlayerController BuildPlayer(Vector3 spawn, Vector3 forward, MaterialDict mats, float speed)
        {
            GameObject playerObj = new GameObject("Player");
            playerObj.transform.position = spawn;
            playerObj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            CharacterController cc = playerObj.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.8f, 0f);

            GameObject model = new GameObject("Model");
            model.transform.parent = playerObj.transform;
            model.transform.localPosition = Vector3.zero;

            Transform torso, head, lLeg, rLeg, lArm, rArm, lFoot, rFoot, lHand, rHand;
            BuildRefinedHumanoidMesh(model.transform, mats.playerMat, mats, out torso, out head,
                out lLeg, out rLeg, out lArm, out rArm, out lFoot, out rFoot, out lHand, out rHand);

            GameObject trailObj = new GameObject("BoostTrail");
            trailObj.transform.parent = playerObj.transform;
            trailObj.transform.localPosition = new Vector3(0f, 0.7f, -0.2f);
            TrailRenderer tr = trailObj.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.startWidth = 0.35f;
            tr.endWidth = 0.05f;
            tr.material = mats.boostMat;
            tr.emitting = false;

            PlayerVisuals visuals = model.AddComponent<PlayerVisuals>();
            visuals.AssignLimbs(torso, head, lLeg, rLeg, lArm, rArm, lFoot, rFoot, lHand, rHand);

            PlayerController controller = playerObj.AddComponent<PlayerController>();
            controller.BaseSpeed = speed;
            return controller;
        }

        public static AIRacerController BuildAIRacer(string name, Color color, float defaultLateral, Vector3 spawn, Material bodyMat, MaterialDict mats, float speed)
        {
            GameObject aiObj = new GameObject(name);
            aiObj.transform.position = spawn;
            aiObj.transform.rotation = Quaternion.identity;

            CharacterController cc = aiObj.AddComponent<CharacterController>();
            cc.height = 1.6f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.8f, 0f);

            GameObject model = new GameObject("Model");
            model.transform.parent = aiObj.transform;
            model.transform.localPosition = Vector3.zero;

            Transform torso, head, lLeg, rLeg, lArm, rArm, lFoot, rFoot, lHand, rHand;
            BuildRefinedHumanoidMesh(model.transform, bodyMat, mats, out torso, out head,
                out lLeg, out rLeg, out lArm, out rArm, out lFoot, out rFoot, out lHand, out rHand);

            AIRacerController aiComp = aiObj.AddComponent<AIRacerController>();
            aiComp.Configure(name, color, speed, defaultLateral, torso, lLeg, rLeg, lArm, rArm, lFoot, rFoot, lHand, rHand, head);
            return aiComp;
        }

        private static void BuildRefinedHumanoidMesh(Transform parent, Material bodyMat, MaterialDict mats,
            out Transform torso, out Transform head, out Transform lLeg, out Transform rLeg,
            out Transform lArm, out Transform rArm, out Transform lFoot, out Transform rFoot,
            out Transform lHand, out Transform rHand)
        {
            // --- 1. PELVIS & TORSO ---
            GameObject pelvisObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pelvisObj.name = "Pelvis";
            pelvisObj.transform.parent = parent;
            pelvisObj.transform.localPosition = new Vector3(0f, 0.58f, 0f);
            pelvisObj.transform.localScale = new Vector3(0.44f, 0.30f, 0.38f);
            pelvisObj.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(pelvisObj.GetComponent<SphereCollider>());

            GameObject torsoObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torsoObj.name = "Torso";
            torsoObj.transform.parent = parent;
            torsoObj.transform.localPosition = new Vector3(0f, 0.88f, 0f);
            torsoObj.transform.localScale = new Vector3(0.46f, 0.36f, 0.38f);
            torsoObj.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(torsoObj.GetComponent<CapsuleCollider>());
            torso = torsoObj.transform;

            // --- 2. HEAD & VISOR ---
            GameObject headObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headObj.name = "Head";
            headObj.transform.parent = torsoObj.transform;
            headObj.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            headObj.transform.localScale = new Vector3(0.95f, 0.92f, 0.95f);
            headObj.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(headObj.GetComponent<SphereCollider>());
            head = headObj.transform;

            // Curved dark glossy visor
            GameObject visorObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visorObj.name = "Visor";
            visorObj.transform.parent = headObj.transform;
            visorObj.transform.localPosition = new Vector3(0f, 0.04f, 0.28f);
            visorObj.transform.localScale = new Vector3(0.72f, 0.38f, 0.44f);
            visorObj.GetComponent<MeshRenderer>().material = mats.visorMat;
            Object.DestroyImmediate(visorObj.GetComponent<SphereCollider>());

            // Arcade visor reflection glint
            GameObject glint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glint.name = "VisorGlint";
            glint.transform.parent = visorObj.transform;
            glint.transform.localPosition = new Vector3(0.18f, 0.16f, 0.38f);
            glint.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            glint.GetComponent<MeshRenderer>().material = mats.visorHighlightMat;
            Object.DestroyImmediate(glint.GetComponent<SphereCollider>());

            // Helmet side knobs
            GameObject earL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            earL.transform.parent = headObj.transform;
            earL.transform.localPosition = new Vector3(-0.52f, 0f, 0f);
            earL.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            earL.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
            earL.GetComponent<MeshRenderer>().material = mats.gloveMat;
            Object.DestroyImmediate(earL.GetComponent<CapsuleCollider>());

            GameObject earR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            earR.transform.parent = headObj.transform;
            earR.transform.localPosition = new Vector3(0.52f, 0f, 0f);
            earR.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            earR.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
            earR.GetComponent<MeshRenderer>().material = mats.gloveMat;
            Object.DestroyImmediate(earR.GetComponent<CapsuleCollider>());

            // --- 3. LEGS & FEET (Feet firmly grounded at y = 0.0f) ---
            // Left Leg Pivot at hip
            GameObject lLegPivot = new GameObject("LeftLeg");
            lLegPivot.transform.parent = parent;
            lLegPivot.transform.localPosition = new Vector3(-0.16f, 0.52f, 0f);
            lLeg = lLegPivot.transform;

            GameObject lLegMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            lLegMesh.transform.parent = lLegPivot.transform;
            lLegMesh.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            lLegMesh.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
            lLegMesh.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(lLegMesh.GetComponent<CapsuleCollider>());

            // Left Running Shoe / Foot
            GameObject lFootObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lFootObj.name = "LeftFoot";
            lFootObj.transform.parent = lLegPivot.transform;
            lFootObj.transform.localPosition = new Vector3(0f, -0.46f, 0.08f);
            lFootObj.transform.localScale = new Vector3(0.18f, 0.12f, 0.28f);
            lFootObj.GetComponent<MeshRenderer>().material = mats.gloveMat;
            Object.DestroyImmediate(lFootObj.GetComponent<BoxCollider>());
            lFoot = lFootObj.transform;

            // Right Leg Pivot at hip
            GameObject rLegPivot = new GameObject("RightLeg");
            rLegPivot.transform.parent = parent;
            rLegPivot.transform.localPosition = new Vector3(0.16f, 0.52f, 0f);
            rLeg = rLegPivot.transform;

            GameObject rLegMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rLegMesh.transform.parent = rLegPivot.transform;
            rLegMesh.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            rLegMesh.transform.localScale = new Vector3(0.16f, 0.22f, 0.16f);
            rLegMesh.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(rLegMesh.GetComponent<CapsuleCollider>());

            // Right Running Shoe / Foot
            GameObject rFootObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rFootObj.name = "RightFoot";
            rFootObj.transform.parent = rLegPivot.transform;
            rFootObj.transform.localPosition = new Vector3(0f, -0.46f, 0.08f);
            rFootObj.transform.localScale = new Vector3(0.18f, 0.12f, 0.28f);
            rFootObj.GetComponent<MeshRenderer>().material = mats.gloveMat;
            Object.DestroyImmediate(rFootObj.GetComponent<BoxCollider>());
            rFoot = rFootObj.transform;

            // --- 4. ARMS & HANDS (MITTENS) ---
            // Left Arm Pivot at shoulder
            GameObject lArmPivot = new GameObject("LeftArm");
            lArmPivot.transform.parent = torsoObj.transform;
            lArmPivot.transform.localPosition = new Vector3(-0.62f, 0.15f, 0f);
            lArm = lArmPivot.transform;

            GameObject lArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            lArmMesh.transform.parent = lArmPivot.transform;
            lArmMesh.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            lArmMesh.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            lArmMesh.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(lArmMesh.GetComponent<CapsuleCollider>());

            // Left Hand / Mitten
            GameObject lHandObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lHandObj.name = "LeftHand";
            lHandObj.transform.parent = lArmPivot.transform;
            lHandObj.transform.localPosition = new Vector3(0f, -0.46f, 0f);
            lHandObj.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            lHandObj.GetComponent<MeshRenderer>().material = mats.gloveMat;
            Object.DestroyImmediate(lHandObj.GetComponent<SphereCollider>());
            lHand = lHandObj.transform;

            // Right Arm Pivot at shoulder
            GameObject rArmPivot = new GameObject("RightArm");
            rArmPivot.transform.parent = torsoObj.transform;
            rArmPivot.transform.localPosition = new Vector3(0.62f, 0.15f, 0f);
            rArm = rArmPivot.transform;

            GameObject rArmMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rArmMesh.transform.parent = rArmPivot.transform;
            rArmMesh.transform.localPosition = new Vector3(0f, -0.22f, 0f);
            rArmMesh.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            rArmMesh.GetComponent<MeshRenderer>().material = bodyMat;
            Object.DestroyImmediate(rArmMesh.GetComponent<CapsuleCollider>());

            // Right Hand / Mitten
            GameObject rHandObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rHandObj.name = "RightHand";
            rHandObj.transform.parent = rArmPivot.transform;
            rHandObj.transform.localPosition = new Vector3(0f, -0.46f, 0f);
            rHandObj.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            rHandObj.GetComponent<MeshRenderer>().material = mats.gloveMat;
            Object.DestroyImmediate(rHandObj.GetComponent<SphereCollider>());
            rHand = rHandObj.transform;
        }

        private static ThirdPersonCamera BuildCamera(Transform target, int levelIndex = 1)
        {
            Camera cam = Camera.main;
            GameObject camObj;
            if (cam == null)
            {
                camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                camObj.AddComponent<AudioListener>();
            }
            else
            {
                camObj = cam.gameObject;
            }

            cam.clearFlags = CameraClearFlags.Skybox;
            if (levelIndex == 1)
                cam.backgroundColor = new Color(0.45f, 0.78f, 0.98f);
            else if (levelIndex == 2)
                cam.backgroundColor = new Color(0.80f, 0.82f, 0.96f);
            else
                cam.backgroundColor = new Color(0.95f, 0.50f, 0.26f);

            cam.fieldOfView = 60f;

            ThirdPersonCamera tpc = camObj.GetComponent<ThirdPersonCamera>() ?? camObj.AddComponent<ThirdPersonCamera>();
            tpc.SetTarget(target);
            return tpc;
        }

        public static UIManager BuildUI()
        {
            UnityEngine.EventSystems.EventSystem es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Object.DestroyImmediate(standalone);
            }

#if ENABLE_INPUT_SYSTEM
            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
#else
            if (es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
#endif

            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // --- 1. Start Menu Panel ---
            GameObject startPanel = CreateUIPanel(canvasObj.transform, "StartMenuPanel", new Color(0.04f, 0.10f, 0.20f, 0.90f));

            CreateUIText(startPanel.transform, "Title", "DASH MASTERS 3D", 72, FontStyle.Bold, Color.yellow, new Vector2(0f, 220f), new Vector2(950f, 110f), defaultFont);
            CreateUIText(startPanel.transform, "Subtitle", "COLORFUL CASUAL 3D OBSTACLE RACING", 28, FontStyle.Normal, Color.white, new Vector2(0f, 150f), new Vector2(900f, 55f), defaultFont);

            Text currentLvlInfo = CreateUIText(startPanel.transform, "TrackInfo", "SELECTED TRACK: LEVEL 1 — LAGOON SPRINT (BEGINNER)", 26, FontStyle.Bold, new Color(0.2f, 1f, 0.6f), new Vector2(0f, 85f), new Vector2(900f, 50f), defaultFont);

            CreateUIText(startPanel.transform, "Controls",
                "CONTROLS:\n* HOLD [W] or [LEFT CLICK] -> RUN FORWARD\n* RELEASE [W] -> STOP & WAIT FOR HAZARDS\n* [SPACEBAR] -> JUMP OVER HURDLES\n* HOLD [S] -> CRAWL UNDER SWEEPERS & CRUSHERS",
                24, FontStyle.Normal, new Color(0.85f, 0.95f, 1f), new Vector2(0f, -15f), new Vector2(850f, 140f), defaultFont);

            Button playBtn = CreateUIButton(startPanel.transform, "PlayButton", "RACE NOW!", 36, new Vector2(0f, -130f), new Vector2(340f, 80f), new Color(0.12f, 0.85f, 0.35f), defaultFont);
            Button openLevelSelectBtn = CreateUIButton(startPanel.transform, "OpenLevelSelectBtn", "SELECT TRACK", 26, new Vector2(-170f, -225f), new Vector2(280f, 65f), new Color(0.15f, 0.65f, 0.95f), defaultFont);
            Button openCostumesBtn = CreateUIButton(startPanel.transform, "OpenCostumesBtn", "COSTUMES & SKINS", 26, new Vector2(170f, -225f), new Vector2(280f, 65f), new Color(0.85f, 0.45f, 0.15f), defaultFont);

            // --- 2. Level Selection Panel ---
            GameObject levelPanel = CreateUIPanel(canvasObj.transform, "LevelSelectPanel", new Color(0.04f, 0.08f, 0.16f, 0.95f));
            CreateUIText(levelPanel.transform, "LvlSelectTitle", "CHOOSE YOUR TRACK", 62, FontStyle.Bold, Color.yellow, new Vector2(0f, 360f), new Vector2(800f, 90f), defaultFont);

            // Card 1: Beginner
            Image card1Hl;
            Button card1Btn = CreateLevelCard(levelPanel.transform, "Card1", new Vector2(-460f, 40f), "LEVEL 1", "LAGOON SPRINT",
                "DIFFICULTY: [★☆☆☆☆] BEGINNER", "Broad gentle curves, wide track, steady single sweepers, and basic hurdles.", "280m • TURQUOISE LAGOON",
                new Color(0.08f, 0.55f, 0.75f), defaultFont, out card1Hl);

            // Card 2: Intermediate
            Image card2Hl;
            Button card2Btn = CreateLevelCard(levelPanel.transform, "Card2", new Vector2(0f, 40f), "LEVEL 2", "CORAL RAPIDS",
                "DIFFICULTY: [★★★☆☆] INTERMEDIATE", "Winding S-curves, elevated crests, dual sweepers, swinging mallets, and spiky rollers.", "420m • EMERALD REEFS",
                new Color(0.12f, 0.65f, 0.45f), defaultFont, out card2Hl);

            // Card 3: Expert
            Image card3Hl;
            Button card3Btn = CreateLevelCard(levelPanel.transform, "Card3", new Vector2(460f, 40f), "LEVEL 3", "TITAN'S TRENCH",
                "DIFFICULTY: [★★★★★] EXPERT", "High-altitude crests (y=3.5m), triple sweepers, nested C-rings, and timed crusher gauntlet!", "530m • OCEANIC TRENCH",
                new Color(0.65f, 0.18f, 0.28f), defaultFont, out card3Hl);

            Button playSelectedLvlBtn = CreateUIButton(levelPanel.transform, "PlaySelectedLvlBtn", "PLAY SELECTED TRACK", 32, new Vector2(0f, -320f), new Vector2(360f, 80f), new Color(0.12f, 0.85f, 0.35f), defaultFont);
            Button backToMenuBtn = CreateUIButton(levelPanel.transform, "BackToMenuBtn", "BACK", 26, new Vector2(0f, -410f), new Vector2(220f, 55f), new Color(0.4f, 0.45f, 0.55f), defaultFont);
            levelPanel.SetActive(false);

            // --- 2.5 Costume Selection Panel ---
            GameObject costumePanel = CreateUIPanel(canvasObj.transform, "CostumeSelectPanel", new Color(0.04f, 0.08f, 0.16f, 0.95f));
            CreateUIText(costumePanel.transform, "CostumeTitle", "RACER COSTUMES & SKINS", 58, FontStyle.Bold, Color.yellow, new Vector2(0f, 360f), new Vector2(850f, 90f), defaultFont);
            Text costumeBankText = CreateUIText(costumePanel.transform, "CostumeBankText", "BANK: 25 COINS", 30, FontStyle.Bold, new Color(0.2f, 1f, 0.6f), new Vector2(0f, 290f), new Vector2(600f, 50f), defaultFont);

            Button[] costumeBtns = new Button[GameManager.Costumes.Length];
            Text[] costumeBtnTexts = new Text[GameManager.Costumes.Length];

            float startX = -480f;
            float spacingX = 320f;
            for (int i = 0; i < GameManager.Costumes.Length; i++)
            {
                var def = GameManager.Costumes[i];
                Vector2 cardPos = new Vector2(startX + i * spacingX, 30f);

                GameObject cardObj = new GameObject($"CostumeCard_{i}");
                cardObj.transform.parent = costumePanel.transform;
                RectTransform rt = cardObj.AddComponent<RectTransform>();
                rt.anchoredPosition = cardPos;
                rt.sizeDelta = new Vector2(290f, 420f);

                Image bg = cardObj.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.14f, 0.22f, 0.92f);

                GameObject swatchObj = new GameObject("Swatch");
                swatchObj.transform.parent = cardObj.transform;
                RectTransform swatchRt = swatchObj.AddComponent<RectTransform>();
                swatchRt.anchoredPosition = new Vector2(0f, 95f);
                swatchRt.sizeDelta = new Vector2(140f, 140f);
                Image swatchImg = swatchObj.AddComponent<Image>();
                swatchImg.color = def.bodyColor;

                GameObject swatchAccent = new GameObject("Accent");
                swatchAccent.transform.parent = swatchObj.transform;
                RectTransform accentRt = swatchAccent.AddComponent<RectTransform>();
                accentRt.anchoredPosition = Vector2.zero;
                accentRt.sizeDelta = new Vector2(60f, 60f);
                Image accentImg = swatchAccent.AddComponent<Image>();
                accentImg.color = def.accentColor;

                CreateUIText(cardObj.transform, "Name", def.name, 22, FontStyle.Bold, Color.white, new Vector2(0f, -10f), new Vector2(270f, 40f), defaultFont);

                Button btn = CreateUIButton(cardObj.transform, "ActionBtn", "EQUIP", 20, new Vector2(0f, -120f), new Vector2(240f, 65f), new Color(0.15f, 0.6f, 0.9f), defaultFont);
                Text btnTxt = btn.GetComponentInChildren<Text>();

                costumeBtns[i] = btn;
                costumeBtnTexts[i] = btnTxt;
            }

            Button closeCostumesBtn = CreateUIButton(costumePanel.transform, "CloseCostumesBtn", "BACK", 26, new Vector2(0f, -380f), new Vector2(220f, 55f), new Color(0.4f, 0.45f, 0.55f), defaultFont);
            costumePanel.SetActive(false);

            // --- 3. Countdown Panel ---
            GameObject countPanel = CreateUIPanel(canvasObj.transform, "CountdownPanel", Color.clear);
            Text countText = CreateUIText(countPanel.transform, "CountdownText", "3", 140, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(500f, 250f), defaultFont);
            countPanel.SetActive(false);

            // --- 4. HUD Panel ---
            GameObject hudPanel = CreateUIPanel(canvasObj.transform, "HUDPanel", Color.clear);

            Text rankText = CreateUIText(hudPanel.transform, "RankText", "1ST / 4", 42, FontStyle.Bold, new Color(1f, 0.85f, 0.1f), new Vector2(-720f, 470f), new Vector2(320f, 80f), defaultFont, TextAnchor.MiddleLeft);
            Text coinText = CreateUIText(hudPanel.transform, "CoinText", "COINS: 0", 32, FontStyle.Bold, Color.yellow, new Vector2(-420f, 470f), new Vector2(250f, 80f), defaultFont, TextAnchor.MiddleLeft);
            Text timerText = CreateUIText(hudPanel.transform, "TimerText", "TIME: 0.0s", 32, FontStyle.Bold, Color.white, new Vector2(0f, 470f), new Vector2(300f, 80f), defaultFont, TextAnchor.MiddleCenter);
            Text scoreText = CreateUIText(hudPanel.transform, "ScoreText", "SCORE: 0", 32, FontStyle.Bold, new Color(0.3f, 1f, 0.5f), new Vector2(450f, 470f), new Vector2(250f, 80f), defaultFont, TextAnchor.MiddleRight);
            Button pauseBtn = CreateUIButton(hudPanel.transform, "PauseButton", "II", 28, new Vector2(850f, 470f), new Vector2(70f, 65f), new Color(0.3f, 0.4f, 0.5f, 0.8f), defaultFont);

            Text trackNameHud = CreateUIText(hudPanel.transform, "TrackNameHud", "LEVEL 1 — LAGOON SPRINT", 22, FontStyle.Bold, new Color(0.7f, 0.9f, 1f, 0.85f), new Vector2(0f, 435f), new Vector2(500f, 35f), defaultFont);
            Slider progressSlider = CreateProgressBar(hudPanel.transform, new Vector2(0f, 395f), new Vector2(620f, 22f), defaultFont);
            hudPanel.SetActive(false);

            // --- 5. Pause Panel ---
            GameObject pausePanel = CreateUIPanel(canvasObj.transform, "PausePanel", new Color(0f, 0f, 0f, 0.82f));
            CreateUIText(pausePanel.transform, "PauseTitle", "GAME PAUSED", 56, FontStyle.Bold, Color.white, new Vector2(0f, 140f), new Vector2(600f, 90f), defaultFont);
            Button resumeBtn = CreateUIButton(pausePanel.transform, "ResumeButton", "RESUME", 30, new Vector2(0f, 40f), new Vector2(280f, 65f), new Color(0.2f, 0.75f, 0.3f), defaultFont);
            Button restartFromPauseBtn = CreateUIButton(pausePanel.transform, "RestartPauseBtn", "RESTART TRACK", 30, new Vector2(0f, -45f), new Vector2(280f, 65f), new Color(0.85f, 0.3f, 0.2f), defaultFont);
            Button changeLvlPauseBtn = CreateUIButton(pausePanel.transform, "ChangeLvlPauseBtn", "CHANGE TRACK", 28, new Vector2(0f, -130f), new Vector2(280f, 65f), new Color(0.15f, 0.65f, 0.95f), defaultFont);
            pausePanel.SetActive(false);

            // --- 6. Result Screen ---
            GameObject resultPanel = CreateUIPanel(canvasObj.transform, "ResultPanel", new Color(0.04f, 0.08f, 0.15f, 0.94f));
            Text resTitle = CreateUIText(resultPanel.transform, "ResTitle", "VICTORY!", 72, FontStyle.Bold, Color.yellow, new Vector2(0f, 230f), new Vector2(700f, 110f), defaultFont);
            Text resRank = CreateUIText(resultPanel.transform, "ResRank", "1st Place (Winner!)", 40, FontStyle.Bold, Color.white, new Vector2(0f, 130f), new Vector2(600f, 70f), defaultFont);
            Text resTime = CreateUIText(resultPanel.transform, "ResTime", "Time: 45.20 seconds", 30, FontStyle.Normal, Color.white, new Vector2(0f, 60f), new Vector2(600f, 50f), defaultFont);
            Text resCoins = CreateUIText(resultPanel.transform, "ResCoins", "Coins Collected: 15", 30, FontStyle.Normal, Color.yellow, new Vector2(0f, 10f), new Vector2(600f, 50f), defaultFont);
            Text resScore = CreateUIText(resultPanel.transform, "ResScore", "Final Score: 850", 32, FontStyle.Bold, new Color(0.3f, 1f, 0.4f), new Vector2(0f, -40f), new Vector2(600f, 50f), defaultFont);

            Button playAgainBtn = CreateUIButton(resultPanel.transform, "PlayAgainBtn", "PLAY AGAIN", 30, new Vector2(-170f, -135f), new Vector2(240f, 70f), new Color(0.12f, 0.85f, 0.35f), defaultFont);
            Button nextLevelBtn = CreateUIButton(resultPanel.transform, "NextLevelBtn", "NEXT TRACK", 30, new Vector2(170f, -135f), new Vector2(240f, 70f), new Color(0.15f, 0.65f, 0.95f), defaultFont);
            Button mainMenuBtn = CreateUIButton(resultPanel.transform, "MainMenuBtn", "MAIN MENU", 26, new Vector2(0f, -225f), new Vector2(260f, 60f), new Color(0.35f, 0.45f, 0.55f), defaultFont);
            resultPanel.SetActive(false);

            // Bind to UIManager via reflection
            UIManager uiMgr = canvasObj.AddComponent<UIManager>();

            SetPrivateField(uiMgr, "startMenuPanel", startPanel);
            SetPrivateField(uiMgr, "levelSelectPanel", levelPanel);
            SetPrivateField(uiMgr, "countdownPanel", countPanel);
            SetPrivateField(uiMgr, "hudPanel", hudPanel);
            SetPrivateField(uiMgr, "pausePanel", pausePanel);
            SetPrivateField(uiMgr, "resultPanel", resultPanel);

            SetPrivateField(uiMgr, "playButton", playBtn);
            SetPrivateField(uiMgr, "openLevelSelectButton", openLevelSelectBtn);
            SetPrivateField(uiMgr, "openCostumesButton", openCostumesBtn);
            SetPrivateField(uiMgr, "currentLevelInfoText", currentLvlInfo);

            SetPrivateField(uiMgr, "costumePanel", costumePanel);
            SetPrivateField(uiMgr, "closeCostumesButton", closeCostumesBtn);
            SetPrivateField(uiMgr, "costumeCoinBankText", costumeBankText);
            SetPrivateField(uiMgr, "costumeButtons", costumeBtns);
            SetPrivateField(uiMgr, "costumeButtonTexts", costumeBtnTexts);

            SetPrivateField(uiMgr, "level1CardButton", card1Btn);
            SetPrivateField(uiMgr, "level2CardButton", card2Btn);
            SetPrivateField(uiMgr, "level3CardButton", card3Btn);
            SetPrivateField(uiMgr, "level1Highlight", card1Hl);
            SetPrivateField(uiMgr, "level2Highlight", card2Hl);
            SetPrivateField(uiMgr, "level3Highlight", card3Hl);
            SetPrivateField(uiMgr, "playSelectedLevelButton", playSelectedLvlBtn);
            SetPrivateField(uiMgr, "backToStartMenuButton", backToMenuBtn);

            SetPrivateField(uiMgr, "countdownText", countText);
            SetPrivateField(uiMgr, "rankText", rankText);
            SetPrivateField(uiMgr, "coinText", coinText);
            SetPrivateField(uiMgr, "scoreText", scoreText);
            SetPrivateField(uiMgr, "timerText", timerText);
            SetPrivateField(uiMgr, "hudTrackNameText", trackNameHud);
            SetPrivateField(uiMgr, "progressSlider", progressSlider);
            SetPrivateField(uiMgr, "pauseButton", pauseBtn);

            SetPrivateField(uiMgr, "resumeButton", resumeBtn);
            SetPrivateField(uiMgr, "restartFromPauseButton", restartFromPauseBtn);
            SetPrivateField(uiMgr, "changeLevelFromPauseButton", changeLvlPauseBtn);

            SetPrivateField(uiMgr, "resultTitleText", resTitle);
            SetPrivateField(uiMgr, "resultRankText", resRank);
            SetPrivateField(uiMgr, "resultTimeText", resTime);
            SetPrivateField(uiMgr, "resultCoinsText", resCoins);
            SetPrivateField(uiMgr, "resultScoreText", resScore);
            SetPrivateField(uiMgr, "playAgainButton", playAgainBtn);
            SetPrivateField(uiMgr, "nextLevelButton", nextLevelBtn);
            SetPrivateField(uiMgr, "mainMenuButton", mainMenuBtn);

            return uiMgr;
        }

        private static Button CreateLevelCard(Transform parent, string name, Vector2 pos, string tag, string title,
            string diff, string desc, string stats, Color accentColor, Font font, out Image highlightImg)
        {
            GameObject cardObj = new GameObject(name);
            cardObj.transform.parent = parent;
            RectTransform rt = cardObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(410f, 490f);

            Image bg = cardObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.14f, 0.24f, 0.95f);

            Button cardBtn = cardObj.AddComponent<Button>();
            cardBtn.targetGraphic = bg;
            ColorBlock cb = cardBtn.colors;
            cb.highlightedColor = new Color(0.14f, 0.22f, 0.35f, 1f);
            cb.pressedColor = new Color(0.05f, 0.08f, 0.15f, 1f);
            cardBtn.colors = cb;

            // Selection border highlight
            GameObject hlObj = new GameObject("Highlight");
            hlObj.transform.parent = cardObj.transform;
            RectTransform hlRt = hlObj.AddComponent<RectTransform>();
            hlRt.anchorMin = Vector2.zero;
            hlRt.anchorMax = Vector2.one;
            hlRt.offsetMin = new Vector2(-5f, -5f);
            hlRt.offsetMax = new Vector2(5f, 5f);
            highlightImg = hlObj.AddComponent<Image>();
            highlightImg.color = new Color(1f, 0.85f, 0.1f, 1f); // Vibrant gold border
            highlightImg.enabled = false;
            highlightImg.raycastTarget = false;

            // Header Banner
            GameObject bannerObj = new GameObject("Banner");
            bannerObj.transform.parent = cardObj.transform;
            RectTransform bannerRt = bannerObj.AddComponent<RectTransform>();
            bannerRt.anchoredPosition = new Vector2(0f, 195f);
            bannerRt.sizeDelta = new Vector2(400f, 85f);
            Image bannerImg = bannerObj.AddComponent<Image>();
            bannerImg.color = accentColor;
            bannerImg.raycastTarget = false;

            CreateUIText(bannerObj.transform, "LvlTag", tag, 22, FontStyle.Bold, Color.white, new Vector2(0f, 16f), new Vector2(380f, 30f), font);
            CreateUIText(bannerObj.transform, "LvlTitle", title, 28, FontStyle.Bold, Color.yellow, new Vector2(0f, -14f), new Vector2(380f, 35f), font);

            // Details
            CreateUIText(cardObj.transform, "DiffText", diff, 21, FontStyle.Bold, Color.white, new Vector2(0f, 105f), new Vector2(380f, 40f), font);
            CreateUIText(cardObj.transform, "DescText", desc, 19, FontStyle.Normal, new Color(0.85f, 0.92f, 1f), new Vector2(0f, 25f), new Vector2(370f, 110f), font);
            CreateUIText(cardObj.transform, "StatsText", stats, 19, FontStyle.Bold, new Color(0.3f, 1f, 0.6f), new Vector2(0f, -55f), new Vector2(380f, 35f), font);

            // Select button
            Button childBtn = CreateUIButton(cardObj.transform, "SelectBtn", "SELECT TRACK", 24, new Vector2(0f, -170f), new Vector2(260f, 60f), accentColor, font);
            childBtn.onClick.AddListener(() => cardBtn.onClick.Invoke());

            return cardBtn;
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.parent = parent;
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (bgColor.a > 0.001f)
            {
                Image img = panel.AddComponent<Image>();
                img.color = bgColor;
            }
            return panel;
        }

        private static Text CreateUIText(Transform parent, string name, string text, int fontSize, FontStyle style, Color color,
            Vector2 anchoredPos, Vector2 size, Font font, TextAnchor align = TextAnchor.MiddleCenter)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.parent = parent;
            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;

            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return txt;
        }

        private static Button CreateUIButton(Transform parent, string name, string text, int fontSize, Vector2 anchoredPos, Vector2 size, Color btnColor, Font font)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.parent = parent;
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = btnColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = btnColor * 1.15f;
            cb.pressedColor = btnColor * 0.85f;
            btn.colors = cb;

            CreateUIText(btnObj.transform, "BtnText", text, fontSize, FontStyle.Bold, Color.white, Vector2.zero, size, font);
            return btn;
        }

        private static Slider CreateProgressBar(Transform parent, Vector2 anchoredPos, Vector2 size, Font font)
        {
            GameObject sliderObj = new GameObject("ProgressSlider");
            sliderObj.transform.parent = parent;
            RectTransform rt = sliderObj.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image bg = sliderObj.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.2f, 0.3f, 0.75f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.parent = sliderObj.transform;
            RectTransform fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(3f, 3f);
            fillAreaRt.offsetMax = new Vector2(-3f, -3f);

            GameObject fill = new GameObject("Fill");
            fill.transform.parent = fillArea.transform;
            RectTransform fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.15f, 0.85f, 0.4f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;

            return slider;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
