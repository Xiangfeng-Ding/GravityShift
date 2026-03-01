using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GravityShift/Level Config Database", fileName = "LevelConfigDatabase")]
public class LevelConfigDatabase : ScriptableObject
{
    [Serializable]
    public class LevelTuningEntry
    {
        [SerializeField] private int levelIndex = 1;
        [SerializeField] private string levelName = "Level";
        [SerializeField, TextArea(2, 3)] private string objectiveSummary = "Collect crystals and reach exit.";
        [SerializeField] private LevelStageBand stageBand = LevelStageBand.Teaching;

        [Header("Core")]
        [SerializeField] private int baseTotalCrystals = 24;
        [SerializeField] private int crystalsPerDifficulty = 2;
        [SerializeField, Range(0.50f, 0.95f)] private float requiredRatio = 0.67f;
        [SerializeField, Range(0f, 0.16f)] private float challengeRequiredBonus = 0.04f;
        [SerializeField, Range(0.55f, 1f)] private float twoStarRatio = 0.88f;
        [SerializeField, Range(0.70f, 1f)] private float threeStarRatio = 1f;

        [Header("Map")]
        [SerializeField] private float baseMapLength = 210f;
        [SerializeField] private float mapLengthPerDifficulty = 18f;
        [SerializeField] private int baseFloatingPlatforms = 8;
        [SerializeField] private int floatingPlatformsPerDifficulty = 1;
        [SerializeField] private int baseMechanisms = 2;
        [SerializeField] private int mechanismsPerDifficulty = 1;
        [SerializeField] private int baseGravityAnchorZones = 1;
        [SerializeField] private int gravityAnchorZonesPerDifficulty = 0;

        [Header("Scenery")]
        [SerializeField] private int baseFloraNearCount = 230;
        [SerializeField] private int baseFloraFarCount = 170;
        [SerializeField, Range(0.45f, 2f)] private float scenicDensityScale = 1f;

        [Header("Timer")]
        [SerializeField] private float baseTimeLimitSeconds = 300f;
        [SerializeField] private float timePenaltyPerDifficulty = 12f;
        [SerializeField, Range(0.40f, 1f)] private float challengeTimeScale = 0.78f;

        public int LevelIndex
        {
            get => Mathf.Max(1, levelIndex);
            set => levelIndex = Mathf.Max(1, value);
        }

