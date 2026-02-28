using System;
using UnityEngine;

public class LevelProgress : MonoBehaviour
{
    public static LevelProgress Instance { get; private set; }

    public event Action<int, int, int> OnCrystalCountChanged;
    public event Action<int, int> OnRunStatsChanged;
    public event Action OnGateUnlocked;
    public event Action OnLevelCompleted;

    public int CollectedCrystals { get; private set; }
    public int TotalCrystals { get; private set; } = 20;
    public int MinimumRequired { get; private set; } = 15;
    public int TwoStarThreshold { get; private set; } = 18;
    public int ThreeStarThreshold { get; private set; } = 20;
    public int DeathCount { get; private set; }
    public int FlipCount { get; private set; }

    public bool GateUnlocked => CollectedCrystals >= MinimumRequired;
    public bool LevelCompleted { get; private set; }

    private bool gateUnlockSent;
    private bool autoCompleteOnAllCrystals;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ConfigureSession(int totalCrystals, int minimumRequired, int twoStarThreshold, int threeStarThreshold)
    {
        TotalCrystals = Mathf.Max(1, totalCrystals);
        MinimumRequired = Mathf.Clamp(minimumRequired, 1, TotalCrystals);
        TwoStarThreshold = Mathf.Clamp(twoStarThreshold, MinimumRequired, TotalCrystals);
        ThreeStarThreshold = Mathf.Clamp(threeStarThreshold, TwoStarThreshold, TotalCrystals);

        CollectedCrystals = 0;
        DeathCount = 0;
        FlipCount = 0;
        LevelCompleted = false;
        gateUnlockSent = false;
        BroadcastProgress();
    }

    public void SetAutoCompleteOnAllCrystals(bool enabled)
    {
        autoCompleteOnAllCrystals = enabled;
    }

    public void AddCrystal()
    {
        if (LevelCompleted)
        {
            return;
        }

        CollectedCrystals = Mathf.Min(TotalCrystals, CollectedCrystals + 1);
        BroadcastProgress();

        if (GateUnlocked && !gateUnlockSent)
        {
            gateUnlockSent = true;
            OnGateUnlocked?.Invoke();
        }

        if (autoCompleteOnAllCrystals && CollectedCrystals >= TotalCrystals)
        {
            TryCompleteLevel();
        }
    }

    public bool TryCompleteLevel()
    {
        if (LevelCompleted)
        {
            return true;
        }

        if (!GateUnlocked)
        {
            return false;
        }

        LevelCompleted = true;
        OnLevelCompleted?.Invoke();
        return true;
    }

    public void RegisterDeath()
    {
        if (LevelCompleted)
        {
            return;
        }

        DeathCount = Mathf.Max(0, DeathCount + 1);
        OnRunStatsChanged?.Invoke(DeathCount, FlipCount);
    }

    public void RegisterFlip()
    {
        if (LevelCompleted)
        {
            return;
        }

        FlipCount = Mathf.Max(0, FlipCount + 1);
        OnRunStatsChanged?.Invoke(DeathCount, FlipCount);
    }

    public int CalculateStars()
    {
        if (CollectedCrystals >= ThreeStarThreshold)
        {
            return 3;
        }

        if (CollectedCrystals >= TwoStarThreshold)
        {
            return 2;
        }

        if (CollectedCrystals >= MinimumRequired)
        {
            return 1;
        }

        return 0;
    }

    private void BroadcastProgress()
    {
        OnCrystalCountChanged?.Invoke(CollectedCrystals, TotalCrystals, MinimumRequired);
        OnRunStatsChanged?.Invoke(DeathCount, FlipCount);
    }
}
