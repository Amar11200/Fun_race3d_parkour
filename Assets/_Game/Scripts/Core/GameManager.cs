using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DashMasters
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Level Configuration")]
        [SerializeField] private int currentLevelIndex = 1;
        [SerializeField] private float trackLength = 280f;
        [SerializeField] private Vector3 playerSpawnPos = new Vector3(-0.35f, 0.4f, 0f);
        [SerializeField] private Vector3[] aiSpawnPositions = new Vector3[] {
            new Vector3(-1.1f, 0.4f, 0f),
            new Vector3(1.1f, 0.4f, 0f),
            new Vector3(0.35f, 0.4f, 0f)
        };

        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private List<AIRacerController> aiRacers = new List<AIRacerController>();
        [SerializeField] private FinishLine finishLine;
        [SerializeField] private Checkpoint[] checkpoints;

        public PlayerController Player => player;
        public List<AIRacerController> AIRacers => aiRacers;

        // Runtime race state
        public GameState CurrentState { get; private set; } = GameState.StartMenu;
        public int CurrentLevelIndex { get => currentLevelIndex; private set => currentLevelIndex = value; }
        public float RaceTime { get; private set; }
        public int CoinsCollected { get; private set; }
        public int Score { get; private set; }
        public int PlayerRank { get; private set; } = 1;
        public float TrackLength => trackLength;
        public float PlayerProgress => (player != null && trackLength > 0.01f) ? Mathf.Clamp01(player.CurrentTrackDistance / trackLength) : 0f;

        // Persistent Bank & Costumes
        public static readonly CostumeDefinition[] Costumes = new CostumeDefinition[]
        {
            new CostumeDefinition(0, "Lagoon Teal", 0, new Color(0.0f, 0.85f, 0.95f), new Color(1f, 1f, 1f)),
            new CostumeDefinition(1, "Golden Champion", 15, new Color(1.0f, 0.82f, 0.12f), new Color(1.0f, 0.55f, 0.05f)),
            new CostumeDefinition(2, "Crimson Blaze", 30, new Color(0.95f, 0.15f, 0.22f), new Color(0.18f, 0.18f, 0.22f)),
            new CostumeDefinition(3, "Shadow Phantom", 50, new Color(0.12f, 0.12f, 0.16f), new Color(0.72f, 0.15f, 0.95f))
        };

        public int TotalCoinsBank { get; private set; }
        public int SelectedCostumeId { get; private set; } = 0;

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnCountdownTick;
        public event Action<int> OnCoinsChanged;
        public event Action<int> OnTotalCoinsChanged;
        public event Action<int> OnScoreChanged;
        public event Action<int> OnRankChanged;
        public event Action<int> OnCostumeChanged;
        public event Action<int, float, int, int> OnRaceCompleted; // rank, time, coins, score

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            TotalCoinsBank = PlayerPrefs.GetInt("DashMasters_TotalCoins", 25);
            SelectedCostumeId = PlayerPrefs.GetInt("DashMasters_SelectedCostume", 0);

            DashMastersRuntimeLevelBuilder.PurgeUnwantedPlatforms();
        }

        private void Start()
        {
            DashMastersRuntimeLevelBuilder.PurgeUnwantedPlatforms();
            ApplyCurrentCostumeToPlayer();
            SetState(GameState.StartMenu);
        }

        public bool IsCostumeUnlocked(int id)
        {
            if (id == 0) return true;
            return PlayerPrefs.GetInt($"DashMasters_CostumeUnlocked_{id}", 0) == 1;
        }

        public bool TryUnlockCostume(int id)
        {
            if (id < 0 || id >= Costumes.Length) return false;
            if (IsCostumeUnlocked(id)) return true;

            int cost = Costumes[id].cost;
            if (TotalCoinsBank >= cost)
            {
                TotalCoinsBank -= cost;
                PlayerPrefs.SetInt("DashMasters_TotalCoins", TotalCoinsBank);
                PlayerPrefs.SetInt($"DashMasters_CostumeUnlocked_{id}", 1);
                PlayerPrefs.Save();
                OnTotalCoinsChanged?.Invoke(TotalCoinsBank);
                SelectCostume(id);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayCoinSound();
                return true;
            }
            return false;
        }

        public void SelectCostume(int id)
        {
            if (id < 0 || id >= Costumes.Length) return;
            if (!IsCostumeUnlocked(id)) return;

            SelectedCostumeId = id;
            PlayerPrefs.SetInt("DashMasters_SelectedCostume", id);
            PlayerPrefs.Save();
            ApplyCurrentCostumeToPlayer();
            OnCostumeChanged?.Invoke(id);
        }

        public void ApplyCurrentCostumeToPlayer()
        {
            if (player == null) player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;

            CostumeDefinition c = Costumes[Mathf.Clamp(SelectedCostumeId, 0, Costumes.Length - 1)];
            Material bodyMat = DashMastersRuntimeLevelBuilder.CreateMat(c.bodyColor, 0.65f);
            Material accentMat = DashMastersRuntimeLevelBuilder.CreateMat(c.accentColor, 0.75f);
            player.SetCostumeMaterials(bodyMat, accentMat);
        }

        private void Update()
        {
            // Pause toggle
            if (CheckPauseInput())
            {
                if (CurrentState == GameState.Racing)
                {
                    PauseGame();
                }
                else if (CurrentState == GameState.Paused)
                {
                    ResumeGame();
                }
            }

            // Direct keyboard shortcuts to switch track (1, 2, 3) from menu, level select, or pause
            if (CurrentState == GameState.StartMenu || CurrentState == GameState.LevelSelect || CurrentState == GameState.Paused)
            {
                CheckQuickTrackSelectInput();
            }

            if (CurrentState == GameState.StartMenu)
            {
                if (CheckStartInput())
                {
                    StartCountdown();
                }
            }
            else if (CurrentState == GameState.Racing)
            {
                RaceTime += Time.deltaTime;
                CalculateRanks();
            }
        }

        private void CheckQuickTrackSelectInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                {
                    SelectAndLoadLevel(1);
                    return;
                }
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                {
                    SelectAndLoadLevel(2);
                    return;
                }
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                {
                    SelectAndLoadLevel(3);
                    return;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { SelectAndLoadLevel(1); return; }
                if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { SelectAndLoadLevel(2); return; }
                if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { SelectAndLoadLevel(3); return; }
            }
            catch {}
