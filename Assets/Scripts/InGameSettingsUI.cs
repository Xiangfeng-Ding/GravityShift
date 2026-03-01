using System;
using UnityEngine;
using UnityEngine.UI;

public class InGameSettingsUI : MonoBehaviour
{
    public event Action<GameDifficulty> OnDifficultyChanged;
    public event Action<float> OnMouseSensitivityChanged;
    public event Action<float> OnCameraFovChanged;
    public event Action<float> OnCameraSmoothnessChanged;
    public event Action<float> OnCameraAutoRecenterChanged;
    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnFeedbackIntensityChanged;
    public event Action<bool> OnCameraShakeChanged;
    public event Action<MotionIntensityPreset> OnMotionPresetChanged;
    public event Action<MotionStyleProfile> OnMotionStyleChanged;
    public event Action<bool> OnVisibilityChanged;

    public bool IsOpen => rootCanvas != null && rootCanvas.enabled;

    private Canvas rootCanvas;
    private GameObject panel;
    private Image panelGlow;
    private Image panelGlass;
    private Image dimBackground;
    private RectTransform[] ambientRects;
    private Image[] ambientImages;
    private Vector2[] ambientBasePositions;
    private float[] ambientSpeeds;
    private float[] ambientAmplitudes;
    private float[] ambientPhases;

    private Slider sensitivitySlider;
    private Slider fovSlider;
    private Slider cameraSmoothnessSlider;
    private Slider autoRecenterSlider;
    private Slider volumeSlider;
    private Slider feedbackSlider;
    private Text difficultyValueText;
    private Text difficultyDetailText;
    private Text sensitivityValueText;
    private Text fovValueText;
    private Text cameraSmoothnessValueText;
    private Text autoRecenterValueText;
    private Text volumeValueText;
    private Text feedbackValueText;
    private Text shakeValueText;
    private Text motionPresetValueText;
    private Text motionStyleValueText;
    private Button shakeToggleButton;

    private GameDifficulty difficulty = GameDifficulty.Normal;
    private bool cameraShakeEnabled = true;
    private MotionIntensityPreset motionPreset = MotionIntensityPreset.Normal;
    private MotionStyleProfile motionStyle = MotionStyleProfile.Stable;
    private bool suppressCallbacks;
    private float uiAnimTime;

    private void Awake()
    {
        BuildUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        AnimateVisuals(Time.unscaledDeltaTime);
    }

    public void Initialize(GameRunSettings settings)
    {
        suppressCallbacks = true;
        difficulty = (GameDifficulty)Mathf.Clamp((int)settings.Difficulty, 0, 2);
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
        suppressCallbacks = false;
    }

    public void Toggle()
    {
        SetVisible(!IsOpen);
    }

    public void SetVisible(bool visible)
    {
        if (rootCanvas == null)
        {
            return;
        }

        if (rootCanvas.enabled == visible)
        {
            return;
        }

        rootCanvas.enabled = visible;
        OnVisibilityChanged?.Invoke(visible);
    }

