using System;
using System.Collections.Generic;
using UnityEngine;

public class GravitySystem : MonoBehaviour
{
    [SerializeField] private float gravityStrength = 25f;
    [SerializeField] private float flipCooldown = 1f;
    [SerializeField] private Vector3 defaultDownDirection = Vector3.down;
    [SerializeField] private float lockedHintCooldown = 0.7f;

    public static GravitySystem Instance { get; private set; }

    public event Action<Vector3> OnGravityChanged;
    public event Action<bool> OnFlipLockChanged;

    public Vector3 DownDirection { get; private set; } = Vector3.down;
    public Vector3 UpDirection => -DownDirection;
    public float CooldownLeft => cooldownLeft;
    public float FlipCooldown => flipCooldown;
    public bool CanFlip => cooldownLeft <= 0f && !IsFlipLocked;
    public bool IsFlipLocked => flipLockOwners.Count > 0;

    private float cooldownLeft;
    private readonly HashSet<int> flipLockOwners = new HashSet<int>();
    private float nextLockedHintTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DownDirection = defaultDownDirection.sqrMagnitude > 0.001f
            ? defaultDownDirection.normalized
            : Vector3.down;
        ApplyGravity();
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
        if (cooldownLeft > 0f)
        {
            cooldownLeft = Mathf.Max(0f, cooldownLeft - Time.deltaTime);
        }

        if (RuntimeInput.FlipPressedThisFrame())
        {
            bool flipped = TryFlip();
            if (!flipped && IsFlipLocked && Time.time >= nextLockedHintTime)
            {
                nextLockedHintTime = Time.time + Mathf.Max(0.1f, lockedHintCooldown);
                RuntimeHUD.Instance?.ShowTransientMessage("Flip disabled in anchor zone", 0.95f);
            }
        }
    }

    public bool TryFlip()
    {
        if (cooldownLeft > 0f)
        {
            return false;
        }

        if (IsFlipLocked)
        {
            return false;
        }

        LevelProgress progress = LevelProgress.Instance;
        if (progress != null && progress.LevelCompleted)
        {
            return false;
        }

        SetDownDirection(-DownDirection, true);
        cooldownLeft = flipCooldown;
        progress?.RegisterFlip();
        return true;
    }

    public void ResetToDefault(bool clearCooldown = true)
    {
        SetDownDirection(defaultDownDirection, true);
        if (clearCooldown)
        {
            cooldownLeft = 0f;
            ClearExternalFlipLocks();
        }
    }

    public void SetFlipCooldown(float seconds)
    {
        flipCooldown = Mathf.Clamp(seconds, 0.2f, 3f);
        cooldownLeft = Mathf.Clamp(cooldownLeft, 0f, flipCooldown);
    }

    public void SetFlipLockedBy(UnityEngine.Object owner, bool locked)
    {
        if (owner == null)
        {
            return;
        }

        int id = owner.GetInstanceID();
        bool changed = false;
        if (locked)
        {
            changed = flipLockOwners.Add(id);
        }
        else
        {
            changed = flipLockOwners.Remove(id);
        }

        if (changed)
        {
            OnFlipLockChanged?.Invoke(IsFlipLocked);
        }
    }

    public void ClearExternalFlipLocks()
    {
        if (flipLockOwners.Count == 0)
        {
            return;
        }

        flipLockOwners.Clear();
        OnFlipLockChanged?.Invoke(false);
    }

    public void SetDownDirection(Vector3 newDownDirection, bool notify)
    {
        Vector3 normalized = newDownDirection.sqrMagnitude > 0.001f
            ? newDownDirection.normalized
            : Vector3.down;

        DownDirection = normalized;
        ApplyGravity();

        if (notify)
        {
            OnGravityChanged?.Invoke(DownDirection);
        }
    }

    private void ApplyGravity()
    {
        Physics.gravity = DownDirection * gravityStrength;
    }
}