#endif
        }

        private bool CheckPauseInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) return true;
            }
            catch {}
#endif
            return false;
        }

        private bool CheckStartInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) return true;
            }
            catch {}
#endif
            return false;
        }

        public void SetState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        public void StartCountdown()
        {
            if (CurrentState != GameState.StartMenu && CurrentState != GameState.Finished && CurrentState != GameState.LevelSelect) return;
            StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            SetState(GameState.Countdown);

            for (int i = 3; i >= 1; i--)
            {
                OnCountdownTick?.Invoke(i);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayCountdownTick();
                yield return new WaitForSeconds(1.0f);
            }

            OnCountdownTick?.Invoke(0); // 0 means "GO!"
            if (AudioManager.Instance != null) AudioManager.Instance.PlayCountdownGo();
            yield return new WaitForSeconds(0.6f);

            SetState(GameState.Racing);
        }

        private void CalculateRanks()
        {
            if (player == null) return;

            int rank = 1;
            float playerDist = player.CurrentTrackDistance;

            foreach (var ai in aiRacers)
            {
                if (ai != null && ai.CurrentTrackDistance > playerDist)
                {
                    rank++;
                }
            }

            // Total 4 racers: 1 to 4
            rank = Mathf.Clamp(rank, 1, 4);

            if (rank != PlayerRank)
            {
                PlayerRank = rank;
                OnRankChanged?.Invoke(PlayerRank);
            }
        }

        public void AddCoins(int amount)
        {
            CoinsCollected += amount;
            TotalCoinsBank += amount;
            PlayerPrefs.SetInt("DashMasters_TotalCoins", TotalCoinsBank);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(CoinsCollected);
            OnTotalCoinsChanged?.Invoke(TotalCoinsBank);
        }

        public void AddScore(int amount)
        {
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }

        public void OnPlayerFinished(int finalRank)
        {
            if (CurrentState != GameState.Racing) return;

            PlayerRank = Mathf.Clamp(finalRank, 1, 4);
            SetState(GameState.Finished);

            // Add finish bonus based on placement
            int bonus = (PlayerRank == 1) ? 600 : (PlayerRank == 2 ? 350 : (PlayerRank == 3 ? 150 : 50));
            AddScore(bonus);

            if (PlayerRank == 1)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayVictorySound();
            }
            else
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDefeatSound();
            }

            StartCoroutine(PodiumCeremonyRoutine(PlayerRank));
        }

        private IEnumerator PodiumCeremonyRoutine(int playerRank)
        {
            Vector3 podiumCenter = new Vector3(0f, 0.5f, trackLength);
            Vector3 trackForward = Vector3.forward;
            Vector3 trackRight = Vector3.right;

            if (finishLine != null)
            {
                podiumCenter = finishLine.transform.position + finishLine.transform.forward * 8.5f;
                trackForward = finishLine.transform.forward;
                trackRight = finishLine.transform.right;
            }
            else if (TrackPath.Instance != null)
            {
                PathPoint pt = TrackPath.Instance.EvaluateDistance(trackLength);
                podiumCenter = pt.position + pt.forward * 8.5f;
                trackForward = pt.forward;
                trackRight = pt.right;
            }

            Vector3 step1 = podiumCenter + Vector3.up * 1.45f;                         // Gold (Center)
            Vector3 step2 = podiumCenter - (trackRight * 2.2f) + Vector3.up * 0.95f;   // Silver (Left)
            Vector3 step3 = podiumCenter + (trackRight * 2.2f) + Vector3.up * 0.65f;   // Bronze (Right)
            Vector3 step4 = podiumCenter + (trackRight * 4.0f) + Vector3.up * 0.25f;   // 4th Place (Floor)
            Quaternion faceForward = Quaternion.LookRotation(-trackForward, Vector3.up);

            // Assign racers to podium steps based on playerRank
            if (player != null)
            {
                Vector3 pPos = (playerRank == 1) ? step1 : (playerRank == 2 ? step2 : (playerRank == 3 ? step3 : step4));
                player.PlaceOnPodium(pPos, faceForward);
            }

            if (aiRacers.Count >= 3)
            {
                if (playerRank == 1)
                {
                    if (aiRacers[0] != null) aiRacers[0].PlaceOnPodium(step2, faceForward);
                    if (aiRacers[1] != null) aiRacers[1].PlaceOnPodium(step3, faceForward);
                    if (aiRacers[2] != null) aiRacers[2].PlaceOnPodium(step4, faceForward);
                }
                else if (playerRank == 2)
                {
                    if (aiRacers[0] != null) aiRacers[0].PlaceOnPodium(step1, faceForward);
                    if (aiRacers[1] != null) aiRacers[1].PlaceOnPodium(step3, faceForward);
                    if (aiRacers[2] != null) aiRacers[2].PlaceOnPodium(step4, faceForward);
                }
                else if (playerRank == 3)
                {
                    if (aiRacers[0] != null) aiRacers[0].PlaceOnPodium(step1, faceForward);
                    if (aiRacers[1] != null) aiRacers[1].PlaceOnPodium(step2, faceForward);
                    if (aiRacers[2] != null) aiRacers[2].PlaceOnPodium(step4, faceForward);
                }
                else
                {
                    if (aiRacers[0] != null) aiRacers[0].PlaceOnPodium(step1, faceForward);
                    if (aiRacers[1] != null) aiRacers[1].PlaceOnPodium(step2, faceForward);
                    if (aiRacers[2] != null) aiRacers[2].PlaceOnPodium(step3, faceForward);
                }
            }

            // Cinematic camera focus on podium
            if (ThirdPersonCamera.Instance != null)
            {
                ThirdPersonCamera.Instance.FocusOnPodium(podiumCenter, trackForward);
            }

            // Celebration confetti cannons
            if (ProceduralFX.Instance != null)
            {
                ProceduralFX.Instance.SpawnConfetti(podiumCenter + Vector3.up * 2f + Vector3.left * 2f);
                ProceduralFX.Instance.SpawnConfetti(podiumCenter + Vector3.up * 2f + Vector3.right * 2f);
                yield return new WaitForSeconds(0.4f);
                ProceduralFX.Instance.SpawnConfetti(podiumCenter + Vector3.up * 3f);
            }

            yield return new WaitForSeconds(1.0f);

            OnRaceCompleted?.Invoke(playerRank, RaceTime, CoinsCollected, Score);
        }

        public void OnAIFinished(string aiName, int rank)
        {
            // AI crossed finish line
        }

        public void PauseGame()
        {
            if (CurrentState != GameState.Racing) return;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (CurrentState != GameState.Paused) return;
            Time.timeScale = 1f;
            SetState(GameState.Racing);
        }

        public void RestartRace()
        {
            Time.timeScale = 1f;
            StopAllCoroutines();

            if (ThirdPersonCamera.Instance != null && player != null)
            {
                ThirdPersonCamera.Instance.ResetCamera(player.transform);
            }

            RaceTime = 0f;
            CoinsCollected = 0;
            Score = 0;
            PlayerRank = 1;

            OnCoinsChanged?.Invoke(0);
            OnScoreChanged?.Invoke(0);
            OnRankChanged?.Invoke(1);

            // Reset player
            if (player != null)
            {
                player.ResetRacer(playerSpawnPos);
            }

            // Reset all 3 AI racers
            for (int i = 0; i < aiRacers.Count; i++)
            {
                if (aiRacers[i] != null)
                {
                    Vector3 spawn = (i < aiSpawnPositions.Length) ? aiSpawnPositions[i] : (playerSpawnPos + Vector3.right * (i + 1));
                    aiRacers[i].ResetRacer(spawn);
                }
            }

            // Reset coins
            Coin[] allCoins = FindObjectsByType<Coin>();
            foreach (var coin in allCoins)
            {
                coin.ResetCoin();
            }

            // Reset finish line
            if (finishLine != null)
            {
                finishLine.ResetFinishLine();
            }

            // Reset checkpoints
            if (checkpoints != null)
            {
                foreach (var cp in checkpoints)
                {
                    if (cp != null) cp.ResetCheckpoint();
                }
            }

            StartCountdown();
        }

        public void ReturnToStartMenu()
        {
            Time.timeScale = 1f;
            StopAllCoroutines();

            if (ThirdPersonCamera.Instance != null && player != null)
            {
                ThirdPersonCamera.Instance.ResetCamera(player.transform);
            }

            RaceTime = 0f;
            CoinsCollected = 0;
            Score = 0;
            PlayerRank = 1;

            if (player != null) player.ResetRacer(playerSpawnPos);
            for (int i = 0; i < aiRacers.Count; i++)
            {
                if (aiRacers[i] != null)
                {
                    Vector3 spawn = (i < aiSpawnPositions.Length) ? aiSpawnPositions[i] : (playerSpawnPos + Vector3.right * (i + 1));
                    aiRacers[i].ResetRacer(spawn);
                }
            }

            Coin[] allCoins = FindObjectsByType<Coin>();
            foreach (var coin in allCoins) coin.ResetCoin();
            if (finishLine != null) finishLine.ResetFinishLine();

            SetState(GameState.StartMenu);
        }

        public void OpenLevelSelect()
        {
            Time.timeScale = 1f;
            SetState(GameState.LevelSelect);
        }

        public void CloseLevelSelect()
        {
            Time.timeScale = 1f;
            SetState(GameState.StartMenu);
        }

        public void SelectAndLoadLevel(int levelIndex)
        {
            Time.timeScale = 1f;
            currentLevelIndex = Mathf.Clamp(levelIndex, 1, 3);
            DashMastersRuntimeLevelBuilder.BuildLevel(currentLevelIndex);
            ApplyCurrentCostumeToPlayer();
            ReturnToStartMenu();
        }

        public void Configure(PlayerController p, List<AIRacerController> ais, FinishLine fl, Checkpoint[] cps, float length, Vector3 pSpawn, Vector3[] aiSpawns, int levelIdx = 1)
        {
            player = p;
            aiRacers = ais;
            finishLine = fl;
            checkpoints = cps;
            trackLength = length;
            playerSpawnPos = pSpawn;
            aiSpawnPositions = aiSpawns;
            currentLevelIndex = levelIdx;
        }
    }
}
