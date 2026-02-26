using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class GameDirector : MonoBehaviour
{
    private const int DefaultFallbackLevelCount = 5;
    private const int TutorialLevelIndex = 1;
    private const int FirstMainLevelIndex = 2;
    private const string MaxUnlockedLevelPrefsKey = "GravityShift.MaxUnlockedLevel";
    private const string FirstRunHintsSeenPrefsKey = "GravityShift.FirstRunHintsSeen";
    private const string BestTimePrefsPrefix = "GravityShift.BestTime";

    public static GameDirector Instance { get; private set; }

    public static bool AllowGameplayInput =>
        Instance != null &&
        Instance.State == GameState.Playing &&
        (Instance.inGameSettingsUI == null || !Instance.inGameSettingsUI.IsOpen) &&
        Time.time >= Instance.gameplayInputLockedUntil;

    public GameState State { get; private set; } = GameState.Menu;
    public GameRunSettings CurrentSettings => currentSettings;
    public LevelRuntimeConfig CurrentConfig => currentConfig;
    public float MouseSensitivity => currentSettings.MouseSensitivity;
    public int MaxUnlockedLevel => maxUnlockedLevel;
    public int AvailableLevelCount => levelConfigDatabase != null ? levelConfigDatabase.LevelCount : DefaultFallbackLevelCount;

    public static LevelRuntimeConfig PreviewConfig(GameRunSettings settings)
    {
        LevelConfigDatabase database = Instance != null
            ? Instance.levelConfigDatabase
            : LevelConfigDatabase.LoadOrCreateRuntime();
        int levelCount = database != null ? database.LevelCount : DefaultFallbackLevelCount;
        GameRunSettings sanitized = SanitizeSettings(settings, levelCount);
        LevelRuntimeConfig config = BuildConfig(database, sanitized);
        return ValidateConfig(config, levelCount);
    }

    private GameRunSettings currentSettings;
    private LevelRuntimeConfig currentConfig;
    private LevelConfigDatabase levelConfigDatabase;

    private MainMenuUI menuUI;
    private RuntimeHUD hud;
    private InGameSettingsUI inGameSettingsUI;
    private LevelOneRuntimeBuilder levelBuilder;
    private GravitySystem gravity;
    private LevelProgress progress;
    private LevelTimer levelTimer;
    private GameFeedback feedback;

    private PlayerGravityMotor activePlayer;
    private GravityCameraFollow activeCamera;
    private PlayerVisualAvatar activeAvatar;

    private bool eventsBound;
    private LevelProgress boundProgress;
    private LevelTimer boundTimer;
    private float gameplayInputLockedUntil;
    private float difficultySwitchLockUntil;
    private int maxUnlockedLevel = 1;
    private Coroutine firstRunHintRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!SceneManager.GetActiveScene().isLoaded)
        {
            return;
        }

        if (FindFirstObjectByType<GameDirector>() != null)
        {
            return;
        }

        GameObject root = new GameObject("GameDirector");
        root.AddComponent<GameDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeDefaults();
        ResolveLevelConfigDatabase();
        LoadProgression();
        EnsureCoreObjects();
        BindEvents();
    }

    private void Start()
    {
        EnterMenu();
    }

    private void Update()
    {
        if (RuntimeInput.MenuPressedThisFrame())
        {
            if (State == GameState.Playing && inGameSettingsUI != null && inGameSettingsUI.IsOpen)
            {
                inGameSettingsUI.SetVisible(false);
                return;
            }

            if (State == GameState.Playing || State == GameState.Completed || State == GameState.Failed)
            {
                EnterMenu();
                return;
            }
        }

        if (State == GameState.Completed)
        {
            if (RuntimeInput.NextLevelPressedThisFrame())
            {
                StartNextLevel();
            }
            else if (RuntimeInput.RestartPressedThisFrame())
            {
                StartRun(currentSettings);
            }
        }
        else if (State == GameState.Failed)
        {
            if (RuntimeInput.RestartPressedThisFrame())
            {
                StartRun(currentSettings);
            }
        }

        if (State == GameState.Playing && RuntimeInput.SettingsPressedThisFrame())
        {
            ToggleInGameSettings();
        }
    }

    private void OnDestroy()
    {
        StopFirstRunHintRoutine();

        if (Instance == this)
        {
            Instance = null;
        }

        if (inGameSettingsUI != null)
        {
            inGameSettingsUI.OnDifficultyChanged -= HandleDifficultyChanged;
            inGameSettingsUI.OnMouseSensitivityChanged -= HandleMouseSensitivityChanged;
            inGameSettingsUI.OnCameraFovChanged -= HandleCameraFovChanged;
            inGameSettingsUI.OnCameraSmoothnessChanged -= HandleCameraSmoothnessChanged;
            inGameSettingsUI.OnCameraAutoRecenterChanged -= HandleCameraAutoRecenterChanged;
            inGameSettingsUI.OnMasterVolumeChanged -= HandleMasterVolumeChanged;
            inGameSettingsUI.OnFeedbackIntensityChanged -= HandleFeedbackIntensityChanged;
            inGameSettingsUI.OnCameraShakeChanged -= HandleCameraShakeChanged;
            inGameSettingsUI.OnMotionPresetChanged -= HandleMotionPresetChanged;
            inGameSettingsUI.OnMotionStyleChanged -= HandleMotionStyleChanged;
            inGameSettingsUI.OnVisibilityChanged -= HandleInGameSettingsVisibilityChanged;
        }

        UnbindEvents();
    }

    private void InitializeDefaults()
    {
        currentSettings = new GameRunSettings
        {
            LevelIndex = 1,
            Difficulty = GameDifficulty.Easy,
            Mode = GameMode.Adventure,
            VisualTheme = PlayerVisualTheme.Astronaut,
            ColorPalette = PlayerColorPalette.Neon,
            AtmospherePreset = MenuAtmospherePreset.Noon,
            FeedbackIntensity = 1f,
            CameraShakeEnabled = true,
            MouseSensitivity = 1.45f,
            CameraFov = 66f,
            CameraSmoothness = 0.56f,
            CameraAutoRecenterSpeed = 0.22f,
            MasterVolume = 1f,
            MotionPreset = MotionIntensityPreset.Normal,
            MotionStyle = MotionStyleProfile.Stable
        };
    }

    private void LoadProgression()
    {
        int available = Mathf.Max(1, AvailableLevelCount);
        int defaultUnlocked = levelConfigDatabase != null
            ? levelConfigDatabase.StartingUnlockedLevel
            : 1;
        maxUnlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt(MaxUnlockedLevelPrefsKey, defaultUnlocked), 1, available);
        currentSettings.LevelIndex = Mathf.Clamp(currentSettings.LevelIndex, 1, maxUnlockedLevel);
    }

    private void SaveProgression()
    {
        PlayerPrefs.SetInt(MaxUnlockedLevelPrefsKey, maxUnlockedLevel);
        PlayerPrefs.Save();
    }

    private bool UnlockNextLevelIfNeeded(int completedLevel)
    {
        int target = Mathf.Clamp(completedLevel + 1, 1, Mathf.Max(1, AvailableLevelCount));
        if (target <= maxUnlockedLevel)
        {
            return false;
        }

        maxUnlockedLevel = target;
        SaveProgression();
        return true;
    }

    private void EnsureCoreObjects()
    {
        ResolveLevelConfigDatabase();
        maxUnlockedLevel = Mathf.Clamp(maxUnlockedLevel, 1, Mathf.Max(1, AvailableLevelCount));
        EnsureEventSystem();

        levelBuilder = FindFirstObjectByType<LevelOneRuntimeBuilder>();
        if (levelBuilder == null)
        {
            GameObject builderObject = new GameObject("LevelBuilder");
            levelBuilder = builderObject.AddComponent<LevelOneRuntimeBuilder>();
        }

        hud = FindFirstObjectByType<RuntimeHUD>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("RuntimeHUD");
            hud = hudObject.AddComponent<RuntimeHUD>();
        }

        inGameSettingsUI = FindFirstObjectByType<InGameSettingsUI>();
        if (inGameSettingsUI == null)
        {
            GameObject settingsObject = new GameObject("InGameSettingsUI");
            inGameSettingsUI = settingsObject.AddComponent<InGameSettingsUI>();
        }

        inGameSettingsUI.OnMouseSensitivityChanged -= HandleMouseSensitivityChanged;
        inGameSettingsUI.OnCameraFovChanged -= HandleCameraFovChanged;
        inGameSettingsUI.OnCameraSmoothnessChanged -= HandleCameraSmoothnessChanged;
        inGameSettingsUI.OnCameraAutoRecenterChanged -= HandleCameraAutoRecenterChanged;
        inGameSettingsUI.OnMasterVolumeChanged -= HandleMasterVolumeChanged;
        inGameSettingsUI.OnFeedbackIntensityChanged -= HandleFeedbackIntensityChanged;
        inGameSettingsUI.OnCameraShakeChanged -= HandleCameraShakeChanged;
        inGameSettingsUI.OnMotionPresetChanged -= HandleMotionPresetChanged;
        inGameSettingsUI.OnMotionStyleChanged -= HandleMotionStyleChanged;
        inGameSettingsUI.OnDifficultyChanged -= HandleDifficultyChanged;
        inGameSettingsUI.OnVisibilityChanged -= HandleInGameSettingsVisibilityChanged;
        inGameSettingsUI.OnDifficultyChanged += HandleDifficultyChanged;
        inGameSettingsUI.OnMouseSensitivityChanged += HandleMouseSensitivityChanged;
        inGameSettingsUI.OnCameraFovChanged += HandleCameraFovChanged;
        inGameSettingsUI.OnCameraSmoothnessChanged += HandleCameraSmoothnessChanged;
        inGameSettingsUI.OnCameraAutoRecenterChanged += HandleCameraAutoRecenterChanged;
        inGameSettingsUI.OnMasterVolumeChanged += HandleMasterVolumeChanged;
        inGameSettingsUI.OnFeedbackIntensityChanged += HandleFeedbackIntensityChanged;
        inGameSettingsUI.OnCameraShakeChanged += HandleCameraShakeChanged;
        inGameSettingsUI.OnMotionPresetChanged += HandleMotionPresetChanged;
        inGameSettingsUI.OnMotionStyleChanged += HandleMotionStyleChanged;
        inGameSettingsUI.OnVisibilityChanged += HandleInGameSettingsVisibilityChanged;

        menuUI = FindFirstObjectByType<MainMenuUI>();
        if (menuUI == null)
        {
            GameObject menuObject = new GameObject("MainMenuUI");
            menuUI = menuObject.AddComponent<MainMenuUI>();
        }
        menuUI.OnStartRequested -= StartRun;
        menuUI.OnStartRequested += StartRun;
        menuUI.SetLevelBounds(maxUnlockedLevel, AvailableLevelCount);

        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
        {
            systems = new GameObject("GameSystems");
        }

        gravity = FindFirstObjectByType<GravitySystem>();
        if (gravity == null)
        {
            gravity = systems.AddComponent<GravitySystem>();
        }

        progress = FindFirstObjectByType<LevelProgress>();
        if (progress == null)
        {
            progress = systems.AddComponent<LevelProgress>();
        }

        levelTimer = FindFirstObjectByType<LevelTimer>();
        if (levelTimer == null)
        {
            levelTimer = systems.AddComponent<LevelTimer>();
        }

        feedback = FindFirstObjectByType<GameFeedback>();
        if (feedback == null)
        {
            feedback = systems.AddComponent<GameFeedback>();
        }
    }

    private void ResolveLevelConfigDatabase()
    {
        levelConfigDatabase = LevelConfigDatabase.LoadOrCreateRuntime();
    }

    private static void EnsureEventSystem()
    {
        EventSystem[] allSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        EventSystem eventSystem = allSystems != null && allSystems.Length > 0
            ? allSystems[0]
            : null;
        if (eventSystem == null)
        {
            GameObject eventRoot = new GameObject("EventSystem");
            eventSystem = eventRoot.AddComponent<EventSystem>();
        }

        if (allSystems != null)
        {
            for (int i = 0; i < allSystems.Length; i++)
            {
                EventSystem extra = allSystems[i];
                if (extra == null || extra == eventSystem)
                {
                    continue;
                }

                extra.enabled = false;
                if (extra.gameObject.activeSelf)
                {
                    extra.gameObject.SetActive(false);
                }
            }
        }

        if (!eventSystem.gameObject.activeSelf)
        {
            eventSystem.gameObject.SetActive(true);
        }
        eventSystem.enabled = true;

#if ENABLE_INPUT_SYSTEM
        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        inputSystemModule.enabled = true;

        StandaloneInputModule standaloneInput = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneInput != null)
        {
            standaloneInput.enabled = false;
        }
