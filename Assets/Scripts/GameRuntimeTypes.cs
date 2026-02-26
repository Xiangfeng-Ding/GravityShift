public enum GameDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public enum GameMode
{
    Adventure = 0,
    Challenge = 1
}

public enum GameState
{
    Menu = 0,
    Playing = 1,
    Completed = 2,
    Failed = 3
}

public enum PlayerVisualTheme
{
    Mechanical = 0,
    Astronaut = 1,
    Minimal = 2
}

public enum PlayerColorPalette
{
    Neon = 0,
    Ember = 1,
    Mono = 2
}

public enum MenuAtmospherePreset
{
    Morning = 0,
    Noon = 1,
    Dusk = 2
}

public enum MotionIntensityPreset
{
    Normal = 0,
    Realistic = 1,
    Cinematic = 2
}

public enum MotionStyleProfile
{
    Stable = 0,
    Agile = 1,
    Exaggerated = 2
}

public enum LevelStageBand
{
    Teaching = 0,
    Advanced = 1,
    Challenge = 2,
    Boss = 3
}

public enum CheckpointPolicy
{
    Dense = 0,
    Normal = 1,
    Sparse = 2
}

public struct GameRunSettings
{
    public int LevelIndex;
    public GameDifficulty Difficulty;
    public GameMode Mode;
    public PlayerVisualTheme VisualTheme;
    public PlayerColorPalette ColorPalette;
    public MenuAtmospherePreset AtmospherePreset;
    public float FeedbackIntensity;
    public bool CameraShakeEnabled;
    public float MouseSensitivity;
    public float CameraFov;
    public float CameraSmoothness;
    public float CameraAutoRecenterSpeed;
    public float MasterVolume;
    public MotionIntensityPreset MotionPreset;
    public MotionStyleProfile MotionStyle;
}

public struct LevelRuntimeConfig
{
    public int LevelIndex;
    public int TotalLevelCount;
    public string LevelName;
    public string ObjectiveSummary;
    public LevelStageBand StageBand;
    public GameDifficulty Difficulty;
    public float FlipCooldownSeconds;
    public CheckpointPolicy CheckpointPolicy;
    public int TotalCrystals;
    public int RequiredCrystals;
    public int TwoStarThreshold;
    public int ThreeStarThreshold;
    public int FloatingPlatformCount;
    public int MechanismCount;
    public int GravityAnchorZoneCount;
    public int FloraNearCount;
    public int FloraFarCount;
    public float ScenicDensityScale;
    public float MapLength;
    public float TimeLimitSeconds;
    public PlayerVisualTheme VisualTheme;
    public PlayerColorPalette ColorPalette;
}

public struct LevelBuildResult
{
    public PlayerGravityMotor Player;
    public GravityCameraFollow CameraFollow;
    public int SpawnedCrystals;
}

public struct LevelRunStats
{
    public float CompletionTimeSeconds;
    public int DeathCount;
    public int FlipCount;
}
