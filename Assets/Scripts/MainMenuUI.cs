using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public event Action<GameRunSettings> OnStartRequested;

    private Canvas rootCanvas;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private CanvasGroup settingsPanelGroup;

    private Text levelValueText;
    private Text modeValueText;
    private Text difficultyValueText;
    private Text themeValueText;
    private Text paletteValueText;
    private Text sensitivityValueText;
    private Text fovValueText;
    private Text cameraSmoothnessValueText;
    private Text autoRecenterValueText;
    private Text volumeValueText;
    private Text feedbackValueText;
    private Text shakeValueText;
    private Text atmosphereValueText;
    private Text titleText;
    private Text subtitleText;
    private Text summaryText;

    private Slider sensitivitySlider;
    private Slider fovSlider;
    private Slider cameraSmoothnessSlider;
    private Slider autoRecenterSlider;
    private Slider volumeSlider;
    private Slider feedbackSlider;
    private Button shakeToggleButton;
    private Button atmosphereToggleButton;
    private Button levelMinusButton;
    private Button levelPlusButton;

    private Image dimBackground;
    private Image skyTopTint;
    private Image skyBottomFog;
    private Image skyColorWash;
    private Image horizonGlow;
    private Image sunHalo;
    private Image sunCore;
    private Image cloudMistBand;
    private Image grassBand;
    private Image soilBand;
    private Image leftVignette;
    private Image rightVignette;
    private Image mainPanelShadow;
    private Image mainPanelGlow;
    private Image mainPanelGlass;
    private Image mainPanelTint;
    private RectTransform mainPanelRect;
    private RectTransform mainPanelShadowRect;
    private RectTransform settingsPanelRect;
    private RectTransform rowsRootRect;
    private RectTransform summaryPanelRect;
    private readonly List<Button> selectorButtons = new List<Button>();
    private readonly List<Image> rowCards = new List<Image>();
    private Button startActionButton;
    private Button settingsActionButton;
    private Button previewActionButton;
    private RectTransform[] ambientRects;
    private Vector2[] ambientBasePositions;
    private RectTransform[] dustRects;
    private Image[] dustImages;
    private Vector2[] dustBasePositions;
    private float[] dustSpeeds;
    private float[] dustAmplitudes;
    private float[] dustPhases;
    private float[] dustBaseAlphas;
    private RawImage cloudNoiseFarOverlay;
    private RawImage cloudNoiseNearOverlay;
    private RawImage filmGrainOverlay;
    private Texture2D ambientBlobTexture;
    private RawImage[] panelEdgeClouds;
    private Vector2[] panelEdgeCloudBasePos;
    private float[] panelEdgeCloudSpeeds;
    private float[] panelEdgeCloudAmplitudes;
    private float[] panelEdgeCloudPhases;
    private float[] panelEdgeCloudBaseAlphas;
    private readonly List<Texture2D> runtimeOverlayTextures = new List<Texture2D>();
    private RectTransform[] cloudRects;
    private Image[] cloudImages;
    private Vector2[] cloudBasePositions;
    private float[] cloudSpeeds;
    private float[] cloudAmplitudes;
    private float[] cloudPhases;
    private float[] cloudBaseAlphas;
    private RectTransform[] mountainRects;
    private Vector2[] mountainBasePositions;
    private float[] mountainShiftStrengths;
    private float[] mountainPhases;
    private float uiAnimTime;
    private float previewShakeTimeLeft;
    private float previewShakeDuration;
    private float previewShakeAmplitude;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private Vector2 menuPanelBasePos = new Vector2(0f, 10f);
    private Vector2 menuPanelShadowBasePos = new Vector2(0f, 4f);
    private bool settingsPanelVisible;
    private float settingsPanelAlpha;
    private float levelLockHintLeft;

    private int levelIndex = 1;
    private int maxUnlockedLevel = 1;
    private int maxAvailableLevel = 5;
    private GameMode mode = GameMode.Adventure;
    private GameDifficulty difficulty = GameDifficulty.Normal;
    private PlayerVisualTheme visualTheme = PlayerVisualTheme.Astronaut;
    private PlayerColorPalette colorPalette = PlayerColorPalette.Neon;
    private MenuAtmospherePreset atmospherePreset = MenuAtmospherePreset.Noon;
    private bool cameraShakeEnabled = true;
    private MotionIntensityPreset motionPreset = MotionIntensityPreset.Normal;
    private MotionStyleProfile motionStyle = MotionStyleProfile.Stable;

    private void Awake()
    {
        BuildUI();
        RefreshLabels();
    }

    private void Update()
    {
        if (levelLockHintLeft > 0f)
        {
            levelLockHintLeft = Mathf.Max(0f, levelLockHintLeft - Time.unscaledDeltaTime);
        }

        ApplyResponsiveLayout();
        AnimateVisuals(Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        DisposeRuntimeOverlays();
    }

    public void Show(bool visible)
    {
        if (rootCanvas != null)
        {
            rootCanvas.enabled = visible;
        }

        if (visible)
        {
            SetSettingsPanelVisible(false, true);
        }
    }

    public void SetMaxUnlockedLevel(int maxLevel)
    {
        SetLevelBounds(maxLevel, Mathf.Max(maxAvailableLevel, maxLevel));
    }

    public void SetLevelBounds(int unlockedLevel, int availableLevelCount)
    {
        maxAvailableLevel = Mathf.Clamp(availableLevelCount, 1, 16);
        maxUnlockedLevel = Mathf.Clamp(unlockedLevel, 1, maxAvailableLevel);
        levelIndex = Mathf.Clamp(levelIndex, 1, maxUnlockedLevel);
        RefreshLabels();
    }

    public void ApplySettings(GameRunSettings settings)
    {
        levelIndex = Mathf.Clamp(settings.LevelIndex, 1, maxUnlockedLevel);
        mode = (GameMode)Mathf.Clamp((int)settings.Mode, 0, 1);
        difficulty = (GameDifficulty)Mathf.Clamp((int)settings.Difficulty, 0, 2);
        visualTheme = (PlayerVisualTheme)Mathf.Clamp((int)settings.VisualTheme, 0, 2);
        colorPalette = (PlayerColorPalette)Mathf.Clamp((int)settings.ColorPalette, 0, 2);
        atmospherePreset = (MenuAtmospherePreset)Mathf.Clamp((int)settings.AtmospherePreset, 0, 2);
        cameraShakeEnabled = settings.CameraShakeEnabled;
        motionPreset = (MotionIntensityPreset)Mathf.Clamp((int)settings.MotionPreset, 0, 2);
        motionStyle = (MotionStyleProfile)Mathf.Clamp((int)settings.MotionStyle, 0, 2);

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = Mathf.Clamp(settings.MouseSensitivity, 0.4f, 3.2f);
        }
        if (fovSlider != null)
        {
            fovSlider.value = Mathf.Clamp(settings.CameraFov <= 1f ? 66f : settings.CameraFov, 55f, 95f);
        }
        if (cameraSmoothnessSlider != null)
        {
            cameraSmoothnessSlider.value = Mathf.Clamp01(settings.CameraSmoothness);
        }
        if (autoRecenterSlider != null)
        {
            autoRecenterSlider.value = Mathf.Clamp(settings.CameraAutoRecenterSpeed, 0f, 2f);
        }
        if (volumeSlider != null)
        {
            volumeSlider.value = Mathf.Clamp01(settings.MasterVolume);
        }
        if (feedbackSlider != null)
        {
            feedbackSlider.value = Mathf.Clamp(settings.FeedbackIntensity, 0f, 2f);
        }

        RefreshLabels();
    }

    public GameRunSettings CurrentSettings
    {
        get
        {
            return new GameRunSettings
            {
                LevelIndex = levelIndex,
                Difficulty = difficulty,
                Mode = mode,
                VisualTheme = visualTheme,
                ColorPalette = colorPalette,
                AtmospherePreset = atmospherePreset,
                FeedbackIntensity = feedbackSlider != null ? feedbackSlider.value : 1f,
                CameraShakeEnabled = cameraShakeEnabled,
                MouseSensitivity = sensitivitySlider != null ? sensitivitySlider.value : 1.45f,
                CameraFov = fovSlider != null ? fovSlider.value : 66f,
                CameraSmoothness = cameraSmoothnessSlider != null ? cameraSmoothnessSlider.value : 0.56f,
                CameraAutoRecenterSpeed = autoRecenterSlider != null ? autoRecenterSlider.value : 0.22f,
                MasterVolume = volumeSlider != null ? volumeSlider.value : 1f,
                MotionPreset = motionPreset,
                MotionStyle = motionStyle
            };
        }
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 300;
        rootCanvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
        scaler.dynamicPixelsPerUnit = 2.0f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        dimBackground = CreatePanel(
            "DimBackground",
            canvasObject.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.03f, 0.05f, 0.08f, 0.84f)
        );
        dimBackground.raycastTarget = true;
        BuildAtmosphereBackground(canvasObject.transform);
        BuildProceduralLensOverlays(canvasObject.transform);

        ambientRects = new RectTransform[3];
        ambientBasePositions = new Vector2[3];
        CreateAmbientShape(
            canvasObject.transform,
            0,
            new Vector2(0.14f, 0.16f),
            new Vector2(560f, 560f),
            24f,
            new Color(0.14f, 0.62f, 0.86f, 0.07f)
        );
        CreateAmbientShape(
            canvasObject.transform,
            1,
            new Vector2(0.84f, 0.84f),
            new Vector2(620f, 620f),
            -16f,
            new Color(0.20f, 0.82f, 0.52f, 0.06f)
        );
        CreateAmbientShape(
            canvasObject.transform,
            2,
            new Vector2(0.82f, 0.22f),
            new Vector2(420f, 420f),
            12f,
            new Color(0.98f, 0.56f, 0.30f, 0.05f)
        );

        mainPanelShadow = CreatePanel(
            "MainPanelShadow",
            canvasObject.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f),
            new Vector2(1270f, 840f),
            new Color(0f, 0f, 0f, 0.34f)
        );
        mainPanelShadow.raycastTarget = false;
        mainPanelShadowRect = mainPanelShadow.rectTransform;

        Image mainPanelImage = CreatePanel(
            "MainPanel",
            canvasObject.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 10f),
            new Vector2(1200f, 792f),
            new Color(0.04f, 0.10f, 0.16f, 0.95f)
        );
        mainPanelImage.raycastTarget = false;
        mainPanel = mainPanelImage.gameObject;
        mainPanelRect = mainPanel.GetComponent<RectTransform>();

        CreatePanel(
            "MainPanelBorder",
            mainPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-6f, -6f),
            new Color(0.56f, 0.82f, 0.98f, 0.14f)
        ).raycastTarget = false;

        mainPanelGlass = CreatePanel(
            "MainPanelGlass",
            mainPanel.transform,
            new Vector2(0f, 0.58f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.86f, 0.94f, 1f, 0.038f)
        );
        mainPanelGlass.raycastTarget = false;

        CreatePanel(
            "MainPanelGlassEdge",
            mainPanel.transform,
            new Vector2(0f, 0.58f),
            new Vector2(1f, 0.58f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -1f),
            new Vector2(0f, 2f),
            new Color(0.60f, 0.84f, 1f, 0.18f)
        ).raycastTarget = false;

        mainPanelTint = CreatePanel(
            "MainPanelTint",
            mainPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            new Color(0.22f, 0.46f, 0.70f, 0.032f)
        );
        mainPanelTint.raycastTarget = false;

        mainPanelGlow = CreatePanel(
            "MainPanelGlow",
            mainPanel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -3f),
            new Vector2(0f, 6f),
            new Color(0.28f, 0.78f, 1f, 0.8f)
        );
        mainPanelGlow.raycastTarget = false;

        Image bottomLine = CreatePanel(
            "MainPanelBottomLine",
            mainPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 3f),
            new Vector2(0f, 3f),
            new Color(0.16f, 0.35f, 0.55f, 0.75f)
        );
        bottomLine.raycastTarget = false;

        titleText = CreateText(
            "TitleText",
            mainPanel.transform,
            font,
            "GRAVITY SHIFT",
            62,
            TextAnchor.MiddleCenter,
            new Color(0.92f, 0.98f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -58f),
            new Vector2(980f, 84f)
        );

        subtitleText = CreateText(
            "SubtitleText",
            mainPanel.transform,
            font,
            "Manipulate gravity, collect crystals, reach the exit.",
            20,
            TextAnchor.MiddleCenter,
            new Color(0.72f, 0.90f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -118f),
            new Vector2(980f, 62f)
        );

        rowCards.Clear();
        selectorButtons.Clear();

        rowsRootRect = CreateEmptyRect(
            "RowsRoot",
            mainPanel.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -8f),
            new Vector2(996f, 430f)
        );

        float[] rowGuideYs = { -26f, -100f, -174f, -248f, -322f };
        for (int i = 0; i < rowGuideYs.Length; i++)
        {
            Image rowCard = CreatePanel(
                "RowCard_" + i,
                rowsRootRect,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, rowGuideYs[i] - 2f),
                new Vector2(-6f, 64f),
                i % 2 == 0
                    ? new Color(0.11f, 0.18f, 0.27f, 0.24f)
                    : new Color(0.10f, 0.16f, 0.24f, 0.20f)
            );
            rowCard.raycastTarget = false;
            rowCards.Add(rowCard);

            CreatePanel(
                "RowCardAccent_" + i,
                rowCard.transform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(4f, -4f),
                new Color(0.28f, 0.72f, 0.96f, 0.34f)
            ).raycastTarget = false;
        }

        levelValueText = CreateSelectorRow(rowsRootRect, font, "Level", -26f, () => ChangeLevel(-1), () => ChangeLevel(1));
        modeValueText = CreateSelectorRow(rowsRootRect, font, "Mode", -100f, () => ChangeMode(-1), () => ChangeMode(1));
        difficultyValueText = CreateSelectorRow(rowsRootRect, font, "Difficulty", -174f, () => ChangeDifficulty(-1), () => ChangeDifficulty(1));
        themeValueText = CreateSelectorRow(rowsRootRect, font, "Theme", -248f, () => ChangeTheme(-1), () => ChangeTheme(1));
        paletteValueText = CreateSelectorRow(rowsRootRect, font, "Palette", -322f, () => ChangePalette(-1), () => ChangePalette(1));
        levelMinusButton = rowsRootRect.Find("Level_Minus") != null
            ? rowsRootRect.Find("Level_Minus").GetComponent<Button>()
            : null;
        levelPlusButton = rowsRootRect.Find("Level_Plus") != null
            ? rowsRootRect.Find("Level_Plus").GetComponent<Button>()
            : null;

        Button startButton = CreateButton(
            "StartButton",
            mainPanel.transform,
            font,
            "Start",
            new Color(0.13f, 0.55f, 0.39f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-156f, 24f),
            new Vector2(282f, 74f)
        );
        startButton.onClick.AddListener(HandleStartClicked);
        startActionButton = startButton;

        Button settingsButton = CreateButton(
            "SettingsButton",
            mainPanel.transform,
            font,
            "Settings",
            new Color(0.20f, 0.36f, 0.62f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(156f, 24f),
            new Vector2(282f, 74f)
        );
        settingsButton.onClick.AddListener(ToggleSettingsPanel);
        settingsActionButton = settingsButton;

        Image summaryPanel = CreatePanel(
            "SummaryPanel",
            mainPanel.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 112f),
            new Vector2(1038f, 68f),
            new Color(0.02f, 0.07f, 0.12f, 0.84f)
        );
        summaryPanelRect = summaryPanel.rectTransform;
        Image summaryAccent = CreatePanel(
            "SummaryPanelAccent",
            summaryPanel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0f, 2f),
            new Color(0.36f, 0.72f, 0.96f, 0.88f)
        );
        summaryAccent.raycastTarget = false;

        summaryText = CreateText(
            "SummaryText",
            summaryPanel.transform,
            font,
            string.Empty,
            21,
            TextAnchor.MiddleCenter,
            new Color(0.88f, 0.96f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-24f, 0f)
        );
        summaryPanel.raycastTarget = false;

        settingsPanel = CreatePanel(
            "SettingsPanel",
            mainPanel.transform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 288f),
            new Vector2(836f, 486f),
            new Color(0.03f, 0.07f, 0.12f, 0.95f)
        ).gameObject;
        settingsPanelRect = settingsPanel.GetComponent<RectTransform>();

        Image settingsAccent = CreatePanel(
            "SettingsAccent",
            settingsPanel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0f, 2f),
            new Color(0.27f, 0.70f, 0.95f, 0.8f)
        );
        settingsAccent.raycastTarget = false;

        CreatePanel(
            "SettingsGlow",
            settingsPanel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            new Color(0.22f, 0.70f, 1f, 0.06f)
        ).raycastTarget = false;

        CreateText(
            "SettingsLabel",
            settingsPanel.transform,
            font,
            "Settings",
            26,
            TextAnchor.MiddleLeft,
            new Color(0.88f, 0.94f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(18f, -22f),
            new Vector2(180f, 34f)
        );

        atmosphereToggleButton = CreateToggleRow(
            settingsPanel.transform,
            font,
            "Sky Preset",
            out atmosphereValueText,
            new Vector2(0.5f, 0.82f),
            CycleAtmospherePreset
        );

        sensitivitySlider = CreateSliderRow(
            settingsPanel.transform,
            font,
            "Mouse Sense",
            "1.45",
            out sensitivityValueText,
            new Vector2(0.5f, 0.70f),
            0.4f,
            3.2f,
            1.45f
        );
        sensitivitySlider.onValueChanged.AddListener(_ => RefreshLabels());

        fovSlider = CreateSliderRow(
            settingsPanel.transform,
            font,
            "Camera FOV",
            "66",
            out fovValueText,
            new Vector2(0.5f, 0.58f),
            55f,
            95f,
            66f
        );
        fovSlider.onValueChanged.AddListener(_ => RefreshLabels());

        cameraSmoothnessSlider = CreateSliderRow(
            settingsPanel.transform,
            font,
            "Camera Smooth",
            "0.56",
            out cameraSmoothnessValueText,
            new Vector2(0.5f, 0.46f),
            0f,
            1f,
            0.56f
        );
        cameraSmoothnessSlider.onValueChanged.AddListener(_ => RefreshLabels());

        autoRecenterSlider = CreateSliderRow(
            settingsPanel.transform,
            font,
            "Auto Recenter",
            "0.22",
            out autoRecenterValueText,
            new Vector2(0.5f, 0.34f),
            0f,
            2f,
            0.22f
        );
        autoRecenterSlider.onValueChanged.AddListener(_ => RefreshLabels());

        volumeSlider = CreateSliderRow(
            settingsPanel.transform,
            font,
            "Master Volume",
            "1.00",
            out volumeValueText,
            new Vector2(0.5f, 0.24f),
            0f,
            1f,
            1f
        );
        volumeSlider.onValueChanged.AddListener(_ => RefreshLabels());

        feedbackSlider = CreateSliderRow(
            settingsPanel.transform,
            font,
            "Feedback",
            "1.00",
            out feedbackValueText,
            new Vector2(0.5f, 0.14f),
            0f,
            2f,
            1f
        );
        feedbackSlider.onValueChanged.AddListener(_ => RefreshLabels());

        shakeToggleButton = CreateToggleRow(
            settingsPanel.transform,
            font,
            "Camera Shake",
            out shakeValueText,
            new Vector2(0.5f, 0.06f),
            ToggleCameraShake
        );

        Button previewFeedbackButton = CreateButton(
            "PreviewFeedbackButton",
            settingsPanel.transform,
            font,
            "Preview Feedback",
            new Color(0.20f, 0.44f, 0.72f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 10f),
            new Vector2(290f, 40f)
        );
        previewFeedbackButton.onClick.AddListener(PreviewFeedback);
        previewActionButton = previewFeedbackButton;

        BuildPanelEdgeClouds(mainPanel.transform);

        selectorButtons.Add(startButton);
        selectorButtons.Add(settingsButton);
        selectorButtons.Add(previewFeedbackButton);
        selectorButtons.Add(shakeToggleButton);
        selectorButtons.Add(atmosphereToggleButton);

        settingsPanelGroup = settingsPanel.GetComponent<CanvasGroup>();
        if (settingsPanelGroup == null)
        {
            settingsPanelGroup = settingsPanel.AddComponent<CanvasGroup>();
        }
        settingsPanelVisible = false;
        settingsPanelAlpha = 0f;
        SetSettingsPanelVisible(false, true);

        if (filmGrainOverlay != null)
        {
            filmGrainOverlay.transform.SetSiblingIndex(2);
        }

        ConfigureInteractionRaycasts();
        ApplyButtonTheme();
    }

    private void HandleStartClicked()
    {
        OnStartRequested?.Invoke(CurrentSettings);
    }

    private void ToggleSettingsPanel()
    {
        if (settingsPanel == null || settingsPanelGroup == null)
        {
            return;
        }

        SetSettingsPanelVisible(!settingsPanelVisible);
    }

    private void SetSettingsPanelVisible(bool visible, bool immediate = false)
    {
        settingsPanelVisible = visible;
        if (settingsPanelGroup == null)
        {
            return;
        }

        if (immediate)
        {
            settingsPanelAlpha = visible ? 1f : 0f;
        }

        settingsPanelGroup.alpha = settingsPanelAlpha;
        settingsPanelGroup.interactable = visible && settingsPanelAlpha > 0.98f;
        settingsPanelGroup.blocksRaycasts = settingsPanelAlpha > 0.05f;
    }

    private void UpdateSettingsPanelTransition(float deltaTime)
    {
        if (settingsPanelGroup == null)
        {
            return;
        }

        float target = settingsPanelVisible ? 1f : 0f;
        float speed = settingsPanelVisible ? 8.4f : 9.2f;
        settingsPanelAlpha = Mathf.MoveTowards(settingsPanelAlpha, target, Mathf.Max(0f, deltaTime) * speed);
        settingsPanelGroup.alpha = settingsPanelAlpha;
        settingsPanelGroup.interactable = settingsPanelVisible && settingsPanelAlpha > 0.98f;
        settingsPanelGroup.blocksRaycasts = settingsPanelAlpha > 0.05f;
    }

    private void TriggerLevelLockFeedback()
    {
        levelLockHintLeft = 1.35f;
        previewShakeDuration = 0.17f;
        previewShakeTimeLeft = previewShakeDuration;
        previewShakeAmplitude = Mathf.Max(previewShakeAmplitude, 4.2f);
    }

    private void ToggleCameraShake()
    {
        cameraShakeEnabled = !cameraShakeEnabled;
        RefreshLabels();
    }

    private void CycleAtmospherePreset()
    {
        int value = (int)atmospherePreset + 1;
        if (value > 2)
        {
            value = 0;
        }

        atmospherePreset = (MenuAtmospherePreset)value;
        RefreshLabels();
    }

    private void PreviewFeedback()
    {
        GameFeedback feedback = GameFeedback.Instance;
        if (feedback != null)
        {
            float intensity = feedbackSlider != null ? feedbackSlider.value : 1f;
            feedback.Configure(intensity, cameraShakeEnabled);
            feedback.PlayPreview(PaletteAccentColor(colorPalette));
        }

        if (cameraShakeEnabled)
        {
            float intensity = feedbackSlider != null ? feedbackSlider.value : 1f;
            previewShakeDuration = 0.22f;
            previewShakeTimeLeft = previewShakeDuration;
            previewShakeAmplitude = Mathf.Lerp(2f, 14f, Mathf.Clamp01(intensity / 2f));
        }
    }

    private void ChangeLevel(int delta)
    {
        if (maxUnlockedLevel <= 1)
        {
            levelIndex = 1;
            TriggerLevelLockFeedback();
            RefreshLabels();
            return;
        }

        levelIndex += delta;
        if (levelIndex < 1)
        {
            levelIndex = maxUnlockedLevel;
        }
        else if (levelIndex > maxUnlockedLevel)
        {
            levelIndex = 1;
        }
        RefreshLabels();
    }

    private void ChangeMode(int delta)
    {
        int value = (int)mode + delta;
        if (value < 0)
        {
            value = 1;
        }
        else if (value > 1)
        {
            value = 0;
        }
        mode = (GameMode)value;
        RefreshLabels();
    }

    private void ChangeDifficulty(int delta)
    {
        int value = (int)difficulty + delta;
        if (value < 0)
        {
            value = 2;
        }
        else if (value > 2)
        {
            value = 0;
        }
        difficulty = (GameDifficulty)value;
        RefreshLabels();
    }

    private void ChangeTheme(int delta)
    {
        int value = (int)visualTheme + delta;
        if (value < 0)
        {
            value = 2;
        }
        else if (value > 2)
        {
            value = 0;
        }
        visualTheme = (PlayerVisualTheme)value;
        RefreshLabels();
    }

    private void ChangePalette(int delta)
    {
        int value = (int)colorPalette + delta;
        if (value < 0)
        {
            value = 2;
        }
        else if (value > 2)
        {
            value = 0;
        }
        colorPalette = (PlayerColorPalette)value;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (levelValueText != null)
        {
            levelValueText.text = levelIndex.ToString();
            levelValueText.color = maxUnlockedLevel < maxAvailableLevel
                ? new Color(1f, 0.86f, 0.66f)
                : Color.white;
        }

        if (modeValueText != null)
        {
            modeValueText.text = mode == GameMode.Adventure ? "Adventure" : "Challenge";
        }

        if (difficultyValueText != null)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    difficultyValueText.text = "Easy";
                    break;
                case GameDifficulty.Hard:
                    difficultyValueText.text = "Hard";
                    break;
                default:
                    difficultyValueText.text = "Normal";
                    break;
            }
        }

        if (themeValueText != null)
        {
            switch (visualTheme)
            {
                case PlayerVisualTheme.Mechanical:
                    themeValueText.text = "Mechanical";
                    break;
                case PlayerVisualTheme.Minimal:
                    themeValueText.text = "Minimal";
                    break;
                default:
                    themeValueText.text = "Astronaut";
                    break;
            }
        }

        if (paletteValueText != null)
        {
            switch (colorPalette)
            {
                case PlayerColorPalette.Ember:
                    paletteValueText.text = "Ember";
                    break;
                case PlayerColorPalette.Mono:
                    paletteValueText.text = "Mono";
                    break;
                default:
                    paletteValueText.text = "Neon";
                    break;
            }
        }

        if (sensitivityValueText != null && sensitivitySlider != null)
        {
            sensitivityValueText.text = sensitivitySlider.value.ToString("0.00");
        }

        if (fovValueText != null && fovSlider != null)
        {
            fovValueText.text = Mathf.RoundToInt(fovSlider.value).ToString();
        }

        if (cameraSmoothnessValueText != null && cameraSmoothnessSlider != null)
        {
            cameraSmoothnessValueText.text = cameraSmoothnessSlider.value.ToString("0.00");
        }

        if (autoRecenterValueText != null && autoRecenterSlider != null)
        {
            autoRecenterValueText.text = autoRecenterSlider.value.ToString("0.00");
        }

        if (volumeValueText != null && volumeSlider != null)
        {
            volumeValueText.text = volumeSlider.value.ToString("0.00");
        }

        if (feedbackValueText != null && feedbackSlider != null)
        {
            feedbackValueText.text = feedbackSlider.value.ToString("0.00");
        }

        if (atmosphereValueText != null)
        {
            atmosphereValueText.text = AtmospherePresetLabel(atmospherePreset);
            atmosphereValueText.color = AtmospherePresetColor(atmospherePreset);
        }

        if (shakeValueText != null)
        {
            shakeValueText.text = cameraShakeEnabled ? "On" : "Off";
            shakeValueText.color = cameraShakeEnabled
                ? new Color(0.64f, 1f, 0.78f)
                : new Color(1f, 0.72f, 0.70f);
        }

        if (shakeToggleButton != null)
        {
            Color accent = PaletteAccentColor(colorPalette);
            Color baseColor = cameraShakeEnabled
                ? Color.Lerp(new Color(0.16f, 0.54f, 0.36f, 1f), accent, 0.22f)
                : new Color(0.52f, 0.22f, 0.22f, 1f);
            ColorBlock cb = shakeToggleButton.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = baseColor * 1.08f;
            cb.pressedColor = baseColor * 0.86f;
            cb.selectedColor = cb.highlightedColor;
            shakeToggleButton.colors = cb;
        }

        if (atmosphereToggleButton != null)
        {
            Color accent = PaletteAccentColor(colorPalette);
            Color baseColor = Color.Lerp(new Color(0.20f, 0.38f, 0.62f, 1f), accent, 0.18f);
            ColorBlock cb = atmosphereToggleButton.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = baseColor * 1.08f;
            cb.pressedColor = baseColor * 0.86f;
            cb.selectedColor = cb.highlightedColor;
            atmosphereToggleButton.colors = cb;
        }

        if (subtitleText != null)
        {
            LevelRuntimeConfig subtitlePreview = GameDirector.PreviewConfig(CurrentSettings);
            string modeText = mode == GameMode.Adventure ? "Adventure" : "Challenge";
            string themeLabel = themeValueText != null ? themeValueText.text : visualTheme.ToString();
            string paletteLabel = paletteValueText != null ? paletteValueText.text : colorPalette.ToString();
            string atmosphereLabel = AtmospherePresetLabel(atmospherePreset);
            string motionPresetLabel = MotionPresetLabel(motionPreset);
            string motionStyleLabel = MotionStyleLabel(motionStyle);
            string levelLabel = string.IsNullOrWhiteSpace(subtitlePreview.LevelName)
                ? LevelDescription(levelIndex)
                : subtitlePreview.LevelName;
            string lockHint = string.Empty;
            if (maxUnlockedLevel < maxAvailableLevel)
            {
                int nextLocked = Mathf.Clamp(maxUnlockedLevel + 1, 1, maxAvailableLevel);
                lockHint = "  |  L" + nextLocked + " Locked (Clear L" + maxUnlockedLevel + ")";
            }

            if (levelLockHintLeft > 0f)
            {
                lockHint = "  |  Clear current unlocked stage to unlock next";
            }

            subtitleText.text = "Level " + levelIndex + ": " + levelLabel + "\n" +
                                modeText + "  |  Theme " + themeLabel + " / " + paletteLabel +
                                "  |  Sky " + atmosphereLabel +
                                "  |  Motion " + motionPresetLabel + " / " + motionStyleLabel +
                                lockHint;
        }

        if (summaryText != null)
        {
            string difficultyLabel = difficultyValueText != null
                ? difficultyValueText.text
                : difficulty.ToString();
            string modeLabel = modeValueText != null
                ? modeValueText.text
                : mode.ToString();
            string themeLabel = themeValueText != null
                ? themeValueText.text
                : visualTheme.ToString();
            string paletteLabel = paletteValueText != null
                ? paletteValueText.text
                : colorPalette.ToString();
            string atmosphereLabel = AtmospherePresetLabel(atmospherePreset);
            string motionPresetLabel = MotionPresetLabel(motionPreset);
            string motionStyleLabel = MotionStyleLabel(motionStyle);
            LevelRuntimeConfig previewConfig = GameDirector.PreviewConfig(CurrentSettings);
            string checkpointPolicy = GameDirector.CheckpointPolicyLabel(previewConfig.CheckpointPolicy);
            string bestText = "--";
            if (GameDirector.TryGetBestTimeSeconds(CurrentSettings, out float best))
            {
                bestText = best.ToString("0.0") + "s";
            }

            Color accent = PaletteAccentColor(colorPalette);
            summaryText.color = Color.Lerp(new Color(0.88f, 0.96f, 1f), accent, 0.28f);
            summaryText.text = "Loadout: L" + levelIndex +
                               "  |  " + difficultyLabel +
                               "  |  " + modeLabel +
                               "  |  " + themeLabel +
                               " + " + paletteLabel +
                               "  |  Sky " + atmosphereLabel +
                               "  |  Motion " + motionPresetLabel + " / " + motionStyleLabel +
                               "  |  Unlocked " + maxUnlockedLevel + "/" + maxAvailableLevel +
                               "  |  Cr " + previewConfig.RequiredCrystals + "/" + previewConfig.TotalCrystals +
                               "  |  Time " + Mathf.RoundToInt(previewConfig.TimeLimitSeconds) + "s" +
                               "  |  FlipCD " + previewConfig.FlipCooldownSeconds.ToString("0.00") + "s" +
                               "  |  Checkpoints " + checkpointPolicy +
                               "  |  Map " + Mathf.RoundToInt(previewConfig.MapLength) + "m" +
                               "  |  Mech " + previewConfig.MechanismCount +
                               "  |  Best " + bestText +
                               "  |  FOV " + (fovSlider != null ? Mathf.RoundToInt(fovSlider.value).ToString() : "66") +
                               "  |  Smooth " + (cameraSmoothnessSlider != null ? cameraSmoothnessSlider.value.ToString("0.00") : "0.56") +
                               "  |  Recenter " + (autoRecenterSlider != null ? autoRecenterSlider.value.ToString("0.00") : "0.22") +
                               "  |  FX " + (feedbackSlider != null ? feedbackSlider.value.ToString("0.00") : "1.00") +
                               "  |  Shake " + (cameraShakeEnabled ? "On" : "Off");
        }

        if (mainPanelGlow != null)
        {
            Color accent = PaletteAccentColor(colorPalette);
            float alpha = mainPanelGlow.color.a;
            mainPanelGlow.color = new Color(accent.r, accent.g, accent.b, alpha);
        }

        ApplyAtmospherePalette();
        ApplyButtonTheme();
        UpdateLevelSelectorLockState();
    }

    private void ApplyResponsiveLayout()
    {
        if (mainPanelRect == null)
        {
            return;
        }

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        if (width == lastScreenWidth && height == lastScreenHeight)
        {
            return;
        }

        lastScreenWidth = width;
        lastScreenHeight = height;

        Rect safe = Screen.safeArea;
        float safeCenterX = (safe.xMin + safe.xMax - width) * 0.5f;
        float safeCenterY = (safe.yMin + safe.yMax - height) * 0.5f;

        float scale = Mathf.Clamp(Mathf.Min(width / 1920f, height / 1080f), 0.74f, 1.12f);
        float verticalOffset = height < 820 ? -28f : (height < 940 ? -10f : 10f);

        menuPanelBasePos = new Vector2(safeCenterX, verticalOffset + safeCenterY * 0.08f);
        menuPanelShadowBasePos = new Vector2(safeCenterX, verticalOffset - 6f + safeCenterY * 0.08f);

        mainPanelRect.localScale = Vector3.one * scale;
        mainPanelRect.anchoredPosition = menuPanelBasePos;

        if (mainPanelShadowRect != null)
        {
            mainPanelShadowRect.localScale = Vector3.one * scale;
            mainPanelShadowRect.anchoredPosition = menuPanelShadowBasePos;
        }

        if (settingsPanelRect != null)
        {
            float settingsY = height < 820 ? 252f : 278f;
            float slideOffset = Mathf.Lerp(-24f, 0f, settingsPanelAlpha);
            settingsPanelRect.anchoredPosition = new Vector2(0f, settingsY + slideOffset);
        }

        if (rowsRootRect != null)
        {
            rowsRootRect.localScale = width < 1440 ? new Vector3(0.96f, 0.96f, 1f) : Vector3.one;
        }

        if (summaryPanelRect != null)
        {
            summaryPanelRect.localScale = width < 1440 ? new Vector3(0.98f, 0.98f, 1f) : Vector3.one;
        }

        ApplyVisualDensity(width, scale);

        float textScale = Mathf.Clamp(scale, 0.86f, 1.16f);
        SetFontSize(titleText, Mathf.RoundToInt(60f * textScale));
        SetFontSize(subtitleText, Mathf.RoundToInt(20f * textScale));
        SetFontSize(summaryText, Mathf.RoundToInt(20f * textScale));
        SetFontSize(levelValueText, Mathf.RoundToInt(35f * textScale));
        SetFontSize(modeValueText, Mathf.RoundToInt(35f * textScale));
        SetFontSize(difficultyValueText, Mathf.RoundToInt(35f * textScale));
        SetFontSize(themeValueText, Mathf.RoundToInt(35f * textScale));
        SetFontSize(paletteValueText, Mathf.RoundToInt(35f * textScale));
    }

    private void ApplyVisualDensity(int width, float scale)
    {
        bool compactMode = width < 1500 || scale < 0.86f;

        if (dustImages != null)
        {
            for (int i = 0; i < dustImages.Length; i++)
            {
                if (dustImages[i] != null)
                {
                    dustImages[i].enabled = !compactMode;
                }
            }
        }

        if (panelEdgeClouds != null)
        {
            for (int i = 0; i < panelEdgeClouds.Length; i++)
            {
                if (panelEdgeClouds[i] != null)
                {
                    panelEdgeClouds[i].enabled = !compactMode;
                }
            }
        }

        if (filmGrainOverlay != null)
        {
            filmGrainOverlay.enabled = !compactMode;
        }
    }

    private void ConfigureInteractionRaycasts()
    {
        if (mainPanel == null)
        {
            return;
        }

        Graphic[] graphics = mainPanel.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }

        if (settingsPanel != null)
        {
            Image settingsBackground = settingsPanel.GetComponent<Image>();
            if (settingsBackground != null)
            {
                settingsBackground.raycastTarget = true;
            }
        }

        EnableRaycastForButtons(selectorButtons);

        EnableSliderRaycast(sensitivitySlider);
        EnableSliderRaycast(fovSlider);
        EnableSliderRaycast(cameraSmoothnessSlider);
        EnableSliderRaycast(autoRecenterSlider);
        EnableSliderRaycast(volumeSlider);
        EnableSliderRaycast(feedbackSlider);
    }

    private static void EnableRaycastForButtons(List<Button> buttons)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            Graphic target = button.targetGraphic;
            if (target != null)
            {
                target.raycastTarget = true;
            }
        }
    }

    private static void EnableSliderRaycast(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        Graphic[] graphics = slider.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }
        }
    }

    private void ApplyButtonTheme()
    {
        Color accent = PaletteAccentColor(colorPalette);
        Color selectorBase = Color.Lerp(new Color(0.17f, 0.27f, 0.40f, 1f), accent, 0.14f);
        for (int i = 0; i < selectorButtons.Count; i++)
        {
            Button button = selectorButtons[i];
            if (button == null ||
                button == startActionButton ||
                button == settingsActionButton ||
                button == previewActionButton ||
                button == shakeToggleButton ||
                button == atmosphereToggleButton)
            {
                continue;
            }

            ApplyButtonColor(button, selectorBase);
        }

        ApplyButtonColor(startActionButton, Color.Lerp(new Color(0.13f, 0.55f, 0.39f, 1f), accent, 0.20f));
        ApplyButtonColor(settingsActionButton, Color.Lerp(new Color(0.20f, 0.36f, 0.62f, 1f), accent, 0.16f));
        ApplyButtonColor(previewActionButton, Color.Lerp(new Color(0.20f, 0.44f, 0.72f, 1f), accent, 0.14f));
    }

    private void UpdateLevelSelectorLockState()
    {
        bool locked = maxUnlockedLevel <= 1 || maxAvailableLevel <= 1;
        if (levelMinusButton != null)
        {
            levelMinusButton.interactable = !locked;
            if (locked)
            {
                ApplyButtonColor(levelMinusButton, new Color(0.34f, 0.34f, 0.38f, 1f));
            }
        }

        if (levelPlusButton != null)
        {
            levelPlusButton.interactable = !locked;
            if (locked)
            {
                ApplyButtonColor(levelPlusButton, new Color(0.34f, 0.34f, 0.38f, 1f));
            }
        }
    }

    private static void ApplyButtonColor(Button button, Color baseColor)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
        }

        ColorBlock cb = button.colors;
        cb.normalColor = baseColor;
        cb.highlightedColor = Color.Lerp(baseColor, Color.white, 0.16f);
        cb.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.32f);
        button.colors = cb;
    }

    private void AnimateVisuals(float deltaTime)
    {
        uiAnimTime += Mathf.Max(0f, deltaTime);
        UpdateSettingsPanelTransition(deltaTime);

        if (dimBackground != null)
        {
            float t = 0.5f + Mathf.Sin(uiAnimTime * 0.27f) * 0.5f;
            float warm = 0.5f + Mathf.Sin(uiAnimTime * 0.045f + 1.2f) * 0.5f;
            dimBackground.color = Color.Lerp(
                new Color(0.02f, 0.04f, 0.08f, 0.72f),
                new Color(0.08f, 0.10f, 0.12f, 0.80f),
                Mathf.Lerp(t, warm, 0.25f)
            );
        }

        AnimateAtmosphere();

        if (mainPanelGlow != null)
        {
            float alpha = 0.48f + Mathf.Sin(uiAnimTime * 1.2f) * 0.16f;
            Color color = mainPanelGlow.color;
            mainPanelGlow.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (mainPanelGlass != null)
        {
            float alpha = 0.04f + Mathf.Sin(uiAnimTime * 0.62f + 1.4f) * 0.018f;
            mainPanelGlass.color = new Color(0.86f, 0.94f, 1f, Mathf.Clamp(alpha, 0.012f, 0.052f));
        }

        if (mainPanelTint != null)
        {
            float warm = 0.5f + Mathf.Sin(uiAnimTime * 0.055f + 0.8f) * 0.5f;
            Color coolTint = new Color(0.18f, 0.44f, 0.70f, 0.022f);
            Color warmTint = new Color(0.62f, 0.36f, 0.22f, 0.036f);
            mainPanelTint.color = Color.Lerp(coolTint, warmTint, warm);
        }

        if (rowCards != null && rowCards.Count > 0)
        {
            Color accent = PaletteAccentColor(colorPalette);
            for (int i = 0; i < rowCards.Count; i++)
            {
                Image row = rowCards[i];
                if (row == null)
                {
                    continue;
                }

                float pulse = 0.5f + Mathf.Sin(uiAnimTime * 0.95f + i * 0.45f) * 0.5f;
                float alpha = Mathf.Lerp(0.16f, 0.25f, pulse);
                Color baseColor = i % 2 == 0
                    ? new Color(0.11f, 0.18f, 0.27f, alpha)
                    : new Color(0.10f, 0.16f, 0.24f, alpha * 0.92f);
                row.color = Color.Lerp(baseColor, new Color(accent.r, accent.g, accent.b, alpha), 0.08f);
            }
        }

        if (mainPanelShadow != null)
        {
            float alpha = 0.34f + Mathf.Sin(uiAnimTime * 0.44f + 0.6f) * 0.04f;
            mainPanelShadow.color = new Color(0f, 0f, 0f, Mathf.Clamp(alpha, 0.20f, 0.36f));
        }

        if (ambientRects != null && ambientBasePositions != null)
        {
            for (int i = 0; i < ambientRects.Length; i++)
            {
                RectTransform rect = ambientRects[i];
                if (rect == null)
                {
                    continue;
                }

                float speed = 0.16f + i * 0.07f;
                float radius = 18f + i * 12f;
                Vector2 basePos = ambientBasePositions[i];
                rect.anchoredPosition = basePos + new Vector2(
                    Mathf.Cos(uiAnimTime * speed + i) * radius,
                    Mathf.Sin(uiAnimTime * (speed + 0.08f) + i * 1.7f) * radius * 0.65f
                );
            }
        }

        if (mainPanelRect != null)
        {
            Vector2 basePos = menuPanelBasePos;
            if (previewShakeTimeLeft > 0f)
            {
                previewShakeTimeLeft = Mathf.Max(0f, previewShakeTimeLeft - deltaTime);
                float normalized = previewShakeDuration > 0.001f ? previewShakeTimeLeft / previewShakeDuration : 0f;
                float amplitude = previewShakeAmplitude * normalized;
                float t = Time.unscaledTime * 34f;
                Vector2 offset = new Vector2(
                    (Mathf.PerlinNoise(t, 0.17f) - 0.5f) * 2f * amplitude,
                    (Mathf.PerlinNoise(0.43f, t) - 0.5f) * 2f * amplitude
                );
                mainPanelRect.anchoredPosition = basePos + offset;
            }
            else
            {
                mainPanelRect.anchoredPosition = basePos;
            }
        }

        if (mainPanelShadowRect != null && mainPanelRect != null)
        {
            Vector2 panelOffset = mainPanelRect.anchoredPosition - menuPanelBasePos;
            mainPanelShadowRect.anchoredPosition = menuPanelShadowBasePos + panelOffset * 0.38f;
        }
    }

    private void BuildAtmosphereBackground(Transform parent)
    {
        skyTopTint = CreatePanel(
            "SkyTopTint",
            parent,
            new Vector2(0f, 0.48f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.12f, 0.22f, 0.36f, 0.46f)
        );
        skyTopTint.raycastTarget = false;

        skyColorWash = CreatePanel(
            "SkyColorWash",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.94f, 0.66f, 0.42f, 0.035f)
        );
        skyColorWash.raycastTarget = false;

        skyBottomFog = CreatePanel(
            "SkyBottomFog",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.56f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.06f, 0.11f, 0.16f, 0.42f)
        );
        skyBottomFog.raycastTarget = false;

        sunHalo = CreatePanel(
            "SunHalo",
            parent,
            new Vector2(0.74f, 0.72f),
            new Vector2(0.74f, 0.72f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(560f, 560f),
            new Color(1f, 0.88f, 0.58f, 0.12f)
        );
        sunHalo.raycastTarget = false;

        sunCore = CreatePanel(
            "SunCore",
            parent,
            new Vector2(0.74f, 0.72f),
            new Vector2(0.74f, 0.72f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(122f, 122f),
            new Color(1f, 0.95f, 0.82f, 0.22f)
        );
        sunCore.raycastTarget = false;

        horizonGlow = CreatePanel(
            "HorizonGlow",
            parent,
            new Vector2(0.5f, 0.44f),
            new Vector2(0.5f, 0.44f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1940f, 220f),
            new Color(0.24f, 0.74f, 0.98f, 0.18f)
        );
        horizonGlow.raycastTarget = false;

        Image horizonCore = CreatePanel(
            "HorizonCore",
            parent,
            new Vector2(0.5f, 0.44f),
            new Vector2(0.5f, 0.44f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1940f, 28f),
            new Color(0.66f, 0.90f, 1f, 0.23f)
        );
        horizonCore.raycastTarget = false;

        cloudMistBand = CreatePanel(
            "CloudMistBand",
            parent,
            new Vector2(0f, 0.51f),
            new Vector2(1f, 0.66f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.78f, 0.88f, 0.96f, 0.05f)
        );
        cloudMistBand.raycastTarget = false;

        Image mountainFogFar = CreatePanel(
            "MountainFogFar",
            parent,
            new Vector2(0f, 0.26f),
            new Vector2(1f, 0.46f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.11f, 0.18f, 0.23f, 0.24f)
        );
        mountainFogFar.raycastTarget = false;

        const int mountainCount = 16;
        mountainRects = new RectTransform[mountainCount];
        mountainBasePositions = new Vector2[mountainCount];
        mountainShiftStrengths = new float[mountainCount];
        mountainPhases = new float[mountainCount];
        for (int i = 0; i < mountainCount; i++)
        {
            float t = mountainCount == 1 ? 0.5f : i / (mountainCount - 1f);
            float x = Mathf.Lerp(0.02f, 0.98f, t);
            float width = Mathf.Lerp(260f, 620f, Wave01(i * 1.71f + 0.42f));
            float height = Mathf.Lerp(140f, 340f, Wave01(i * 2.13f + 1.16f));
            float y = Mathf.Lerp(0.30f, 0.43f, Wave01(i * 1.27f + 0.77f));
            float rot = Mathf.Lerp(-8f, 8f, Wave01(i * 1.49f + 0.23f));
            Color color = Color.Lerp(
                new Color(0.08f, 0.14f, 0.20f, 0.46f),
                new Color(0.15f, 0.23f, 0.30f, 0.42f),
                Wave01(i * 0.93f + 0.54f)
            );

            Image mountain = CreatePanel(
                "MountainPeak_" + i,
                parent,
                new Vector2(x, y),
                new Vector2(x, y),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(width, height),
                color
            );
            mountain.raycastTarget = false;
            mountain.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
            mountainRects[i] = mountain.rectTransform;
            mountainBasePositions[i] = mountain.rectTransform.anchoredPosition;
            mountainShiftStrengths[i] = Mathf.Lerp(3f, 16f, Wave01(i * 1.73f + 0.17f));
            mountainPhases[i] = i * 0.41f;
        }

        grassBand = CreatePanel(
            "ForestBand",
            parent,
            new Vector2(0f, 0.22f),
            new Vector2(1f, 0.315f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.08f, 0.24f, 0.16f, 0.42f)
        );
        grassBand.raycastTarget = false;

        soilBand = CreatePanel(
            "SoilBand",
            parent,
            new Vector2(0f, 0.14f),
            new Vector2(1f, 0.235f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.21f, 0.16f, 0.12f, 0.36f)
        );
        soilBand.raycastTarget = false;

        const int treeCount = 56;
        for (int i = 0; i < treeCount; i++)
        {
            float x = Mathf.Lerp(0.01f, 0.99f, i / (treeCount - 1f));
            float h = Mathf.Lerp(18f, 72f, Wave01(i * 2.57f + 0.31f));
            float w = Mathf.Lerp(8f, 24f, Wave01(i * 1.47f + 1.12f));
            float y = Mathf.Lerp(0.248f, 0.282f, Wave01(i * 1.19f + 0.66f));

            Image tree = CreatePanel(
                "ForestTree_" + i,
                parent,
                new Vector2(x, y),
                new Vector2(x, y),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(w, h),
                new Color(0.07f, 0.25f, 0.15f, 0.48f)
            );
            tree.raycastTarget = false;
            tree.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-6f, 6f, Wave01(i * 0.89f + 0.44f)));
        }

        leftVignette = CreatePanel(
            "LeftVignette",
            parent,
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(360f, 0f),
            new Color(0f, 0f, 0f, 0.16f)
        );
        leftVignette.raycastTarget = false;

        rightVignette = CreatePanel(
            "RightVignette",
            parent,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            Vector2.zero,
            new Vector2(360f, 0f),
            new Color(0f, 0f, 0f, 0.16f)
        );
        rightVignette.raycastTarget = false;

        Image beamL = CreatePanel(
            "AtmosphereBeamL",
            parent,
            new Vector2(0.22f, 0.74f),
            new Vector2(0.22f, 0.74f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(420f, 980f),
            new Color(0.34f, 0.80f, 1f, 0.035f)
        );
        beamL.raycastTarget = false;
        beamL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 16f);

        Image beamR = CreatePanel(
            "AtmosphereBeamR",
            parent,
            new Vector2(0.78f, 0.72f),
            new Vector2(0.78f, 0.72f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(380f, 920f),
            new Color(0.24f, 0.96f, 0.70f, 0.03f)
        );
        beamR.raycastTarget = false;
        beamR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);

        const int cloudCount = 10;
        cloudRects = new RectTransform[cloudCount];
        cloudImages = new Image[cloudCount];
        cloudBasePositions = new Vector2[cloudCount];
        cloudSpeeds = new float[cloudCount];
        cloudAmplitudes = new float[cloudCount];
        cloudPhases = new float[cloudCount];
        cloudBaseAlphas = new float[cloudCount];

        for (int i = 0; i < cloudCount; i++)
        {
            float x = Mathf.Lerp(-0.08f, 1.08f, Wave01(i * 1.91f + 0.43f));
            float y = Mathf.Lerp(0.60f, 0.90f, Wave01(i * 2.39f + 0.14f));
            float width = Mathf.Lerp(260f, 860f, Wave01(i * 1.67f + 0.76f));
            float height = Mathf.Lerp(80f, 220f, Wave01(i * 2.73f + 1.37f));
            float alpha = Mathf.Lerp(0.035f, 0.09f, Wave01(i * 2.17f + 0.59f));
            float rotation = Mathf.Lerp(-4f, 4f, Wave01(i * 1.33f + 0.27f));

            Image cloud = CreatePanel(
                "CloudLayer_" + i,
                parent,
                new Vector2(x, y),
                new Vector2(x, y),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(width, height),
                new Color(0.88f, 0.95f, 1f, alpha)
            );
            cloud.raycastTarget = false;
            cloud.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            cloudImages[i] = cloud;
            cloudRects[i] = cloud.rectTransform;
            cloudBasePositions[i] = cloud.rectTransform.anchoredPosition;
            cloudSpeeds[i] = Mathf.Lerp(0.010f, 0.046f, Wave01(i * 2.11f + 1.08f));
            cloudAmplitudes[i] = Mathf.Lerp(10f, 42f, Wave01(i * 1.43f + 0.33f));
            cloudPhases[i] = i * 0.57f;
            cloudBaseAlphas[i] = alpha;
        }

        const int dustCount = 24;
        dustRects = new RectTransform[dustCount];
        dustImages = new Image[dustCount];
        dustBasePositions = new Vector2[dustCount];
        dustSpeeds = new float[dustCount];
        dustAmplitudes = new float[dustCount];
        dustPhases = new float[dustCount];
        dustBaseAlphas = new float[dustCount];

        for (int i = 0; i < dustCount; i++)
        {
            float x = Mathf.Lerp(0.04f, 0.96f, Wave01(i * 2.31f + 0.27f));
            float y = Mathf.Lerp(0.36f, 0.96f, Wave01(i * 3.19f + 1.12f));
            float size = Mathf.Lerp(2f, 6f, Wave01(i * 1.93f + 0.81f));
            float alpha = Mathf.Lerp(0.03f, 0.11f, Wave01(i * 2.79f + 0.5f));

            Image dust = CreatePanel(
                "AtmosphereDust_" + i,
                parent,
                new Vector2(x, y),
                new Vector2(x, y),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(size, size),
                new Color(0.78f, 0.90f, 1f, alpha * 0.42f)
            );
            dust.raycastTarget = false;

            dustImages[i] = dust;
            dustRects[i] = dust.rectTransform;
            dustBasePositions[i] = dust.rectTransform.anchoredPosition;
            dustSpeeds[i] = Mathf.Lerp(0.08f, 0.26f, Wave01(i * 1.61f + 0.2f));
            dustAmplitudes[i] = Mathf.Lerp(2f, 8f, Wave01(i * 2.03f + 0.9f));
            dustPhases[i] = i * 0.47f;
            dustBaseAlphas[i] = alpha;
        }
    }

    private void BuildProceduralLensOverlays(Transform parent)
    {
        Texture2D cloudFarTex = CreateCloudNoiseTexture(
            width: 512,
            height: 256,
            seed: 3,
            baseScale: 3.9f,
            octaves: 5,
            persistence: 0.54f,
            lacunarity: 1.92f
        );

        cloudNoiseFarOverlay = CreateRawPanel(
            "CloudNoiseFarOverlay",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.84f, 0.93f, 1f, 0.010f),
            cloudFarTex,
            new Rect(0f, 0f, 2.35f, 1.35f)
        );
        cloudNoiseFarOverlay.raycastTarget = false;

        Texture2D cloudNearTex = CreateCloudNoiseTexture(
            width: 512,
            height: 256,
            seed: 11,
            baseScale: 5.6f,
            octaves: 4,
            persistence: 0.50f,
            lacunarity: 2.1f
        );

        cloudNoiseNearOverlay = CreateRawPanel(
            "CloudNoiseNearOverlay",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(1f, 0.84f, 0.66f, 0.006f),
            cloudNearTex,
            new Rect(0.08f, 0.04f, 2.9f, 1.7f)
        );
        cloudNoiseNearOverlay.raycastTarget = false;

        Texture2D grainTex = CreateFilmGrainTexture(176, 176, 19);
        filmGrainOverlay = CreateRawPanel(
            "FilmGrainOverlay",
            parent,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(1f, 1f, 1f, 0.004f),
            grainTex,
            new Rect(0f, 0f, 16f, 9f)
        );
        filmGrainOverlay.raycastTarget = false;
    }

    private void BuildPanelEdgeClouds(Transform panelParent)
    {
        if (panelParent == null)
        {
            return;
        }

        Texture2D edgeTex = CreateCloudNoiseTexture(
            width: 384,
            height: 192,
            seed: 29,
            baseScale: 4.5f,
            octaves: 4,
            persistence: 0.53f,
            lacunarity: 2.05f
        );

        panelEdgeClouds = new RawImage[6];
        panelEdgeCloudBasePos = new Vector2[6];
        panelEdgeCloudSpeeds = new float[6];
        panelEdgeCloudAmplitudes = new float[6];
        panelEdgeCloudPhases = new float[6];
        panelEdgeCloudBaseAlphas = new float[6];

        panelEdgeClouds[0] = CreateRawPanel(
            "PanelEdgeCloud_TopL",
            panelParent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(-36f, -26f),
            new Vector2(320f, 112f),
            new Color(0.94f, 0.98f, 1f, 0.08f),
            edgeTex,
            new Rect(0.06f, 0.12f, 1.7f, 1f)
        );
        panelEdgeClouds[0].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);

        panelEdgeClouds[1] = CreateRawPanel(
            "PanelEdgeCloud_TopR",
            panelParent,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(42f, -34f),
            new Vector2(360f, 124f),
            new Color(1f, 0.90f, 0.78f, 0.08f),
            edgeTex,
            new Rect(0.42f, 0.25f, 1.8f, 1f)
        );
        panelEdgeClouds[1].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);

        panelEdgeClouds[2] = CreateRawPanel(
            "PanelEdgeCloud_LeftMid",
            panelParent,
            new Vector2(0f, 0.55f),
            new Vector2(0f, 0.55f),
            new Vector2(0f, 0.5f),
            new Vector2(-32f, 0f),
            new Vector2(220f, 96f),
            new Color(0.88f, 0.95f, 1f, 0.07f),
            edgeTex,
            new Rect(0.20f, 0.08f, 1.5f, 1f)
        );
        panelEdgeClouds[2].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -5f);

        panelEdgeClouds[3] = CreateRawPanel(
            "PanelEdgeCloud_RightMid",
            panelParent,
            new Vector2(1f, 0.50f),
            new Vector2(1f, 0.50f),
            new Vector2(1f, 0.5f),
            new Vector2(30f, -4f),
            new Vector2(240f, 100f),
            new Color(1f, 0.88f, 0.74f, 0.07f),
            edgeTex,
            new Rect(0.55f, 0.16f, 1.55f, 1f)
        );
        panelEdgeClouds[3].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);

        panelEdgeClouds[4] = CreateRawPanel(
            "PanelEdgeCloud_BottomL",
            panelParent,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0.5f),
            new Vector2(-26f, 32f),
            new Vector2(280f, 100f),
            new Color(0.82f, 0.90f, 1f, 0.06f),
            edgeTex,
            new Rect(0.32f, 0.36f, 1.65f, 1f)
        );
        panelEdgeClouds[4].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);

        panelEdgeClouds[5] = CreateRawPanel(
            "PanelEdgeCloud_BottomR",
            panelParent,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0.5f),
            new Vector2(22f, 26f),
            new Vector2(300f, 108f),
            new Color(1f, 0.86f, 0.70f, 0.07f),
            edgeTex,
            new Rect(0.72f, 0.30f, 1.75f, 1f)
        );
        panelEdgeClouds[5].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 7f);

        for (int i = 0; i < panelEdgeClouds.Length; i++)
        {
            RawImage cloud = panelEdgeClouds[i];
            if (cloud == null)
            {
                continue;
            }

            cloud.raycastTarget = false;
            panelEdgeCloudBasePos[i] = cloud.rectTransform.anchoredPosition;
            panelEdgeCloudSpeeds[i] = Mathf.Lerp(0.032f, 0.086f, Wave01(i * 1.37f + 0.54f));
            panelEdgeCloudAmplitudes[i] = Mathf.Lerp(8f, 24f, Wave01(i * 1.91f + 0.22f));
            panelEdgeCloudPhases[i] = i * 0.63f;
            panelEdgeCloudBaseAlphas[i] = cloud.color.a;
        }
    }

    private Texture2D GetAmbientBlobTexture()
    {
        if (ambientBlobTexture != null)
        {
            return ambientBlobTexture;
        }

        ambientBlobTexture = CreateAmbientBlobTexture(256, 256, 97);
        return ambientBlobTexture;
    }

    private Texture2D CreateAmbientBlobTexture(int width, int height, int seed)
    {
        width = Mathf.Max(64, width);
        height = Mathf.Max(64, height);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        float invW = 1f / Mathf.Max(1f, width - 1f);
        float invH = 1f / Mathf.Max(1f, height - 1f);
        float seedX = seed * 3.17f + 2.71f;
        float seedY = seed * 4.37f + 7.19f;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            float ny = y * invH;
            for (int x = 0; x < width; x++)
            {
                float nx = x * invW;
                float dx = nx - 0.5f;
                float dy = ny - 0.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float radial = Mathf.Clamp01(1f - radius);
                radial = Mathf.SmoothStep(0f, 1f, radial);

                float n0 = Mathf.PerlinNoise(nx * 3.6f + seedX, ny * 3.6f + seedY);
                float n1 = Mathf.PerlinNoise(nx * 7.4f + seedX * 0.6f, ny * 7.4f + seedY * 0.62f);
                float noise = (n0 * 0.72f + n1 * 0.28f);
                float alpha = radial * Mathf.Lerp(0.82f, 1.04f, noise);
                alpha = Mathf.Clamp01(alpha * 0.92f);

                pixels[row + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        runtimeOverlayTextures.Add(texture);
        return texture;
    }

    private Texture2D CreateCloudNoiseTexture(
        int width,
        int height,
        int seed,
        float baseScale,
        int octaves,
        float persistence,
        float lacunarity)
    {
        width = Mathf.Max(32, width);
        height = Mathf.Max(32, height);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        float seedX = 19.13f * (seed + 1f);
        float seedY = 7.91f * (seed + 1f);
        float invW = 1f / Mathf.Max(1f, width - 1f);
        float invH = 1f / Mathf.Max(1f, height - 1f);

        for (int y = 0; y < height; y++)
        {
            float ny = y * invH;
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                float nx = x * invW;
                float value = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float amplitudeSum = 0f;

                for (int o = 0; o < octaves; o++)
                {
                    float sampleX = nx * baseScale * frequency + seedX;
                    float sampleY = ny * baseScale * frequency + seedY;
                    float n = Mathf.PerlinNoise(sampleX, sampleY);
                    value += n * amplitude;
                    amplitudeSum += amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                value /= Mathf.Max(0.0001f, amplitudeSum);
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.76f, value));
                pixels[row + x] = new Color(cloud, cloud, cloud, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        runtimeOverlayTextures.Add(texture);
        return texture;
    }

    private Texture2D CreateFilmGrainTexture(int width, int height, int seed)
    {
        width = Mathf.Max(32, width);
        height = Mathf.Max(32, height);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;

        Color[] pixels = new Color[width * height];
        float seedOffset = seed * 13.37f + 7.73f;
        int idx = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float hash = Mathf.Sin((x + 1f) * 12.9898f + (y + 1f) * 78.233f + seedOffset) * 43758.5453f;
                float grain = Mathf.Repeat(hash, 1f);
                pixels[idx++] = new Color(grain, grain, grain, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        runtimeOverlayTextures.Add(texture);
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
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        RawImage image = go.GetComponent<RawImage>();
        image.color = color;
        image.texture = texture;
        image.uvRect = uvRect;
        return image;
    }

    private void DisposeRuntimeOverlays()
    {
        for (int i = 0; i < runtimeOverlayTextures.Count; i++)
        {
            Texture2D texture = runtimeOverlayTextures[i];
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        runtimeOverlayTextures.Clear();
        ambientBlobTexture = null;
    }

    private void AnimateAtmosphere()
    {
        Color accent = PaletteAccentColor(colorPalette);
        GetAtmospherePresetTargets(
            out float dayBase,
            out float warmBase,
            out float dayAmplitude,
            out float warmAmplitude,
            out float driftSpeed
        );

        float daylight = Mathf.Clamp01(dayBase + Mathf.Sin(uiAnimTime * driftSpeed) * dayAmplitude);
        float warmShift = Mathf.Clamp01(warmBase + Mathf.Sin(uiAnimTime * (driftSpeed * 0.74f) + 1.1f) * warmAmplitude);
        float hazePulse = 0.5f + Mathf.Sin(uiAnimTime * 0.22f + 0.6f) * 0.5f;

        if (cloudNoiseFarOverlay != null)
        {
            float farX = Mathf.Repeat(uiAnimTime * 0.0042f, 1f);
            float farY = Mathf.Sin(uiAnimTime * 0.015f) * 0.03f;
            cloudNoiseFarOverlay.uvRect = new Rect(farX, farY, 2.35f, 1.35f);
            cloudNoiseFarOverlay.color = Color.Lerp(
                new Color(0.82f, 0.92f, 1f, 0.004f),
                new Color(0.96f, 0.84f, 0.70f, 0.014f),
                warmShift * 0.55f
            );
        }

        if (cloudNoiseNearOverlay != null)
        {
            float nearX = Mathf.Repeat(uiAnimTime * 0.0069f + 0.12f, 1f);
            float nearY = Mathf.Cos(uiAnimTime * 0.019f) * 0.04f;
            cloudNoiseNearOverlay.uvRect = new Rect(nearX, nearY, 2.9f, 1.7f);
            cloudNoiseNearOverlay.color = Color.Lerp(
                new Color(0.94f, 0.98f, 1f, 0.002f),
                new Color(1f, 0.82f, 0.60f, 0.010f),
                warmShift * 0.7f
            );
        }

        if (filmGrainOverlay != null)
        {
            float grainX = Mathf.Repeat(uiAnimTime * 0.31f + Mathf.Sin(uiAnimTime * 4.8f) * 0.01f, 1f);
            float grainY = Mathf.Repeat(uiAnimTime * 0.27f + Mathf.Cos(uiAnimTime * 3.9f) * 0.012f, 1f);
            filmGrainOverlay.uvRect = new Rect(grainX, grainY, 16f, 9f);
            float alpha = Mathf.Lerp(0.0012f, 0.0042f, 1f - daylight);
            alpha *= Mathf.Lerp(0.86f, 1.1f, hazePulse);
            filmGrainOverlay.color = new Color(1f, 1f, 1f, alpha);
        }

        if (panelEdgeClouds != null && panelEdgeCloudBasePos != null)
        {
            for (int i = 0; i < panelEdgeClouds.Length; i++)
            {
                RawImage cloud = panelEdgeClouds[i];
                if (cloud == null)
                {
                    continue;
                }

                float speed = panelEdgeCloudSpeeds != null && i < panelEdgeCloudSpeeds.Length
                    ? panelEdgeCloudSpeeds[i]
                    : 0.05f;
                float amp = panelEdgeCloudAmplitudes != null && i < panelEdgeCloudAmplitudes.Length
                    ? panelEdgeCloudAmplitudes[i]
                    : 12f;
                float phase = panelEdgeCloudPhases != null && i < panelEdgeCloudPhases.Length
                    ? panelEdgeCloudPhases[i]
                    : i;
                Vector2 basePos = panelEdgeCloudBasePos[i];

                cloud.rectTransform.anchoredPosition = basePos + new Vector2(
                    Mathf.Sin(uiAnimTime * speed + phase) * amp,
                    Mathf.Cos(uiAnimTime * (speed * 0.76f) + phase * 1.4f) * amp * 0.32f
                );

                Rect uv = cloud.uvRect;
                uv.x = Mathf.Repeat(uv.x + Time.unscaledDeltaTime * (0.012f + speed * 0.08f), 1f);
                cloud.uvRect = uv;

                float baseAlpha = panelEdgeCloudBaseAlphas != null && i < panelEdgeCloudBaseAlphas.Length
                    ? panelEdgeCloudBaseAlphas[i]
                    : 0.13f;
                float pulse = 0.72f + Mathf.Sin(uiAnimTime * (speed * 8.4f) + phase * 0.9f) * 0.28f;
                float alpha = baseAlpha * pulse;

                Color tint = Color.Lerp(
                    new Color(0.84f, 0.93f, 1f, alpha),
                    new Color(1f, 0.84f, 0.64f, alpha),
                    warmShift * 0.58f
                );
                cloud.color = tint;
            }
        }

        if (horizonGlow != null)
        {
            float pulse = 0.56f + Mathf.Sin(uiAnimTime * 0.36f) * 0.44f;
            Color horizonTint = Color.Lerp(
                new Color(accent.r * 0.88f, accent.g * 0.92f, accent.b, 1f),
                new Color(1f, 0.70f, 0.42f, 1f),
                warmShift * 0.45f
            );
            horizonGlow.color = new Color(horizonTint.r, horizonTint.g, horizonTint.b, Mathf.Lerp(0.10f, 0.24f, pulse));
        }

        if (skyTopTint != null)
        {
            float t = 0.5f + Mathf.Sin(uiAnimTime * 0.16f) * 0.5f;
            Color c = Color.Lerp(
                new Color(0.10f, 0.16f, 0.28f, 0.44f),
                new Color(0.22f, 0.26f, 0.34f, 0.50f),
                Mathf.Lerp(t, warmShift, 0.35f)
            );
            c = Color.Lerp(c, new Color(0.35f, 0.27f, 0.22f, 0.42f), (1f - daylight) * 0.42f);
            skyTopTint.color = c;
        }

        if (skyColorWash != null)
        {
            Color wash = Color.Lerp(
                new Color(0.25f, 0.36f, 0.52f, 0.026f),
                new Color(1f, 0.64f, 0.36f, 0.068f),
                warmShift
            );
            wash.a *= Mathf.Lerp(0.8f, 1.2f, 1f - daylight);
            skyColorWash.color = wash;
        }

        if (skyBottomFog != null)
        {
            float t = 0.5f + Mathf.Sin(uiAnimTime * 0.2f + 1.2f) * 0.5f;
            skyBottomFog.color = Color.Lerp(
                new Color(0.06f, 0.10f, 0.15f, 0.38f),
                new Color(0.12f, 0.16f, 0.19f, 0.44f),
                Mathf.Lerp(t, warmShift, 0.25f)
            );
        }

        if (sunHalo != null)
        {
            RectTransform haloRect = sunHalo.rectTransform;
            Vector2 basePos = new Vector2(Mathf.Lerp(-120f, 160f, daylight), Mathf.Lerp(-40f, 34f, daylight));
            haloRect.anchoredPosition = basePos + new Vector2(Mathf.Sin(uiAnimTime * 0.11f) * 18f, Mathf.Cos(uiAnimTime * 0.08f) * 8f);

            Color haloColor = Color.Lerp(
                new Color(1f, 0.82f, 0.52f, 0.16f),
                new Color(0.90f, 0.95f, 1f, 0.08f),
                daylight
            );
            haloColor.a *= Mathf.Lerp(1.2f, 0.82f, daylight);
            sunHalo.color = haloColor;
        }

        if (sunCore != null)
        {
            RectTransform coreRect = sunCore.rectTransform;
            coreRect.anchoredPosition = new Vector2(Mathf.Lerp(-114f, 148f, daylight), Mathf.Lerp(-38f, 30f, daylight));

            float alpha = Mathf.Lerp(0.24f, 0.14f, daylight) + Mathf.Sin(uiAnimTime * 0.48f) * 0.02f;
            sunCore.color = Color.Lerp(
                new Color(1f, 0.92f, 0.72f, alpha),
                new Color(0.86f, 0.93f, 1f, alpha * 0.8f),
                daylight
            );
        }

        if (cloudMistBand != null)
        {
            cloudMistBand.color = Color.Lerp(
                new Color(0.86f, 0.90f, 0.95f, 0.036f),
                new Color(0.94f, 0.84f, 0.72f, 0.06f),
                warmShift * 0.5f
            );
        }

        if (grassBand != null)
        {
            grassBand.color = Color.Lerp(
                new Color(0.07f, 0.23f, 0.14f, 0.40f),
                new Color(0.12f, 0.29f, 0.16f, 0.46f),
                daylight
            );
        }

        if (soilBand != null)
        {
            soilBand.color = Color.Lerp(
                new Color(0.17f, 0.13f, 0.10f, 0.30f),
                new Color(0.25f, 0.18f, 0.13f, 0.36f),
                warmShift * 0.65f
            );
        }

        if (mountainRects != null && mountainBasePositions != null)
        {
            for (int i = 0; i < mountainRects.Length; i++)
            {
                RectTransform rect = mountainRects[i];
                if (rect == null)
                {
                    continue;
                }

                float shift = mountainShiftStrengths != null && i < mountainShiftStrengths.Length
                    ? mountainShiftStrengths[i]
                    : 6f;
                float phase = mountainPhases != null && i < mountainPhases.Length
                    ? mountainPhases[i]
                    : i * 0.35f;
                Vector2 basePos = mountainBasePositions[i];
                rect.anchoredPosition = basePos + new Vector2(Mathf.Sin(uiAnimTime * 0.028f + phase) * shift, 0f);
            }
        }

        if (cloudRects != null && cloudImages != null && cloudBasePositions != null)
        {
            for (int i = 0; i < cloudRects.Length; i++)
            {
                RectTransform rect = cloudRects[i];
                Image cloud = cloudImages[i];
                if (rect == null || cloud == null)
                {
                    continue;
                }

                float speed = cloudSpeeds != null && i < cloudSpeeds.Length ? cloudSpeeds[i] : 0.04f;
                float amp = cloudAmplitudes != null && i < cloudAmplitudes.Length ? cloudAmplitudes[i] : 24f;
                float phase = cloudPhases != null && i < cloudPhases.Length ? cloudPhases[i] : i;
                Vector2 basePos = cloudBasePositions[i];

                rect.anchoredPosition = basePos + new Vector2(
                    Mathf.Sin(uiAnimTime * speed + phase) * amp,
                    Mathf.Cos(uiAnimTime * (speed * 0.62f) + phase * 1.2f) * (amp * 0.16f)
                );

                float pulse = 0.75f + Mathf.Sin(uiAnimTime * (speed * 5.4f) + phase * 0.8f) * 0.25f;
                float baseAlpha = cloudBaseAlphas != null && i < cloudBaseAlphas.Length ? cloudBaseAlphas[i] : 0.1f;
                float alpha = baseAlpha * pulse;
                Color tint = Color.Lerp(
                    new Color(0.86f, 0.92f, 1f, alpha),
                    new Color(0.98f, 0.86f, 0.74f, alpha),
                    warmShift * 0.48f
                );
                cloud.color = tint;
            }
        }

        if (dustRects != null && dustImages != null && dustBasePositions != null)
        {
            for (int i = 0; i < dustRects.Length; i++)
            {
                RectTransform rect = dustRects[i];
                Image dust = dustImages[i];
                if (rect == null || dust == null)
                {
                    continue;
                }

                float speed = dustSpeeds[i];
                float phase = dustPhases[i];
                float amp = dustAmplitudes[i];
                Vector2 basePos = dustBasePositions[i];
                rect.anchoredPosition = basePos + new Vector2(
                    Mathf.Cos(uiAnimTime * speed + phase) * (amp * 0.55f),
                    Mathf.Sin(uiAnimTime * (speed * 0.83f) + phase * 1.2f) * amp
                );

                float alphaPulse = 0.66f + Mathf.Sin(uiAnimTime * (speed * 1.8f) + phase * 1.4f) * 0.34f;
                float alpha = dustBaseAlphas[i] * Mathf.Clamp01(alphaPulse);
                Color dustTint = Color.Lerp(
                    new Color(0.78f, 0.90f, 1f, alpha),
                    new Color(1f, 0.84f, 0.62f, alpha),
                    warmShift * 0.42f
                );
                dustTint.a *= Mathf.Lerp(0.84f, 1.16f, hazePulse);
                dust.color = dustTint;
            }
        }
    }

    private void ApplyAtmospherePalette()
    {
        Color accent = PaletteAccentColor(colorPalette);
        if (horizonGlow != null)
        {
            float alpha = horizonGlow.color.a > 0.0001f ? horizonGlow.color.a : 0.14f;
            horizonGlow.color = new Color(accent.r, accent.g, accent.b, alpha);
        }

        if (skyColorWash != null)
        {
            float washAlpha = skyColorWash.color.a > 0.0001f ? skyColorWash.color.a : 0.06f;
            Color warmAccent = Color.Lerp(accent, new Color(1f, 0.66f, 0.38f), 0.32f);
            skyColorWash.color = new Color(warmAccent.r, warmAccent.g, warmAccent.b, washAlpha);
        }

        if (leftVignette != null)
        {
            leftVignette.color = new Color(0f, 0f, 0f, 0.16f);
        }

        if (rightVignette != null)
        {
            rightVignette.color = new Color(0f, 0f, 0f, 0.16f);
        }
    }

    private static float Wave01(float value)
    {
        return 0.5f + Mathf.Sin(value) * 0.5f;
    }

    private void CreateAmbientShape(
        Transform parent,
        int index,
        Vector2 anchor,
        Vector2 size,
        float rotation,
        Color color)
    {
        if (ambientRects == null || ambientBasePositions == null || index < 0 || index >= ambientRects.Length)
        {
            return;
        }

        RawImage panel = CreateRawPanel(
            "AmbientShape_" + index,
            parent,
            anchor,
            anchor,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            size,
            color,
            GetAmbientBlobTexture(),
            new Rect(0f, 0f, 1f, 1f)
        );
        panel.raycastTarget = false;

        RectTransform rect = panel.rectTransform;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        ambientRects[index] = rect;
        ambientBasePositions[index] = rect.anchoredPosition;
    }

    private static Color PaletteAccentColor(PlayerColorPalette palette)
    {
        switch (palette)
        {
            case PlayerColorPalette.Ember:
                return new Color(1f, 0.56f, 0.24f);
            case PlayerColorPalette.Mono:
                return new Color(0.78f, 0.86f, 0.96f);
            default:
                return new Color(0.24f, 0.82f, 1f);
        }
    }

    private static string AtmospherePresetLabel(MenuAtmospherePreset preset)
    {
        switch (preset)
        {
            case MenuAtmospherePreset.Morning:
                return "Morning";
            case MenuAtmospherePreset.Dusk:
                return "Dusk";
            default:
                return "Noon";
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

    private static Color AtmospherePresetColor(MenuAtmospherePreset preset)
    {
        switch (preset)
        {
            case MenuAtmospherePreset.Morning:
                return new Color(1f, 0.86f, 0.62f);
            case MenuAtmospherePreset.Dusk:
                return new Color(1f, 0.70f, 0.56f);
            default:
                return new Color(0.70f, 0.92f, 1f);
        }
    }

    private void GetAtmospherePresetTargets(
        out float dayBase,
        out float warmBase,
        out float dayAmplitude,
        out float warmAmplitude,
        out float driftSpeed)
    {
        switch (atmospherePreset)
        {
            case MenuAtmospherePreset.Morning:
                dayBase = 0.64f;
                warmBase = 0.68f;
                dayAmplitude = 0.12f;
                warmAmplitude = 0.10f;
                driftSpeed = 0.046f;
                break;
            case MenuAtmospherePreset.Dusk:
                dayBase = 0.36f;
                warmBase = 0.84f;
                dayAmplitude = 0.10f;
                warmAmplitude = 0.09f;
                driftSpeed = 0.034f;
                break;
            default:
                dayBase = 0.84f;
                warmBase = 0.34f;
                dayAmplitude = 0.08f;
                warmAmplitude = 0.07f;
                driftSpeed = 0.042f;
                break;
        }
    }

    private static string LevelDescription(int level)
    {
        switch (Mathf.Clamp(level, 1, 5))
        {
            case 1:
                return "1-minute straight tutorial lane";
            case 2:
                return "Teaching route + A->B chain";
            case 3:
                return "Trampoline + moving platform + dual rhythm gates";
            case 4:
                return "Longest challenge + high vertical route";
            default:
                return "Boss mechanism chain + anchor pressure section";
        }
    }

    private static RectTransform CreateEmptyRect(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
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
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
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
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.text = value;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        if (fontSize >= 48)
        {
            text.fontStyle = FontStyle.Bold;
            AddSoftShadow(text, new Color(0f, 0f, 0f, 0.38f), new Vector2(0f, -1.0f));
            AddSubtleOutline(text, new Color(0f, 0f, 0f, 0.15f), new Vector2(0.5f, -0.5f));
        }
        else
        {
            text.fontStyle = FontStyle.Normal;
            AddSoftShadow(text, new Color(0f, 0f, 0f, 0.50f), new Vector2(0f, -1.1f));
            AddSubtleOutline(text, new Color(0f, 0f, 0f, 0.20f), new Vector2(0.55f, -0.55f));
        }
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
        ColorBlock cb = button.colors;
        cb.normalColor = background;
        cb.highlightedColor = background * 1.1f;
        cb.pressedColor = background * 0.85f;
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(background.r, background.g, background.b, 0.3f);
        button.colors = cb;

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

        CreatePanel(
            name + "_BottomShade",
            image.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, 2f),
            new Color(0f, 0f, 0f, 0.22f)
        ).raycastTarget = false;

        Text label = CreateText(
            name + "_Label",
            image.transform,
            font,
            title,
            26,
            TextAnchor.MiddleCenter,
            Color.white,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
        AddSoftShadow(label, new Color(0f, 0f, 0f, 0.62f), new Vector2(0f, -1.5f));
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

    private static void SetFontSize(Text text, int size)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = Mathf.Clamp(size, 14, 72);
    }

    private Text CreateSelectorRow(
        Transform parent,
        Font font,
        string label,
        float y,
        Action onMinus,
        Action onPlus)
    {
        CreateText(
            label + "_Label",
            parent,
            font,
            label,
            34,
            TextAnchor.MiddleLeft,
            new Color(0.85f, 0.92f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(14f, y),
            new Vector2(320f, 66f)
        );

        Text valueText = CreateText(
            label + "_Value",
            parent,
            font,
            "--",
            35,
            TextAnchor.MiddleCenter,
            Color.white,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(-6f, y),
            new Vector2(386f, 66f)
        );

        Button minusButton = CreateButton(
            label + "_Minus",
            parent,
            font,
            "<",
            new Color(0.18f, 0.28f, 0.40f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-208f, y),
            new Vector2(92f, 58f)
        );
        minusButton.onClick.AddListener(() => onMinus?.Invoke());
        selectorButtons.Add(minusButton);

        Button plusButton = CreateButton(
            label + "_Plus",
            parent,
            font,
            ">",
            new Color(0.18f, 0.28f, 0.40f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-106f, y),
            new Vector2(92f, 58f)
        );
        plusButton.onClick.AddListener(() => onPlus?.Invoke());
        selectorButtons.Add(plusButton);

        return valueText;
    }

    private Button CreateToggleRow(
        Transform parent,
        Font font,
        string label,
        out Text valueText,
        Vector2 anchor,
        Action onToggle)
    {
        CreateText(
            label + "_ToggleLabel",
            parent,
            font,
            label,
            22,
            TextAnchor.MiddleLeft,
            new Color(0.84f, 0.92f, 1f),
            new Vector2(0f, anchor.y),
            new Vector2(0f, anchor.y),
            new Vector2(0f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(200f, 34f)
        );

        valueText = CreateText(
            label + "_ToggleValue",
            parent,
            font,
            "On",
            22,
            TextAnchor.MiddleCenter,
            new Color(0.64f, 1f, 0.78f),
            new Vector2(1f, anchor.y),
            new Vector2(1f, anchor.y),
            new Vector2(1f, 0.5f),
            new Vector2(-118f, 0f),
            new Vector2(84f, 34f)
        );

        Button button = CreateButton(
            label + "_ToggleButton",
            parent,
            font,
            "Toggle",
            new Color(0.16f, 0.54f, 0.36f, 1f),
            new Vector2(1f, anchor.y),
            new Vector2(1f, anchor.y),
            new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f),
            new Vector2(128f, 38f)
        );
        button.onClick.AddListener(() => onToggle?.Invoke());
        return button;
    }

    private Slider CreateSliderRow(
        Transform parent,
        Font font,
        string label,
        string value,
        out Text valueText,
        Vector2 anchor,
        float min,
        float max,
        float initial)
    {
        CreateText(
            label + "_Label",
            parent,
            font,
            label,
            22,
            TextAnchor.MiddleLeft,
            new Color(0.84f, 0.92f, 1f),
            new Vector2(0f, anchor.y),
            new Vector2(0f, anchor.y),
            new Vector2(0f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(190f, 34f)
        );

        valueText = CreateText(
            label + "_Value",
            parent,
            font,
            value,
            22,
            TextAnchor.MiddleRight,
            new Color(1f, 0.95f, 0.82f),
            new Vector2(1f, anchor.y),
            new Vector2(1f, anchor.y),
            new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f),
            new Vector2(100f, 34f)
        );

        GameObject sliderRoot = new GameObject(label + "_SliderRoot", typeof(RectTransform));
        sliderRoot.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderRoot.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, anchor.y);
        sliderRect.anchorMax = new Vector2(1f, anchor.y);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.sizeDelta = new Vector2(-340f, 26f);

        Image track = CreatePanel(
            label + "_Track",
            sliderRoot.transform,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 10f),
            new Color(0.10f, 0.18f, 0.28f, 1f)
        );

        Image fill = CreatePanel(
            label + "_Fill",
            track.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.20f, 0.65f, 0.85f, 1f)
        );

        Image handle = CreatePanel(
            label + "_Handle",
            track.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(22f, 22f),
            new Color(0.90f, 0.95f, 1f, 1f)
        );

        Slider slider = sliderRoot.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = initial;
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = handle;
        slider.handleRect = handle.rectTransform;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }
}