        public LevelRuntimeConfig BuildRuntimeConfig(GameRunSettings settings, int totalLevelCount)
        {
            int diff = Mathf.Clamp((int)settings.Difficulty, 0, 2);
            bool challengeMode = settings.Mode == GameMode.Challenge;

            int totalCrystals = Mathf.Max(8, baseTotalCrystals + diff * Mathf.Max(0, crystalsPerDifficulty));
            if (challengeMode)
            {
                totalCrystals += Mathf.Max(1, Mathf.RoundToInt(crystalsPerDifficulty * 0.5f));
            }

            float effectiveRequiredRatio = Mathf.Clamp(requiredRatio + (challengeMode ? challengeRequiredBonus : 0f), 0.50f, 0.96f);
            int required = Mathf.CeilToInt(totalCrystals * effectiveRequiredRatio);
            if (totalCrystals >= 2)
            {
                required = Mathf.Clamp(required, 1, totalCrystals - 1);
            }
            else
            {
                required = Mathf.Clamp(required, 1, totalCrystals);
            }

            int runtimeLevel = Mathf.Max(1, levelIndex);
            if (runtimeLevel == 1)
            {
                totalCrystals = Mathf.Max(8, totalCrystals - 2);
                int tutorialRequired;
                switch (settings.Difficulty)
                {
                    case GameDifficulty.Easy:
                        tutorialRequired = 3;
                        break;
                    case GameDifficulty.Hard:
                        tutorialRequired = 5;
                        break;
                    default:
                        tutorialRequired = 4;
                        break;
                }

                required = totalCrystals >= 2
                    ? Mathf.Clamp(tutorialRequired, 1, totalCrystals - 1)
                    : Mathf.Clamp(tutorialRequired, 1, totalCrystals);
            }
            else if (runtimeLevel == 2)
            {
                totalCrystals = Mathf.Max(8, totalCrystals - 4);
                int levelOneRequired;
                switch (settings.Difficulty)
                {
                    case GameDifficulty.Easy:
                        levelOneRequired = 4;
                        break;
                    case GameDifficulty.Hard:
                        levelOneRequired = 7;
                        break;
                    default:
                        levelOneRequired = 6;
                        break;
                }
                required = totalCrystals >= 2
                    ? Mathf.Clamp(levelOneRequired, 1, totalCrystals - 1)
                    : Mathf.Clamp(levelOneRequired, 1, totalCrystals);
            }

            int threeStar = Mathf.Clamp(Mathf.CeilToInt(totalCrystals * Mathf.Clamp(threeStarRatio, 0.70f, 1f)), required, totalCrystals);
            int twoStar = Mathf.Clamp(Mathf.CeilToInt(totalCrystals * Mathf.Clamp(twoStarRatio, 0.55f, 1f)), required, threeStar);

            if (totalCrystals >= 3)
            {
                if (threeStar < required + 2)
                {
                    threeStar = totalCrystals;
                }

                if (twoStar >= threeStar)
                {
                    twoStar = threeStar - 1;
                }
            }

            float mapLength = baseMapLength + diff * mapLengthPerDifficulty;
            if (challengeMode)
            {
                mapLength += Mathf.Max(4f, mapLengthPerDifficulty * 0.45f);
            }

            int floatingCount = baseFloatingPlatforms + diff * Mathf.Max(0, floatingPlatformsPerDifficulty);
            int mechanismCount = baseMechanisms + diff * Mathf.Max(0, mechanismsPerDifficulty) + (challengeMode ? 1 : 0);
            int anchorCount = baseGravityAnchorZones + diff * Mathf.Max(0, gravityAnchorZonesPerDifficulty);
            if (stageBand == LevelStageBand.Boss)
            {
                anchorCount += 1;
            }

            float timeLimit = Mathf.Max(72f, baseTimeLimitSeconds - diff * Mathf.Max(0f, timePenaltyPerDifficulty));
            if (challengeMode)
            {
                timeLimit *= Mathf.Clamp(challengeTimeScale, 0.40f, 1f);
            }
            if (runtimeLevel == 1)
            {
                timeLimit += challengeMode ? 16f : 28f;
            }
            else if (runtimeLevel == 2)
            {
                timeLimit += challengeMode ? 20f : 36f;
            }

            int floraNear = Mathf.RoundToInt(baseFloraNearCount * (1f + diff * 0.14f) * (challengeMode ? 1.08f : 1f));
            int floraFar = Mathf.RoundToInt(baseFloraFarCount * (1f + diff * 0.12f) * (challengeMode ? 1.08f : 1f));

            return new LevelRuntimeConfig
            {
                LevelIndex = Mathf.Max(1, levelIndex),
                TotalLevelCount = Mathf.Max(1, totalLevelCount),
                LevelName = string.IsNullOrWhiteSpace(levelName) ? ("Level " + Mathf.Max(1, levelIndex)) : levelName.Trim(),
                ObjectiveSummary = string.IsNullOrWhiteSpace(objectiveSummary) ? "Collect enough crystals and reach exit." : objectiveSummary.Trim(),
                StageBand = stageBand,
                Difficulty = settings.Difficulty,
                FlipCooldownSeconds = 1f,
                CheckpointPolicy = CheckpointPolicy.Normal,
                TotalCrystals = totalCrystals,
                RequiredCrystals = required,
                TwoStarThreshold = twoStar,
                ThreeStarThreshold = threeStar,
                FloatingPlatformCount = floatingCount,
                MechanismCount = mechanismCount,
                GravityAnchorZoneCount = anchorCount,
                FloraNearCount = floraNear,
                FloraFarCount = floraFar,
                ScenicDensityScale = scenicDensityScale,
                MapLength = mapLength,
                TimeLimitSeconds = timeLimit,
                VisualTheme = settings.VisualTheme,
                ColorPalette = settings.ColorPalette
            };
        }

