using System;
using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    public static LevelTimer Instance { get; private set; }

    public event Action<float, float> OnTimeChanged;
    public event Action OnTimerExpired;

    public float DurationSeconds { get; private set; }
    public float TimeLeftSeconds { get; private set; }
    public float ElapsedSeconds => Mathf.Max(0f, DurationSeconds - TimeLeftSeconds);
    public bool IsRunning { get; private set; }
    public bool HasExpired { get; private set; }

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

    private void Update()
    {
        if (!IsRunning || HasExpired)
        {
            return;
        }

        if (GameDirector.Instance == null || GameDirector.Instance.State != GameState.Playing)
        {
            return;
        }

        TimeLeftSeconds = Mathf.Max(0f, TimeLeftSeconds - Time.deltaTime);
        OnTimeChanged?.Invoke(TimeLeftSeconds, DurationSeconds);

        if (TimeLeftSeconds <= 0f && !HasExpired)
        {
            HasExpired = true;
            IsRunning = false;
            OnTimerExpired?.Invoke();
        }
    }

    public void Configure(float durationSeconds)
    {
        DurationSeconds = Mathf.Max(1f, durationSeconds);
        TimeLeftSeconds = DurationSeconds;
        HasExpired = false;
        IsRunning = false;
        OnTimeChanged?.Invoke(TimeLeftSeconds, DurationSeconds);
    }

    public void StartTimer()
    {
        if (DurationSeconds <= 0f)
        {
            Configure(60f);
        }

        IsRunning = true;
        HasExpired = false;
    }

    public void StopTimer()
    {
        IsRunning = false;
    }
}
