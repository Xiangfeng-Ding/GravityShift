using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeHUD : MonoBehaviour
{
    public static RuntimeHUD Instance { get; private set; }

    private Canvas rootCanvas;
    private GameObject gameplayRoot;
    private GameObject resultRoot;

    private Text titleText;
    private Text objectiveText;
    private Text statusText;
    private Text timerText;
    private Text starsText;
    private Text messageText;
    private Image messagePanel;
    private Image infoPanel;
    private Image timerPanel;

    private Image alertPanel;
    private Text alertText;

    private Image flashOverlay;
    private RectTransform countdownBarsRoot;
    private CountdownBarSlot[] countdownBars;
    private RectTransform infoPanelRect;
    private RectTransform timerPanelRect;
    private RectTransform messagePanelRect;
    private RectTransform alertPanelRect;
    private Image infoPanelSweep;
    private Image timerPanelSweep;
    private Image messagePanelSweep;
    private Image alertPanelSweep;
    private RawImage infoPanelDistortion;
    private RawImage timerPanelDistortion;
    private RawImage messagePanelDistortion;
    private RawImage alertPanelDistortion;
    private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private float cardVisualTime;

    private Text resultTitleText;
    private Text resultBodyText;
    private Text resultHintText;
    private Text resultStarsText;
    private Button resultActionButton;
    private Text resultActionButtonText;
    private Action resultActionCallback;

    private GameObject introRoot;
    private Text introTitleText;
    private Text introBodyText;
    private Text introRouteText;

    private GameRunSettings boundSettings;
    private LevelRuntimeConfig boundConfig;
    private bool hasBoundSession;
    private bool gameplayVisible;
    private bool hasCachedBestTime;
    private float cachedBestTimeSeconds;
    private float nextBestTimePollAt = -1f;

    private float messageHideAt = -1f;
    private bool messagePersistent;

    private float alertShowAt = -1f;
    private float alertDuration;

    private float flashTimeLeft;
    private float flashDuration;
    private float flashMaxAlpha;
    private Color flashColor = Color.white;
    private float introHideAt = -1f;

    private sealed class CountdownBarSlot
    {
        public string Key;
        public string Label;
        public float Duration;
        public float EndTime;
        public bool Active;
        public Image Root;
        public Image Fill;
        public Text Text;
        public Color AccentColor = new Color(0.35f, 0.78f, 1f);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
    }

    private void Update()
    {
        ApplyResponsiveLayout();
        AnimateCardVisuals(Time.unscaledDeltaTime);
        UpdateGameplayTexts();
        UpdateMessageLifetime();
        UpdateAlertBanner();
        UpdateCountdownBars();
        UpdateFlashOverlay();
        UpdateIntroCard();
        UpdateCanvasAutoVisibility();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        DisposeRuntimeTextures();
    }

    public void BindSession(GameRunSettings settings, LevelRuntimeConfig config)
    {
        boundSettings = settings;
        boundConfig = config;
        hasBoundSession = true;
        RefreshCachedBestTime(true);
        UpdateGameplayTexts();
    }

    public void SetGameplayVisible(bool visible)
    {
        gameplayVisible = visible;
        if (gameplayRoot != null)
        {
            gameplayRoot.SetActive(visible);
        }

        if (!visible)
        {
            HideLevelIntroCard();
            ClearAllCountdownBars();
            hasCachedBestTime = false;
            cachedBestTimeSeconds = 0f;
            nextBestTimePollAt = -1f;
        }

        if (visible)
        {
            rootCanvas.enabled = true;
        }
        else if (resultRoot == null || !resultRoot.activeSelf)
        {
            rootCanvas.enabled = false;
        }
    }

    public void HideLevelIntroCard()
    {
        if (introRoot != null)
        {
            introRoot.SetActive(false);
        }
        introHideAt = -1f;
    }

    public void ShowTransientMessage(string message, float duration)
    {
        ShowMessageInternal(message, duration, false);
    }

    public void ShowAlertBanner(string message, Color color, float duration = 1.2f)
    {
        if (alertPanel == null || alertText == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        alertPanel.enabled = true;
        alertText.enabled = true;
        if (alertPanelSweep != null)
        {
            alertPanelSweep.enabled = true;
        }
        if (alertPanelDistortion != null)
        {
            alertPanelDistortion.enabled = true;
        }
        alertText.text = message;
        alertPanel.color = new Color(color.r, color.g, color.b, 0.7f);
        alertText.color = new Color(1f, 1f, 1f, 0.96f);
        alertShowAt = Time.time;
        alertDuration = Mathf.Max(0.2f, duration);
        rootCanvas.enabled = true;
    }

    public void FlashScreen(Color color, float duration = 0.15f, float maxAlpha = 0.12f)
    {
        if (flashOverlay == null || maxAlpha <= 0f)
        {
            return;
        }

        flashColor = color;
        flashDuration = Mathf.Max(0.05f, duration);
        flashMaxAlpha = Mathf.Clamp(maxAlpha, 0.02f, 0.65f);
        flashTimeLeft = flashDuration;
        flashOverlay.enabled = true;
        rootCanvas.enabled = true;
    }

    public void ShowCountdownBar(string key, string label, Color color, float duration)
    {
        if (countdownBars == null || countdownBars.Length == 0 || string.IsNullOrEmpty(key) || duration <= 0f)
        {
            return;
        }

        int slotIndex = FindCountdownSlotByKey(key);
        if (slotIndex < 0)
        {
            slotIndex = FindAvailableCountdownSlot();
        }
        if (slotIndex < 0)
        {
            slotIndex = FindOldestCountdownSlot();
        }
        if (slotIndex < 0)
        {
            return;
        }

        CountdownBarSlot slot = countdownBars[slotIndex];
        if (slot == null || slot.Root == null || slot.Fill == null || slot.Text == null)
        {
            return;
        }

        slot.Key = key;
        slot.Label = string.IsNullOrEmpty(label) ? key : label;
        slot.Duration = Mathf.Max(0.2f, duration);
        slot.EndTime = Time.time + slot.Duration;
        slot.AccentColor = color;
        slot.Active = true;

        slot.Root.enabled = true;
        slot.Fill.enabled = true;
        slot.Text.enabled = true;
        slot.Fill.color = color;
        slot.Fill.rectTransform.anchorMax = new Vector2(1f, 1f);
        slot.Text.text = slot.Label + "  " + slot.Duration.ToString("0.0") + "s";

        if (countdownBarsRoot != null)
        {
            countdownBarsRoot.gameObject.SetActive(true);
        }

        rootCanvas.enabled = true;
    }

    public void HideCountdownBar(string key)
    {
        if (countdownBars == null || countdownBars.Length == 0 || string.IsNullOrEmpty(key))
        {
            return;
        }

        int slotIndex = FindCountdownSlotByKey(key);
        if (slotIndex < 0)
        {
            return;
        }

        ClearCountdownSlot(slotIndex);
    }

    public void ShowResultPanel(
        bool success,
        int stars,
        string body,
        string hint,
        string actionButtonLabel = null,
        Action onActionButtonClicked = null)
    {
        if (resultRoot == null)
        {
            return;
        }

        ClearAllCountdownBars();
        resultRoot.SetActive(true);
        resultTitleText.text = success ? "Stage Cleared" : "Mission Failed";
        resultTitleText.color = success
            ? new Color(0.80f, 1f, 0.86f)
            : new Color(1f, 0.58f, 0.58f);

        resultBodyText.text = body;
        resultHintText.text = hint;

        if (success)
        {
            resultStarsText.text = BuildStarString(stars);
            resultStarsText.gameObject.SetActive(true);
        }
        else
        {
            resultStarsText.gameObject.SetActive(false);
        }

        bool showAction = !string.IsNullOrWhiteSpace(actionButtonLabel) && onActionButtonClicked != null;
        resultActionCallback = showAction ? onActionButtonClicked : null;
        if (resultActionButton != null)
        {
            resultActionButton.gameObject.SetActive(showAction);
            resultActionButton.interactable = showAction;
        }
        if (resultActionButtonText != null)
        {
            resultActionButtonText.text = showAction ? actionButtonLabel.Trim() : string.Empty;
        }

        ShowMessageInternal(string.Empty, 0f, false);
        alertShowAt = -1f;
        if (alertPanel != null)
        {
            alertPanel.enabled = false;
        }
        if (alertPanelSweep != null)
        {
            alertPanelSweep.enabled = false;
        }
        if (alertPanelDistortion != null)
        {
            alertPanelDistortion.enabled = false;
        }
        if (alertText != null)
        {
            alertText.enabled = false;
        }
        rootCanvas.enabled = true;
    }

    public void HideResultPanel()
    {
        resultActionCallback = null;
        if (resultActionButton != null)
        {
            resultActionButton.gameObject.SetActive(false);
            resultActionButton.interactable = false;
        }

        if (resultRoot != null)
        {
            resultRoot.SetActive(false);
        }

        if (!gameplayVisible && rootCanvas != null)
        {
            rootCanvas.enabled = false;
        }
    }

    private void HandleResultActionButtonClicked()
    {
        Action callback = resultActionCallback;
        if (callback == null)
        {
            return;
        }

        resultActionCallback = null;
        callback.Invoke();
    }

    public void ShowLevelIntroCard(GameRunSettings settings, LevelRuntimeConfig config, float duration = 1f)
    {
        if (introRoot == null || introTitleText == null || introBodyText == null || introRouteText == null)
        {
            return;
        }

        int level = Mathf.Clamp(config.LevelIndex > 0 ? config.LevelIndex : settings.LevelIndex, 1, 16);
        int levelCount = Mathf.Clamp(config.TotalLevelCount > 0 ? config.TotalLevelCount : 16, 1, 16);
        level = Mathf.Clamp(level, 1, levelCount);
        introTitleText.text = "Level " + level + " Briefing";
        introBodyText.text = BuildLevelGoalText(level, config.RequiredCrystals, config.TwoStarThreshold, config.ThreeStarThreshold, config.ObjectiveSummary);
        introRouteText.text = BuildLevelRouteAdvice(level);
        introRoot.SetActive(true);
        introHideAt = Time.time + Mathf.Max(0.4f, duration);
        rootCanvas.enabled = true;
    }

    private void UpdateGameplayTexts()
    {
        if (!gameplayVisible || !hasBoundSession || statusText == null)
        {
            return;
        }

        LevelProgress progress = LevelProgress.Instance;
        LevelTimer timer = LevelTimer.Instance;
        GravitySystem gravity = GravitySystem.Instance;

        if (titleText != null)
        {
            string levelLabel = string.IsNullOrWhiteSpace(boundConfig.LevelName)
                ? ("L" + boundSettings.LevelIndex)
                : boundConfig.LevelName;
            titleText.text = "L" + boundSettings.LevelIndex + "  " +
                             levelLabel + "  " +
                             DifficultyLabel(boundSettings.Difficulty) + " / " +
                             ModeLabel(boundSettings.Mode);
        }

        int current = progress != null ? progress.CollectedCrystals : 0;
        int total = progress != null ? progress.TotalCrystals : boundConfig.TotalCrystals;
        int required = progress != null ? progress.MinimumRequired : boundConfig.RequiredCrystals;
        int missing = Mathf.Max(0, required - current);
        string difficultyLabel = DifficultyLabel(boundSettings.Difficulty);
        float configuredFlipCd = Mathf.Max(0.2f, boundConfig.FlipCooldownSeconds > 0f ? boundConfig.FlipCooldownSeconds : 1f);
        string checkpointLabel = GameDirector.CheckpointPolicyLabel(boundConfig.CheckpointPolicy);

        if (objectiveText != null)
        {
            objectiveText.text = "Diff " + difficultyLabel +
                                 " | CD " + configuredFlipCd.ToString("0.00") + "s" +
                                 " | CP " + checkpointLabel +
                                 " | " + (missing > 0 ? ("Obj " + missing + " left") : "Obj unlocked");
        }

        float cooldown = gravity != null ? Mathf.Max(0f, gravity.CooldownLeft) : 0f;
        string cd;
        if (gravity != null && gravity.IsFlipLocked)
        {
            cd = "Locked";
        }
        else
        {
            cd = cooldown > 0f ? cooldown.ToString("0.0") + "s" : "Ready";
        }

        int deaths = progress != null ? progress.DeathCount : 0;
        int flips = progress != null ? progress.FlipCount : 0;

        statusText.text = "C " + current + "/" + total +
                          "  Need " + required +
                          "  Flip " + cd +
                          "  D " + deaths +
                          "  F " + flips;

        if (timerText != null)
        {
            float timeLeft = timer != null ? timer.TimeLeftSeconds : boundConfig.TimeLimitSeconds;
            float elapsed = timer != null ? timer.ElapsedSeconds : Mathf.Max(0f, boundConfig.TimeLimitSeconds - timeLeft);
            RefreshCachedBestTime(false);
            string bestText = hasCachedBestTime
                ? cachedBestTimeSeconds.ToString("0.0") + "s"
                : "--";

            timerText.text = "Left: " + Mathf.CeilToInt(timeLeft) + "s   Run: " + elapsed.ToString("0.0") + "s\nBest: " + bestText;

            if (timeLeft <= 20f)
            {
                timerText.color = new Color(1f, 0.42f, 0.42f);
            }
            else if (timeLeft <= 45f)
            {
                timerText.color = new Color(1f, 0.78f, 0.38f);
            }
            else
            {
                timerText.color = new Color(0.80f, 0.95f, 1f);
            }
        }

        if (starsText != null)
        {
            int twoStar = progress != null ? progress.TwoStarThreshold : boundConfig.TwoStarThreshold;
            int threeStar = progress != null ? progress.ThreeStarThreshold : boundConfig.ThreeStarThreshold;
            int stars = PreviewStars(current, required, twoStar, threeStar);
            starsText.text = "Stars " + BuildStarString(stars) +
                             "  " + required + "/" + twoStar + "/" + threeStar;
        }
    }

    private void RefreshCachedBestTime(bool force)
    {
        if (!hasBoundSession)
        {
            hasCachedBestTime = false;
            cachedBestTimeSeconds = 0f;
            nextBestTimePollAt = -1f;
            return;
        }

        if (!force && nextBestTimePollAt >= 0f && Time.unscaledTime < nextBestTimePollAt)
        {
            return;
        }

        hasCachedBestTime = GameDirector.TryGetBestTimeSeconds(boundSettings, out cachedBestTimeSeconds);
        if (!hasCachedBestTime)
        {
            cachedBestTimeSeconds = 0f;
        }

        nextBestTimePollAt = Time.unscaledTime + 1f;
    }

    private void UpdateMessageLifetime()
    {
        if (messagePersistent || messageHideAt < 0f)
        {
            return;
        }

        if (Time.time >= messageHideAt)
        {
            if (messageText != null)
            {
                messageText.text = string.Empty;
            }

            if (messagePanel != null)
            {
                messagePanel.enabled = false;
                if (messagePanelSweep != null)
                {
                    messagePanelSweep.enabled = false;
                }
                if (messagePanelDistortion != null)
                {
                    messagePanelDistortion.enabled = false;
                }
            }

            messageHideAt = -1f;
        }
    }

    private void UpdateAlertBanner()
    {
        if (alertPanel == null || alertText == null || alertShowAt < 0f || alertDuration <= 0f)
        {
            return;
        }

        float t = (Time.time - alertShowAt) / alertDuration;
        if (t >= 1f)
        {
            alertPanel.enabled = false;
            alertText.enabled = false;
            if (alertPanelSweep != null)
            {
                alertPanelSweep.enabled = false;
            }
            if (alertPanelDistortion != null)
            {
                alertPanelDistortion.enabled = false;
            }
            alertShowAt = -1f;
            alertDuration = 0f;
            return;
        }

        float alpha;
        if (t < 0.16f)
        {
            alpha = Mathf.SmoothStep(0f, 1f, t / 0.16f);
        }
        else if (t > 0.78f)
        {
            alpha = Mathf.SmoothStep(1f, 0f, (t - 0.78f) / 0.22f);
        }
        else
        {
            alpha = 1f;
        }

        Color panelColor = alertPanel.color;
        alertPanel.color = new Color(panelColor.r, panelColor.g, panelColor.b, alpha * 0.72f);
        Color textColor = alertText.color;
        alertText.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
    }

    private void UpdateCountdownBars()
    {
        if (countdownBars == null || countdownBars.Length == 0)
        {
            return;
        }

        bool anyActive = false;
        for (int i = 0; i < countdownBars.Length; i++)
        {
            CountdownBarSlot slot = countdownBars[i];
            if (slot == null || !slot.Active)
            {
                continue;
            }

            float timeLeft = slot.EndTime - Time.time;
            if (timeLeft <= 0f)
            {
                ClearCountdownSlot(i);
                continue;
            }

            anyActive = true;
            float normalized = slot.Duration > 0.0001f
                ? Mathf.Clamp01(timeLeft / slot.Duration)
                : 0f;
            slot.Fill.rectTransform.anchorMax = new Vector2(normalized, 1f);
            slot.Fill.color = Color.Lerp(new Color(1f, 0.35f, 0.35f, 0.95f), slot.AccentColor, normalized);
            slot.Text.text = slot.Label + "  " + timeLeft.ToString("0.0") + "s";
        }

        if (countdownBarsRoot != null)
        {
            countdownBarsRoot.gameObject.SetActive(anyActive);
        }
    }

    private int FindCountdownSlotByKey(string key)
    {
        if (countdownBars == null)
        {
            return -1;
        }

        for (int i = 0; i < countdownBars.Length; i++)
        {
            CountdownBarSlot slot = countdownBars[i];
            if (slot != null && slot.Active && slot.Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindAvailableCountdownSlot()
    {
        if (countdownBars == null)
        {
            return -1;
        }

        for (int i = 0; i < countdownBars.Length; i++)
        {
            CountdownBarSlot slot = countdownBars[i];
            if (slot != null && !slot.Active)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindOldestCountdownSlot()
    {
        if (countdownBars == null || countdownBars.Length == 0)
        {
            return -1;
        }

        int selected = -1;
        float minEndTime = float.MaxValue;
        for (int i = 0; i < countdownBars.Length; i++)
        {
            CountdownBarSlot slot = countdownBars[i];
            if (slot == null || !slot.Active)
            {
                continue;
            }

            if (slot.EndTime < minEndTime)
            {
                minEndTime = slot.EndTime;
                selected = i;
            }
        }

        return selected;
    }

    private void ClearCountdownSlot(int slotIndex)
    {
        if (countdownBars == null || slotIndex < 0 || slotIndex >= countdownBars.Length)
        {
            return;
        }

        CountdownBarSlot slot = countdownBars[slotIndex];
        if (slot == null)
        {
            return;
        }

        slot.Active = false;
        slot.Key = null;
        slot.Label = null;
        slot.Duration = 0f;
        slot.EndTime = 0f;

        if (slot.Root != null)
        {
            slot.Root.enabled = false;
        }
        if (slot.Fill != null)
        {
            slot.Fill.enabled = false;
            slot.Fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        }
        if (slot.Text != null)
        {
            slot.Text.enabled = false;
            slot.Text.text = string.Empty;
        }
    }

    private void UpdateFlashOverlay()
    {
        if (flashOverlay == null || flashTimeLeft <= 0f)
        {
            if (flashOverlay != null && flashOverlay.enabled)
            {
                flashOverlay.enabled = false;
            }
            return;
        }

        flashTimeLeft = Mathf.Max(0f, flashTimeLeft - Time.deltaTime);
        float normalized = flashDuration > 0.0001f ? flashTimeLeft / flashDuration : 0f;
        float alpha = Mathf.SmoothStep(0f, flashMaxAlpha, normalized);
        flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);

        if (flashTimeLeft <= 0f)
        {
            flashOverlay.enabled = false;
        }
    }

    private void UpdateIntroCard()
    {
        if (introRoot == null || introHideAt < 0f || !introRoot.activeSelf)
        {
            return;
        }

        if (Time.time >= introHideAt)
        {
            introRoot.SetActive(false);
            introHideAt = -1f;
        }
    }

    private void UpdateCanvasAutoVisibility()
    {
        if (rootCanvas == null)
        {
            return;
        }

        if (gameplayVisible)
        {
            rootCanvas.enabled = true;
            return;
        }

        if (resultRoot != null && resultRoot.activeSelf)
        {
            rootCanvas.enabled = true;
            return;
        }

        bool transientVisible =
            (messagePanel != null && messagePanel.enabled) ||
            (alertPanel != null && alertPanel.enabled) ||
            (countdownBarsRoot != null && countdownBarsRoot.gameObject.activeSelf) ||
            (flashOverlay != null && flashOverlay.enabled) ||
            (introRoot != null && introRoot.activeSelf);

        rootCanvas.enabled = transientVisible;
    }

    private void ShowMessageInternal(string message, float duration, bool persistent)
    {
        if (messageText == null || messagePanel == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            messageText.text = string.Empty;
            messagePanel.enabled = false;
            if (messagePanelSweep != null)
            {
                messagePanelSweep.enabled = false;
            }
            if (messagePanelDistortion != null)
            {
                messagePanelDistortion.enabled = false;
            }
            messageHideAt = -1f;
            messagePersistent = false;
            return;
        }

        messageText.text = message;
        messagePanel.enabled = true;
        if (messagePanelSweep != null)
        {
            messagePanelSweep.enabled = true;
        }
        if (messagePanelDistortion != null)
        {
            messagePanelDistortion.enabled = true;
        }
        messagePersistent = persistent;
        messageHideAt = persistent ? -1f : Time.time + Mathf.Max(0.1f, duration);
    }

    private void ClearAllCountdownBars()
    {
        if (countdownBars == null)
        {
            return;
        }

        for (int i = 0; i < countdownBars.Length; i++)
        {
            ClearCountdownSlot(i);
        }

        if (countdownBarsRoot != null)
        {
            countdownBarsRoot.gameObject.SetActive(false);
        }
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("RuntimeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        scaler.dynamicPixelsPerUnit = 1.2f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        Texture2D distortionTex = CreateRefractionNoiseTexture(192, 192, 41);

        RectTransform gameplayRect = CreateUIRoot("GameplayHUD", canvasObject.transform);
        gameplayRoot = gameplayRect.gameObject;

        CreatePanel(
            "TopShade",
            gameplayRoot.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 0f),
            Vector2.zero,
            new Color(0f, 0f, 0f, 0f)
        );

        infoPanel = CreatePanel(
            "InfoPanel",
            gameplayRoot.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -18f),
            new Vector2(600f, 112f),
            new Color(0.03f, 0.06f, 0.11f, 0.44f)
        );
        infoPanel.raycastTarget = false;
        infoPanelRect = infoPanel.rectTransform;

        CreatePanel(
            "InfoPanelGlass",
            infoPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-6f, -6f),
            new Color(0.80f, 0.93f, 1f, 0.045f)
        ).raycastTarget = false;

        CreatePanel(
            "InfoPanelEdgeLeft",
            infoPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(2f, -6f),
            new Color(0.42f, 0.78f, 1f, 0.34f)
        ).raycastTarget = false;

        CreatePanel(
            "InfoPanelEdgeRight",
            infoPanel.transform,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            Vector2.zero,
            new Vector2(2f, -6f),
            new Color(0.36f, 0.70f, 0.94f, 0.22f)
        ).raycastTarget = false;

        infoPanelSweep = CreatePanel(
            "InfoPanelSweep",
            infoPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(-260f, 0f),
            new Vector2(230f, -8f),
            new Color(1f, 1f, 1f, 0.075f)
        );
        infoPanelSweep.raycastTarget = false;
        infoPanelSweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        infoPanelDistortion = CreateRawPanel(
            "InfoPanelDistortion",
            infoPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            new Color(0.86f, 0.96f, 1f, 0.052f),
            distortionTex,
            new Rect(0.08f, 0.16f, 1.9f, 1.2f)
        );
        infoPanelDistortion.raycastTarget = false;

        CreatePanel(
            "InfoPanelAccent",
            infoPanel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -2f),
            new Vector2(0f, 4f),
            new Color(0.18f, 0.65f, 0.95f, 0.85f)
        ).raycastTarget = false;

        CreatePanel(
            "InfoPanelBottomAccent",
            infoPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 2f),
            new Color(0.28f, 0.62f, 0.84f, 0.45f)
        ).raycastTarget = false;

        titleText = CreateText(
            "TitleText",
            infoPanel.transform,
            font,
            "GRAVITY SHIFT",
            20,
            TextAnchor.MiddleLeft,
            new Color(0.92f, 0.98f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(12f, -10f),
            new Vector2(-24f, 30f)
        );

        objectiveText = CreateText(
            "ObjectiveText",
            infoPanel.transform,
            font,
            "Objective: collect crystals",
            16,
            TextAnchor.MiddleLeft,
            new Color(0.82f, 1f, 0.92f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(12f, -44f),
            new Vector2(-24f, 24f)
        );

        statusText = CreateText(
            "StatusText",
            infoPanel.transform,
            font,
            string.Empty,
            16,
            TextAnchor.MiddleLeft,
            new Color(0.88f, 0.95f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(12f, 10f),
            new Vector2(-24f, 28f)
        );

        timerPanel = CreatePanel(
            "TimerPanel",
            gameplayRoot.transform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -18f),
            new Vector2(360f, 104f),
            new Color(0.03f, 0.06f, 0.11f, 0.46f)
        );
        timerPanel.raycastTarget = false;
        timerPanelRect = timerPanel.rectTransform;

        CreatePanel(
            "TimerPanelGlass",
            timerPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-6f, -6f),
            new Color(0.82f, 0.95f, 1f, 0.05f)
        ).raycastTarget = false;

        CreatePanel(
            "TimerPanelEdgeLeft",
            timerPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(2f, -6f),
            new Color(0.46f, 0.80f, 1f, 0.34f)
        ).raycastTarget = false;

        CreatePanel(
            "TimerPanelEdgeRight",
            timerPanel.transform,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            Vector2.zero,
            new Vector2(2f, -6f),
            new Color(0.40f, 0.74f, 0.98f, 0.22f)
        ).raycastTarget = false;

        timerPanelSweep = CreatePanel(
            "TimerPanelSweep",
            timerPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(-180f, 0f),
            new Vector2(160f, -8f),
            new Color(1f, 1f, 1f, 0.08f)
        );
        timerPanelSweep.raycastTarget = false;
        timerPanelSweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -9f);

        timerPanelDistortion = CreateRawPanel(
            "TimerPanelDistortion",
            timerPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            new Color(0.88f, 0.97f, 1f, 0.054f),
            distortionTex,
            new Rect(0.32f, 0.14f, 1.65f, 1.15f)
        );
        timerPanelDistortion.raycastTarget = false;

        CreatePanel(
            "TimerPanelAccent",
            timerPanel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0f, 2f),
            new Color(0.22f, 0.68f, 0.95f, 0.84f)
        ).raycastTarget = false;

        CreatePanel(
            "TimerPanelBottomAccent",
            timerPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 2f),
            new Color(0.30f, 0.66f, 0.90f, 0.42f)
        ).raycastTarget = false;

        timerText = CreateText(
            "TimerText",
            timerPanel.transform,
            font,
            "Time: --",
            23,
            TextAnchor.MiddleLeft,
            new Color(0.80f, 0.95f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(10f, -8f),
            new Vector2(-18f, 64f)
        );

        starsText = CreateText(
            "StarsText",
            timerPanel.transform,
            font,
            "Stars [---]",
            15,
            TextAnchor.MiddleLeft,
            new Color(1f, 0.90f, 0.55f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(10f, 10f),
            new Vector2(-18f, 24f)
        );

        messagePanel = CreatePanel(
            "MessagePanel",
            gameplayRoot.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -136f),
            new Vector2(600f, 38f),
            new Color(0.02f, 0.04f, 0.08f, 0.38f)
        );
        messagePanel.enabled = false;
        messagePanel.raycastTarget = false;
        messagePanelRect = messagePanel.rectTransform;

        CreatePanel(
            "MessagePanelGlass",
            messagePanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-4f, -4f),
            new Color(0.86f, 0.95f, 1f, 0.06f)
        ).raycastTarget = false;

        messagePanelSweep = CreatePanel(
            "MessagePanelSweep",
            messagePanel.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(-140f, 0f),
            new Vector2(120f, -4f),
            new Color(1f, 1f, 1f, 0.06f)
        );
        messagePanelSweep.raycastTarget = false;
        messagePanelSweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
        messagePanelSweep.enabled = false;

        messagePanelDistortion = CreateRawPanel(
            "MessagePanelDistortion",
            messagePanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-6f, -6f),
            new Color(0.90f, 0.97f, 1f, 0.05f),
            distortionTex,
            new Rect(0.18f, 0.24f, 1.45f, 1f)
        );
        messagePanelDistortion.raycastTarget = false;
        messagePanelDistortion.enabled = false;

        messageText = CreateText(
            "MessageText",
            messagePanel.transform,
            font,
            string.Empty,
            17,
            TextAnchor.MiddleLeft,
            new Color(1f, 0.95f, 0.74f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(14f, 0f),
            new Vector2(-22f, 0f)
        );

        alertPanel = CreatePanel(
            "AlertPanel",
            gameplayRoot.transform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -180f),
            new Vector2(600f, 34f),
            new Color(0.24f, 0.78f, 1f, 0.64f)
        );
        alertPanel.enabled = false;
        alertPanel.raycastTarget = false;
        alertPanelRect = alertPanel.rectTransform;

        CreatePanel(
            "AlertPanelGlass",
            alertPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-4f, -4f),
            new Color(0.88f, 0.96f, 1f, 0.07f)
        ).raycastTarget = false;

        alertPanelSweep = CreatePanel(
            "AlertPanelSweep",
            alertPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(-140f, 0f),
            new Vector2(120f, -4f),
            new Color(1f, 1f, 1f, 0.065f)
        );
        alertPanelSweep.raycastTarget = false;
        alertPanelSweep.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
        alertPanelSweep.enabled = false;

        alertPanelDistortion = CreateRawPanel(
            "AlertPanelDistortion",
            alertPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-6f, -6f),
            new Color(0.92f, 0.98f, 1f, 0.055f),
            distortionTex,
            new Rect(0.52f, 0.22f, 1.4f, 1f)
        );
        alertPanelDistortion.raycastTarget = false;
        alertPanelDistortion.enabled = false;

        alertText = CreateText(
            "AlertText",
            alertPanel.transform,
            font,
            string.Empty,
            15,
            TextAnchor.MiddleLeft,
            new Color(1f, 1f, 1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(12f, 0f),
            new Vector2(-22f, 0f)
        );
        alertText.enabled = false;

        GameObject countdownRootObject = new GameObject("CountdownBarsRoot", typeof(RectTransform));
        countdownRootObject.transform.SetParent(gameplayRoot.transform, false);
        countdownBarsRoot = countdownRootObject.GetComponent<RectTransform>();
        countdownBarsRoot.anchorMin = new Vector2(0.5f, 1f);
        countdownBarsRoot.anchorMax = new Vector2(0.5f, 1f);
        countdownBarsRoot.pivot = new Vector2(0.5f, 1f);
        countdownBarsRoot.anchoredPosition = new Vector2(0f, -18f);
        countdownBarsRoot.sizeDelta = new Vector2(560f, 90f);
        countdownBarsRoot.gameObject.SetActive(false);

        countdownBars = new CountdownBarSlot[2];
        for (int i = 0; i < countdownBars.Length; i++)
        {
            float y = -i * 40f;
            Image slotPanel = CreatePanel(
                "CountdownSlot_" + i,
                countdownBarsRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, y),
                new Vector2(560f, 36f),
                new Color(0.03f, 0.08f, 0.14f, 0.72f)
            );
            slotPanel.raycastTarget = false;
            slotPanel.enabled = false;

            Image fillTrack = CreatePanel(
                "FillTrack",
                slotPanel.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-4f, -6f),
                new Color(0.12f, 0.18f, 0.27f, 0.96f)
            );
            fillTrack.raycastTarget = false;

            Image fill = CreatePanel(
                "Fill",
                fillTrack.transform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(0f, -2f),
                new Color(0.35f, 0.78f, 1f, 0.95f)
            );
            fill.raycastTarget = false;
            fill.enabled = false;

            Text text = CreateText(
                "Text",
                slotPanel.transform,
                font,
                string.Empty,
                14,
                TextAnchor.MiddleCenter,
                new Color(0.92f, 0.98f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-12f, 0f)
            );
            text.enabled = false;

            countdownBars[i] = new CountdownBarSlot
            {
                Root = slotPanel,
                Fill = fill,
                Text = text
            };
            ClearCountdownSlot(i);
        }

        RectTransform introRect = CreateUIRoot("LevelIntroCard", canvasObject.transform);
        introRoot = introRect.gameObject;
        introRoot.SetActive(false);

        Image introPanel = CreatePanel(
            "IntroPanel",
            introRoot.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 110f),
            new Vector2(820f, 186f),
            new Color(0.02f, 0.08f, 0.12f, 0.9f)
        );
        CreatePanel(
            "IntroPanelAccent",
            introPanel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0f, 3f),
            new Color(0.32f, 0.84f, 1f, 0.9f)
        );

        introTitleText = CreateText(
            "IntroTitle",
            introPanel.transform,
            font,
            "Level Briefing",
            34,
            TextAnchor.MiddleCenter,
            new Color(0.90f, 0.98f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -32f),
            new Vector2(-40f, 44f)
        );

        introBodyText = CreateText(
            "IntroBody",
            introPanel.transform,
            font,
            string.Empty,
            22,
            TextAnchor.MiddleCenter,
            new Color(0.86f, 0.95f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -8f),
            new Vector2(-42f, 56f)
        );

        introRouteText = CreateText(
            "IntroRoute",
            introPanel.transform,
            font,
            string.Empty,
            19,
            TextAnchor.MiddleCenter,
            new Color(0.78f, 0.90f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 24f),
            new Vector2(-42f, 44f)
        );

        flashOverlay = CreatePanel(
            "FlashOverlay",
            canvasObject.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(1f, 1f, 1f, 0f)
        );
        flashOverlay.enabled = false;
        flashOverlay.raycastTarget = false;

        RectTransform resultRect = CreateUIRoot("ResultPanel", canvasObject.transform);
        resultRoot = resultRect.gameObject;
        resultRoot.SetActive(false);

        Image resultBg = CreatePanel(
            "ResultBg",
            resultRoot.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(960f, 420f),
            new Color(0.02f, 0.05f, 0.09f, 0.95f)
        );

        resultTitleText = CreateText(
            "ResultTitle",
            resultBg.transform,
            font,
            "Result",
            54,
            TextAnchor.MiddleCenter,
            new Color(0.9f, 1f, 0.93f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -72f),
            new Vector2(-40f, 90f)
        );

        resultStarsText = CreateText(
            "ResultStars",
            resultBg.transform,
            font,
            "[---]",
            52,
            TextAnchor.MiddleCenter,
            new Color(1f, 0.92f, 0.48f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -148f),
            new Vector2(-40f, 74f)
        );

        resultBodyText = CreateText(
            "ResultBody",
            resultBg.transform,
            font,
            string.Empty,
            31,
            TextAnchor.MiddleCenter,
            new Color(0.85f, 0.94f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -8f),
            new Vector2(-60f, 92f)
        );

        resultHintText = CreateText(
            "ResultHint",
            resultBg.transform,
            font,
            string.Empty,
            24,
            TextAnchor.MiddleCenter,
            new Color(0.75f, 0.88f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 52f),
            new Vector2(-60f, 70f)
        );

        resultActionButton = CreateButton(
            "ResultActionButton",
            resultBg.transform,
            font,
            "Continue",
            new Color(0.18f, 0.56f, 0.42f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 20f),
            new Vector2(300f, 68f)
        );
        resultActionButton.onClick.AddListener(HandleResultActionButtonClicked);
        resultActionButton.gameObject.SetActive(false);
        resultActionButton.interactable = false;
        resultActionButtonText = resultActionButton.GetComponentInChildren<Text>();

        SetGameplayVisible(false);
    }

    private void AnimateCardVisuals(float deltaTime)
    {
        cardVisualTime += Mathf.Max(0f, deltaTime);

        if (infoPanel != null)
        {
            float alpha = 0.42f + Mathf.Sin(cardVisualTime * 0.58f) * 0.04f;
            infoPanel.color = new Color(0.03f, 0.06f, 0.11f, Mathf.Clamp(alpha, 0.34f, 0.52f));
        }

        if (timerPanel != null)
        {
            float alpha = 0.44f + Mathf.Sin(cardVisualTime * 0.66f + 0.8f) * 0.045f;
            timerPanel.color = new Color(0.03f, 0.06f, 0.11f, Mathf.Clamp(alpha, 0.34f, 0.54f));
        }

        AnimateSweep(infoPanelSweep, infoPanelRect, 0.13f, 0f, 230f);
        AnimateSweep(timerPanelSweep, timerPanelRect, 0.17f, 0.42f, 160f);
        AnimateSweep(messagePanelSweep, messagePanelRect, 0.21f, 0.24f, 120f);
        AnimateSweep(alertPanelSweep, alertPanelRect, 0.24f, 0.58f, 120f);

        AnimateRefraction(infoPanelDistortion, infoPanelRect, 0.06f, 0.0f, 0.052f, true);
        AnimateRefraction(timerPanelDistortion, timerPanelRect, 0.08f, 0.37f, 0.054f, true);
        AnimateRefraction(messagePanelDistortion, messagePanelRect, 0.11f, 0.18f, 0.050f, messagePanel != null && messagePanel.enabled);
        AnimateRefraction(alertPanelDistortion, alertPanelRect, 0.13f, 0.51f, 0.055f, alertPanel != null && alertPanel.enabled);
    }

    private void AnimateSweep(Image sweep, RectTransform panelRect, float speed, float phaseOffset, float sweepWidth)
    {
        if (sweep == null || panelRect == null)
        {
            return;
        }

        float width = Mathf.Max(200f, panelRect.rect.width);
        float t = Mathf.Repeat(cardVisualTime * speed + phaseOffset, 1f);
        float x = Mathf.Lerp(-sweepWidth - 80f, width + 100f, t);
        RectTransform rect = sweep.rectTransform;
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);

        float alphaPulse = 0.76f + Mathf.Sin(cardVisualTime * (speed * 12f) + phaseOffset * 6f) * 0.24f;
        float baseAlpha = sweep == infoPanelSweep
            ? 0.075f
            : sweep == timerPanelSweep
                ? 0.08f
                : sweep == messagePanelSweep
                    ? 0.06f
                    : 0.065f;
        sweep.color = new Color(1f, 1f, 1f, baseAlpha * Mathf.Clamp01(alphaPulse));
    }

    private void AnimateRefraction(
        RawImage distortion,
        RectTransform panelRect,
        float speed,
        float phase,
        float baseAlpha,
        bool active)
    {
        if (distortion == null || panelRect == null)
        {
            return;
        }

        distortion.enabled = active;
        if (!active)
        {
            return;
        }

        float t = cardVisualTime * speed + phase;
        float u = Mathf.Repeat(0.08f + t * 0.11f, 1f);
        float v = Mathf.Repeat(0.14f + t * 0.09f, 1f);
        distortion.uvRect = new Rect(u, v, 1.9f, 1.2f);

        float alphaPulse = 0.72f + Mathf.Sin(cardVisualTime * (speed * 24f) + phase * 7f) * 0.28f;
        float alpha = baseAlpha * Mathf.Clamp01(alphaPulse);
        distortion.color = new Color(0.88f, 0.96f, 1f, alpha);

        float jitterX = Mathf.Sin(cardVisualTime * (speed * 31f) + phase * 5f) * 0.7f;
        float jitterY = Mathf.Cos(cardVisualTime * (speed * 27f) + phase * 3.2f) * 0.45f;
        distortion.rectTransform.anchoredPosition = new Vector2(jitterX, jitterY);
    }

    private void ApplyResponsiveLayout()
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        if (width == lastScreenWidth && height == lastScreenHeight)
        {
            return;
        }

        lastScreenWidth = width;
        lastScreenHeight = height;

        Rect safe = Screen.safeArea;
        float leftInset = Mathf.Max(0f, safe.xMin);
        float rightInset = Mathf.Max(0f, width - safe.xMax);
        float topInset = Mathf.Max(0f, height - safe.yMax);
        float margin = height < 900 ? 12f : 18f;
        float uiScale = Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.74f, 1.08f);

        float leftCardWidth = Mathf.Clamp(width * 0.29f, 320f, 620f);
        float timerCardWidth = Mathf.Clamp(width * 0.18f, 220f, 360f);
        float messageYOffset = height < 760f ? 124f : 136f;
        float alertYOffset = height < 760f ? 164f : 180f;
        float infoHeight = Mathf.Lerp(102f, 112f, uiScale);
        float timerHeight = Mathf.Lerp(78f, 86f, uiScale);
        float messageHeight = Mathf.Lerp(34f, 38f, uiScale);
        float alertHeight = Mathf.Lerp(30f, 34f, uiScale);

        SetFontSize(titleText, Mathf.RoundToInt(20f * uiScale));
        SetFontSize(objectiveText, Mathf.RoundToInt(16f * uiScale));
        SetFontSize(statusText, Mathf.RoundToInt(16f * uiScale));
        SetFontSize(timerText, Mathf.RoundToInt(30f * uiScale));
        SetFontSize(starsText, Mathf.RoundToInt(15f * uiScale));
        SetFontSize(messageText, Mathf.RoundToInt(17f * uiScale));
        SetFontSize(alertText, Mathf.RoundToInt(15f * uiScale));

        if (infoPanelRect != null)
        {
            infoPanelRect.anchoredPosition = new Vector2(leftInset + margin, -(topInset + margin));
            infoPanelRect.sizeDelta = new Vector2(leftCardWidth, infoHeight);
        }

        if (timerPanelRect != null)
        {
            timerPanelRect.anchoredPosition = new Vector2(-(rightInset + margin), -(topInset + margin));
            timerPanelRect.sizeDelta = new Vector2(timerCardWidth, timerHeight);
        }

        if (messagePanelRect != null)
        {
            messagePanelRect.anchoredPosition = new Vector2(leftInset + margin, -(topInset + messageYOffset));
            messagePanelRect.sizeDelta = new Vector2(leftCardWidth, messageHeight);
        }

        if (alertPanelRect != null)
        {
            alertPanelRect.anchoredPosition = new Vector2(leftInset + margin, -(topInset + alertYOffset));
            alertPanelRect.sizeDelta = new Vector2(leftCardWidth, alertHeight);
        }

        if (countdownBarsRoot != null)
        {
            float countdownWidth = Mathf.Clamp(width * 0.30f, 320f, 620f);
            countdownBarsRoot.anchoredPosition = new Vector2(0f, -(topInset + margin));
            countdownBarsRoot.sizeDelta = new Vector2(countdownWidth, 90f);

            if (countdownBars != null)
            {
                for (int i = 0; i < countdownBars.Length; i++)
                {
                    CountdownBarSlot slot = countdownBars[i];
                    if (slot == null || slot.Root == null)
                    {
                        continue;
                    }

                    slot.Root.rectTransform.sizeDelta = new Vector2(countdownWidth, 36f);
                    if (slot.Text != null)
                    {
                        slot.Text.fontSize = Mathf.RoundToInt(14f * uiScale);
                    }
                }
            }
        }
    }

    private static void SetFontSize(Text text, int size)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = Mathf.Clamp(size, 12, 64);
    }

    private Texture2D CreateRefractionNoiseTexture(int width, int height, int seed)
    {
        width = Mathf.Max(64, width);
        height = Mathf.Max(64, height);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        float sx = seed * 9.37f + 2.11f;
        float sy = seed * 5.73f + 1.87f;
        float invW = 1f / Mathf.Max(1f, width - 1f);
        float invH = 1f / Mathf.Max(1f, height - 1f);

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            float ny = y * invH;
            for (int x = 0; x < width; x++)
            {
                float nx = x * invW;
                float low = Mathf.PerlinNoise(nx * 4.8f + sx, ny * 4.8f + sy);
                float mid = Mathf.PerlinNoise(nx * 9.3f + sx * 0.73f, ny * 9.3f + sy * 0.77f);
                float hi = Mathf.PerlinNoise(nx * 17.9f + sx * 0.41f, ny * 17.9f + sy * 0.45f);
                float n = low * 0.52f + mid * 0.33f + hi * 0.15f;
                n = Mathf.SmoothStep(0f, 1f, n);
                pixels[row + x] = new Color(n, n, n, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        runtimeTextures.Add(texture);
        return texture;
    }

    private static RawImage CreateRawPanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Texture texture,
        Rect uvRect)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        RawImage image = panelObject.GetComponent<RawImage>();
        image.color = color;
        image.texture = texture;
        image.uvRect = uvRect;
        return image;
    }

    private void DisposeRuntimeTextures()
    {
        for (int i = 0; i < runtimeTextures.Count; i++)
        {
            Texture2D tex = runtimeTextures[i];
            if (tex != null)
            {
                Destroy(tex);
            }
        }

        runtimeTextures.Clear();
    }

    private static RectTransform CreateUIRoot(string name, Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static string ModeLabel(GameMode mode)
    {
        return mode == GameMode.Adventure ? "Adventure" : "Challenge";
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

    private static string ThemeLabel(PlayerVisualTheme theme)
    {
        switch (theme)
        {
            case PlayerVisualTheme.Mechanical:
                return "Mech";
            case PlayerVisualTheme.Minimal:
                return "Minimal";
            default:
                return "Astronaut";
        }
    }

    private static string PaletteLabel(PlayerColorPalette palette)
    {
        switch (palette)
        {
            case PlayerColorPalette.Ember:
                return "Ember";
            case PlayerColorPalette.Mono:
                return "Mono";
            default:
                return "Neon";
        }
    }

    private static int PreviewStars(int crystals, int required, int twoStar, int threeStar)
    {
        if (crystals >= threeStar)
        {
            return 3;
        }

        if (crystals >= twoStar)
        {
            return 2;
        }

        if (crystals >= required)
        {
            return 1;
        }

        return 0;
    }

    private static string BuildStarString(int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);
        char[] chars = new char[3];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = i < stars ? '*' : '-';
        }

        return "[" + new string(chars) + "]";
    }

    private static string BuildLevelGoalText(int level, int oneStar, int twoStar, int threeStar, string objectiveSummary)
    {
        string stageGoal;
        switch (Mathf.Clamp(level, 1, 5))
        {
            case 1:
                stageGoal = "Tutorial lane: movement, jump timing, and first gravity-flip switch.";
                break;
            case 2:
                stageGoal = "Learn core loop: flip, chain A->B, collect, unlock.";
                break;
            case 3:
                stageGoal = "Master trampoline route, moving platform timing, dual rhythm windows (A->B), and rotating hazards.";
                break;
            case 4:
                stageGoal = "Final route: denser hazards, tighter timing, strict resource management.";
                break;
            default:
                stageGoal = "Boss route: compound mechanism chain with gravity anchor pressure and final gate rush.";
                break;
        }

        if (!string.IsNullOrWhiteSpace(objectiveSummary))
        {
            stageGoal = objectiveSummary.Trim();
        }

        return stageGoal + "  Stars: [*--] " + oneStar +
               "  [**-] " + twoStar +
               "  [***] " + threeStar;
    }

    private static string BuildLevelRouteAdvice(int level)
    {
        switch (Mathf.Clamp(level, 1, 5))
        {
            case 1:
                return "Suggested route: stay center lane, hit the flip switch, open gate, then exit.";
            case 2:
                return "Suggested route: clear chain first, then high platform crystals.";
            case 3:
                return "Suggested route: trigger A, ride mover, clear A gate, trigger B, rush B gate, then clear rotors.";
            case 4:
                return "Suggested route: secure checkpoints and save flips for final section.";
            default:
                return "Suggested route: stabilize anchor lane, clear boss A->B chain, then sprint to exit gate.";
        }
    }

    private static Image CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        string value,
        int fontSize,
        TextAnchor align,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;
        text.raycastTarget = false;
        AddSoftShadow(text, new Color(0f, 0f, 0f, 0.56f), new Vector2(0f, -1.2f));
        AddSubtleOutline(text, new Color(0f, 0f, 0f, 0.24f), new Vector2(0.6f, -0.6f));
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Font font,
        string title,
        Color background,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        Image image = CreatePanel(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, background);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = background * 1.08f;
        colors.pressedColor = background * 0.84f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(background.r, background.g, background.b, 0.3f);
        button.colors = colors;

        CreatePanel(
            name + "_TopGloss",
            image.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0f, 2f),
            new Color(1f, 1f, 1f, 0.22f)
        ).raycastTarget = false;

        Text label = CreateText(
            name + "_Label",
            image.transform,
            font,
            title,
            27,
            TextAnchor.MiddleCenter,
            Color.white,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
        AddSoftShadow(label, new Color(0f, 0f, 0f, 0.62f), new Vector2(0f, -1.4f));
        return button;
    }

    private static void AddSoftShadow(Graphic graphic, Color color, Vector2 offset)
    {
        if (graphic == null)
        {
            return;
        }

        Shadow shadow = graphic.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = graphic.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = color;
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }

    private static void AddSubtleOutline(Graphic graphic, Color color, Vector2 distance)
    {
        if (graphic == null)
        {
            return;
        }

        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = graphic.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }
}