        public static LevelTuningEntry CreateDefault(
            int index,
            string displayName,
            string summary,
            LevelStageBand band,
            int baseCrystals,
            int crystalsStep,
            float requiredRatioValue,
            float mapLengthValue,
            float mapLengthStep,
            int baseFloating,
            int floatingStep,
            int baseMechanism,
            int mechanismStep,
            int baseAnchors,
            float baseTime,
            float timeStep,
            int floraNear,
            int floraFar,
            float densityScale,
            float challengeScale,
            float twoStarRatioValue,
            float threeStarRatioValue)
        {
            return new LevelTuningEntry
            {
                levelIndex = Mathf.Max(1, index),
                levelName = displayName,
                objectiveSummary = summary,
                stageBand = band,
                baseTotalCrystals = Mathf.Max(8, baseCrystals),
                crystalsPerDifficulty = Mathf.Max(0, crystalsStep),
                requiredRatio = Mathf.Clamp(requiredRatioValue, 0.50f, 0.95f),
                challengeRequiredBonus = 0.04f,
                twoStarRatio = Mathf.Clamp(twoStarRatioValue, 0.55f, 1f),
                threeStarRatio = Mathf.Clamp(threeStarRatioValue, 0.70f, 1f),
                baseMapLength = Mathf.Max(120f, mapLengthValue),
                mapLengthPerDifficulty = Mathf.Max(0f, mapLengthStep),
                baseFloatingPlatforms = Mathf.Max(0, baseFloating),
                floatingPlatformsPerDifficulty = Mathf.Max(0, floatingStep),
                baseMechanisms = Mathf.Max(1, baseMechanism),
                mechanismsPerDifficulty = Mathf.Max(0, mechanismStep),
                baseGravityAnchorZones = Mathf.Max(0, baseAnchors),
                gravityAnchorZonesPerDifficulty = 0,
                baseFloraNearCount = Mathf.Max(20, floraNear),
                baseFloraFarCount = Mathf.Max(20, floraFar),
                scenicDensityScale = Mathf.Clamp(densityScale, 0.45f, 2f),
                baseTimeLimitSeconds = Mathf.Max(60f, baseTime),
                timePenaltyPerDifficulty = Mathf.Max(0f, timeStep),
                challengeTimeScale = Mathf.Clamp(challengeScale, 0.40f, 1f)
            };
        }
    }

    [SerializeField] private List<LevelTuningEntry> levels = new List<LevelTuningEntry>();
    [SerializeField] private int startingUnlockedLevel = 1;

    private static LevelConfigDatabase runtimeFallback;

    public int LevelCount => Mathf.Max(1, levels != null ? levels.Count : 0);
    public int StartingUnlockedLevel => Mathf.Clamp(startingUnlockedLevel, 1, LevelCount);

    public bool HasValidLevels()
    {
        return levels != null && levels.Count > 0;
    }

    public LevelRuntimeConfig BuildConfig(GameRunSettings settings)
    {
        EnsureDefaultLevelsIfEmpty();
        EnsureTutorialLevelInsertedIfLegacy();
        int count = Mathf.Max(1, levels.Count);
        int levelIndex = Mathf.Clamp(settings.LevelIndex, 1, count);
        LevelTuningEntry tuning = GetTuning(levelIndex);
        if (tuning == null)
        {
            tuning = levels[Mathf.Clamp(levelIndex - 1, 0, levels.Count - 1)];
        }

        return tuning.BuildRuntimeConfig(settings, count);
    }

    public int ClampLevelIndex(int levelIndex)
    {
        return Mathf.Clamp(levelIndex, 1, LevelCount);
    }

    public LevelTuningEntry GetTuning(int levelIndex)
    {
        if (levels == null || levels.Count == 0)
        {
            return null;
        }

        int clamped = Mathf.Max(1, levelIndex);
        for (int i = 0; i < levels.Count; i++)
        {
            LevelTuningEntry entry = levels[i];
            if (entry != null && entry.LevelIndex == clamped)
            {
                return entry;
            }
        }

        return null;
    }

