using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class GravityAnchorZone : MonoBehaviour
{
    [SerializeField] private string lockHint = "Gravity anchor active: flip disabled";
    [SerializeField] private string unlockHint = "Left anchor zone: flip restored";
    [SerializeField] private float hintCooldown = 0.7f;

    private readonly HashSet<PlayerGravityMotor> trackedPlayers = new HashSet<PlayerGravityMotor>();
    private float nextHintTime = -100f;
    private GravitySystem activeGravity;
    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        zoneCollider.isTrigger = true;
    }

    private void OnEnable()
    {
        PlayerGravityMotor.OnAnyRespawned += HandleAnyRespawned;
    }

    private void OnDisable()
    {
        PlayerGravityMotor.OnAnyRespawned -= HandleAnyRespawned;
        trackedPlayers.Clear();
        ReleaseLock();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerGravityMotor player = ResolvePlayer(other);
        if (player == null)
        {
            return;
        }

        if (!trackedPlayers.Add(player))
        {
            return;
        }

        ApplyLock();
        ShowHint(lockHint);
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerGravityMotor player = ResolvePlayer(other);
        if (player == null)
        {
            return;
        }

        trackedPlayers.Add(player);
        ApplyLock();
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerGravityMotor player = ResolvePlayer(other);
        if (player == null)
        {
            return;
        }

        if (!trackedPlayers.Remove(player))
        {
            return;
        }

        if (trackedPlayers.Count != 0)
        {
            return;
        }

        ReleaseLock();
        ShowHint(unlockHint);
    }

    private void LateUpdate()
    {
        if (trackedPlayers.Count <= 0)
        {
            return;
        }

        bool removedAny = false;
        trackedPlayers.RemoveWhere(player =>
        {
            bool remove = player == null;
            if (remove)
            {
                removedAny = true;
            }
            return remove;
        });

        if (!removedAny)
        {
            return;
        }

        if (trackedPlayers.Count > 0)
        {
            ApplyLock();
            return;
        }

        ReleaseLock();
    }

    private void HandleAnyRespawned(PlayerGravityMotor respawnedPlayer)
    {
        trackedPlayers.Clear();
        if (IsPlayerInsideZone(respawnedPlayer))
        {
            trackedPlayers.Add(respawnedPlayer);
            ApplyLock();
            return;
        }

        ReleaseLock();
    }

    private void ApplyLock()
    {
        activeGravity = GravitySystem.Instance;
        activeGravity?.SetFlipLockedBy(this, true);
    }

    private void ReleaseLock()
    {
        if (activeGravity == null)
        {
            activeGravity = GravitySystem.Instance;
        }

        activeGravity?.SetFlipLockedBy(this, false);
    }

    private static PlayerGravityMotor ResolvePlayer(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        return other.GetComponentInParent<PlayerGravityMotor>();
    }

    private bool IsPlayerInsideZone(PlayerGravityMotor player)
    {
        if (player == null || zoneCollider == null || !zoneCollider.enabled)
        {
            return false;
        }

        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null)
        {
            return zoneCollider.bounds.Intersects(playerCollider.bounds);
        }

        return zoneCollider.bounds.Contains(player.transform.position);
    }

    private void ShowHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        GameDirector director = GameDirector.Instance;
        if (director == null || director.State != GameState.Playing)
        {
            return;
        }

        if (Time.time < nextHintTime)
        {
            return;
        }

        nextHintTime = Time.time + Mathf.Max(0.2f, hintCooldown);
        RuntimeHUD.Instance?.ShowTransientMessage(message, 1.25f);
    }
}
