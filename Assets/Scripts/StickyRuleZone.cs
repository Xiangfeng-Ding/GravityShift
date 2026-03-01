using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StickyRuleZone : MonoBehaviour
{
    private readonly HashSet<PlayerGravityMotor> trackedPlayers = new HashSet<PlayerGravityMotor>();
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
        foreach (PlayerGravityMotor player in trackedPlayers)
        {
            if (player != null)
            {
                player.SetStickyRuleZoneActive(this, false);
            }
        }

        trackedPlayers.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        PlayerGravityMotor player = other.GetComponentInParent<PlayerGravityMotor>();
        if (player == null)
        {
            return;
        }

        if (trackedPlayers.Add(player))
        {
            player.SetStickyRuleZoneActive(this, true);
            RuntimeHUD.Instance?.ShowTransientMessage("Sticky zone active: inverted state needs StickySurface", 1.4f);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null)
        {
            return;
        }

        PlayerGravityMotor player = other.GetComponentInParent<PlayerGravityMotor>();
        if (player == null)
        {
            return;
        }

        if (!trackedPlayers.Contains(player))
        {
            trackedPlayers.Add(player);
        }

        player.SetStickyRuleZoneActive(this, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        PlayerGravityMotor player = other.GetComponentInParent<PlayerGravityMotor>();
        if (player == null)
        {
            return;
        }

        if (trackedPlayers.Remove(player))
        {
            player.SetStickyRuleZoneActive(this, false);
        }
    }

    private void HandleAnyRespawned(PlayerGravityMotor respawnedPlayer)
    {
        foreach (PlayerGravityMotor player in trackedPlayers)
        {
            if (player != null)
            {
                player.SetStickyRuleZoneActive(this, false);
            }
        }

        trackedPlayers.Clear();

        if (!IsPlayerInsideZone(respawnedPlayer))
        {
            return;
        }

        trackedPlayers.Add(respawnedPlayer);
        respawnedPlayer.SetStickyRuleZoneActive(this, true);
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
}