    private void AnimateVisuals(float deltaTime)
    {
        uiAnimTime += Mathf.Max(0f, deltaTime);
        float warm = 0.5f + Mathf.Sin(uiAnimTime * 0.08f + 0.7f) * 0.5f;

        if (dimBackground != null)
        {
            dimBackground.color = Color.Lerp(
                new Color(0.02f, 0.05f, 0.09f, 0.70f),
                new Color(0.08f, 0.09f, 0.10f, 0.76f),
                warm
            );
        }

        if (panelGlow != null)
        {
            float alpha = 0.06f + Mathf.Sin(uiAnimTime * 1.15f) * 0.03f;
            panelGlow.color = new Color(0.23f, 0.76f, 1f, Mathf.Clamp(alpha, 0.03f, 0.13f));
        }

        if (panelGlass != null)
        {
            float alpha = 0.045f + Mathf.Sin(uiAnimTime * 0.64f + 1.1f) * 0.015f;
            panelGlass.color = new Color(0.86f, 0.95f, 1f, Mathf.Clamp(alpha, 0.02f, 0.08f));
        }

        if (ambientRects != null && ambientImages != null && ambientBasePositions != null)
        {
            for (int i = 0; i < ambientRects.Length; i++)
            {
                RectTransform rect = ambientRects[i];
                Image image = ambientImages[i];
                if (rect == null || image == null)
                {
                    continue;
                }

                float speed = ambientSpeeds != null && i < ambientSpeeds.Length ? ambientSpeeds[i] : 0.08f;
                float amp = ambientAmplitudes != null && i < ambientAmplitudes.Length ? ambientAmplitudes[i] : 8f;
                float phase = ambientPhases != null && i < ambientPhases.Length ? ambientPhases[i] : i;
                Vector2 basePos = ambientBasePositions[i];

                rect.anchoredPosition = basePos + new Vector2(
                    Mathf.Sin(uiAnimTime * speed + phase) * amp,
                    Mathf.Cos(uiAnimTime * (speed * 0.76f) + phase * 1.2f) * amp * 0.52f
                );

                float alphaPulse = 0.78f + Mathf.Sin(uiAnimTime * (speed * 2.8f) + phase * 0.7f) * 0.22f;
                Color c = image.color;
                image.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alphaPulse) * 0.10f);
            }
        }
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("InGameSettingsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 280;

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

        dimBackground = CreatePanel(
            "Dim",
            canvasObject.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.02f, 0.05f, 0.09f, 0.72f)
        );
        dimBackground.raycastTarget = true;

        CreatePanel(
            "DimTopTint",
            canvasObject.transform,
            new Vector2(0f, 0.42f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.14f, 0.28f, 0.42f, 0.08f)
        ).raycastTarget = false;

        CreatePanel(
            "DimBottomWarm",
            canvasObject.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.36f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.46f, 0.33f, 0.24f, 0.06f)
        ).raycastTarget = false;

        ambientRects = new RectTransform[3];
        ambientImages = new Image[3];
        ambientBasePositions = new Vector2[3];
        ambientSpeeds = new float[] { 0.19f, 0.14f, 0.23f };
        ambientAmplitudes = new float[] { 18f, 12f, 16f };
        ambientPhases = new float[] { 0.1f, 1.8f, 2.9f };

        CreateAmbientBlob(
            canvasObject.transform,
            0,
            new Vector2(0.14f, 0.16f),
            new Vector2(460f, 460f),
            16f,
            new Color(0.25f, 0.70f, 1f, 0.10f)
        );

        CreateAmbientBlob(
            canvasObject.transform,
            1,
            new Vector2(0.86f, 0.82f),
            new Vector2(520f, 520f),
            -14f,
            new Color(0.32f, 0.96f, 0.72f, 0.08f)
        );

        CreateAmbientBlob(
            canvasObject.transform,
            2,
            new Vector2(0.84f, 0.20f),
            new Vector2(380f, 380f),
            8f,
            new Color(0.95f, 0.58f, 0.30f, 0.08f)
        );

        Image panelShadow = CreatePanel(
            "SettingsPanelShadow",
            canvasObject.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 14f),
            new Vector2(868f, 696f),
            new Color(0f, 0f, 0f, 0.50f)
        );
        panelShadow.raycastTarget = false;

        panel = CreatePanel(
            "SettingsPanel",
            canvasObject.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 18f),
            new Vector2(804f, 632f),
            new Color(0.04f, 0.09f, 0.14f, 0.93f)
        ).gameObject;

        CreatePanel(
            "SettingsPanelBorder",
            panel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-6f, -6f),
            new Color(0.62f, 0.86f, 1f, 0.08f)
        ).raycastTarget = false;

        panelGlass = CreatePanel(
            "SettingsPanelGlass",
            panel.transform,
            new Vector2(0f, 0.60f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            new Color(0.86f, 0.95f, 1f, 0.06f)
        );
        panelGlass.raycastTarget = false;

        panelGlow = CreatePanel(
            "SettingsPanelGlow",
            panel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(-8f, -8f),
            new Color(0.23f, 0.76f, 1f, 0.08f)
        );
        panelGlow.raycastTarget = false;

        CreatePanel(
            "PanelAccent",
            panel.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -2f),
            new Vector2(0f, 4f),
            new Color(0.23f, 0.76f, 1f, 0.9f)
        );

        CreatePanel(
            "PanelBottomAccent",
            panel.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 2f),
            new Vector2(0f, 3f),
            new Color(0.19f, 0.42f, 0.62f, 0.72f)
        ).raycastTarget = false;

        CreateText(
            "Title",
            panel.transform,
            font,
            "Live Settings",
            38,
            TextAnchor.MiddleCenter,
            new Color(0.92f, 0.98f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -38f),
            new Vector2(-30f, 56f)
        );

        CreateText(
            "Hint",
            panel.transform,
            font,
            "Changes apply instantly. Difficulty switch restarts current run.",
            19,
            TextAnchor.MiddleCenter,
            new Color(0.72f, 0.88f, 1f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -82f),
            new Vector2(-30f, 34f)
        );

        difficultyValueText = CreateSelectorRow(
            panel.transform,
            font,
            "Difficulty",
            new Vector2(0.5f, 0.86f),
            () => ChangeDifficulty(-1),
            () => ChangeDifficulty(1)
        );

        difficultyDetailText = CreateText(
            "DifficultyDetail",
            panel.transform,
            font,
            string.Empty,
            18,
            TextAnchor.MiddleRight,
            new Color(0.80f, 0.95f, 1f),
            new Vector2(0f, 0.82f),
            new Vector2(1f, 0.82f),
            new Vector2(1f, 0.5f),
            new Vector2(-18f, 0f),
            new Vector2(-30f, 28f)
        );

        motionPresetValueText = CreateSelectorRow(
            panel.transform,
            font,
            "Motion Preset",
            new Vector2(0.5f, 0.76f),
            () => ChangeMotionPreset(-1),
            () => ChangeMotionPreset(1)
        );

        motionStyleValueText = CreateSelectorRow(
            panel.transform,
            font,
            "Motion Style",
            new Vector2(0.5f, 0.68f),
            () => ChangeMotionStyle(-1),
            () => ChangeMotionStyle(1)
        );

        sensitivitySlider = CreateSliderRow(
            panel.transform,
            font,
            "Mouse Sense",
            out sensitivityValueText,
            new Vector2(0.5f, 0.58f),
            0.4f,
            3.2f,
            1.45f
        );
        sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);

        fovSlider = CreateSliderRow(
            panel.transform,
            font,
            "Camera FOV",
            out fovValueText,
            new Vector2(0.5f, 0.50f),
            55f,
            95f,
            66f
        );
        fovSlider.onValueChanged.AddListener(HandleFovChanged);

        cameraSmoothnessSlider = CreateSliderRow(
            panel.transform,
            font,
            "Camera Smooth",
            out cameraSmoothnessValueText,
            new Vector2(0.5f, 0.42f),
            0f,
            1f,
            0.56f
        );
        cameraSmoothnessSlider.onValueChanged.AddListener(HandleCameraSmoothnessChanged);

        autoRecenterSlider = CreateSliderRow(
            panel.transform,
            font,
            "Auto Recenter",
            out autoRecenterValueText,
            new Vector2(0.5f, 0.34f),
            0f,
            2f,
            0.22f
        );
        autoRecenterSlider.onValueChanged.AddListener(HandleAutoRecenterChanged);

        volumeSlider = CreateSliderRow(
            panel.transform,
            font,
            "Master Volume",
            out volumeValueText,
            new Vector2(0.5f, 0.24f),
            0f,
            1f,
            1f
        );
        volumeSlider.onValueChanged.AddListener(HandleVolumeChanged);

        feedbackSlider = CreateSliderRow(
            panel.transform,
            font,
            "Feedback",
            out feedbackValueText,
            new Vector2(0.5f, 0.16f),
            0f,
            2f,
            1f
        );
        feedbackSlider.onValueChanged.AddListener(HandleFeedbackChanged);

        shakeToggleButton = CreateToggleRow(
            panel.transform,
            font,
            "Camera Shake",
            out shakeValueText,
            new Vector2(0.5f, 0.08f),
            ToggleCameraShake
        );

        float[] rowGuideAnchors = { 0.81f, 0.73f, 0.65f, 0.55f, 0.47f, 0.39f, 0.31f, 0.23f, 0.15f };
        for (int i = 0; i < rowGuideAnchors.Length; i++)
        {
            CreatePanel(
                "RowGuide_" + i,
                panel.transform,
                new Vector2(0f, rowGuideAnchors[i]),
                new Vector2(1f, rowGuideAnchors[i]),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-26f, 2f),
                new Color(0.34f, 0.55f, 0.72f, 0.14f)
            ).raycastTarget = false;
        }

        Button closeButton = CreateButton(
            "CloseButton",
            panel.transform,
            font,
            "Close",
            new Color(0.18f, 0.44f, 0.62f, 1f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 24f),
            new Vector2(220f, 46f)
        );
        closeButton.onClick.AddListener(() => SetVisible(false));

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

        if (!suppressCallbacks)
        {
            OnDifficultyChanged?.Invoke(difficulty);
        }
    }

    private void ChangeMotionPreset(int delta)
    {
        int value = (int)motionPreset + delta;
        if (value < 0)
        {
            value = 2;
        }
        else if (value > 2)
        {
            value = 0;
        }

        motionPreset = (MotionIntensityPreset)value;
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnMotionPresetChanged?.Invoke(motionPreset);
        }
    }

    private void ChangeMotionStyle(int delta)
    {
        int value = (int)motionStyle + delta;
        if (value < 0)
        {
            value = 2;
        }
        else if (value > 2)
        {
            value = 0;
        }

        motionStyle = (MotionStyleProfile)value;
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnMotionStyleChanged?.Invoke(motionStyle);
        }
    }

    private void CreateAmbientBlob(
        Transform parent,
        int index,
        Vector2 anchor,
        Vector2 size,
        float rotation,
        Color color)
    {
        if (ambientRects == null || ambientImages == null || ambientBasePositions == null ||
            index < 0 || index >= ambientRects.Length)
        {
            return;
        }

        Image blob = CreatePanel(
            "AmbientBlob_" + index,
            parent,
            anchor,
            anchor,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            size,
            color
        );
        blob.raycastTarget = false;
        blob.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        ambientImages[index] = blob;
        ambientRects[index] = blob.rectTransform;
        ambientBasePositions[index] = blob.rectTransform.anchoredPosition;
    }

    private void HandleSensitivityChanged(float value)
    {
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnMouseSensitivityChanged?.Invoke(value);
        }
    }

    private void HandleFovChanged(float value)
    {
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnCameraFovChanged?.Invoke(value);
        }
    }

    private void HandleCameraSmoothnessChanged(float value)
    {
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnCameraSmoothnessChanged?.Invoke(value);
        }
    }

    private void HandleAutoRecenterChanged(float value)
    {
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnCameraAutoRecenterChanged?.Invoke(value);
        }
    }

    private void HandleVolumeChanged(float value)
    {
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnMasterVolumeChanged?.Invoke(value);
        }
    }

    private void HandleFeedbackChanged(float value)
    {
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnFeedbackIntensityChanged?.Invoke(value);
        }
    }

    private void ToggleCameraShake()
    {
        cameraShakeEnabled = !cameraShakeEnabled;
        RefreshLabels();
        if (!suppressCallbacks)
        {
            OnCameraShakeChanged?.Invoke(cameraShakeEnabled);
        }
    }

    private void RefreshLabels()
    {
        if (difficultyValueText != null)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    difficultyValueText.text = "Easy";
                    difficultyValueText.color = new Color(0.74f, 1f, 0.80f);
                    break;
                case GameDifficulty.Hard:
                    difficultyValueText.text = "Hard";
                    difficultyValueText.color = new Color(1f, 0.72f, 0.66f);
                    break;
                default:
                    difficultyValueText.text = "Normal";
                    difficultyValueText.color = new Color(0.86f, 0.96f, 1f);
                    break;
            }
        }

        if (motionPresetValueText != null)
        {
            switch (motionPreset)
            {
                case MotionIntensityPreset.Realistic:
                    motionPresetValueText.text = "Realistic";
                    motionPresetValueText.color = new Color(0.82f, 0.96f, 1f);
                    break;
                case MotionIntensityPreset.Cinematic:
                    motionPresetValueText.text = "Cinematic";
                    motionPresetValueText.color = new Color(1f, 0.86f, 0.68f);
                    break;
                default:
                    motionPresetValueText.text = "Normal";
                    motionPresetValueText.color = new Color(0.84f, 1f, 0.82f);
                    break;
            }
        }

        if (motionStyleValueText != null)
        {
            switch (motionStyle)
            {
                case MotionStyleProfile.Agile:
                    motionStyleValueText.text = "Agile";
                    motionStyleValueText.color = new Color(0.78f, 0.96f, 1f);
                    break;
                case MotionStyleProfile.Exaggerated:
                    motionStyleValueText.text = "Exaggerated";
                    motionStyleValueText.color = new Color(1f, 0.78f, 0.66f);
                    break;
                default:
                    motionStyleValueText.text = "Stable";
                    motionStyleValueText.color = new Color(0.78f, 1f, 0.82f);
                    break;
            }
        }

        if (difficultyDetailText != null)
        {
            GameMode mode = GameDirector.Instance != null
                ? GameDirector.Instance.CurrentSettings.Mode
                : GameMode.Adventure;
            float flipCd = GameDirector.ResolveFlipCooldown(difficulty, mode);
            CheckpointPolicy policy = GameDirector.ResolveCheckpointPolicy(difficulty, mode);
            string presetLabel = motionPreset == MotionIntensityPreset.Cinematic
                ? "Cinematic"
                : motionPreset == MotionIntensityPreset.Realistic
                    ? "Realistic"
                    : "Normal";
            string styleLabel = motionStyle == MotionStyleProfile.Exaggerated
                ? "Exaggerated"
                : motionStyle == MotionStyleProfile.Agile
                    ? "Agile"
                    : "Stable";
            difficultyDetailText.text = "FlipCD " + flipCd.ToString("0.00") + "s  |  Checkpoints " + GameDirector.CheckpointPolicyLabel(policy) +
                                        "  |  Motion " + presetLabel + " / " + styleLabel;
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

        if (shakeValueText != null)
        {
            shakeValueText.text = cameraShakeEnabled ? "On" : "Off";
            shakeValueText.color = cameraShakeEnabled
                ? new Color(0.64f, 1f, 0.78f)
                : new Color(1f, 0.72f, 0.70f);
        }

        if (shakeToggleButton != null)
        {
            Color baseColor = cameraShakeEnabled
                ? new Color(0.16f, 0.54f, 0.36f, 1f)
                : new Color(0.52f, 0.22f, 0.22f, 1f);
            ColorBlock cb = shakeToggleButton.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = baseColor * 1.08f;
            cb.pressedColor = baseColor * 0.86f;
            cb.selectedColor = cb.highlightedColor;
            shakeToggleButton.colors = cb;
        }
    }

    private static Text CreateSelectorRow(
        Transform parent,
        Font font,
        string label,
        Vector2 anchor,
        Action onMinus,
        Action onPlus)
    {
        CreateText(
            label + "_SelectorLabel",
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

        Text valueText = CreateText(
            label + "_SelectorValue",
            parent,
            font,
            "--",
            22,
            TextAnchor.MiddleCenter,
            new Color(0.86f, 0.96f, 1f),
            new Vector2(1f, anchor.y),
            new Vector2(1f, anchor.y),
            new Vector2(1f, 0.5f),
            new Vector2(-150f, 0f),
            new Vector2(110f, 34f)
        );

        Button minus = CreateButton(
            label + "_SelectorMinus",
            parent,
            font,
            "<",
            new Color(0.18f, 0.30f, 0.42f, 1f),
            new Vector2(1f, anchor.y),
            new Vector2(1f, anchor.y),
            new Vector2(1f, 0.5f),
            new Vector2(-80f, 0f),
            new Vector2(52f, 34f)
        );
        minus.onClick.AddListener(() => onMinus?.Invoke());

        Button plus = CreateButton(
            label + "_SelectorPlus",
            parent,
            font,
            ">",
            new Color(0.18f, 0.30f, 0.42f, 1f),
            new Vector2(1f, anchor.y),
            new Vector2(1f, anchor.y),
            new Vector2(1f, 0.5f),
            new Vector2(-20f, 0f),
            new Vector2(52f, 34f)
        );
        plus.onClick.AddListener(() => onPlus?.Invoke());

        return valueText;
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
        AddSoftShadow(text, new Color(0f, 0f, 0f, 0.52f), new Vector2(0f, -1.2f));
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
            new Color(0f, 0f, 0f, 0.20f)
        ).raycastTarget = false;

        Text label = CreateText(
            name + "_Label",
            image.transform,
            font,
            title,
            24,
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

    private static Slider CreateSliderRow(
        Transform parent,
        Font font,
        string label,
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
            23,
            TextAnchor.MiddleLeft,
            new Color(0.86f, 0.93f, 1f),
            new Vector2(0f, anchor.y),
            new Vector2(0f, anchor.y),
            new Vector2(0f, 0.5f),
            new Vector2(26f, 0f),
            new Vector2(210f, 36f)
        );

        valueText = CreateText(
            label + "_Value",
            parent,
            font,
            "1.00",
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
            new Color(0.10f, 0.18f, 0.28f, 0.95f)
        );

        CreatePanel(
            label + "_TrackTop",
            track.transform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -1f),
            new Vector2(0f, 2f),
            new Color(1f, 1f, 1f, 0.16f)
        ).raycastTarget = false;

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

    private static Button CreateToggleRow(
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
            23,
            TextAnchor.MiddleLeft,
            new Color(0.84f, 0.92f, 1f),
            new Vector2(0f, anchor.y),
            new Vector2(0f, anchor.y),
            new Vector2(0f, 0.5f),
            new Vector2(26f, 0f),
            new Vector2(220f, 36f)
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
}