#else
        StandaloneInputModule standaloneInput = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneInput == null)
        {
            standaloneInput = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
        standaloneInput.enabled = true;
#endif
    }

    private void BindEvents()
    {
        if (progress == null || levelTimer == null)
        {
            return;
        }

        if (eventsBound && boundProgress == progress && boundTimer == levelTimer)
        {
            return;
        }

        UnbindEvents();

        progress.OnLevelCompleted += HandleLevelCompleted;
        levelTimer.OnTimerExpired += HandleTimerExpired;
        boundProgress = progress;
        boundTimer = levelTimer;
        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (boundProgress != null)
        {
            boundProgress.OnLevelCompleted -= HandleLevelCompleted;
        }

        if (boundTimer != null)
        {
            boundTimer.OnTimerExpired -= HandleTimerExpired;
        }

        boundProgress = null;
        boundTimer = null;
        eventsBound = false;
    }

    public void EnterMenu()
    {
        EnsureCoreObjects();
        BindEvents();
        StopFirstRunHintRoutine();

        State = GameState.Menu;
        gameplayInputLockedUntil = 0f;
        levelTimer.StopTimer();
        levelBuilder.ClearLevel();
        activePlayer = null;
        activeCamera = null;
        activeAvatar = null;
        if (inGameSettingsUI != null)
        {
            inGameSettingsUI.SetVisible(false);
        }

        if (menuUI != null)
        {
            currentSettings = SanitizeSettings(currentSettings, AvailableLevelCount);
            currentSettings.LevelIndex = Mathf.Clamp(currentSettings.LevelIndex, 1, maxUnlockedLevel);
            menuUI.SetLevelBounds(maxUnlockedLevel, AvailableLevelCount);
            menuUI.ApplySettings(currentSettings);
            menuUI.Show(true);
        }

        if (hud != null)
        {
            hud.SetGameplayVisible(false);
            hud.HideResultPanel();
        }

        ApplyCursorState(false);
    }

    public void StartRun(GameRunSettings settings)
    {
        EnsureCoreObjects();
        BindEvents();
        StopFirstRunHintRoutine();

        if (settings.LevelIndex > maxUnlockedLevel)
        {
            Debug.LogWarning("Requested locked level " + settings.LevelIndex + ". Clamping to unlocked max " + maxUnlockedLevel + ".");
        }

        currentSettings = SanitizeSettings(settings, AvailableLevelCount);
        currentSettings.LevelIndex = Mathf.Clamp(currentSettings.LevelIndex, 1, maxUnlockedLevel);
        currentConfig = ValidateConfig(BuildConfig(levelConfigDatabase, currentSettings), AvailableLevelCount);
        currentConfig.Difficulty = currentSettings.Difficulty;
        currentConfig.FlipCooldownSeconds = ResolveFlipCooldown(currentSettings.Difficulty, currentSettings.Mode);
        currentConfig.CheckpointPolicy = ResolveCheckpointPolicy(currentSettings.Difficulty, currentSettings.Mode);
        if (currentSettings.LevelIndex <= FirstMainLevelIndex)
        {
            // Lower early-stage friction so first-time players can learn mechanics smoothly.
            currentConfig.FlipCooldownSeconds = Mathf.Max(0.2f, currentConfig.FlipCooldownSeconds - 0.14f);
            currentConfig.CheckpointPolicy = CheckpointPolicy.Dense;
        }
        AudioListener.volume = Mathf.Clamp01(currentSettings.MasterVolume);
        if (feedback != null)
        {
            feedback.Configure(currentSettings.FeedbackIntensity, currentSettings.CameraShakeEnabled);
        }

        progress.ConfigureSession(
            currentConfig.TotalCrystals,
            currentConfig.RequiredCrystals,
            currentConfig.TwoStarThreshold,
            currentConfig.ThreeStarThreshold
        );

        gravity.SetFlipCooldown(currentConfig.FlipCooldownSeconds);
        gravity.ResetToDefault();
        KillZone.ResetGlobalCooldown();

        LevelBuildResult buildResult;
        try
        {
            buildResult = levelBuilder.BuildLevel(currentConfig);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Debug.LogError("Level build failed with an exception. Returning to menu.");
            EnterMenu();
            return;
        }

        if (buildResult.Player == null || buildResult.CameraFollow == null)
        {
            Debug.LogError("Level build failed: missing player or camera follow component.");
            EnterMenu();
            return;
        }

        activePlayer = buildResult.Player;
        activeCamera = buildResult.CameraFollow;
        activeAvatar = activePlayer != null ? activePlayer.GetComponent<PlayerVisualAvatar>() : null;
        int spawnedCrystals = Mathf.Max(1, buildResult.SpawnedCrystals);
        int threeStar = spawnedCrystals;
        int required = Mathf.Clamp(currentConfig.RequiredCrystals, 1, threeStar);
        if (threeStar >= 2 && required >= threeStar)
        {
            required = threeStar - 1;
        }

        int twoStarMax = threeStar >= 2 ? threeStar - 1 : threeStar;
        int twoStar = Mathf.Clamp(currentConfig.TwoStarThreshold, required, twoStarMax);

        if (spawnedCrystals != currentConfig.TotalCrystals ||
            required != currentConfig.RequiredCrystals ||
            twoStar != currentConfig.TwoStarThreshold ||
            threeStar != currentConfig.ThreeStarThreshold)
        {
            currentConfig.TotalCrystals = spawnedCrystals;
            currentConfig.RequiredCrystals = required;
            currentConfig.TwoStarThreshold = twoStar;
            currentConfig.ThreeStarThreshold = threeStar;
            progress.ConfigureSession(
                spawnedCrystals,
                required,
                twoStar,
                threeStar
            );
        }

        progress.SetAutoCompleteOnAllCrystals(currentSettings.LevelIndex == TutorialLevelIndex);

        if (activeCamera != null)
        {
            activeCamera.SetMouseSensitivity(currentSettings.MouseSensitivity);
            activeCamera.SetFieldOfView(currentSettings.CameraFov);
            activeCamera.SetMotionResponse(currentSettings.CameraSmoothness);
            activeCamera.SetAutoRecenterSpeed(currentSettings.CameraAutoRecenterSpeed);
            activeCamera.SetMotionPreset(currentSettings.MotionPreset);
        }

        if (activeAvatar != null)
        {
            activeAvatar.ApplyMotionProfile(currentSettings.MotionPreset, currentSettings.MotionStyle);
        }

        if (inGameSettingsUI != null)
        {
            inGameSettingsUI.Initialize(currentSettings);
            inGameSettingsUI.SetVisible(false);
        }

        levelTimer.Configure(currentConfig.TimeLimitSeconds);
        levelTimer.StartTimer();

        State = GameState.Playing;
        gameplayInputLockedUntil = Time.time + 1f;
        if (menuUI != null)
        {
            menuUI.Show(false);
        }

        if (hud != null)
        {
            hud.BindSession(currentSettings, currentConfig);
            hud.SetGameplayVisible(true);
            hud.HideResultPanel();
            hud.ShowLevelIntroCard(currentSettings, currentConfig, 1f);
            string openingHint = currentSettings.LevelIndex == TutorialLevelIndex
                ? "Tutorial: move, jump, flip to activate the switch, then reach exit."
                : "Collect crystals, clear chain mechanisms, unlock gate, then reach exit.";
            hud.ShowTransientMessage(openingHint, 2.5f);
        }

        TryShowFirstRunHints();

        ApplyCursorState(true);
    }

    public void StartNextLevel()
    {
        GameRunSettings next = currentSettings;
        next.LevelIndex = Mathf.Clamp(next.LevelIndex + 1, 1, maxUnlockedLevel);
        StartRun(next);
    }

    private void HandleTimerExpired()
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = GameState.Failed;
        StopFirstRunHintRoutine();
        if (inGameSettingsUI != null)
        {
            inGameSettingsUI.SetVisible(false);
        }
        FreezePlayer();

        if (hud != null)
        {
            string detail = BuildRunStatsSummary(includeCrystals: true, includeCompletionTime: false);
            hud.ShowResultPanel(
                false,
                0,
                "Time up\n" + detail,
                "Press R to retry or Esc to return menu."
            );
        }

        ApplyCursorState(false);
    }

    private void HandleLevelCompleted()
    {
        if (State != GameState.Playing)
        {
            return;
        }

        State = GameState.Completed;
        StopFirstRunHintRoutine();
        levelTimer.StopTimer();
        if (inGameSettingsUI != null)
        {
            inGameSettingsUI.SetVisible(false);
        }
        FreezePlayer();

        int stars = progress.CalculateStars();
        float completionSeconds = levelTimer != null ? Mathf.Max(0f, levelTimer.ElapsedSeconds) : 0f;
        bool isNewBest = TrySetBestTime(currentSettings, completionSeconds, out float bestTimeSeconds);
        string detail = BuildRunStatsSummary(includeCrystals: true, includeCompletionTime: true);
        if (bestTimeSeconds > 0f)
        {
            detail += "\nBest Time: " + bestTimeSeconds.ToString("0.0") + "s" + (isNewBest ? "  (NEW)" : string.Empty);
        }
        bool isTutorialCompletion = currentSettings.LevelIndex == TutorialLevelIndex;
        bool hasNextLevel = currentSettings.LevelIndex < Mathf.Max(1, AvailableLevelCount);
        string hint = hasNextLevel
            ? "Press N for next level, R retry, Esc menu."
            : "Press R retry, Esc menu.";

        if (UnlockNextLevelIfNeeded(currentSettings.LevelIndex))
        {
            int unlockedLevel = Mathf.Clamp(currentSettings.LevelIndex + 1, 1, Mathf.Max(1, AvailableLevelCount));
            hud?.ShowTransientMessage("Level " + unlockedLevel + " unlocked in menu.", 2.2f);
            if (menuUI != null)
            {
                menuUI.SetLevelBounds(maxUnlockedLevel, AvailableLevelCount);
            }
        }

        if (hud != null)
        {
            if (isTutorialCompletion && AvailableLevelCount >= FirstMainLevelIndex)
            {
                GameRunSettings firstMainSettings = currentSettings;
                firstMainSettings.LevelIndex = Mathf.Clamp(FirstMainLevelIndex, 1, Mathf.Max(1, maxUnlockedLevel));
                LevelRuntimeConfig firstMainPreview = PreviewConfig(firstMainSettings);
                string firstMainLabel = string.IsNullOrWhiteSpace(firstMainPreview.LevelName)
                    ? ("Level " + firstMainPreview.LevelIndex)
                    : firstMainPreview.LevelName;
                string tutorialBody = "Tutorial Complete\n" + detail;
                string tutorialHint = "Congrats! Click the button to jump into " + firstMainLabel + ".";
                hud.ShowResultPanel(
                    true,
                    stars,
                    tutorialBody,
                    tutorialHint,
                    "Go To " + firstMainLabel,
                    StartFirstMainLevelFromTutorialResult
                );
            }
            else
            {
                hud.ShowResultPanel(true, stars, detail, hint);
            }

            if (isNewBest)
            {
                hud.ShowTransientMessage("New best time: " + bestTimeSeconds.ToString("0.0") + "s", 2.2f);
            }
        }

        ApplyCursorState(false);
    }

    private void StartFirstMainLevelFromTutorialResult()
    {
        if (State != GameState.Completed)
        {
            return;
        }

        GameRunSettings next = currentSettings;
        next.LevelIndex = Mathf.Clamp(FirstMainLevelIndex, 1, maxUnlockedLevel);
        StartRun(next);
    }

    private string BuildRunStatsSummary(bool includeCrystals, bool includeCompletionTime)
    {
        LevelProgress activeProgressRef = progress != null ? progress : LevelProgress.Instance;
        LevelTimer activeTimerRef = levelTimer != null ? levelTimer : LevelTimer.Instance;

        string crystalsPart = string.Empty;
        if (includeCrystals && activeProgressRef != null)
        {
            crystalsPart = "Crystals: " + activeProgressRef.CollectedCrystals + "/" + activeProgressRef.TotalCrystals +
                           " (Need " + activeProgressRef.MinimumRequired + ")";
        }

        float elapsed = 0f;
        if (activeTimerRef != null)
        {
            elapsed = Mathf.Max(0f, activeTimerRef.DurationSeconds - activeTimerRef.TimeLeftSeconds);
        }

        string timePart = includeCompletionTime
            ? "Clear Time: " + elapsed.ToString("0.0") + "s"
            : "Elapsed: " + elapsed.ToString("0.0") + "s";

        int deaths = activeProgressRef != null ? activeProgressRef.DeathCount : 0;
        int flips = activeProgressRef != null ? activeProgressRef.FlipCount : 0;
        string statsPart = "Deaths: " + deaths + "   Flips: " + flips;
        string difficultyPart = "Difficulty: " + DifficultyLabel(currentSettings.Difficulty) +
                                "   Flip CD: " + currentConfig.FlipCooldownSeconds.ToString("0.00") + "s" +
                                "   Checkpoints: " + CheckpointPolicyLabel(currentConfig.CheckpointPolicy);

        if (!string.IsNullOrEmpty(crystalsPart))
        {
            return crystalsPart + "\n" + timePart + "\n" + statsPart + "\n" + difficultyPart;
        }

        return timePart + "\n" + statsPart + "\n" + difficultyPart;
    }

    private void TryShowFirstRunHints()
    {
        if (State != GameState.Playing || currentSettings.LevelIndex != TutorialLevelIndex)
        {
            return;
        }

        if (PlayerPrefs.GetInt(FirstRunHintsSeenPrefsKey, 0) != 0)
        {
            return;
        }

        PlayerPrefs.SetInt(FirstRunHintsSeenPrefsKey, 1);
        PlayerPrefs.Save();
        firstRunHintRoutine = StartCoroutine(FirstRunHintSequence());
    }

    private System.Collections.IEnumerator FirstRunHintSequence()
    {
        yield return new WaitForSeconds(1.2f);
        if (State == GameState.Playing)
        {
            hud?.ShowTransientMessage("Step 1/4: WASD move, mouse look, Space jump.", 2.4f);
        }

        yield return new WaitForSeconds(2.5f);
        if (State == GameState.Playing)
        {
            hud?.ShowTransientMessage("Step 2/4: Press F to flip gravity (1s cooldown).", 2.4f);
        }

        yield return new WaitForSeconds(2.5f);
        if (State == GameState.Playing)
        {
            hud?.ShowTransientMessage("Step 3/4: Collect crystals, unlock gate, then reach exit.", 2.4f);
        }

        yield return new WaitForSeconds(2.4f);
        if (State == GameState.Playing)
        {
            hud?.ShowTransientMessage("Step 4/4: Need comfort? Tune FOV/camera smoothing in Settings (O).", 2.6f);
        }

        firstRunHintRoutine = null;
    }

    private void StopFirstRunHintRoutine()
    {
        if (firstRunHintRoutine == null)
        {
            return;
        }

        StopCoroutine(firstRunHintRoutine);
        firstRunHintRoutine = null;
    }

    private void FreezePlayer()
    {
        if (activePlayer == null)
        {
            return;
        }

        activePlayer.enabled = false;

        Rigidbody body = activePlayer.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    private void ToggleInGameSettings()
    {
        if (inGameSettingsUI == null || State != GameState.Playing)
        {
            return;
        }

        inGameSettingsUI.Toggle();
    }

    private void HandleMouseSensitivityChanged(float value)
    {
        currentSettings.MouseSensitivity = Mathf.Clamp(value, 0.4f, 3.2f);
        if (activeCamera != null)
        {
            activeCamera.SetMouseSensitivity(currentSettings.MouseSensitivity);
        }
    }

    private void HandleDifficultyChanged(GameDifficulty difficulty)
    {
        GameDifficulty clamped = (GameDifficulty)Mathf.Clamp((int)difficulty, 0, 2);
        if (currentSettings.Difficulty == clamped)
        {
            return;
        }

        bool isPlaying = State == GameState.Playing;
        if (isPlaying && Time.unscaledTime < difficultySwitchLockUntil)
        {
            // Keep UI and active runtime settings coherent during lock window.
            inGameSettingsUI?.Initialize(currentSettings);
            hud?.ShowTransientMessage("Difficulty switch cooling down...", 0.8f);
            return;
        }

        currentSettings.Difficulty = clamped;
        if (!isPlaying)
        {
            return;
        }

        difficultySwitchLockUntil = Time.unscaledTime + 0.35f;
        GameRunSettings rerun = currentSettings;
        rerun.Difficulty = currentSettings.Difficulty;
        hud?.ShowTransientMessage("Applying difficulty: " + DifficultyLabel(rerun.Difficulty), 0.9f);
        StartRun(rerun);
        hud?.ShowTransientMessage("Difficulty switched to " + DifficultyLabel(currentSettings.Difficulty), 1.6f);
    }

    private void HandleCameraFovChanged(float value)
    {
        currentSettings.CameraFov = Mathf.Clamp(value, 55f, 95f);
        if (activeCamera != null)
        {
            activeCamera.SetFieldOfView(currentSettings.CameraFov);
        }
    }

    private void HandleCameraSmoothnessChanged(float value)
    {
        currentSettings.CameraSmoothness = Mathf.Clamp01(value);
        if (activeCamera != null)
        {
            activeCamera.SetMotionResponse(currentSettings.CameraSmoothness);
        }
    }

    private void HandleCameraAutoRecenterChanged(float value)
    {
        currentSettings.CameraAutoRecenterSpeed = Mathf.Clamp(value, 0f, 2f);
        if (activeCamera != null)
        {
            activeCamera.SetAutoRecenterSpeed(currentSettings.CameraAutoRecenterSpeed);
        }
    }

    private void HandleMasterVolumeChanged(float value)
    {
        currentSettings.MasterVolume = Mathf.Clamp01(value);
        AudioListener.volume = currentSettings.MasterVolume;
    }

    private void HandleFeedbackIntensityChanged(float value)
    {
        currentSettings.FeedbackIntensity = Mathf.Clamp(value, 0f, 2f);
        if (feedback != null)
        {
            feedback.Configure(currentSettings.FeedbackIntensity, currentSettings.CameraShakeEnabled);
        }

        ApplyRenderEnhancerProfile();
    }

    private void HandleCameraShakeChanged(bool enabled)
    {
        currentSettings.CameraShakeEnabled = enabled;
        if (feedback != null)
        {
            feedback.Configure(currentSettings.FeedbackIntensity, currentSettings.CameraShakeEnabled);
        }
    }

    private void HandleMotionPresetChanged(MotionIntensityPreset preset)
    {
        currentSettings.MotionPreset = (MotionIntensityPreset)Mathf.Clamp((int)preset, 0, 2);
        if (activeAvatar != null)
        {
            activeAvatar.ApplyMotionProfile(currentSettings.MotionPreset, currentSettings.MotionStyle);
        }

        if (activeCamera != null)
        {
            activeCamera.SetMotionPreset(currentSettings.MotionPreset);
        }

        hud?.ShowTransientMessage("Motion preset: " + MotionPresetLabel(currentSettings.MotionPreset), 1.1f);
    }

    private void HandleMotionStyleChanged(MotionStyleProfile style)
    {
        currentSettings.MotionStyle = (MotionStyleProfile)Mathf.Clamp((int)style, 0, 2);
        if (activeAvatar != null)
        {
            activeAvatar.ApplyMotionProfile(currentSettings.MotionPreset, currentSettings.MotionStyle);
        }

        hud?.ShowTransientMessage("Motion style: " + MotionStyleLabel(currentSettings.MotionStyle), 1.1f);
    }

    private void HandleInGameSettingsVisibilityChanged(bool visible)
    {
        if (State != GameState.Playing)
        {
            return;
        }

        ApplyCursorState(!visible);
        if (visible)
        {
            hud?.ShowTransientMessage("Live settings open", 0.9f);
        }
    }

    private void ApplyRenderEnhancerProfile()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        MonoBehaviour enhancer = camera.GetComponent("CameraRenderEnhancer") as MonoBehaviour;
        if (enhancer == null)
        {
            return;
        }

        MethodInfo configure = enhancer.GetType().GetMethod("ConfigureProfile", new[] { typeof(float) });
        if (configure == null)
        {
            return;
        }

        float profile = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp01(currentSettings.FeedbackIntensity * 0.5f));
        configure.Invoke(enhancer, new object[] { profile });
    }

    private static GameRunSettings SanitizeSettings(GameRunSettings settings, int maxLevelCount = DefaultFallbackLevelCount)
    {
        int levelCap = Mathf.Max(1, maxLevelCount);
        settings.LevelIndex = Mathf.Clamp(settings.LevelIndex, 1, levelCap);
        settings.Difficulty = (GameDifficulty)Mathf.Clamp((int)settings.Difficulty, 0, 2);
        settings.Mode = (GameMode)Mathf.Clamp((int)settings.Mode, 0, 1);
        settings.VisualTheme = (PlayerVisualTheme)Mathf.Clamp((int)settings.VisualTheme, 0, 2);
        settings.ColorPalette = (PlayerColorPalette)Mathf.Clamp((int)settings.ColorPalette, 0, 2);
        settings.AtmospherePreset = (MenuAtmospherePreset)Mathf.Clamp((int)settings.AtmospherePreset, 0, 2);
        settings.FeedbackIntensity = Mathf.Clamp(settings.FeedbackIntensity, 0f, 2f);
        settings.MouseSensitivity = Mathf.Clamp(settings.MouseSensitivity, 0.4f, 3.2f);
        settings.CameraFov = Mathf.Clamp(settings.CameraFov > 1f ? settings.CameraFov : 66f, 55f, 95f);
        settings.CameraSmoothness = Mathf.Clamp01(settings.CameraSmoothness);
        settings.CameraAutoRecenterSpeed = Mathf.Clamp(settings.CameraAutoRecenterSpeed, 0f, 2f);
        settings.MasterVolume = Mathf.Clamp01(settings.MasterVolume);
        settings.MotionPreset = (MotionIntensityPreset)Mathf.Clamp((int)settings.MotionPreset, 0, 2);
        settings.MotionStyle = (MotionStyleProfile)Mathf.Clamp((int)settings.MotionStyle, 0, 2);
        return settings;
    }

    private static LevelRuntimeConfig BuildConfig(LevelConfigDatabase database, GameRunSettings settings)
    {
        LevelConfigDatabase resolvedDatabase = database != null
            ? database
            : LevelConfigDatabase.LoadOrCreateRuntime();

        if (resolvedDatabase == null)
        {
            return new LevelRuntimeConfig
            {
                LevelIndex = 1,
                TotalLevelCount = 1,
                LevelName = "Fallback",
                ObjectiveSummary = "Collect crystals and reach exit.",
                StageBand = LevelStageBand.Teaching,
                Difficulty = settings.Difficulty,
                FlipCooldownSeconds = ResolveFlipCooldown(settings.Difficulty, settings.Mode),
                CheckpointPolicy = ResolveCheckpointPolicy(settings.Difficulty, settings.Mode),
                TotalCrystals = 20,
                RequiredCrystals = 14,
                TwoStarThreshold = 17,
                ThreeStarThreshold = 20,
                FloatingPlatformCount = 8,
                MechanismCount = 2,
                GravityAnchorZoneCount = 1,
                FloraNearCount = 180,
                FloraFarCount = 120,
                ScenicDensityScale = 1f,
                MapLength = 200f,
                TimeLimitSeconds = 300f,
                VisualTheme = settings.VisualTheme,
                ColorPalette = settings.ColorPalette
            };
        }

        LevelRuntimeConfig config = resolvedDatabase.BuildConfig(settings);
        config.Difficulty = settings.Difficulty;
        config.FlipCooldownSeconds = ResolveFlipCooldown(settings.Difficulty, settings.Mode);
        config.CheckpointPolicy = ResolveCheckpointPolicy(settings.Difficulty, settings.Mode);
        if (settings.LevelIndex <= FirstMainLevelIndex)
        {
            config.FlipCooldownSeconds = Mathf.Max(0.2f, config.FlipCooldownSeconds - 0.14f);
            config.CheckpointPolicy = CheckpointPolicy.Dense;
        }
        return config;
    }

    private static LevelRuntimeConfig ValidateConfig(LevelRuntimeConfig config, int maxLevelCount = DefaultFallbackLevelCount)
    {
        int levelCap = Mathf.Max(1, maxLevelCount);
        config.LevelIndex = Mathf.Clamp(config.LevelIndex, 1, levelCap);
        config.TotalLevelCount = Mathf.Clamp(config.TotalLevelCount <= 0 ? levelCap : config.TotalLevelCount, 1, 16);
        config.TotalCrystals = Mathf.Clamp(config.TotalCrystals, 8, 120);
        config.RequiredCrystals = Mathf.Clamp(config.RequiredCrystals, 1, config.TotalCrystals);
        if (config.TotalCrystals >= 2 && config.RequiredCrystals >= config.TotalCrystals)
        {
            config.RequiredCrystals = config.TotalCrystals - 1;
        }

        config.ThreeStarThreshold = Mathf.Clamp(config.ThreeStarThreshold, config.RequiredCrystals, config.TotalCrystals);
        config.TwoStarThreshold = Mathf.Clamp(config.TwoStarThreshold, config.RequiredCrystals, config.ThreeStarThreshold);
        if (config.TotalCrystals >= 3 && config.TwoStarThreshold >= config.ThreeStarThreshold)
        {
            config.TwoStarThreshold = config.ThreeStarThreshold - 1;
        }

        config.FloatingPlatformCount = Mathf.Clamp(config.FloatingPlatformCount, 3, 48);
        config.MechanismCount = Mathf.Clamp(config.MechanismCount, 1, 18);
        config.GravityAnchorZoneCount = Mathf.Clamp(config.GravityAnchorZoneCount, 0, 8);
        config.FloraNearCount = Mathf.Clamp(config.FloraNearCount, 20, 1200);
        config.FloraFarCount = Mathf.Clamp(config.FloraFarCount, 20, 1200);
        config.ScenicDensityScale = Mathf.Clamp(config.ScenicDensityScale <= 0f ? 1f : config.ScenicDensityScale, 0.45f, 2f);
        config.MapLength = Mathf.Clamp(config.MapLength, 90f, 520f);
        config.TimeLimitSeconds = Mathf.Clamp(config.TimeLimitSeconds, 60f, 1200f);
        if (string.IsNullOrWhiteSpace(config.LevelName))
        {
            config.LevelName = "Level " + config.LevelIndex;
        }
        if (string.IsNullOrWhiteSpace(config.ObjectiveSummary))
        {
            config.ObjectiveSummary = "Collect enough crystals and reach exit.";
        }
        config.StageBand = (LevelStageBand)Mathf.Clamp((int)config.StageBand, 0, 3);
        config.Difficulty = (GameDifficulty)Mathf.Clamp((int)config.Difficulty, 0, 2);
        config.FlipCooldownSeconds = Mathf.Clamp(config.FlipCooldownSeconds <= 0f ? 1f : config.FlipCooldownSeconds, 0.2f, 3f);
        config.CheckpointPolicy = (CheckpointPolicy)Mathf.Clamp((int)config.CheckpointPolicy, 0, 2);
        return config;
    }

    public static float ResolveFlipCooldown(GameDifficulty difficulty, GameMode mode)
    {
        float value;
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                value = 0.82f;
                break;
            case GameDifficulty.Hard:
                value = 1.24f;
                break;
            default:
                value = 1.00f;
                break;
        }

        if (mode == GameMode.Challenge)
        {
            value += 0.08f;
        }

        return Mathf.Clamp(value, 0.2f, 3f);
    }

    public static CheckpointPolicy ResolveCheckpointPolicy(GameDifficulty difficulty, GameMode mode)
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return CheckpointPolicy.Dense;
            case GameDifficulty.Hard:
                return mode == GameMode.Challenge
                    ? CheckpointPolicy.Sparse
                    : CheckpointPolicy.Normal;
            default:
                return mode == GameMode.Challenge
                    ? CheckpointPolicy.Sparse
                    : CheckpointPolicy.Normal;
        }
    }

    public static string CheckpointPolicyLabel(CheckpointPolicy policy)
    {
        switch (policy)
        {
            case CheckpointPolicy.Dense:
                return "Dense";
            case CheckpointPolicy.Sparse:
                return "Sparse";
            default:
                return "Normal";
        }
    }

    public static bool TryGetBestTimeSeconds(GameRunSettings settings, out float seconds)
    {
        string key = BuildBestTimePrefsKey(settings.LevelIndex, settings.Difficulty, settings.Mode);
        seconds = PlayerPrefs.GetFloat(key, -1f);
        return seconds > 0f;
    }

    private bool TrySetBestTime(GameRunSettings settings, float candidateSeconds, out float bestSeconds)
    {
        bestSeconds = Mathf.Max(0f, candidateSeconds);
        if (bestSeconds <= 0f)
        {
            return false;
        }

        string key = BuildBestTimePrefsKey(settings.LevelIndex, settings.Difficulty, settings.Mode);
        float existing = PlayerPrefs.GetFloat(key, -1f);
        if (existing > 0f && existing <= bestSeconds)
        {
            bestSeconds = existing;
            return false;
        }

        PlayerPrefs.SetFloat(key, bestSeconds);
        PlayerPrefs.Save();
        return true;
    }

    private static string BuildBestTimePrefsKey(int levelIndex, GameDifficulty difficulty, GameMode mode)
    {
        return BestTimePrefsPrefix + ".L" + Mathf.Max(1, levelIndex) +
               ".D" + Mathf.Clamp((int)difficulty, 0, 2) +
               ".M" + Mathf.Clamp((int)mode, 0, 1);
    }

    private static string DifficultyLabel(GameDifficulty difficulty)
    {
        switch (difficulty)
        {
            case GameDifficulty.Easy:
                return "Easy";
            case GameDifficulty.Hard:
                return "Hard";
            default:
                return "Normal";
        }
    }

    private static string MotionPresetLabel(MotionIntensityPreset preset)
    {
        switch (preset)
        {
            case MotionIntensityPreset.Realistic:
                return "Realistic";
            case MotionIntensityPreset.Cinematic:
                return "Cinematic";
            default:
                return "Normal";
        }
    }

    private static string MotionStyleLabel(MotionStyleProfile style)
    {
        switch (style)
        {
            case MotionStyleProfile.Agile:
                return "Agile";
            case MotionStyleProfile.Exaggerated:
                return "Exaggerated";
            default:
                return "Stable";
        }
    }

    private static void ApplyCursorState(bool lockCursor)
    {
        Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !lockCursor;
    }
}