    public static LevelConfigDatabase LoadOrCreateRuntime()
    {
        LevelConfigDatabase loaded = Resources.Load<LevelConfigDatabase>("LevelConfigDatabase");
        if (loaded != null)
        {
            loaded.EnsureDefaultLevelsIfEmpty();
            loaded.EnsureTutorialLevelInsertedIfLegacy();
            loaded.NormalizeLevelIndexes();
            return loaded;
        }

        if (runtimeFallback == null)
        {
            runtimeFallback = CreateInstance<LevelConfigDatabase>();
            runtimeFallback.hideFlags = HideFlags.DontUnloadUnusedAsset;
            runtimeFallback.EnsureDefaultLevelsIfEmpty();
            runtimeFallback.EnsureTutorialLevelInsertedIfLegacy();
            runtimeFallback.NormalizeLevelIndexes();
        }

        return runtimeFallback;
    }

    private void OnEnable()
    {
        EnsureDefaultLevelsIfEmpty();
        EnsureTutorialLevelInsertedIfLegacy();
        NormalizeLevelIndexes();
    }

    private void EnsureDefaultLevelsIfEmpty()
    {
        if (levels != null && levels.Count > 0)
        {
            return;
        }

        levels = new List<LevelTuningEntry>
        {
            LevelTuningEntry.CreateDefault(
                1,
                "Tutorial: First Steps",
                "Straight 1-minute tutorial: move, jump, flip to activate a switch, then reach the exit.",
                LevelStageBand.Teaching,
                12,
                1,
                0.55f,
                118f,
                6f,
                2,
                0,
                1,
                0,
                0,
                95f,
                4f,
                130,
                100,
                0.88f,
                0.92f,
                0.75f,
                0.90f),
            LevelTuningEntry.CreateDefault(
                2,
                "L1 Teaching Route",
                "Learn gravity flip, collect crystals, clear chain A->B, unlock exit.",
                LevelStageBand.Teaching,
                24,
                2,
                0.67f,
                214f,
                18f,
                8,
                1,
                2,
                1,
                0,
                312f,
                10f,
                220,
                170,
                1.00f,
                0.78f,
                0.85f,
                1.00f),
            LevelTuningEntry.CreateDefault(
                3,
                "L2 Advanced Flow",
                "Use trampoline, moving platform, and rhythm A->B windows with tighter hazards.",
                LevelStageBand.Advanced,
                30,
                3,
                0.67f,
                252f,
                22f,
                11,
                2,
                3,
                1,
                1,
                304f,
                14f,
                260,
                190,
                1.06f,
                0.80f,
                0.85f,
                1.00f),
            LevelTuningEntry.CreateDefault(
                4,
                "L3 Challenge Run",
                "Long route with denser hazards, stronger rhythm pressure, and stricter pathing.",
                LevelStageBand.Challenge,
                36,
                3,
                0.71f,
                308f,
                24f,
                14,
                2,
                4,
                1,
                2,
                282f,
                16f,
                300,
                220,
                1.14f,
                0.78f,
                0.87f,
                1.00f),
            LevelTuningEntry.CreateDefault(
                5,
                "L4 Boss Mechanism",
                "Boss chain: combined anchor zones, dual gates, and final pressure route.",
                LevelStageBand.Boss,
                42,
                4,
                0.74f,
                344f,
                26f,
                16,
                2,
                5,
                1,
                3,
                270f,
                18f,
                340,
                260,
                1.22f,
                0.76f,
                0.88f,
                1.00f)
        };

        startingUnlockedLevel = 1;
    }

    private void EnsureTutorialLevelInsertedIfLegacy()
    {
        if (levels == null || levels.Count != 4)
        {
            return;
        }

        levels.Insert(
            0,
            LevelTuningEntry.CreateDefault(
                1,
                "Tutorial: First Steps",
                "Straight 1-minute tutorial: move, jump, flip to activate a switch, then reach the exit.",
                LevelStageBand.Teaching,
                12,
                1,
                0.55f,
                118f,
                6f,
                2,
                0,
                1,
                0,
                0,
                95f,
                4f,
                130,
                100,
                0.88f,
                0.92f,
                0.75f,
                0.90f));
    }

    private void NormalizeLevelIndexes()
    {
        if (levels == null || levels.Count == 0)
        {
            return;
        }

        levels.Sort((a, b) =>
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }

            return a.LevelIndex.CompareTo(b.LevelIndex);
        });

        int index = 1;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == null)
            {
                continue;
            }

            levels[i].LevelIndex = index++;
        }
    }
}
