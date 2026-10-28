using UnityEngine;
using UnityEngine.UI;

namespace DashMasters
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject startMenuPanel;
        [SerializeField] private GameObject levelSelectPanel;
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject resultPanel;

        [Header("Start Menu Elements")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button openLevelSelectButton;
        [SerializeField] private Button openCostumesButton;
        [SerializeField] private Text currentLevelInfoText;

        [Header("Costume Selection Elements")]
        [SerializeField] private GameObject costumePanel;
        [SerializeField] private Button closeCostumesButton;
        [SerializeField] private Text costumeCoinBankText;
        [SerializeField] private Button[] costumeButtons;
        [SerializeField] private Text[] costumeButtonTexts;

        [Header("Level Select Elements")]
        [SerializeField] private Button level1CardButton;
        [SerializeField] private Button level2CardButton;
        [SerializeField] private Button level3CardButton;
        [SerializeField] private Image level1Highlight;
        [SerializeField] private Image level2Highlight;
        [SerializeField] private Image level3Highlight;
        [SerializeField] private Button playSelectedLevelButton;
        [SerializeField] private Button backToStartMenuButton;

        [Header("Countdown Elements")]
        [SerializeField] private Text countdownText;

        [Header("HUD Elements")]
        [SerializeField] private Text rankText;
        [SerializeField] private Text coinText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text hudTrackNameText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Button pauseButton;

        [Header("Pause Menu Elements")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartFromPauseButton;
        [SerializeField] private Button changeLevelFromPauseButton;

        [Header("Result Screen Elements")]
        [SerializeField] private Text resultTitleText;
        [SerializeField] private Text resultRankText;
        [SerializeField] private Text resultTimeText;
        [SerializeField] private Text resultCoinsText;
        [SerializeField] private Text resultScoreText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button mainMenuButton;

        private int pendingSelectedLevel = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureEventSystemModule();
        }

        private void EnsureEventSystemModule()
        {
            var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
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
        }

        private void Start()
        {
            // Bind button listeners
            if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
            if (openLevelSelectButton != null) openLevelSelectButton.onClick.AddListener(OnOpenLevelSelectClicked);
            if (openCostumesButton != null) openCostumesButton.onClick.AddListener(OpenCostumes);
            if (closeCostumesButton != null) closeCostumesButton.onClick.AddListener(CloseCostumes);

            if (costumeButtons != null)
            {
                for (int i = 0; i < costumeButtons.Length; i++)
                {
                    int costumeId = i;
                    if (costumeButtons[i] != null)
                    {
                        costumeButtons[i].onClick.AddListener(() => OnCostumeCardClicked(costumeId));
                    }
                }
            }

            if (level1CardButton != null) level1CardButton.onClick.AddListener(() => SelectLevelCard(1));
            if (level2CardButton != null) level2CardButton.onClick.AddListener(() => SelectLevelCard(2));
            if (level3CardButton != null) level3CardButton.onClick.AddListener(() => SelectLevelCard(3));
            if (playSelectedLevelButton != null) playSelectedLevelButton.onClick.AddListener(OnPlaySelectedLevelClicked);
            if (backToStartMenuButton != null) backToStartMenuButton.onClick.AddListener(OnBackToStartMenuClicked);

            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (restartFromPauseButton != null) restartFromPauseButton.onClick.AddListener(OnRestartClicked);
            if (changeLevelFromPauseButton != null) changeLevelFromPauseButton.onClick.AddListener(OnOpenLevelSelectClicked);

            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnRestartClicked);
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);

            // Subscribe to GameManager events
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                GameManager.Instance.OnCountdownTick += HandleCountdownTick;
                GameManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                GameManager.Instance.OnTotalCoinsChanged += HandleTotalCoinsChanged;
                GameManager.Instance.OnScoreChanged += HandleScoreChanged;
                GameManager.Instance.OnRankChanged += HandleRankChanged;
                GameManager.Instance.OnCostumeChanged += HandleCostumeChanged;
                GameManager.Instance.OnRaceCompleted += HandleRaceCompleted;
                pendingSelectedLevel = GameManager.Instance.CurrentLevelIndex;
            }

            UpdateRankDisplay(1);
            UpdateCoinsDisplay(0);
            UpdateScoreDisplay(0);
            UpdateLevelDisplay(pendingSelectedLevel);
            UpdateCostumeDisplay();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                GameManager.Instance.OnCountdownTick -= HandleCountdownTick;
                GameManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
                GameManager.Instance.OnTotalCoinsChanged -= HandleTotalCoinsChanged;
                GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
                GameManager.Instance.OnRankChanged -= HandleRankChanged;
                GameManager.Instance.OnCostumeChanged -= HandleCostumeChanged;
                GameManager.Instance.OnRaceCompleted -= HandleRaceCompleted;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Racing)
            {
                if (timerText != null)
                {
                    timerText.text = $"TIME: {GameManager.Instance.RaceTime:F1}s";
                }

                if (progressSlider != null)
                {
                    progressSlider.value = GameManager.Instance.PlayerProgress;
                }
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (startMenuPanel != null) startMenuPanel.SetActive(state == GameState.StartMenu);
            if (levelSelectPanel != null) levelSelectPanel.SetActive(state == GameState.LevelSelect);
            if (countdownPanel != null) countdownPanel.SetActive(state == GameState.Countdown);
            if (hudPanel != null) hudPanel.SetActive(state == GameState.Racing);
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);
            if (resultPanel != null) resultPanel.SetActive(state == GameState.Finished);

            if (state == GameState.LevelSelect)
            {
                Time.timeScale = 1f;
                if (levelSelectPanel != null)
                {
                    levelSelectPanel.transform.SetAsLastSibling();
                }
                if (GameManager.Instance != null)
                {
                    SelectLevelCard(GameManager.Instance.CurrentLevelIndex);
                }
            }
            else if (state == GameState.StartMenu && GameManager.Instance != null)
            {
                Time.timeScale = 1f;
                UpdateLevelDisplay(GameManager.Instance.CurrentLevelIndex);
            }
        }

        private void HandleCountdownTick(int tick)
        {
            if (countdownText == null) return;

            if (tick > 0)
            {
                countdownText.text = tick.ToString();
                countdownText.color = Color.yellow;
            }
            else
            {
                countdownText.text = "GO!";
                countdownText.color = Color.green;
            }
        }

        private void HandleCoinsChanged(int coins) => UpdateCoinsDisplay(coins);
        private void HandleScoreChanged(int score) => UpdateScoreDisplay(score);
        private void HandleRankChanged(int rank) => UpdateRankDisplay(rank);

        private void UpdateRankDisplay(int rank)
        {
            if (rankText == null) return;
            string suffix = (rank == 1) ? "1ST" : (rank == 2 ? "2ND" : (rank == 3 ? "3RD" : "4TH"));
            rankText.text = $"{suffix} / 4";

            if (rank == 1) rankText.color = new Color(1f, 0.85f, 0.1f);      // Gold
            else if (rank == 2) rankText.color = new Color(0.85f, 0.88f, 0.95f);// Silver
            else if (rank == 3) rankText.color = new Color(0.9f, 0.55f, 0.2f); // Bronze
            else rankText.color = new Color(0.7f, 0.7f, 0.75f);
        }

        private void UpdateCoinsDisplay(int coins)
        {
            if (coinText != null) coinText.text = $"COINS: {coins}";
        }

        private void UpdateScoreDisplay(int score)
        {
            if (scoreText != null) scoreText.text = $"SCORE: {score}";
        }

        public void UpdateLevelDisplay(int levelIndex)
        {
            string trackName = GetTrackTitle(levelIndex);
            if (currentLevelInfoText != null)
            {
                currentLevelInfoText.text = $"SELECTED TRACK: {trackName}";
            }
            if (hudTrackNameText != null)
            {
                hudTrackNameText.text = trackName;
            }

            SelectLevelCard(levelIndex);
        }

        private string GetTrackTitle(int levelIndex)
        {
            switch (levelIndex)
            {
                case 1: return "LEVEL 1 — LAGOON SPRINT (BEGINNER)";
                case 2: return "LEVEL 2 — CORAL RAPIDS (INTERMEDIATE)";
                case 3: return "LEVEL 3 — TITAN'S TRENCH (EXPERT)";
                default: return $"LEVEL {levelIndex}";
            }
        }

        private void SelectLevelCard(int levelIdx)
        {
            pendingSelectedLevel = levelIdx;

            if (level1Highlight != null) level1Highlight.enabled = (levelIdx == 1);
            if (level2Highlight != null) level2Highlight.enabled = (levelIdx == 2);
            if (level3Highlight != null) level3Highlight.enabled = (levelIdx == 3);

            string title = GetTrackTitle(levelIdx);
            if (currentLevelInfoText != null)
            {
                currentLevelInfoText.text = $"SELECTED TRACK: {title}";
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCoinSound();
            }
        }

        private void HandleRaceCompleted(int rank, float time, int coins, int score)
        {
            if (resultTitleText != null)
            {
                if (rank == 1)
                {
                    resultTitleText.text = "VICTORY! - 1ST PLACE CHAMPION";
                    resultTitleText.color = new Color(1f, 0.85f, 0.1f);
                }
                else
                {
                    resultTitleText.text = "RACE FINISHED";
                    resultTitleText.color = Color.white;
                }
            }

            if (resultRankText != null)
            {
                string suffix = (rank == 1) ? "1st Place (Winner! Gold Trophy)" : (rank == 2 ? "2nd Place (Silver Medal)" : (rank == 3 ? "3rd Place (Bronze Medal)" : "4th Place (Participant)"));
                resultRankText.text = suffix;
            }

            int minutes = Mathf.FloorToInt(time / 60f);
            float seconds = time % 60f;
            string timeFormatted = $"{minutes:00}:{seconds:00.00}";
            if (resultTimeText != null) resultTimeText.text = $"Completion Time: {timeFormatted}";
            int totalBank = GameManager.Instance != null ? GameManager.Instance.TotalCoinsBank : coins;
            if (resultCoinsText != null) resultCoinsText.text = $"Coins Earned: +{coins}   |   Bank: {totalBank}";
            if (resultScoreText != null) resultScoreText.text = $"Final Score: {score:N0} PTS";
        }

        // Button handlers
        private void OnPlayClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.StartCountdown();
        }

        private void OnOpenLevelSelectClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.OpenLevelSelect();
        }

        private void OnPlaySelectedLevelClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SelectAndLoadLevel(pendingSelectedLevel);
                GameManager.Instance.StartCountdown();
            }
        }

        private void OnBackToStartMenuClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.CloseLevelSelect();
        }

        private void OnPauseClicked()
        {
            if (GameManager.Instance != null) GameManager.Instance.PauseGame();
        }

        private void OnResumeClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
        }

        private void OnRestartClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.RestartRace();
        }

        private void OnNextLevelClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
            {
                int nextLevel = (GameManager.Instance.CurrentLevelIndex % 3) + 1;
                GameManager.Instance.SelectAndLoadLevel(nextLevel);
                GameManager.Instance.StartCountdown();
            }
        }

        private void OnMainMenuClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.ReturnToStartMenu();
        }

        public void OpenCostumes()
        {
            if (costumePanel != null) costumePanel.SetActive(true);
            UpdateCostumeDisplay();
        }

        public void CloseCostumes()
        {
            if (costumePanel != null) costumePanel.SetActive(false);
        }

        private void HandleCostumeChanged(int costumeId)
        {
            UpdateCostumeDisplay();
        }

        private void HandleTotalCoinsChanged(int totalCoins)
        {
            UpdateCostumeDisplay();
        }

        public void UpdateCostumeDisplay()
        {
            if (GameManager.Instance == null) return;

            if (costumeCoinBankText != null)
            {
                costumeCoinBankText.text = $"BANK: {GameManager.Instance.TotalCoinsBank} COINS";
            }

            var costumes = GameManager.Costumes;
            if (costumes == null || costumeButtons == null) return;

            int selectedId = GameManager.Instance.SelectedCostumeId;

            for (int i = 0; i < costumeButtons.Length && i < costumes.Length; i++)
            {
                var def = costumes[i];
                bool isUnlocked = GameManager.Instance.IsCostumeUnlocked(def.id);
                bool isSelected = (selectedId == def.id);

                if (costumeButtonTexts != null && i < costumeButtonTexts.Length && costumeButtonTexts[i] != null)
                {
                    if (isSelected)
                    {
                        costumeButtonTexts[i].text = $"{def.name}\n[SELECTED]";
                        costumeButtonTexts[i].color = new Color(0.2f, 1f, 0.4f);
                    }
                    else if (isUnlocked)
                    {
                        costumeButtonTexts[i].text = $"{def.name}\n[EQUIP]";
                        costumeButtonTexts[i].color = Color.white;
                    }
                    else
                    {
                        costumeButtonTexts[i].text = $"{def.name}\n[UNLOCK: {def.cost} COINS]";
                        costumeButtonTexts[i].color = new Color(1f, 0.85f, 0.2f);
                    }
                }
            }
        }

        private void OnCostumeCardClicked(int costumeId)
        {
            if (GameManager.Instance == null) return;

            if (GameManager.Instance.IsCostumeUnlocked(costumeId))
            {
                GameManager.Instance.SelectCostume(costumeId);
            }
            else
            {
                GameManager.Instance.TryUnlockCostume(costumeId);
            }
            UpdateCostumeDisplay();
        }
    }
}
