using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

public class LevelOneRuntimeBuilder : MonoBehaviour
{
    private const string RootName = "RuntimeLevelRoot";
    private const string CrystalPoolKey = "Crystal";
    private const float CrystalCeilingPadding = 0.42f;
    private const float CrystalPlacementProbeRadius = 0.22f;
    private const int TutorialLevelIndex = 1;
    private const int MainDesignLevelCount = 4;

    private readonly List<Vector3> routePoints = new List<Vector3>();
    private readonly List<float> routeDistances = new List<float>();
    private readonly List<Vector3> crystalCandidates = new List<Vector3>();
    private readonly List<Vector3> checkpointPositions = new List<Vector3>();

    private Material routeMaterial;
    private Material routeAccentMaterial;
    private Material wallMaterial;
    private Material platformMaterial;
    private Material crystalMaterial;
    private Material laserMaterial;
    private Material gateMaterial;
    private Material exitMaterial;
    private Material switchMaterial;
    private Material terrainMaterial;
    private Material terrainSoilMaterial;
    private Material terrainRockMaterial;
    private Material mountainNearMaterial;
    private Material mountainNearWarmMaterial;
    private Material mountainFarMaterial;
    private Material mountainFarWarmMaterial;
    private Material treeTrunkMaterial;
    private Material treeLeafMaterial;
    private Material cloudMaterial;
    private Material skyboxMaterial;
    private Material bouncePadMaterial;
    private Material challengeNeonMaterial;
    private Material dangerMaterial;
    private Material collapsePlatformMaterial;
    private Material movingPlatformMaterial;
    private Material floraStemMaterial;
    private Material floraFlowerPetalMaterial;
    private Material floraFlowerCoreMaterial;

    private Material playerPrimaryMaterial;
    private Material playerSecondaryMaterial;
    private Material playerGlowMaterial;
    private PhysicsMaterial playerPhysicsMaterial;
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private readonly List<Texture2D> runtimeTextures = new List<Texture2D>();
    private Mesh pyramidMesh;
    private readonly List<Transform> cloudVisuals = new List<Transform>();

    private Transform root;
    private LevelRuntimeConfig currentConfig;

    public void ClearLevel()
    {
        GameObject knownRoot = root != null ? root.gameObject : null;
        if (knownRoot != null)
        {
            CrystalPickup[] pickups = knownRoot.GetComponentsInChildren<CrystalPickup>(true);
            for (int i = 0; i < pickups.Length; i++)
            {
                CrystalPickup pickup = pickups[i];
                if (pickup != null)
                {
                    pickup.RecycleImmediate();
                }
            }

            knownRoot.SetActive(false);
            Destroy(knownRoot);
            root = null;
        }

        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null && existingRoot != knownRoot)
        {
            existingRoot.SetActive(false);
            Destroy(existingRoot);
        }

        crystalCandidates.Clear();
        checkpointPositions.Clear();
        routePoints.Clear();
        routeDistances.Clear();
        cloudVisuals.Clear();
        DisposeRuntimeMaterials();
        DisposeRuntimeTextures();
    }

    private void OnDestroy()
    {
        DisposeRuntimeMaterials();
        DisposeRuntimeTextures();
        if (playerPhysicsMaterial != null)
        {
            Destroy(playerPhysicsMaterial);
            playerPhysicsMaterial = null;
        }

        if (pyramidMesh != null)
        {
            Destroy(pyramidMesh);
            pyramidMesh = null;
        }
    }

    private static bool IsTutorialLevelIndex(int levelIndex)
    {
        return levelIndex <= TutorialLevelIndex;
    }

    private static int GetDesignLevelFromRuntimeLevelIndex(int levelIndex)
    {
        int shiftedLevel = Mathf.Max(TutorialLevelIndex + 1, levelIndex) - TutorialLevelIndex;
        return Mathf.Clamp(shiftedLevel, 1, MainDesignLevelCount);
    }

    private bool IsTutorialLevel()
    {
        return IsTutorialLevelIndex(currentConfig.LevelIndex);
    }

    private int GetDesignLevel()
    {
        return GetDesignLevelFromRuntimeLevelIndex(currentConfig.LevelIndex);
    }

    public LevelBuildResult BuildLevel(LevelRuntimeConfig config)
    {
        currentConfig = SanitizeRuntimeConfig(config);
        ClearLevel();
        BuildMaterials(currentConfig);
        SetupEnvironment();

        root = new GameObject(RootName).transform;
        crystalCandidates.Clear();

        GenerateRoute(currentConfig.MapLength);
        EnsureRouteValidity(currentConfig.MapLength);
        BuildRouteGeometry();
        BuildRouteFillLights();
        BuildScenicBackdrop(currentConfig.MapLength);
        BuildGlobalReflectionProbe(currentConfig.MapLength);
        BuildProceduralFlora();

        if (IsTutorialLevel())
        {
            BuildTutorialStraightFlow();
        }
        else if (GetDesignLevel() == 1)
        {
            BuildLevelOneFiveRegionFlow();
        }
        else
        {
            BuildMechanisms(currentConfig.MechanismCount);
            BuildSecondaryMechanismChain(currentConfig.MechanismCount);
            BuildFloatingPlatforms(currentConfig.FloatingPlatformCount);
            BuildAdvancedChallengeSetpieces();
            BuildGravityAnchorZones(currentConfig.GravityAnchorZoneCount);
        }

        BuildPathCrystals(currentConfig.TotalCrystals);
        BuildCheckpoints();
        BuildEnergyGateAndExit();
        BuildKillZone();
        BuildHintBoards();
        BuildHelloNpcs();

        int spawnedCrystals = SpawnCrystals(currentConfig.TotalCrystals);
        ValidateCompletionFlow(spawnedCrystals);
        Vector3 spawnOrigin = routePoints.Count > 0 ? routePoints[0] : Vector3.zero;
        PlayerGravityMotor player = BuildPlayer(spawnOrigin + Vector3.up * 2.1f);
        GravityCameraFollow cameraFollow = SetupCamera(player.transform);
        Light sun = EnsureDirectionalLight();
        SetupScenicAtmosphere(sun);

        return new LevelBuildResult
        {
            Player = player,
            CameraFollow = cameraFollow,
            SpawnedCrystals = spawnedCrystals
        };
    }

    private void ValidateCompletionFlow(int spawnedCrystals)
    {
        if (root == null)
        {
            return;
        }

        int exitCount = root.GetComponentsInChildren<ExitZone>(true).Length;
        int energyGateCount = root.GetComponentsInChildren<EnergyGate>(true).Length;
        int checkpointCount = root.GetComponentsInChildren<CheckpointZone>(true).Length;
        int killZoneCount = root.GetComponentsInChildren<KillZone>(true).Length;

        if (exitCount <= 0)
        {
            Debug.LogError("[LevelBuilder] Missing ExitZone. Level cannot be completed.");
        }

        if (energyGateCount <= 0)
        {
            Debug.LogError("[LevelBuilder] Missing EnergyGate. Progression gate is absent.");
        }

        if (checkpointCount <= 0)
        {
            Debug.LogWarning("[LevelBuilder] No checkpoints found. Retry flow may feel punishing.");
        }

        if (killZoneCount <= 0)
        {
            Debug.LogWarning("[LevelBuilder] No kill zone found. Fail/retry loop may break.");
        }

        ValidateLocalCrystalGates();
        ValidatePressureGateWiring();
        ValidateMechanismWiring();

        if (spawnedCrystals <= 0)
        {
            Debug.LogError("[LevelBuilder] Spawned crystal count is zero.");
        }
        else if (currentConfig.RequiredCrystals > spawnedCrystals)
        {
            Debug.LogWarning(
                "[LevelBuilder] Required crystals (" + currentConfig.RequiredCrystals +
                ") exceed spawned crystals (" + spawnedCrystals + "). Session config will be adjusted by GameDirector.");
        }
    }

    private void ValidatePressureGateWiring()
    {
        if (root == null)
        {
            return;
        }

        PressurePlateGate[] gates = root.GetComponentsInChildren<PressurePlateGate>(true);
        if (gates == null || gates.Length == 0)
        {
            return;
        }

        HashSet<PressurePlateTrigger> linkedPlates = new HashSet<PressurePlateTrigger>();
        int missingLinkedPlateCount = 0;

        for (int i = 0; i < gates.Length; i++)
        {
            PressurePlateGate gate = gates[i];
            if (gate == null)
            {
                continue;
            }

            PressurePlateTrigger linkedPlate = gate.LinkedPlate;
            if (linkedPlate == null)
            {
                missingLinkedPlateCount++;
                Debug.LogWarning(
                    "[LevelBuilder] Pressure gate missing linked plate: " + gate.name +
                    ". It will open fail-safe.");
                continue;
            }

            linkedPlates.Add(linkedPlate);
        }

        if (missingLinkedPlateCount > 0)
        {
            Debug.LogWarning(
                "[LevelBuilder] Pressure gate wiring warnings: " + missingLinkedPlateCount +
                " gate(s) missing linked plate references.");
        }

        PressurePlateTrigger[] plates = root.GetComponentsInChildren<PressurePlateTrigger>(true);
        for (int i = 0; i < plates.Length; i++)
        {
            PressurePlateTrigger plate = plates[i];
            if (plate == null || linkedPlates.Contains(plate))
            {
                continue;
            }

            Debug.LogWarning(
                "[LevelBuilder] Pressure plate has no linked gate: " + plate.name +
                ". This can confuse expected progression.");
        }
    }

    private void ValidateMechanismWiring()
    {
        if (root == null)
        {
            return;
        }

        ValidateLaserBarrierWiring();
        ValidateSequentialChainWiring();
        ValidateRhythmChainWiring();
    }

    private void ValidateLaserBarrierWiring()
    {
        LaserBarrier[] barriers = root.GetComponentsInChildren<LaserBarrier>(true);
        if (barriers == null || barriers.Length == 0)
        {
            return;
        }

        int missingLinkCount = 0;
        for (int i = 0; i < barriers.Length; i++)
        {
            LaserBarrier barrier = barriers[i];
            if (barrier == null || barrier.LinkedButton != null)
            {
                continue;
            }

            missingLinkCount++;
            Debug.LogWarning(
                "[LevelBuilder] Laser barrier missing linked button: " + barrier.name +
                ". Barrier may stay permanently locked.");
        }

        if (missingLinkCount > 0)
        {
            Debug.LogWarning(
                "[LevelBuilder] Laser barrier wiring warnings: " + missingLinkCount +
                " barrier(s) missing linked button references.");
        }
    }

    private void ValidateSequentialChainWiring()
    {
        SequentialMechanismChain[] chains = root.GetComponentsInChildren<SequentialMechanismChain>(true);
        if (chains == null || chains.Length == 0)
        {
            return;
        }

        for (int i = 0; i < chains.Length; i++)
        {
            SequentialMechanismChain chain = chains[i];
            if (chain == null)
            {
                continue;
            }

            if (chain.FirstSwitch == null || chain.SecondSwitch == null)
            {
                Debug.LogWarning(
                    "[LevelBuilder] Sequential mechanism chain is missing switch wiring: " + chain.name +
                    ". A->B progression can become invalid.");
            }
        }
    }

    private void ValidateRhythmChainWiring()
    {
        RhythmGateChainController[] controllers = root.GetComponentsInChildren<RhythmGateChainController>(true);
        HashSet<RhythmGate> rhythmGatesUsedByChain = new HashSet<RhythmGate>();

        if (controllers != null && controllers.Length > 0)
        {
            for (int i = 0; i < controllers.Length; i++)
            {
                RhythmGateChainController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                if (controller.SwitchA == null ||
                    controller.SwitchB == null ||
                    controller.GateA == null ||
                    controller.GateB == null)
                {
                    Debug.LogWarning(
                        "[LevelBuilder] Rhythm chain controller missing wiring: " + controller.name +
                        ". Rhythm progression may become impossible.");
                }

                if (controller.GateA != null)
                {
                    rhythmGatesUsedByChain.Add(controller.GateA);
                }
                if (controller.GateB != null)
                {
                    rhythmGatesUsedByChain.Add(controller.GateB);
                }
            }
        }

        RhythmGate[] allRhythmGates = root.GetComponentsInChildren<RhythmGate>(true);
        if (allRhythmGates == null || allRhythmGates.Length == 0)
        {
            return;
        }

        for (int i = 0; i < allRhythmGates.Length; i++)
        {
            RhythmGate gate = allRhythmGates[i];
            if (gate == null)
            {
                continue;
            }

            bool hasActivationSource = gate.LinkedSwitch != null || rhythmGatesUsedByChain.Contains(gate);
            if (!hasActivationSource)
            {
                Debug.LogWarning(
                    "[LevelBuilder] Rhythm gate has no activation source: " + gate.name +
                    ". Link a switch or a chain controller.");
            }
        }
    }

    private void ValidateLocalCrystalGates()
    {
        if (root == null)
        {
            return;
        }

        LocalCrystalGate[] gates = root.GetComponentsInChildren<LocalCrystalGate>(true);
        if (gates == null || gates.Length == 0)
        {
            return;
        }

        Dictionary<LocalCrystalGate, int> gatePickupCounts = new Dictionary<LocalCrystalGate, int>(gates.Length);
        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] != null && !gatePickupCounts.ContainsKey(gates[i]))
            {
                gatePickupCounts.Add(gates[i], 0);
            }
        }

        LocalCrystalPickup[] pickups = root.GetComponentsInChildren<LocalCrystalPickup>(true);
        for (int i = 0; i < pickups.Length; i++)
        {
            LocalCrystalPickup pickup = pickups[i];
            if (pickup == null)
            {
                continue;
            }

            LocalCrystalGate linkedGate = pickup.LinkedGate;
            if (linkedGate == null)
            {
                Debug.LogWarning("[LevelBuilder] Found local crystal pickup without linked gate: " + pickup.name);
                continue;
            }

            if (gatePickupCounts.ContainsKey(linkedGate))
            {
                gatePickupCounts[linkedGate] = gatePickupCounts[linkedGate] + 1;
            }
        }

        foreach (KeyValuePair<LocalCrystalGate, int> kvp in gatePickupCounts)
        {
            LocalCrystalGate gate = kvp.Key;
            if (gate == null)
            {
                continue;
            }

            int available = kvp.Value;
            if (available <= 0)
            {
                Debug.LogError("[LevelBuilder] Local gate has no linked crystals: " + gate.name);
                gate.ClampRequirementToAvailable(1);
                // Fail-safe to avoid hard-lock if configuration accidentally removed local pickups.
                gate.RegisterCrystalCollected();
                continue;
            }

            if (available < gate.RequiredCrystals)
            {
                Debug.LogWarning(
                    "[LevelBuilder] Local gate '" + gate.GateLabel +
                    "' requires " + gate.RequiredCrystals +
                    " but only " + available + " linked crystals exist. Clamping requirement.");
                gate.ClampRequirementToAvailable(available);
            }
        }
    }

    private static LevelRuntimeConfig SanitizeRuntimeConfig(LevelRuntimeConfig config)
    {
        int totalLevelCount = Mathf.Clamp(config.TotalLevelCount <= 0 ? 16 : config.TotalLevelCount, 1, 16);
        config.TotalLevelCount = totalLevelCount;
        config.LevelIndex = Mathf.Clamp(config.LevelIndex, 1, totalLevelCount);
        config.TotalCrystals = Mathf.Clamp(config.TotalCrystals, 8, 120);
        config.RequiredCrystals = Mathf.Clamp(config.RequiredCrystals, 1, config.TotalCrystals);
        config.TwoStarThreshold = Mathf.Clamp(config.TwoStarThreshold, config.RequiredCrystals, config.TotalCrystals);
        config.ThreeStarThreshold = Mathf.Clamp(config.ThreeStarThreshold, config.TwoStarThreshold, config.TotalCrystals);
        config.FloatingPlatformCount = Mathf.Clamp(config.FloatingPlatformCount, 3, 48);
        config.MechanismCount = Mathf.Clamp(config.MechanismCount, 1, 18);
        config.GravityAnchorZoneCount = Mathf.Clamp(config.GravityAnchorZoneCount, 0, 8);
        config.FloraNearCount = Mathf.Clamp(config.FloraNearCount, 20, 1200);
        config.FloraFarCount = Mathf.Clamp(config.FloraFarCount, 20, 1200);
        config.ScenicDensityScale = Mathf.Clamp(config.ScenicDensityScale <= 0f ? 1f : config.ScenicDensityScale, 0.45f, 2f);
        config.MapLength = Mathf.Clamp(config.MapLength, 90f, 520f);
        config.TimeLimitSeconds = Mathf.Clamp(config.TimeLimitSeconds, 60f, 1200f);
        return config;
    }

    private void BuildMaterials(LevelRuntimeConfig config)
    {
        DisposeRuntimeMaterials();

        routeMaterial = CreateMaterial("M_Route", new Color(0.25f, 0.28f, 0.31f), 0.05f, 0.34f);
        bool onboardingLevel = IsTutorialLevelIndex(config.LevelIndex) || GetDesignLevelFromRuntimeLevelIndex(config.LevelIndex) == 1;
        float routeAccentEmissionBoost = onboardingLevel ? 1.35f : 1.02f;
        routeAccentMaterial = CreateMaterial(
            "M_RouteAccent",
            new Color(0.18f, 0.56f, 0.68f),
            0.03f,
            0.63f,
            new Color(0.18f, 0.56f, 0.68f) * routeAccentEmissionBoost
        );
        wallMaterial = CreateMaterial("M_Wall", new Color(0.32f, 0.35f, 0.39f), 0.05f, 0.42f);
        platformMaterial = CreateMaterial("M_Platform", new Color(0.29f, 0.52f, 0.44f), 0.08f, 0.54f);
        crystalMaterial = CreateMaterial("M_Crystal", new Color(0.35f, 0.82f, 0.95f), 0f, 0.78f, new Color(0.24f, 0.62f, 0.92f) * 1.15f);
        laserMaterial = CreateMaterial("M_Laser", new Color(0.96f, 0.23f, 0.15f), 0f, 0.66f, new Color(0.96f, 0.20f, 0.15f) * 1.25f);
        gateMaterial = CreateMaterial("M_Gate", new Color(0.20f, 0.70f, 0.78f), 0f, 0.62f, new Color(0.20f, 0.70f, 0.78f) * 1.05f);
        exitMaterial = CreateMaterial("M_Exit", new Color(0.95f, 0.74f, 0.30f), 0.08f, 0.62f, new Color(0.95f, 0.74f, 0.30f) * 0.95f);
        switchMaterial = CreateMaterial("M_Switch", new Color(1f, 0.50f, 0.20f), 0f, 0.6f, new Color(1f, 0.50f, 0.20f) * 1.3f);
        terrainMaterial = CreateMaterial("M_TerrainGrass", new Color(0.30f, 0.43f, 0.29f), 0.02f, 0.40f);
        terrainSoilMaterial = CreateMaterial("M_TerrainSoil", new Color(0.39f, 0.30f, 0.22f), 0.01f, 0.30f);
        terrainRockMaterial = CreateMaterial("M_TerrainRock", new Color(0.31f, 0.31f, 0.32f), 0.03f, 0.28f);
        mountainNearMaterial = CreateMaterial("M_MountainNearCool", new Color(0.40f, 0.47f, 0.50f), 0.05f, 0.46f);
        mountainNearWarmMaterial = CreateMaterial("M_MountainNearWarm", new Color(0.53f, 0.44f, 0.36f), 0.05f, 0.42f);
        mountainFarMaterial = CreateMaterial("M_MountainFarCool", new Color(0.33f, 0.39f, 0.45f), 0.02f, 0.36f);
        mountainFarWarmMaterial = CreateMaterial("M_MountainFarWarm", new Color(0.45f, 0.38f, 0.34f), 0.02f, 0.33f);
        treeTrunkMaterial = CreateMaterial("M_TreeTrunk", new Color(0.34f, 0.24f, 0.16f), 0.02f, 0.26f);
        treeLeafMaterial = CreateMaterial("M_TreeLeaf", new Color(0.24f, 0.52f, 0.31f), 0.04f, 0.42f);
        cloudMaterial = CreateMaterial("M_Cloud", new Color(0.90f, 0.95f, 0.99f), 0f, 0.72f, new Color(0.90f, 0.95f, 0.99f) * 0.08f);
        skyboxMaterial = CreateSkyboxMaterial();
        bouncePadMaterial = CreateMaterial("M_BouncePad", new Color(0.86f, 0.26f, 0.86f), 0.02f, 0.62f, new Color(0.86f, 0.26f, 0.86f) * 1.12f);
        challengeNeonMaterial = CreateMaterial("M_ChallengeNeon", new Color(0.24f, 0.74f, 0.88f), 0f, 0.60f, new Color(0.24f, 0.74f, 0.88f) * 1.08f);
        dangerMaterial = CreateMaterial("M_Danger", new Color(0.90f, 0.28f, 0.24f), 0.04f, 0.58f, new Color(0.90f, 0.28f, 0.24f) * 1.02f);
        collapsePlatformMaterial = CreateMaterial("M_Collapse", new Color(0.92f, 0.60f, 0.18f), 0.03f, 0.45f, new Color(1f, 0.66f, 0.20f) * 1.25f);
        movingPlatformMaterial = CreateMaterial("M_MovingPlatform", new Color(0.16f, 0.72f, 0.92f), 0.06f, 0.68f, new Color(0.16f, 0.72f, 0.92f) * 1.25f);
        floraStemMaterial = CreateMaterial("M_FloraStem", new Color(0.22f, 0.46f, 0.25f), 0.01f, 0.30f);
        floraFlowerPetalMaterial = CreateMaterial("M_FloraPetal", new Color(0.90f, 0.64f, 0.74f), 0.01f, 0.58f);
        floraFlowerCoreMaterial = CreateMaterial("M_FloraCore", new Color(0.95f, 0.82f, 0.32f), 0.02f, 0.62f, new Color(0.95f, 0.82f, 0.32f) * 0.28f);

        ResolvePlayerPalette(
            config.VisualTheme,
            config.ColorPalette,
            out Color primaryColor,
            out Color secondaryColor,
            out Color glowColor);

        playerPrimaryMaterial = CreateMaterial("M_PlayerPrimary", primaryColor, 0.05f, 0.45f);
        playerSecondaryMaterial = CreateMaterial("M_PlayerSecondary", secondaryColor, 0.25f, 0.62f);
        playerGlowMaterial = CreateMaterial("M_PlayerGlow", glowColor, 0f, 0.8f, glowColor * 1.6f);

        ApplyMaterialDetailTextures();

        TrackRuntimeMaterial(routeMaterial);
        TrackRuntimeMaterial(routeAccentMaterial);
        TrackRuntimeMaterial(wallMaterial);
        TrackRuntimeMaterial(platformMaterial);
        TrackRuntimeMaterial(crystalMaterial);
        TrackRuntimeMaterial(laserMaterial);
        TrackRuntimeMaterial(gateMaterial);
        TrackRuntimeMaterial(exitMaterial);
        TrackRuntimeMaterial(switchMaterial);
        TrackRuntimeMaterial(terrainMaterial);
        TrackRuntimeMaterial(terrainSoilMaterial);
        TrackRuntimeMaterial(terrainRockMaterial);
        TrackRuntimeMaterial(mountainNearMaterial);
        TrackRuntimeMaterial(mountainNearWarmMaterial);
        TrackRuntimeMaterial(mountainFarMaterial);
        TrackRuntimeMaterial(mountainFarWarmMaterial);
        TrackRuntimeMaterial(treeTrunkMaterial);
        TrackRuntimeMaterial(treeLeafMaterial);
        TrackRuntimeMaterial(cloudMaterial);
        TrackRuntimeMaterial(skyboxMaterial);
        TrackRuntimeMaterial(bouncePadMaterial);
        TrackRuntimeMaterial(challengeNeonMaterial);
        TrackRuntimeMaterial(dangerMaterial);
        TrackRuntimeMaterial(collapsePlatformMaterial);
        TrackRuntimeMaterial(movingPlatformMaterial);
        TrackRuntimeMaterial(floraStemMaterial);
        TrackRuntimeMaterial(floraFlowerPetalMaterial);
        TrackRuntimeMaterial(floraFlowerCoreMaterial);
        TrackRuntimeMaterial(playerPrimaryMaterial);
        TrackRuntimeMaterial(playerSecondaryMaterial);
        TrackRuntimeMaterial(playerGlowMaterial);
    }

    private static void ResolvePlayerPalette(
        PlayerVisualTheme theme,
        PlayerColorPalette palette,
        out Color primary,
        out Color secondary,
        out Color glow)
    {
        switch (palette)
        {
            case PlayerColorPalette.Ember:
                primary = new Color(0.95f, 0.84f, 0.70f);
                secondary = new Color(0.48f, 0.24f, 0.14f);
                glow = new Color(1f, 0.52f, 0.24f);
                break;
            case PlayerColorPalette.Mono:
                primary = new Color(0.86f, 0.89f, 0.92f);
                secondary = new Color(0.24f, 0.27f, 0.33f);
                glow = new Color(0.79f, 0.88f, 1f);
                break;
            default:
                primary = new Color(0.92f, 0.95f, 1f);
                secondary = new Color(0.26f, 0.37f, 0.52f);
                glow = new Color(0.16f, 0.95f, 1f);
                break;
        }

        if (theme == PlayerVisualTheme.Mechanical)
        {
            secondary = Color.Lerp(secondary, new Color(0.30f, 0.31f, 0.36f), 0.48f);
        }
        else if (theme == PlayerVisualTheme.Minimal)
        {
            secondary = Color.Lerp(secondary, primary, 0.4f);
        }
    }

    private void SetupEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.38f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.25f, 0.22f);
        RenderSettings.ambientIntensity = 1.20f;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.68f, 0.73f, 0.77f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        float fogScale = Mathf.Lerp(0.84f, 1.22f, Mathf.InverseLerp(0.45f, 2f, currentConfig.ScenicDensityScale));
        RenderSettings.fogDensity = 0.00155f * fogScale;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1.08f;
        RenderSettings.reflectionBounces = 2;
        RenderSettings.flareStrength = 0.36f;
        RenderSettings.flareFadeSpeed = 3.5f;
        RenderSettings.haloStrength = 0.26f;

        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void GenerateRoute(float mapLength)
    {
        routePoints.Clear();

        if (IsTutorialLevel())
        {
            float straightLength = Mathf.Max(80f, mapLength);
            routePoints.Add(new Vector3(0f, 0f, 0f));
            routePoints.Add(new Vector3(0f, 0f, straightLength * 0.34f));
            routePoints.Add(new Vector3(0f, 0f, straightLength * 0.68f));
            routePoints.Add(new Vector3(0f, 0f, straightLength));
            RebuildRouteDistances();
            return;
        }

        float side = Mathf.Lerp(18f, 30f, Mathf.InverseLerp(120f, 220f, mapLength));
        int level = GetDesignLevel();

        if (level == 1)
        {
            routePoints.Add(new Vector3(0f, 0f, 0f));
            routePoints.Add(new Vector3(0f, 0f, mapLength * 0.22f));
            routePoints.Add(new Vector3(side * 0.82f, 0f, mapLength * 0.44f));
            routePoints.Add(new Vector3(-side * 0.66f, 0f, mapLength * 0.66f));
            routePoints.Add(new Vector3(side * 0.36f, 0f, mapLength * 0.84f));
            routePoints.Add(new Vector3(0f, 0f, mapLength));
        }
        else if (level == 2)
        {
            routePoints.Add(new Vector3(0f, 0f, 0f));
            routePoints.Add(new Vector3(0f, 0f, mapLength * 0.16f));
            routePoints.Add(new Vector3(side, 0f, mapLength * 0.30f));
            routePoints.Add(new Vector3(-side * 0.95f, 0f, mapLength * 0.48f));
            routePoints.Add(new Vector3(side * 1.05f, 0f, mapLength * 0.66f));
            routePoints.Add(new Vector3(-side * 0.56f, 0f, mapLength * 0.84f));
            routePoints.Add(new Vector3(0f, 0f, mapLength));
        }
        else if (level == 3)
        {
            float highSide = side * 1.18f;
            routePoints.Add(new Vector3(0f, 0f, 0f));
            routePoints.Add(new Vector3(0f, 0f, mapLength * 0.12f));
            routePoints.Add(new Vector3(highSide, 0f, mapLength * 0.25f));
            routePoints.Add(new Vector3(-highSide * 1.04f, 0f, mapLength * 0.40f));
            routePoints.Add(new Vector3(highSide * 1.08f, 0f, mapLength * 0.56f));
            routePoints.Add(new Vector3(-highSide * 0.92f, 0f, mapLength * 0.72f));
            routePoints.Add(new Vector3(highSide * 0.48f, 0f, mapLength * 0.88f));
            routePoints.Add(new Vector3(0f, 0f, mapLength));
        }
        else
        {
            float bossSide = side * 1.30f;
            routePoints.Add(new Vector3(0f, 0f, 0f));
            routePoints.Add(new Vector3(0f, 0f, mapLength * 0.10f));
            routePoints.Add(new Vector3(bossSide * 0.95f, 0f, mapLength * 0.20f));
            routePoints.Add(new Vector3(-bossSide * 1.15f, 0f, mapLength * 0.34f));
            routePoints.Add(new Vector3(bossSide * 1.22f, 0f, mapLength * 0.48f));
            routePoints.Add(new Vector3(-bossSide * 1.05f, 0f, mapLength * 0.62f));
            routePoints.Add(new Vector3(bossSide * 0.82f, 0f, mapLength * 0.74f));
            routePoints.Add(new Vector3(-bossSide * 0.55f, 0f, mapLength * 0.86f));
            routePoints.Add(new Vector3(0f, 0f, mapLength));
        }

        RebuildRouteDistances();
    }

    private void EnsureRouteValidity(float mapLength)
    {
        if (routePoints.Count >= 2 && routeDistances.Count == routePoints.Count)
        {
            float totalLength = routeDistances[routeDistances.Count - 1];
            if (totalLength > 1f)
            {
                return;
            }
        }

        routePoints.Clear();
        routePoints.Add(new Vector3(0f, 0f, 0f));
        routePoints.Add(new Vector3(0f, 0f, Mathf.Max(60f, mapLength * 0.5f)));
        routePoints.Add(new Vector3(0f, 0f, Mathf.Max(120f, mapLength)));
        RebuildRouteDistances();
    }

    private void RebuildRouteDistances()
    {
        routeDistances.Clear();
        if (routePoints.Count == 0)
        {
            return;
        }

        routeDistances.Add(0f);
        float cumulative = 0f;
        for (int i = 1; i < routePoints.Count; i++)
        {
            cumulative += Vector3.Distance(routePoints[i - 1], routePoints[i]);
            routeDistances.Add(cumulative);
        }
    }

    private void BuildRouteGeometry()
    {
        const float routeWidth = 18f;
        const float floorThickness = 1f;
        const float wallHeight = 3f;
        float ceilingHeight = GetRouteCeilingHeight();
        float ceilingWallY = ceilingHeight - (wallHeight * 0.5f);

        for (int i = 0; i < routePoints.Count - 1; i++)
        {
            Vector3 start = routePoints[i];
            Vector3 end = routePoints[i + 1];
            Vector3 delta = end - start;
            Vector3 forward = delta.normalized;
            float length = delta.magnitude;
            Vector3 mid = (start + end) * 0.5f;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            CreateOrientedCube(
                "RouteFloor_" + i,
                mid + Vector3.down * (floorThickness * 0.5f),
                new Vector3(routeWidth, floorThickness, length + 1f),
                forward,
                routeMaterial,
                root
            );

            CreateRouteTerrainSupport(i, mid, forward, right, length, routeWidth);

            CreateOrientedCube(
                "RouteCeiling_" + i,
                mid + Vector3.up * (ceilingHeight + floorThickness * 0.5f),
                new Vector3(routeWidth, floorThickness, length + 1f),
                forward,
                routeMaterial,
                root
            );

            CreateOrientedCube(
                "RouteStripe_" + i,
                mid + Vector3.up * 0.03f,
                new Vector3(1.1f, 0.06f, length),
                forward,
                routeAccentMaterial,
                root
            );

            CreateOrientedCube(
                "RouteCeilingStripe_" + i,
                mid + Vector3.up * (ceilingHeight - 0.03f),
                new Vector3(1.1f, 0.06f, length),
                forward,
                routeAccentMaterial,
                root
            );

            CreateOrientedCube(
                "WallL_" + i,
                mid + right * (routeWidth * 0.5f + 0.4f) + Vector3.up * (wallHeight * 0.5f),
                new Vector3(0.75f, wallHeight, length + 1f),
                forward,
                wallMaterial,
                root
            );

            CreateOrientedCube(
                "WallR_" + i,
                mid - right * (routeWidth * 0.5f + 0.4f) + Vector3.up * (wallHeight * 0.5f),
                new Vector3(0.75f, wallHeight, length + 1f),
                forward,
                wallMaterial,
                root
            );

            CreateOrientedCube(
                "WallUpperL_" + i,
                mid + right * (routeWidth * 0.5f + 0.4f) + Vector3.up * ceilingWallY,
                new Vector3(0.75f, wallHeight, length + 1f),
                forward,
                wallMaterial,
                root
            );

            CreateOrientedCube(
                "WallUpperR_" + i,
                mid - right * (routeWidth * 0.5f + 0.4f) + Vector3.up * ceilingWallY,
                new Vector3(0.75f, wallHeight, length + 1f),
                forward,
                wallMaterial,
                root
            );
        }
    }

    private void CreateRouteTerrainSupport(int index, Vector3 mid, Vector3 forward, Vector3 right, float length, float routeWidth)
    {
        Material rockMat = terrainRockMaterial != null ? terrainRockMaterial : routeMaterial;
        Material soilMat = terrainSoilMaterial != null ? terrainSoilMaterial : wallMaterial;
        Material grassMat = terrainMaterial != null ? terrainMaterial : platformMaterial;

        GameObject roadbed = CreateOrientedCube(
            "RouteRoadbed_" + index,
            mid + Vector3.down * 1.28f,
            new Vector3(routeWidth + 1.8f, 1.95f, length + 1.2f),
            forward,
            rockMat,
            root
        );
        DisableCollider(roadbed);
        ConfigureSceneryRenderer(roadbed);

        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f;
            float slopeRoll = -side * 22f;

            GameObject shoulderSoil = CreateOrientedCube(
                "RouteShoulderSoil_" + index + "_" + s,
                mid + right * side * (routeWidth * 0.5f + 2.1f) + Vector3.down * 1.08f,
                new Vector3(3.8f, 2.15f, length + 0.9f),
                forward,
                soilMat,
                root
            );
            shoulderSoil.transform.rotation = Quaternion.AngleAxis(slopeRoll, forward) * shoulderSoil.transform.rotation;
            DisableCollider(shoulderSoil);
            ConfigureSceneryRenderer(shoulderSoil);

            GameObject shoulderGrass = CreateOrientedCube(
                "RouteShoulderGrass_" + index + "_" + s,
                mid + right * side * (routeWidth * 0.5f + 1.55f) + Vector3.down * 0.48f,
                new Vector3(2.0f, 0.62f, length + 0.6f),
                forward,
                grassMat,
                root
            );
            shoulderGrass.transform.rotation = Quaternion.AngleAxis(-side * 16f, forward) * shoulderGrass.transform.rotation;
            DisableCollider(shoulderGrass);
            ConfigureSceneryRenderer(shoulderGrass);
        }
    }

    private float GetRouteCeilingHeight()
    {
        if (IsTutorialLevel())
        {
            return 9.5f;
        }

        switch (GetDesignLevel())
        {
            case 1:
                return 6.6f;
            case 2:
                // Keep enough clearance for level 2 trampoline and rhythm sections.
                return 22f;
            case 3:
                // Keep enough clearance for level 3 high vertical challenge route.
                return 26f;
            default:
                // Boss level needs even more headroom for compound sections.
                return 30f;
        }
    }

    private void BuildRouteFillLights()
    {
        if (root == null || routePoints.Count < 2)
        {
            return;
        }

        int level = GetDesignLevel();
        bool brightenLevelOneCorridor = IsTutorialLevel() || level == 1;
        float density01 = Mathf.InverseLerp(0.45f, 2f, GetSceneryDensityScale());
        int lightCount = Mathf.Clamp(Mathf.RoundToInt(currentConfig.MapLength / 42f * Mathf.Lerp(0.82f, 1.12f, density01)), 6, 14);
        if (brightenLevelOneCorridor)
        {
            lightCount = Mathf.Clamp(lightCount + 3, 8, 18);
        }

        float topLightIntensity = brightenLevelOneCorridor ? 1.62f : 1.08f;
        float topLightRange = brightenLevelOneCorridor ? 44f : 32f;
        float sideLightIntensity = brightenLevelOneCorridor ? 0.88f : 0.58f;
        float sideLightRange = brightenLevelOneCorridor ? 28f : 21f;

        for (int i = 0; i < lightCount; i++)
        {
            float t = lightCount <= 1 ? 0.5f : (float)i / (lightCount - 1f);
            Vector3 point = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            CreateFillLight(
                "RouteFillLight_" + i,
                point + Vector3.up * 4.8f,
                new Color(0.90f, 0.96f, 1f),
                topLightIntensity,
                topLightRange
            );

            float side = (i % 2 == 0) ? -1f : 1f;
            CreateFillLight(
                "RouteKickerLight_" + i,
                point + right * (side * 8.2f) + Vector3.up * 2.9f,
                new Color(0.62f, 0.82f, 1f),
                sideLightIntensity,
                sideLightRange
            );

            if (brightenLevelOneCorridor)
            {
                float edgeSide = i % 2 == 0 ? 1f : -1f;
                CreateFillLight(
                    "RouteEdgeLight_" + i,
                    point + right * (edgeSide * 6.8f) + Vector3.up * 1.15f,
                    new Color(0.70f, 0.90f, 1f),
                    0.64f,
                    22f
                );
            }
        }
    }

    private void CreateFillLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        if (root == null)
        {
            return;
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = position;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = Mathf.Clamp(intensity, 0.1f, 8f);
        light.range = Mathf.Clamp(range, 2f, 120f);
        light.shadows = LightShadows.None;
    }

    private void BuildScenicBackdrop(float mapLength)
    {
        if (root == null || routePoints.Count < 2)
        {
            return;
        }

        cloudVisuals.Clear();

        float minX = routePoints[0].x;
        float maxX = routePoints[0].x;
        float minZ = routePoints[0].z;
        float maxZ = routePoints[0].z;
        for (int i = 1; i < routePoints.Count; i++)
        {
            Vector3 p = routePoints[i];
            if (p.x < minX)
            {
                minX = p.x;
            }
            if (p.x > maxX)
            {
                maxX = p.x;
            }
            if (p.z < minZ)
            {
                minZ = p.z;
            }
            if (p.z > maxZ)
            {
                maxZ = p.z;
            }
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float routeHalfWidth = Mathf.Max(20f, (maxX - minX) * 0.5f + 16f);
        float backdropSideOffset = routeHalfWidth + Mathf.Lerp(52f, 78f, Mathf.InverseLerp(120f, 260f, mapLength));

        GameObject groundGrass = CreateCube(
            "BackdropGroundGrass",
            new Vector3(centerX, -1.30f, centerZ + mapLength * 0.02f),
            new Vector3(routeHalfWidth * 7.0f, 0.60f, mapLength + 260f),
            terrainMaterial,
            root,
            false
        );
        ConfigureSceneryRenderer(groundGrass);

        GameObject groundSoil = CreateCube(
            "BackdropGroundSoil",
            new Vector3(centerX, -2.05f, centerZ + mapLength * 0.02f),
            new Vector3(routeHalfWidth * 7.5f, 0.90f, mapLength + 290f),
            terrainSoilMaterial,
            root,
            false
        );
        ConfigureSceneryRenderer(groundSoil);

        GameObject groundRock = CreateCube(
            "BackdropGroundRock",
            new Vector3(centerX, -3.25f, centerZ + mapLength * 0.02f),
            new Vector3(routeHalfWidth * 8.0f, 1.70f, mapLength + 330f),
            terrainRockMaterial,
            root,
            false
        );
        ConfigureSceneryRenderer(groundRock);

        for (int s = 0; s < 2; s++)
        {
            float side = s == 0 ? -1f : 1f;
            float sideX = centerX + side * (routeHalfWidth * 1.45f + 24f);

            GameObject bandGrass = CreateCube(
                "SideStrataGrass_" + s,
                new Vector3(sideX, -0.20f, centerZ),
                new Vector3(26f, 3.20f, mapLength + 240f),
                terrainMaterial,
                root,
                false
            );
            ConfigureSceneryRenderer(bandGrass);

            GameObject bandSoil = CreateCube(
                "SideStrataSoil_" + s,
                new Vector3(sideX, -2.10f, centerZ),
                new Vector3(28f, 2.90f, mapLength + 250f),
                terrainSoilMaterial,
                root,
                false
            );
            ConfigureSceneryRenderer(bandSoil);

            GameObject bandRock = CreateCube(
                "SideStrataRock_" + s,
                new Vector3(sideX, -4.60f, centerZ),
                new Vector3(30f, 3.80f, mapLength + 265f),
                terrainRockMaterial,
                root,
                false
            );
            ConfigureSceneryRenderer(bandRock);
        }

        float scenicScale = GetSceneryDensityScale();
        int nearMountainCount = Mathf.Clamp(Mathf.RoundToInt(mapLength / 22f * scenicScale), 8, 24);
        int farMountainCount = Mathf.Clamp(Mathf.RoundToInt(mapLength / 30f * scenicScale), 7, 20);
        BuildMountainRidge("MountainNear_L", centerX, mapLength, -1f, backdropSideOffset, nearMountainCount, mountainNearMaterial, mountainNearWarmMaterial, -5.5f, 0.48f);
        BuildMountainRidge("MountainNear_R", centerX, mapLength, 1f, backdropSideOffset, nearMountainCount, mountainNearMaterial, mountainNearWarmMaterial, -5.5f, 0.56f);
        BuildMountainRidge("MountainFar_L", centerX, mapLength, -1f, backdropSideOffset + 64f, farMountainCount, mountainFarMaterial, mountainFarWarmMaterial, -10f, 0.30f);
        BuildMountainRidge("MountainFar_R", centerX, mapLength, 1f, backdropSideOffset + 64f, farMountainCount, mountainFarMaterial, mountainFarWarmMaterial, -10f, 0.36f);

        int forestCountNear = Mathf.Clamp(Mathf.RoundToInt(mapLength * 0.24f * scenicScale), 56, 220);
        int forestCountFar = Mathf.Clamp(Mathf.RoundToInt(mapLength * 0.16f * scenicScale), 44, 180);
        BuildForestBand("ForestNear", backdropSideOffset * 0.62f, forestCountNear, 0.17f, 0.95f);
        BuildForestBand("ForestFar", backdropSideOffset * 0.78f, forestCountFar, 0.63f, 0.78f);
        BuildGroundUndulationBands(mapLength, routeHalfWidth);
        BuildRouteSidePropScatter(mapLength, routeHalfWidth);
        BuildValleyMistLayer(centerX, centerZ, mapLength, routeHalfWidth + 86f);
        BuildCloudLayer(centerX, mapLength, routeHalfWidth + 112f);
    }

    private void BuildGlobalReflectionProbe(float mapLength)
    {
        if (root == null || routePoints.Count == 0)
        {
            return;
        }

        float minX = routePoints[0].x;
        float maxX = routePoints[0].x;
        float minZ = routePoints[0].z;
        float maxZ = routePoints[0].z;
        for (int i = 1; i < routePoints.Count; i++)
        {
            Vector3 p = routePoints[i];
            if (p.x < minX)
            {
                minX = p.x;
            }
            if (p.x > maxX)
            {
                maxX = p.x;
            }
            if (p.z < minZ)
            {
                minZ = p.z;
            }
            if (p.z > maxZ)
            {
                maxZ = p.z;
            }
        }

        GameObject probeObject = new GameObject("GlobalReflectionProbe");
        probeObject.transform.SetParent(root, false);
        probeObject.transform.position = new Vector3(
            (minX + maxX) * 0.5f,
            Mathf.Max(10f, GetRouteCeilingHeight() * 0.74f),
            (minZ + maxZ) * 0.5f + mapLength * 0.08f
        );

        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.boxProjection = true;
        probe.resolution = 512;
        probe.intensity = 1.24f;
        probe.nearClipPlane = 0.2f;
        probe.farClipPlane = Mathf.Max(320f, mapLength + 200f);
        probe.cullingMask = ~0;

        probe.size = new Vector3(
            Mathf.Max(180f, (maxX - minX) + 240f),
            Mathf.Max(80f, GetRouteCeilingHeight() + 56f),
            Mathf.Max(260f, (maxZ - minZ) + 260f)
        );
        probe.center = new Vector3(0f, probe.size.y * 0.5f - 8f, 0f);

        if (Application.isPlaying)
        {
            probe.RenderProbe();
        }
    }

    private void BuildMountainRidge(
        string prefix,
        float centerX,
        float mapLength,
        float side,
        float sideOffset,
        int count,
        Material coolMaterial,
        Material warmMaterial,
        float yOffset,
        float warmBias)
    {
        if (count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (count - 1f);
            float z = Mathf.Lerp(-140f, mapLength + 170f, t);

            float n0 = ScenicNoise01(i * 1.41f + side * 0.19f + 0.6f);
            float n1 = ScenicNoise01(i * 2.03f + side * 0.83f + 1.7f);
            float n2 = ScenicNoise01(i * 1.17f + 2.3f);

            float distance = sideOffset + Mathf.Lerp(4f, 34f, n0);
            float width = Mathf.Lerp(30f, 84f, n1);
            float height = Mathf.Lerp(24f, 92f, n2);
            float depth = Mathf.Lerp(28f, 78f, n0);

            Vector3 mountainPos = new Vector3(
                centerX + side * distance + ScenicNoiseSigned(i * 0.87f + 2.5f) * 16f,
                yOffset + height * 0.5f,
                z + ScenicNoiseSigned(i * 1.31f + 0.4f) * 14f
            );

            float yaw = ScenicNoiseSigned(i * 0.91f + 3.1f) * 19f;
            float warmPick = Mathf.Clamp01(n1 * 0.74f + ScenicNoise01(i * 1.83f + side * 1.7f) * 0.26f + warmBias - 0.28f);
            Material selected = warmPick > 0.5f ? warmMaterial : coolMaterial;

            GameObject mountain = CreateBackdropPyramid(
                prefix + "_" + i,
                mountainPos,
                new Vector3(width, height, depth),
                Quaternion.Euler(0f, yaw, 0f),
                selected
            );
            if (mountain != null && prefix.Contains("Far"))
            {
                ScenicDistanceCuller culler = mountain.AddComponent<ScenicDistanceCuller>();
                culler.Configure(Mathf.Lerp(180f, 260f, ScenicNoise01(i * 0.91f + 0.33f)), 0.44f);
            }
        }
    }

    private void BuildForestBand(
        string prefix,
        float sideOffset,
        int treeCount,
        float seedOffset,
        float scaleMultiplier)
    {
        if (treeCount <= 0)
        {
            return;
        }

        for (int i = 0; i < treeCount; i++)
        {
            float t = Mathf.Lerp(0.02f, 0.98f, (i + 0.5f) / treeCount);
            Vector3 routePoint = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            float sideSign = ((i + Mathf.RoundToInt(seedOffset * 19f)) % 2 == 0) ? 1f : -1f;
            float lateral = sideOffset + Mathf.Lerp(2f, 26f, ScenicNoise01(seedOffset + i * 1.37f));
            float forwardJitter = ScenicNoiseSigned(seedOffset * 3.2f + i * 1.73f) * 12f;
            Vector3 position = routePoint + right * sideSign * lateral + forward * forwardJitter;
            position.y = -0.02f;

            float treeScale = Mathf.Lerp(0.82f, 1.70f, ScenicNoise01(seedOffset * 5.1f + i * 1.11f)) * scaleMultiplier;
            float yaw = ScenicNoiseSigned(seedOffset * 7.7f + i * 0.91f) * 28f;
            Transform treeRoot = CreateBackdropTree(prefix + "_" + i, position, treeScale, yaw);
            if (treeRoot != null && prefix.Contains("Far"))
            {
                ScenicDistanceCuller culler = treeRoot.gameObject.AddComponent<ScenicDistanceCuller>();
                culler.Configure(Mathf.Lerp(110f, 170f, ScenicNoise01(seedOffset * 1.21f + i * 0.77f)), 0.30f);
            }
        }
    }

    private void BuildCloudLayer(float centerX, float mapLength, float horizontalSpread)
    {
        int cloudCount = Mathf.Clamp(Mathf.RoundToInt(mapLength / 30f * GetSceneryDensityScale()), 8, 24);
        for (int i = 0; i < cloudCount; i++)
        {
            float t = cloudCount == 1 ? 0.5f : i / (cloudCount - 1f);
            Vector3 center = new Vector3(
                centerX + ScenicNoiseSigned(i * 1.63f + 0.47f) * horizontalSpread,
                Mathf.Lerp(28f, 56f, ScenicNoise01(i * 1.27f + 2.1f)),
                Mathf.Lerp(-90f, mapLength + 130f, t) + ScenicNoiseSigned(i * 0.73f + 1.3f) * 38f
            );

            float sizeX = Mathf.Lerp(9f, 22f, ScenicNoise01(i * 1.92f + 0.3f));
            float sizeY = sizeX * Mathf.Lerp(0.26f, 0.45f, ScenicNoise01(i * 1.39f + 1.7f));
            float sizeZ = sizeX * Mathf.Lerp(0.55f, 1.10f, ScenicNoise01(i * 1.13f + 2.9f));
            float yaw = ScenicNoiseSigned(i * 1.17f + 0.6f) * 16f;

            GameObject cloud = CreateLocalPrimitive(
                "Cloud_" + i,
                PrimitiveType.Sphere,
                root,
                center,
                new Vector3(sizeX, sizeY, sizeZ),
                cloudMaterial,
                false
            );
            cloud.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ConfigureSceneryRenderer(cloud);
            ScenicDistanceCuller culler = cloud.AddComponent<ScenicDistanceCuller>();
            culler.Configure(Mathf.Lerp(190f, 280f, ScenicNoise01(i * 1.19f + 0.52f)), 0.34f);
            cloudVisuals.Add(cloud.transform);
        }
    }

    private void BuildGroundUndulationBands(float mapLength, float routeHalfWidth)
    {
        int bandCount = Mathf.Clamp(Mathf.RoundToInt(mapLength / 16f * GetSceneryDensityScale()), 16, 68);
        Material rockMat = terrainRockMaterial != null ? terrainRockMaterial : wallMaterial;
        Material soilMat = terrainSoilMaterial != null ? terrainSoilMaterial : wallMaterial;
        Material grassMat = terrainMaterial != null ? terrainMaterial : platformMaterial;

        for (int i = 0; i < bandCount; i++)
        {
            float t = bandCount <= 1 ? 0.5f : (float)i / (bandCount - 1f);
            Vector3 routePoint = EvaluateRoutePoint(Mathf.Lerp(0.01f, 0.99f, t));
            Vector3 forward = EvaluateRouteDirection(Mathf.Lerp(0.01f, 0.99f, t));
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            float side = (i % 2 == 0) ? -1f : 1f;
            float n0 = ScenicNoise01(i * 1.23f + 0.41f);
            float n1 = ScenicNoise01(i * 1.77f + 1.14f);
            float n2 = ScenicNoise01(i * 2.11f + 2.19f);

            float lateral = routeHalfWidth + Mathf.Lerp(14f, 54f, n0);
            float height = Mathf.Lerp(1.0f, 3.6f, n1);
            float width = Mathf.Lerp(9f, 28f, n2);
            float depth = Mathf.Lerp(12f, 40f, n0);
            float forwardJitter = ScenicNoiseSigned(i * 2.03f + 0.72f) * 11f;
            Vector3 pos = routePoint + right * (side * lateral) + forward * forwardJitter + Vector3.down * (1.8f - height * 0.35f);

            Material selected = n2 > 0.66f ? rockMat : (n2 > 0.33f ? soilMat : grassMat);
            GameObject band = CreateOrientedCube(
                "GroundBand_" + i,
                pos,
                new Vector3(width, height, depth),
                forward,
                selected,
                root
            );
            band.transform.rotation = Quaternion.AngleAxis(side * Mathf.Lerp(8f, 24f, n1), forward) * band.transform.rotation;
            DisableCollider(band);
            ConfigureSceneryRenderer(band);
        }
    }

    private void BuildRouteSidePropScatter(float mapLength, float routeHalfWidth)
    {
        int clusterCount = Mathf.Clamp(Mathf.RoundToInt(mapLength / 20f * GetSceneryDensityScale()), 10, 52);
        Material rockMat = terrainRockMaterial != null ? terrainRockMaterial : wallMaterial;
        Material trunkMat = treeTrunkMaterial != null ? treeTrunkMaterial : wallMaterial;
        Material leafMat = treeLeafMaterial != null ? treeLeafMaterial : platformMaterial;

        for (int i = 0; i < clusterCount; i++)
        {
            float t = Mathf.Lerp(0.03f, 0.97f, (i + 0.5f) / clusterCount);
            Vector3 routePoint = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            float side = (i % 2 == 0) ? -1f : 1f;
            float lateral = routeHalfWidth * 0.55f + Mathf.Lerp(6f, 20f, ScenicNoise01(i * 1.47f + 0.52f));
            float forwardJitter = ScenicNoiseSigned(i * 1.91f + 1.27f) * 8f;
            Vector3 clusterCenter = routePoint + right * (side * lateral) + forward * forwardJitter;

            int rocks = 2 + Mathf.FloorToInt(ScenicNoise01(i * 1.63f + 2.41f) * 3f);
            for (int r = 0; r < rocks; r++)
            {
                float rn = ScenicNoise01(i * 2.73f + r * 1.37f + 0.28f);
                float rf = ScenicNoiseSigned(i * 3.11f + r * 1.91f + 1.77f);
                Vector3 offset = right * (rf * 3.2f) + forward * (ScenicNoiseSigned(i * 2.49f + r * 2.17f + 0.83f) * 4.4f);
                float sx = Mathf.Lerp(0.7f, 2.3f, rn);
                float sy = Mathf.Lerp(0.45f, 1.45f, ScenicNoise01(i * 1.19f + r * 2.03f + 3.6f));
                float sz = Mathf.Lerp(0.7f, 2.4f, ScenicNoise01(i * 1.53f + r * 1.29f + 4.1f));
                Vector3 pos = clusterCenter + offset + Vector3.up * (sy * 0.5f - 0.02f);

                GameObject rock = CreateLocalPrimitive(
                    "ScenicRock_" + i + "_" + r,
                    PrimitiveType.Sphere,
                    root,
                    pos,
                    new Vector3(sx, sy, sz),
                    rockMat,
                    false
                );
                rock.transform.rotation = Quaternion.Euler(0f, ScenicNoiseSigned(i * 2.88f + r * 1.22f + 0.91f) * 26f, 0f);
                ConfigureSceneryRenderer(rock);
            }

            if (ScenicNoise01(i * 1.84f + 0.73f) > 0.42f)
            {
                float trunkHeight = Mathf.Lerp(0.65f, 1.45f, ScenicNoise01(i * 2.03f + 1.42f));
                Vector3 trunkPos = clusterCenter + right * (side * 1.4f) + Vector3.up * (trunkHeight * 0.5f);
                GameObject trunk = CreateLocalPrimitive(
                    "ShrubTrunk_" + i,
                    PrimitiveType.Cylinder,
                    root,
                    trunkPos,
                    new Vector3(0.14f, trunkHeight, 0.14f),
                    trunkMat,
                    false
                );
                ConfigureSceneryRenderer(trunk);

                float crown = Mathf.Lerp(0.45f, 1.1f, ScenicNoise01(i * 1.31f + 2.71f));
                GameObject crownObj = CreateLocalPrimitive(
                    "ShrubCrown_" + i,
                    PrimitiveType.Sphere,
                    root,
                    trunkPos + Vector3.up * (trunkHeight * 0.58f),
                    new Vector3(crown, crown * 0.85f, crown),
                    leafMat,
                    false
                );
                ConfigureSceneryRenderer(crownObj);
            }
        }
    }

    private void BuildValleyMistLayer(float centerX, float centerZ, float mapLength, float horizontalSpread)
    {
        if (cloudMaterial == null)
        {
            return;
        }

        int mistCount = Mathf.Clamp(Mathf.RoundToInt(mapLength / 38f * Mathf.Lerp(0.85f, 1.20f, Mathf.InverseLerp(0.45f, 2f, GetSceneryDensityScale()))), 7, 24);
        for (int i = 0; i < mistCount; i++)
        {
            float t = mistCount <= 1 ? 0.5f : (float)i / (mistCount - 1f);
            float nx = ScenicNoiseSigned(i * 1.37f + 0.67f);
            float nz = ScenicNoiseSigned(i * 1.93f + 1.12f);
            Vector3 center = new Vector3(
                centerX + nx * horizontalSpread,
                Mathf.Lerp(0.9f, 2.2f, ScenicNoise01(i * 1.81f + 2.37f)),
                Mathf.Lerp(-120f, mapLength + 150f, t) + nz * 26f + (centerZ * 0.06f)
            );

            float sx = Mathf.Lerp(18f, 52f, ScenicNoise01(i * 1.47f + 0.31f));
            float sy = Mathf.Lerp(1.8f, 6.2f, ScenicNoise01(i * 2.09f + 1.61f));
            float sz = Mathf.Lerp(14f, 44f, ScenicNoise01(i * 1.63f + 2.83f));

            GameObject mist = CreateLocalPrimitive(
                "ValleyMist_" + i,
                PrimitiveType.Sphere,
                root,
                center,
                new Vector3(sx, sy, sz),
                cloudMaterial,
                false
            );
            mist.transform.rotation = Quaternion.Euler(0f, ScenicNoiseSigned(i * 1.73f + 0.49f) * 18f, 0f);
            ConfigureSceneryRenderer(mist);
        }
    }

    private void BuildProceduralFlora()
    {
        if (root == null || routePoints.Count < 2)
        {
            return;
        }

        float densityScale = GetSceneryDensityScale();
        int nearCount = Mathf.Clamp(Mathf.RoundToInt(currentConfig.FloraNearCount * densityScale), 32, 1500);
        int farCount = Mathf.Clamp(Mathf.RoundToInt(currentConfig.FloraFarCount * densityScale), 24, 1200);

        SpawnFloraBand("FloraNear", nearCount, 8f, 24f, true, 0.17f);
        SpawnFloraBand("FloraFar", farCount, 20f, 58f, false, 1.11f);
    }

    private float GetSceneryDensityScale()
    {
        float configScale = Mathf.Clamp(currentConfig.ScenicDensityScale <= 0f ? 1f : currentConfig.ScenicDensityScale, 0.45f, 2f);
        int qualityLevels = QualitySettings.names != null ? Mathf.Max(1, QualitySettings.names.Length) : 1;
        float quality01 = qualityLevels > 1
            ? Mathf.Clamp01(QualitySettings.GetQualityLevel() / (float)(qualityLevels - 1))
            : 0.7f;
        float qualityScale = Mathf.Lerp(0.74f, 1.16f, quality01);
        if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2048)
        {
            qualityScale *= 0.84f;
        }

        return configScale * qualityScale;
    }

    private void SpawnFloraBand(
        string prefix,
        int count,
        float minLateral,
        float maxLateral,
        bool highDetail,
        float seedOffset)
    {
        if (count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(0.01f, 0.99f, (i + 0.5f) / count);
            Vector3 routePoint = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            float side = (i % 2 == 0) ? -1f : 1f;
            float lateral = Mathf.Lerp(minLateral, maxLateral, ScenicNoise01(seedOffset + i * 1.49f));
            float forwardJitter = ScenicNoiseSigned(seedOffset * 3.7f + i * 1.79f) * (highDetail ? 12f : 24f);
            float groundY = Mathf.Lerp(-0.06f, 0.28f, ScenicNoise01(seedOffset * 2.9f + i * 2.11f));
            Vector3 position = routePoint + right * (side * lateral) + forward * forwardJitter + Vector3.up * groundY;

            float scale = Mathf.Lerp(highDetail ? 0.68f : 0.78f, highDetail ? 1.34f : 1.48f, ScenicNoise01(seedOffset * 5.1f + i * 1.07f));
            bool flower = ScenicNoise01(seedOffset * 1.87f + i * 1.31f) > (highDetail ? 0.30f : 0.58f);
            CreateFloraCluster(prefix + "_" + i, position, forward, scale, flower, highDetail, seedOffset + i * 0.91f);
        }
    }

    private void CreateFloraCluster(
        string name,
        Vector3 position,
        Vector3 forward,
        float scale,
        bool flower,
        bool highDetail,
        float seed)
    {
        Transform clusterRoot = new GameObject(name).transform;
        clusterRoot.SetParent(root, false);
        clusterRoot.position = position;
        clusterRoot.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward, Vector3.up);
        clusterRoot.localRotation *= Quaternion.Euler(0f, ScenicNoiseSigned(seed * 1.37f + 0.9f) * 42f, 0f);

        float stemHeight = Mathf.Lerp(0.32f, 0.92f, ScenicNoise01(seed * 1.91f + 0.27f)) * scale;
        float stemRadius = Mathf.Lerp(0.020f, 0.055f, ScenicNoise01(seed * 2.37f + 1.43f)) * scale;

        GameObject stem = CreateLocalPrimitive(
            "Stem",
            PrimitiveType.Cylinder,
            clusterRoot,
            new Vector3(0f, stemHeight * 0.5f, 0f),
            new Vector3(stemRadius, stemHeight * 0.5f, stemRadius),
            floraStemMaterial != null ? floraStemMaterial : terrainMaterial,
            false
        );
        ConfigureSceneryRenderer(stem);

        if (highDetail)
        {
            float sideOffset = Mathf.Lerp(0.02f, 0.07f, ScenicNoise01(seed * 1.29f + 2.17f)) * scale;
            float sideHeight = stemHeight * Mathf.Lerp(0.42f, 0.72f, ScenicNoise01(seed * 1.77f + 0.61f));
            GameObject sideStem = CreateLocalPrimitive(
                "StemSide",
                PrimitiveType.Cylinder,
                clusterRoot,
                new Vector3(sideOffset, sideHeight * 0.5f, 0f),
                new Vector3(stemRadius * 0.85f, sideHeight * 0.5f, stemRadius * 0.85f),
                floraStemMaterial != null ? floraStemMaterial : terrainMaterial,
                false
            );
            sideStem.transform.localRotation = Quaternion.Euler(0f, 0f, ScenicNoiseSigned(seed * 2.39f + 0.34f) * 22f);
            ConfigureSceneryRenderer(sideStem);
        }

        if (flower)
        {
            Material petalMat = floraFlowerPetalMaterial != null ? floraFlowerPetalMaterial : platformMaterial;
            float petalHeight = stemHeight + Mathf.Lerp(0.06f, 0.16f, ScenicNoise01(seed * 1.47f + 1.2f)) * scale;
            float petalScale = Mathf.Lerp(0.07f, 0.19f, ScenicNoise01(seed * 2.01f + 0.17f)) * scale;
            int petals = highDetail ? 5 : 3;
            for (int p = 0; p < petals; p++)
            {
                float angle = (360f / petals) * p;
                Vector3 ring = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (petalScale * 0.58f);
                GameObject petal = CreateLocalPrimitive(
                    "Petal_" + p,
                    PrimitiveType.Sphere,
                    clusterRoot,
                    new Vector3(ring.x, petalHeight, ring.z),
                    new Vector3(petalScale, petalScale * 0.40f, petalScale),
                    petalMat,
                    false
                );
                ConfigureSceneryRenderer(petal);
            }

            GameObject core = CreateLocalPrimitive(
                "Core",
                PrimitiveType.Sphere,
                clusterRoot,
                new Vector3(0f, petalHeight, 0f),
                Vector3.one * (petalScale * 0.72f),
                floraFlowerCoreMaterial != null ? floraFlowerCoreMaterial : switchMaterial,
                false
            );
            ConfigureSceneryRenderer(core);
        }
        else
        {
            Material leafMat = floraFlowerPetalMaterial != null ? floraFlowerPetalMaterial : platformMaterial;
            float leafHeight = stemHeight * Mathf.Lerp(0.34f, 0.66f, ScenicNoise01(seed * 1.62f + 0.97f));
            float leafScale = Mathf.Lerp(0.08f, 0.18f, ScenicNoise01(seed * 2.17f + 1.22f)) * scale;
            int leaves = highDetail ? 3 : 2;
            for (int l = 0; l < leaves; l++)
            {
                float side = l % 2 == 0 ? -1f : 1f;
                float fwd = l == 2 ? 0.05f : -0.03f;
                GameObject leaf = CreateLocalPrimitive(
                    "Leaf_" + l,
                    PrimitiveType.Sphere,
                    clusterRoot,
                    new Vector3(leafScale * side * 0.66f, leafHeight + l * 0.02f, leafScale * fwd),
                    new Vector3(leafScale * 1.1f, leafScale * 0.42f, leafScale * 0.92f),
                    leafMat,
                    false
                );
                ConfigureSceneryRenderer(leaf);
            }
        }

        ApplyFloraHueVariation(clusterRoot, seed, flower);

        FloraSwayMotion sway = clusterRoot.gameObject.AddComponent<FloraSwayMotion>();
        sway.Configure(
            amplitude: Mathf.Lerp(3f, 12f, ScenicNoise01(seed * 1.11f + 0.32f)),
            frequency: Mathf.Lerp(0.72f, 1.55f, ScenicNoise01(seed * 1.53f + 1.87f)),
            twistAmplitude: Mathf.Lerp(2f, 8f, ScenicNoise01(seed * 1.93f + 0.48f)),
            phaseOffset: seed * 2.13f);

        if (!highDetail)
        {
            ScenicDistanceCuller culler = clusterRoot.gameObject.AddComponent<ScenicDistanceCuller>();
            culler.Configure(Mathf.Lerp(86f, 132f, ScenicNoise01(seed * 1.87f + 2.14f)), 0.22f);
        }
    }

    private void ApplyFloraHueVariation(Transform clusterRoot, float seed, bool flower)
    {
        if (clusterRoot == null)
        {
            return;
        }

        Renderer[] renderers = clusterRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            float hueShift = flower
                ? Mathf.Lerp(-0.13f, 0.13f, ScenicNoise01(seed * 1.17f + i * 0.83f))
                : Mathf.Lerp(-0.06f, 0.06f, ScenicNoise01(seed * 1.37f + i * 0.63f));
            float satMul = flower
                ? Mathf.Lerp(0.86f, 1.26f, ScenicNoise01(seed * 2.01f + i * 0.79f))
                : Mathf.Lerp(0.80f, 1.14f, ScenicNoise01(seed * 1.73f + i * 0.57f));
            float valMul = Mathf.Lerp(0.86f, 1.16f, ScenicNoise01(seed * 2.23f + i * 0.37f));
            ApplyRendererTint(renderer, hueShift, satMul, valMul);
        }
    }

    private static void ApplyRendererTint(Renderer renderer, float hueShift, float saturationMultiplier, float valueMultiplier)
    {
        if (renderer == null || renderer.sharedMaterial == null)
        {
            return;
        }

        Color source = renderer.sharedMaterial.HasProperty("_Color")
            ? renderer.sharedMaterial.color
            : Color.white;
        Color.RGBToHSV(source, out float h, out float s, out float v);
        h = Mathf.Repeat(h + hueShift, 1f);
        s = Mathf.Clamp01(s * saturationMultiplier);
        v = Mathf.Clamp01(v * valueMultiplier);
        Color tinted = Color.HSVToRGB(h, s, v);

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_Color", tinted);
        if (renderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            block.SetColor("_EmissionColor", tinted * 0.08f);
        }
        renderer.SetPropertyBlock(block);
    }

    private Transform CreateBackdropTree(string name, Vector3 position, float scale, float yaw)
    {
        Transform treeRoot = new GameObject(name).transform;
        treeRoot.SetParent(root, false);
        treeRoot.position = position;
        treeRoot.rotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject trunk = CreateLocalPrimitive(
            "Trunk",
            PrimitiveType.Cylinder,
            treeRoot,
            new Vector3(0f, 0.90f * scale, 0f),
            new Vector3(0.15f * scale, 0.90f * scale, 0.15f * scale),
            treeTrunkMaterial,
            false
        );
        ConfigureSceneryRenderer(trunk);

        GameObject leafBase = CreateLocalPrimitive(
            "LeafBase",
            PrimitiveType.Sphere,
            treeRoot,
            new Vector3(0f, 2.00f * scale, 0f),
            new Vector3(1.00f * scale, 1.00f * scale, 1.00f * scale),
            treeLeafMaterial,
            false
        );
        ConfigureSceneryRenderer(leafBase);

        GameObject leafTop = CreateLocalPrimitive(
            "LeafTop",
            PrimitiveType.Sphere,
            treeRoot,
            new Vector3(0f, 2.76f * scale, 0f),
            new Vector3(0.66f * scale, 0.66f * scale, 0.66f * scale),
            treeLeafMaterial,
            false
        );
        ConfigureSceneryRenderer(leafTop);
        return treeRoot;
    }

    private GameObject CreateBackdropPyramid(
        string name,
        Vector3 position,
        Vector3 scale,
        Quaternion rotation,
        Material material)
    {
        GameObject pyramid = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        pyramid.transform.SetParent(root, false);
        pyramid.transform.position = position;
        pyramid.transform.rotation = rotation;
        pyramid.transform.localScale = scale;

        MeshFilter meshFilter = pyramid.GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GetPyramidMesh();
        MeshRenderer renderer = pyramid.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return pyramid;
    }

    private Mesh GetPyramidMesh()
    {
        if (pyramidMesh != null)
        {
            return pyramidMesh;
        }

        pyramidMesh = new Mesh
        {
            name = "BackdropPyramidMesh"
        };

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0f, 0.5f, 0f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f)
        };

        int[] triangles = new int[]
        {
            0, 4, 3,
            0, 3, 2,
            0, 2, 1,
            0, 1, 4,
            1, 3, 4,
            1, 2, 3
        };

        pyramidMesh.vertices = vertices;
        pyramidMesh.triangles = triangles;
        pyramidMesh.RecalculateNormals();
        pyramidMesh.RecalculateBounds();
        return pyramidMesh;
    }

    private static float ScenicNoise01(float seed)
    {
        float a = Mathf.Sin(seed * 1.37f);
        float b = Mathf.Sin(seed * 2.19f + 1.21f);
        return Mathf.Clamp01(0.5f + a * 0.32f + b * 0.18f);
    }

    private static float ScenicNoiseSigned(float seed)
    {
        return ScenicNoise01(seed) * 2f - 1f;
    }

    private void BuildTutorialStraightFlow()
    {
        if (root == null || routePoints.Count < 2)
        {
            return;
        }

        Vector3 center = EvaluateRoutePoint(0.50f);
        Vector3 forward = EvaluateRouteDirection(0.50f);

        Transform tutorialRoot = new GameObject("TutorialStraightFlow").transform;
        tutorialRoot.SetParent(root, false);
        tutorialRoot.position = center + Vector3.up * 0.02f;
        tutorialRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        CreateLocalCube("TutorialLaneFloor", tutorialRoot, new Vector3(0f, -0.42f, 0f), new Vector3(13.6f, 0.82f, 34f), routeMaterial, true);
        CreateLocalCube("TutorialLaneWallL", tutorialRoot, new Vector3(-6.8f, 2.2f, 0f), new Vector3(0.42f, 4.4f, 34f), wallMaterial, true);
        CreateLocalCube("TutorialLaneWallR", tutorialRoot, new Vector3(6.8f, 2.2f, 0f), new Vector3(0.42f, 4.4f, 34f), wallMaterial, true);

        CreateChallengePlatform(tutorialRoot, "TutorialStep_A", new Vector3(-1.9f, 1.0f, -10.4f), new Vector3(3.6f, 0.62f, 3.4f), false, 0f, 0f);
        CreateChallengePlatform(tutorialRoot, "TutorialStep_B", new Vector3(1.9f, 1.7f, -4.1f), new Vector3(3.4f, 0.62f, 3.2f), false, 0f, 0f);
        CreateChallengePlatform(tutorialRoot, "TutorialExitDeck", new Vector3(0f, 0.9f, 18.2f), new Vector3(6.8f, 0.74f, 6.4f), false, 0f, 0f);

        GravitySwitchButton tutorialSwitch = CreateGravitySwitch(
            "TutorialFlipSwitch",
            tutorialRoot.TransformPoint(new Vector3(0f, 5.5f, 4.8f)),
            forward
        );
        CreateWorldLabel("Flip + touch switch", tutorialSwitch.transform.position + Vector3.up * 1.1f, forward, new Color(1f, 0.88f, 0.34f));

        LaserBarrier tutorialGate = CreateLaserGate(tutorialRoot, "TutorialGate", new Vector3(0f, 3.0f, 11.4f), new Vector2(13f, 5.4f));
        tutorialGate.ConfigureFeedbackType(MechanismFeedbackType.GateA);
        tutorialGate.LinkButton(tutorialSwitch);

        CreateAccentLight(tutorialRoot, "TutorialLight_Start", new Vector3(0f, 2.0f, -12f), new Color(0.70f, 0.90f, 1f), 2.2f, 10f);
        CreateAccentLight(tutorialRoot, "TutorialLight_Switch", new Vector3(0f, 6.2f, 4.8f), new Color(1f, 0.80f, 0.32f), 2.8f, 9f);
        CreateAccentLight(tutorialRoot, "TutorialLight_Gate", new Vector3(0f, 3.8f, 11.4f), new Color(0.28f, 0.90f, 1f), 2.6f, 9f);

        crystalCandidates.Add(tutorialRoot.TransformPoint(new Vector3(-1.9f, 2.1f, -10.4f)));
        crystalCandidates.Add(tutorialRoot.TransformPoint(new Vector3(1.9f, 2.8f, -4.1f)));
        crystalCandidates.Add(tutorialRoot.TransformPoint(new Vector3(0f, 6.7f, 4.8f)));
        crystalCandidates.Add(tutorialRoot.TransformPoint(new Vector3(0f, 2.3f, 8.8f)));
        crystalCandidates.Add(tutorialRoot.TransformPoint(new Vector3(-2.1f, 2.2f, 15.8f)));
        crystalCandidates.Add(tutorialRoot.TransformPoint(new Vector3(2.1f, 2.2f, 17.2f)));
    }

    private void BuildLevelOneFiveRegionFlow()
    {
        BuildLevelOneRegionEntryPrompts();
        BuildLevelOneRegionTutorial();
        BuildLevelOneRegionCrystalGate();
        BuildLevelOneRegionStickyPuzzle();
        BuildLevelOneRegionCratePressurePuzzle();
        BuildLevelOneRegionCompositeChallenge();
    }

    private void BuildLevelOneRegionEntryPrompts()
    {
        CreateRegionEntrySignAndTrigger(
            1,
            0.03f,
            "Tutorial lane: move, jump, flip, and learn checkpoint safety.",
            "Move with WASD, jump with Space, flip with F. Checkpoint saves your retry spot.");
        CreateRegionEntrySignAndTrigger(
            2,
            0.21f,
            "Core loop: collect local crystals to unlock the first gate.",
            "Collect 1 local crystal to open the crystal gate.");
        CreateRegionEntrySignAndTrigger(
            3,
            0.40f,
            "Sticky rule: in inverted state, only StickySurface supports you.",
            "Flip up and step on StickySurface. Non-sticky ceilings will drop you.");
        CreateRegionEntrySignAndTrigger(
            4,
            0.60f,
            "Mechanism puzzle: push crate onto pressure plate to open route.",
            "Push the crate onto the plate to unlock the pressure gate.");
        CreateRegionEntrySignAndTrigger(
            5,
            0.79f,
            "Final mix: crystal gate + mechanism + timed flip choices.",
            "Use 2-3 flips, clear local gate, trigger mechanism, then pass hazards.");
    }

    private void BuildLevelOneRegionTutorial()
    {
        Vector3 point = EvaluateRoutePoint(0.10f);
        Vector3 forward = EvaluateRouteDirection(0.10f);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        CreateOrientedCube(
            "R1_JumpStep_A",
            point + right * 2.3f + Vector3.up * 0.35f,
            new Vector3(2.6f, 0.7f, 2.4f),
            forward,
            platformMaterial,
            root
        );
        CreateOrientedCube(
            "R1_JumpStep_B",
            point + right * 4.6f + Vector3.up * 0.9f,
            new Vector3(2.8f, 0.8f, 2.6f),
            forward,
            platformMaterial,
            root
        );

        float ceilingY = GetRouteCeilingHeight() - 0.48f;
        GameObject flipPad = CreateOrientedCube(
            "R1_FlipPracticePad",
            EvaluateRoutePoint(0.15f) + Vector3.up * ceilingY,
            new Vector3(4.4f, 0.34f, 4.4f),
            EvaluateRouteDirection(0.15f),
            platformMaterial,
            root
        );
        flipPad.AddComponent<StickySurface>();

        CreateOrientedCube(
            "R1_SafeLandingStripe",
            EvaluateRoutePoint(0.18f) + Vector3.up * 0.04f,
            new Vector3(7.0f, 0.08f, 3.2f),
            EvaluateRouteDirection(0.18f),
            routeAccentMaterial,
            root
        );

        crystalCandidates.Add(point + right * 4.6f + Vector3.up * 2.0f);
        crystalCandidates.Add(ResolveCeilingCrystalPosition(flipPad.transform.position, 0.90f));
        CreateWorldLabel("Checkpoint ahead = safe retry", EvaluateRoutePoint(0.18f) + Vector3.up * 1.35f, forward, new Color(0.82f, 0.94f, 1f));
    }

    private void BuildLevelOneRegionCrystalGate()
    {
        float gateT = 0.35f;
        Vector3 gatePoint = EvaluateRoutePoint(gateT);
        Vector3 gateForward = EvaluateRouteDirection(gateT);

        LocalCrystalGate localGate = CreateLocalCrystalGate(
            "R2_CrystalGate",
            gatePoint + Vector3.up * 2.6f,
            gateForward,
            1,
            "Zone2 Gate",
            MechanismFeedbackType.GateA
        );

        Vector3 first = EvaluateRoutePoint(0.245f) + Vector3.up * 1.3f;
        Vector3 secondBase = EvaluateRoutePoint(0.285f);
        Vector3 secondForward = EvaluateRouteDirection(0.285f);
        Vector3 secondRight = Vector3.Cross(Vector3.up, secondForward).normalized;
        Vector3 second = secondBase + secondRight * 3.2f + Vector3.up * 2.1f;

        float ceilingY = GetRouteCeilingHeight() - 0.42f;
        Vector3 thirdBase = EvaluateRoutePoint(0.32f);
        Vector3 thirdForward = EvaluateRouteDirection(0.32f);
        Vector3 thirdRight = Vector3.Cross(Vector3.up, thirdForward).normalized;
        Vector3 thirdPadPos = thirdBase - thirdRight * 2.1f + Vector3.up * ceilingY;
        GameObject thirdPad = CreateOrientedCube(
            "R2_CeilingCrystalPad",
            thirdPadPos,
            new Vector3(4.2f, 0.34f, 4.2f),
            thirdForward,
            platformMaterial,
            root
        );
        thirdPad.AddComponent<StickySurface>();
        Vector3 third = ResolveCeilingCrystalPosition(thirdPadPos, 0.90f);

        CreateLocalCrystalPickup("R2_LocalCrystal_1", first, localGate);
        CreateLocalCrystalPickup("R2_LocalCrystal_2", second, localGate);
        CreateLocalCrystalPickup("R2_LocalCrystal_3", third, localGate);

        CreateWorldLabel("Collect local crystals to open", gatePoint + Vector3.up * 4.1f, gateForward, new Color(1f, 0.88f, 0.42f));
    }

    private void BuildLevelOneRegionStickyPuzzle()
    {
        Vector3 center = EvaluateRoutePoint(0.49f);
        Vector3 forward = EvaluateRouteDirection(0.49f);

        Transform zoneRoot = new GameObject("R3_StickyRuleZone").transform;
        zoneRoot.SetParent(root, false);
        zoneRoot.position = center + Vector3.up * 1.5f;
        zoneRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        BoxCollider zoneCollider = zoneRoot.gameObject.AddComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
        zoneCollider.center = Vector3.zero;
        zoneCollider.size = new Vector3(17.5f, 7.2f, 34f);
        zoneRoot.gameObject.AddComponent<StickyRuleZone>();

        float ceilingY = GetRouteCeilingHeight() - 0.38f;
        GameObject nonStickyCeiling = CreateOrientedCube(
            "R3_NonStickyCeiling",
            center + Vector3.up * ceilingY,
            new Vector3(16.8f, 0.24f, 33f),
            forward,
            dangerMaterial,
            root
        );
        nonStickyCeiling.AddComponent<NonStickySurface>();

        GameObject stickySafetyLane = CreateOrientedCube(
            "R3_StickySafetyLane",
            center + Vector3.up * (ceilingY - 0.19f),
            new Vector3(2.8f, 0.34f, 30f),
            forward,
            platformMaterial,
            root
        );
        stickySafetyLane.AddComponent<StickySurface>();

        GameObject floorBlocker = CreateOrientedCube(
            "R3_FloorBlocker",
            EvaluateRoutePoint(0.485f) + Vector3.up * 2.2f,
            new Vector3(18.6f, 3.8f, 1.5f),
            EvaluateRouteDirection(0.485f),
            wallMaterial,
            root
        );
        floorBlocker.AddComponent<NonStickySurface>();

        for (int i = 0; i < 6; i++)
        {
            float t = 0.444f + i * 0.020f;
            Vector3 point = EvaluateRoutePoint(t);
            Vector3 dir = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float offset = i % 2 == 0 ? -2.2f : 2.2f;
            Vector3 padPos = point + right * offset + Vector3.up * (ceilingY - 0.19f);
            GameObject stickyPad = CreateOrientedCube(
                "R3_StickyPad_" + i,
                padPos,
                new Vector3(4.8f, 0.38f, 4.8f),
                dir,
                platformMaterial,
                root
            );
            stickyPad.AddComponent<StickySurface>();

            if (i == 1 || i == 3 || i == 4)
            {
                crystalCandidates.Add(ResolveCeilingCrystalPosition(stickyPad.transform.position, 0.86f));
            }
        }

        Vector3 entryPadPoint = EvaluateRoutePoint(0.438f);
        Vector3 entryPadDir = EvaluateRouteDirection(0.438f);
        GameObject stickyEntryPad = CreateOrientedCube(
            "R3_StickyPad_Entry",
            entryPadPoint + Vector3.up * (ceilingY - 0.19f),
            new Vector3(5.4f, 0.36f, 4.8f),
            entryPadDir,
            platformMaterial,
            root
        );
        stickyEntryPad.AddComponent<StickySurface>();

        Vector3 exitPadPoint = EvaluateRoutePoint(0.553f);
        Vector3 exitPadDir = EvaluateRouteDirection(0.553f);
        GameObject stickyExitPad = CreateOrientedCube(
            "R3_StickyPad_Exit",
            exitPadPoint + Vector3.up * (ceilingY - 0.19f),
            new Vector3(5.4f, 0.36f, 4.8f),
            exitPadDir,
            platformMaterial,
            root
        );
        stickyExitPad.AddComponent<StickySurface>();

        CreateWorldLabel("Inverted: only StickySurface stands", center + Vector3.up * 4.4f, forward, new Color(0.42f, 0.92f, 1f));
    }

    private void BuildLevelOneRegionCratePressurePuzzle()
    {
        Vector3 areaPoint = EvaluateRoutePoint(0.66f);
        Vector3 forward = EvaluateRouteDirection(0.66f);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Transform plateRoot = new GameObject("R4_PressurePlate").transform;
        plateRoot.SetParent(root, false);
        plateRoot.position = areaPoint + right * 3.6f + Vector3.up * 0.05f;
        plateRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        BoxCollider plateTrigger = plateRoot.gameObject.AddComponent<BoxCollider>();
        plateTrigger.isTrigger = true;
        plateTrigger.center = new Vector3(0f, 0.75f, 0f);
        plateTrigger.size = new Vector3(2.8f, 1.5f, 2.8f);

        CreateLocalCube("PlateBase", plateRoot, new Vector3(0f, 0.05f, 0f), new Vector3(2.8f, 0.10f, 2.8f), switchMaterial, false);
        CreateLocalCube("PlateTop", plateRoot, new Vector3(0f, 0.16f, 0f), new Vector3(2.2f, 0.14f, 2.2f), routeAccentMaterial, false);

        PressurePlateTrigger pressurePlate = plateRoot.gameObject.AddComponent<PressurePlateTrigger>();
        // Fail-safe: allow player activation so this teaching section cannot hard-lock
        // if the crate is pushed out of position.
        pressurePlate.Configure(true, true);

        GameObject crate = CreateOrientedCube(
            "R4_PushCrate",
            areaPoint - right * 3.1f + Vector3.up * 0.85f,
            new Vector3(1.3f, 1.3f, 1.3f),
            forward,
            wallMaterial,
            root
        );
        crate.AddComponent<Rigidbody>();
        crate.AddComponent<PushCrateMarker>();

        CreateOrientedCube(
            "R4_CrateLane_Left",
            areaPoint + right * 5.9f + Vector3.up * 1.1f,
            new Vector3(0.55f, 2.2f, 12f),
            forward,
            wallMaterial,
            root
        );
        CreateOrientedCube(
            "R4_CrateLane_Right",
            areaPoint - right * 5.9f + Vector3.up * 1.1f,
            new Vector3(0.55f, 2.2f, 12f),
            forward,
            wallMaterial,
            root
        );

        Vector3 gatePoint = EvaluateRoutePoint(0.735f);
        Vector3 gateForward = EvaluateRouteDirection(0.735f);
        Transform gateRoot = new GameObject("R4_PressureGate").transform;
        gateRoot.SetParent(root, false);
        gateRoot.position = gatePoint + Vector3.up * 2.4f;
        gateRoot.rotation = Quaternion.LookRotation(gateForward, Vector3.up);

        GameObject barrier = CreateLocalCube("GateBarrier", gateRoot, Vector3.zero, new Vector3(17.2f, 7.8f, 0.7f), gateMaterial, true);
        CreateLocalCube("GateFrameL", gateRoot, new Vector3(-8.8f, 0f, 0f), new Vector3(0.7f, 8.4f, 1f), exitMaterial, true);
        CreateLocalCube("GateFrameR", gateRoot, new Vector3(8.8f, 0f, 0f), new Vector3(0.7f, 8.4f, 1f), exitMaterial, true);
        CreateLocalCube("GateFrameTop", gateRoot, new Vector3(0f, 4.2f, 0f), new Vector3(18.4f, 0.7f, 1f), exitMaterial, true);

        PressurePlateGate gate = barrier.AddComponent<PressurePlateGate>();
        gate.Configure(pressurePlate, true, MechanismFeedbackType.GateB);

        CreateWorldLabel("Push crate to the plate", areaPoint + Vector3.up * 3.7f, forward, new Color(0.74f, 1f, 0.78f));
    }

    private void BuildLevelOneRegionCompositeChallenge()
    {
        Vector3 gatePoint = EvaluateRoutePoint(0.842f);
        Vector3 gateForward = EvaluateRouteDirection(0.842f);
        LocalCrystalGate comboGate = CreateLocalCrystalGate(
            "R5_ComboCrystalGate",
            gatePoint + Vector3.up * 2.4f,
            gateForward,
            1,
            "Zone5 Gate",
            MechanismFeedbackType.RhythmGateA
        );

        Vector3 zoneCenter = EvaluateRoutePoint(0.845f);
        Transform stickyZoneRoot = new GameObject("R5_StickyZone").transform;
        stickyZoneRoot.SetParent(root, false);
        stickyZoneRoot.position = zoneCenter + Vector3.up * 1.6f;
        stickyZoneRoot.rotation = Quaternion.LookRotation(gateForward, Vector3.up);

        BoxCollider stickyCollider = stickyZoneRoot.gameObject.AddComponent<BoxCollider>();
        stickyCollider.isTrigger = true;
        stickyCollider.size = new Vector3(17.5f, 7.4f, 24f);
        stickyZoneRoot.gameObject.AddComponent<StickyRuleZone>();

        float ceilingY = GetRouteCeilingHeight() - 0.36f;
        GameObject nonStickyCeiling = CreateOrientedCube(
            "R5_NonStickyCeiling",
            zoneCenter + Vector3.up * ceilingY,
            new Vector3(16.6f, 0.24f, 24f),
            gateForward,
            dangerMaterial,
            root
        );
        nonStickyCeiling.AddComponent<NonStickySurface>();

        Vector3 firstStickyPadPos = EvaluateRoutePoint(0.80f) + Vector3.up * (ceilingY - 0.18f);
        for (int i = 0; i < 3; i++)
        {
            float t = 0.80f + i * 0.045f;
            Vector3 point = EvaluateRoutePoint(t);
            Vector3 dir = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 padPos = point + right * (i == 1 ? 2.3f : -2.3f) + Vector3.up * (ceilingY - 0.18f);
            if (i == 0)
            {
                firstStickyPadPos = padPos;
            }

            GameObject stickyPad = CreateOrientedCube(
                "R5_StickyPad_" + i,
                padPos,
                new Vector3(3.4f, 0.36f, 3.4f),
                dir,
                platformMaterial,
                root
            );
            stickyPad.AddComponent<StickySurface>();
        }

        Vector3 floorCrystal = EvaluateRoutePoint(0.818f) + Vector3.up * 1.3f;
        Vector3 ceilingCrystal = ResolveCeilingCrystalPosition(firstStickyPadPos, 0.82f);
        CreateLocalCrystalPickup("R5_LocalCrystal_Floor", floorCrystal, comboGate);
        CreateLocalCrystalPickup("R5_LocalCrystal_Ceiling", ceilingCrystal, comboGate);

        Vector3 mechanismPoint = EvaluateRoutePoint(0.862f);
        Vector3 mechanismForward = EvaluateRouteDirection(0.862f);
        Vector3 mechanismRight = Vector3.Cross(Vector3.up, mechanismForward).normalized;

        Transform plateRoot = new GameObject("R5_PressurePlate").transform;
        plateRoot.SetParent(root, false);
        plateRoot.position = mechanismPoint + mechanismRight * 2.8f + Vector3.up * 0.05f;
        plateRoot.rotation = Quaternion.LookRotation(mechanismForward, Vector3.up);
        BoxCollider plateTrigger = plateRoot.gameObject.AddComponent<BoxCollider>();
        plateTrigger.isTrigger = true;
        plateTrigger.center = new Vector3(0f, 0.75f, 0f);
        plateTrigger.size = new Vector3(2.6f, 1.5f, 2.6f);
        CreateLocalCube("PlateBase", plateRoot, new Vector3(0f, 0.05f, 0f), new Vector3(2.6f, 0.10f, 2.6f), switchMaterial, false);
        CreateLocalCube("PlateTop", plateRoot, new Vector3(0f, 0.16f, 0f), new Vector3(2.0f, 0.13f, 2.0f), routeAccentMaterial, false);

        PressurePlateTrigger plate = plateRoot.gameObject.AddComponent<PressurePlateTrigger>();
        plate.Configure(true, false);

        Vector3 plateGatePoint = EvaluateRoutePoint(0.873f);
        Vector3 plateGateForward = EvaluateRouteDirection(0.873f);
        Transform plateGateRoot = new GameObject("R5_PlateGate").transform;
        plateGateRoot.SetParent(root, false);
        plateGateRoot.position = plateGatePoint + Vector3.up * 2.2f;
        plateGateRoot.rotation = Quaternion.LookRotation(plateGateForward, Vector3.up);
        GameObject plateBarrier = CreateLocalCube("GateBarrier", plateGateRoot, Vector3.zero, new Vector3(17.2f, 7.6f, 0.65f), gateMaterial, true);
        CreateLocalCube("GateFrameL", plateGateRoot, new Vector3(-8.8f, 0f, 0f), new Vector3(0.62f, 8.2f, 0.9f), exitMaterial, true);
        CreateLocalCube("GateFrameR", plateGateRoot, new Vector3(8.8f, 0f, 0f), new Vector3(0.62f, 8.2f, 0.9f), exitMaterial, true);
        PressurePlateGate plateGate = plateBarrier.AddComponent<PressurePlateGate>();
        plateGate.Configure(plate, true, MechanismFeedbackType.RhythmGateB);

        CreateOrientedCube(
            "R5_LowBlocker",
            EvaluateRoutePoint(0.895f) + Vector3.up * 2.2f,
            new Vector3(18.6f, 4.4f, 1.3f),
            EvaluateRouteDirection(0.895f),
            wallMaterial,
            root
        );
        CreateOrientedCube(
            "R5_HighBlocker",
            EvaluateRoutePoint(0.918f) + Vector3.up * 5.7f,
            new Vector3(18.6f, 2.0f, 1.3f),
            EvaluateRouteDirection(0.918f),
            wallMaterial,
            root
        );

        crystalCandidates.Add(EvaluateRoutePoint(0.905f) + Vector3.up * 2.1f);
        crystalCandidates.Add(EvaluateRoutePoint(0.928f) + Vector3.up * 1.4f);
        CreateWorldLabel("Final mix: flip-readiness matters", EvaluateRoutePoint(0.89f) + Vector3.up * 4.2f, gateForward, new Color(0.95f, 0.86f, 0.42f));
    }

    private void CreateRegionEntrySignAndTrigger(int regionIndex, float routeT, string boardText, string hudText)
    {
        Vector3 point = EvaluateRoutePoint(routeT);
        Vector3 forward = EvaluateRouteDirection(routeT);
        string boardName = "R" + regionIndex + "_Board";
        string triggerName = "R" + regionIndex + "_Trigger";

        CreateHintBoard(boardName, boardText, point + Vector3.up * 0.02f, forward);

        GameObject triggerObject = new GameObject(triggerName);
        triggerObject.transform.SetParent(root, false);
        triggerObject.transform.position = point + Vector3.up * 0.2f;
        triggerObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.4f, 0f);
        trigger.size = new Vector3(18f, 3.4f, 3.6f);

        RegionMechanicTrigger regionTrigger = triggerObject.AddComponent<RegionMechanicTrigger>();
        regionTrigger.Configure(regionIndex, "Region " + regionIndex, hudText, 3f);
    }

    private LocalCrystalGate CreateLocalCrystalGate(
        string name,
        Vector3 position,
        Vector3 forward,
        int requiredCrystals,
        string gateLabel,
        MechanismFeedbackType feedbackType)
    {
        Transform gateRoot = new GameObject(name).transform;
        gateRoot.SetParent(root, false);
        gateRoot.position = position;
        gateRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject barrier = CreateLocalCube("Barrier", gateRoot, Vector3.zero, new Vector3(17.2f, 8f, 0.62f), gateMaterial, true);
        CreateLocalCube("FrameL", gateRoot, new Vector3(-8.9f, 0f, 0f), new Vector3(0.64f, 8.6f, 0.9f), exitMaterial, true);
        CreateLocalCube("FrameR", gateRoot, new Vector3(8.9f, 0f, 0f), new Vector3(0.64f, 8.6f, 0.9f), exitMaterial, true);
        CreateLocalCube("FrameTop", gateRoot, new Vector3(0f, 4.3f, 0f), new Vector3(18.6f, 0.64f, 0.9f), exitMaterial, true);

        GameObject counterObj = new GameObject("CounterText");
        counterObj.transform.SetParent(gateRoot, false);
        counterObj.transform.localPosition = new Vector3(0f, 4.55f, -0.72f);
        counterObj.transform.localRotation = Quaternion.identity;

        TextMesh counter = counterObj.AddComponent<TextMesh>();
        counter.characterSize = 0.11f;
        counter.fontSize = 54;
        counter.anchor = TextAnchor.MiddleCenter;
        counter.alignment = TextAlignment.Center;
        counter.color = new Color(0.95f, 0.96f, 1f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (font != null)
        {
            counter.font = font;
            MeshRenderer meshRenderer = counterObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = font.material;
            }
        }

        LocalCrystalGate gate = barrier.AddComponent<LocalCrystalGate>();
        gate.Configure(gateLabel, requiredCrystals, counter, feedbackType);
        return gate;
    }

    private void CreateLocalCrystalPickup(string name, Vector3 position, LocalCrystalGate gate)
    {
        position = ClampCrystalUnderCeiling(position);

        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crystal.name = name;
        crystal.transform.SetParent(root, false);
        crystal.transform.position = position;
        crystal.transform.localScale = Vector3.one * 0.72f;

        Renderer renderer = crystal.GetComponent<Renderer>();
        if (renderer != null && crystalMaterial != null)
        {
            renderer.sharedMaterial = crystalMaterial;
        }

        Collider trigger = crystal.GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }

        LocalCrystalPickup pickup = crystal.AddComponent<LocalCrystalPickup>();
        pickup.Configure(gate, true);
    }

    private Vector3 ResolveCeilingCrystalPosition(Vector3 ceilingPadPosition, float dropDistance)
    {
        float drop = Mathf.Clamp(dropDistance, 0.35f, 2.4f);
        Vector3 desired = ceilingPadPosition - Vector3.up * drop;
        return ClampCrystalUnderCeiling(desired);
    }

    private Vector3 ClampCrystalUnderCeiling(Vector3 position)
    {
        float ceilingBottomY = GetRouteCeilingHeight();
        float maxY = ceilingBottomY - CrystalCeilingPadding;
        if (position.y > maxY)
        {
            position.y = maxY;
        }

        return position;
    }

    private void BuildMechanisms(int mechanismCount)
    {
        mechanismCount = Mathf.Max(1, mechanismCount);
        for (int i = 0; i < mechanismCount; i++)
        {
            float t = Mathf.Lerp(0.22f, 0.76f, (i + 0.5f) / mechanismCount);
            Vector3 center = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Transform cageRoot = new GameObject("LaserCage_" + i).transform;
            cageRoot.SetParent(root, false);
            cageRoot.position = center + Vector3.up * 0.01f;
            cageRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

            CreateLocalCube("CageBase", cageRoot, new Vector3(0f, 0f, 0f), new Vector3(9f, 0.2f, 9f), wallMaterial, true);
            CreateLocalCube("CageSideL", cageRoot, new Vector3(-4.5f, 2.2f, 0f), new Vector3(0.2f, 4.4f, 9f), wallMaterial, true);
            CreateLocalCube("CageSideR", cageRoot, new Vector3(4.5f, 2.2f, 0f), new Vector3(0.2f, 4.4f, 9f), wallMaterial, true);
            CreateLocalCube("CageBack", cageRoot, new Vector3(0f, 2.2f, -4.5f), new Vector3(9f, 4.4f, 0.2f), wallMaterial, true);
            CreateLocalCube("CageTop", cageRoot, new Vector3(0f, 4.4f, 0f), new Vector3(9f, 0.2f, 9f), wallMaterial, true);

            Transform barrierRoot = new GameObject("LaserBarrier").transform;
            barrierRoot.SetParent(cageRoot, false);
            barrierRoot.localPosition = new Vector3(0f, 2.2f, 4.45f);
            barrierRoot.localRotation = Quaternion.identity;

            for (int beam = 0; beam < 4; beam++)
            {
                float y = -1.5f + beam * 1.0f;
                CreateLocalCube(
                    "LaserH_" + beam,
                    barrierRoot,
                    new Vector3(0f, y, 0f),
                    new Vector3(8.2f, 0.16f, 0.16f),
                    laserMaterial,
                    true
                );
            }

            CreateLocalCube("LaserV_Left", barrierRoot, new Vector3(-4f, 0f, 0f), new Vector3(0.16f, 3.4f, 0.16f), laserMaterial, true);
            CreateLocalCube("LaserV_Right", barrierRoot, new Vector3(4f, 0f, 0f), new Vector3(0.16f, 3.4f, 0.16f), laserMaterial, true);

            LaserBarrier laserBarrier = barrierRoot.gameObject.AddComponent<LaserBarrier>();

            CreateOrientedCube(
                "CeilingRoute_" + i,
                center + Vector3.up * 8f,
                new Vector3(12f, 1f, 15f),
                forward,
                platformMaterial,
                root
            );

            GravitySwitchButton button = CreateGravitySwitch(
                "GravitySwitch_" + i,
                center + forward * 0.3f + right * (i % 2 == 0 ? 2.2f : -2.2f) + Vector3.up * 6.8f,
                forward
            );
            laserBarrier.LinkButton(button);

            int crystalsInCage = i == 0 ? 3 : 2;
            for (int c = 0; c < crystalsInCage; c++)
            {
                Vector3 local = new Vector3(-2.4f + c * 2.4f, 1.2f, -1.3f + (c % 2) * 2.6f);
                Vector3 world = cageRoot.TransformPoint(local);
                crystalCandidates.Add(world);
            }
        }
    }

    private void BuildFloatingPlatforms(int platformCount)
    {
        platformCount = Mathf.Max(2, platformCount);
        for (int i = 0; i < platformCount; i++)
        {
            float t = Mathf.Lerp(0.14f, 0.94f, (i + 1f) / (platformCount + 1f));
            Vector3 routePoint = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float side = (i % 2 == 0) ? 1f : -1f;
            float lateral = 6f + (i % 3) * 1.6f;
            float height = 2.3f + (i % 4) * 0.75f;

            Vector3 platformPos = routePoint + right * lateral * side + Vector3.up * height;
            CreateOrientedCube(
                "FloatPlatform_" + i,
                platformPos,
                new Vector3(4f, 0.65f, 4f),
                forward,
                platformMaterial,
                root
            );

            CreateOrientedCube(
                "FloatPlatformGlow_" + i,
                platformPos + Vector3.up * 0.34f,
                new Vector3(2.1f, 0.06f, 2.1f),
                forward,
                routeAccentMaterial,
                root
            );

            crystalCandidates.Add(platformPos + Vector3.up * 1.2f);
        }
    }

    private void BuildSecondaryMechanismChain(int mechanismCount)
    {
        if (mechanismCount < 2)
        {
            return;
        }

        int level = GetDesignLevel();
        float levelOffset = level == 1 ? 0f : (level == 2 ? 0.05f : 0.1f);
        float chainT = Mathf.Clamp01(0.48f + levelOffset + (mechanismCount - 2) * 0.02f);
        Vector3 center = EvaluateRoutePoint(chainT);
        Vector3 forward = EvaluateRouteDirection(chainT);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Transform chainRoot = new GameObject("MechanismChain_AB").transform;
        chainRoot.SetParent(root, false);
        chainRoot.position = center + Vector3.up * 0.02f;
        chainRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        CreateLocalCube("ChainFloor", chainRoot, new Vector3(0f, -0.42f, 0f), new Vector3(16f, 0.8f, 30f), routeMaterial, true);
        CreateLocalCube("ChainWallL", chainRoot, new Vector3(-7.8f, 2.5f, 0f), new Vector3(0.45f, 5f, 30f), wallMaterial, true);
        CreateLocalCube("ChainWallR", chainRoot, new Vector3(7.8f, 2.5f, 0f), new Vector3(0.45f, 5f, 30f), wallMaterial, true);

        CreateLocalCube("ChainCeilingA", chainRoot, new Vector3(0f, 6.7f, -8f), new Vector3(8.5f, 0.7f, 8f), platformMaterial, true);
        CreateLocalCube("ChainMidPedestal", chainRoot, new Vector3(0f, 0.65f, 1.2f), new Vector3(2.8f, 1.2f, 2.8f), platformMaterial, true);

        GravitySwitchButton switchA = CreateGravitySwitch(
            "ChainSwitch_A",
            chainRoot.TransformPoint(new Vector3(0f, 7.2f, -8f)),
            forward);
        CreateWorldLabel("Switch A", switchA.transform.position + Vector3.up * 1.1f, forward, new Color(1f, 0.84f, 0.32f));

        GravitySwitchButton switchB = CreateGravitySwitch(
            "ChainSwitch_B",
            chainRoot.TransformPoint(new Vector3(0f, 1.35f, 1.2f)),
            forward);
        CreateWorldLabel("Switch B", switchB.transform.position + Vector3.up * 1.05f, forward, new Color(0.62f, 0.88f, 1f));

        LaserBarrier gateA = CreateLaserGate(chainRoot, "ChainGate_A", new Vector3(0f, 3.2f, -3.4f), new Vector2(15.2f, 6f));
        LaserBarrier gateB = CreateLaserGate(chainRoot, "ChainGate_B", new Vector3(0f, 3.2f, 7.6f), new Vector2(15.2f, 6f));
        gateA.ConfigureFeedbackType(MechanismFeedbackType.GateA);
        gateB.ConfigureFeedbackType(MechanismFeedbackType.GateB);

        gateA.LinkButton(switchA);
        gateB.LinkButton(switchB);

        SequentialMechanismChain chainController = chainRoot.gameObject.AddComponent<SequentialMechanismChain>();
        chainController.Configure(switchA, switchB, "Activate switch A first");

        CreateOrientedCube(
            "ChainSideBridge",
            center + right * 10f + Vector3.up * 2.1f,
            new Vector3(4f, 0.7f, 9f),
            forward,
            platformMaterial,
            root
        );

        crystalCandidates.Add(chainRoot.TransformPoint(new Vector3(-2.2f, 1.2f, 11.2f)));
        crystalCandidates.Add(chainRoot.TransformPoint(new Vector3(2.2f, 1.2f, 11.2f)));
        crystalCandidates.Add(chainRoot.TransformPoint(new Vector3(0f, 7.9f, -8f)));
    }

    private LaserBarrier CreateLaserGate(Transform parent, string name, Vector3 localPosition, Vector2 size)
    {
        Transform gateRoot = new GameObject(name).transform;
        gateRoot.SetParent(parent, false);
        gateRoot.localPosition = localPosition;
        gateRoot.localRotation = Quaternion.identity;

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        float beamWidth = 0.16f;

        CreateLocalCube("BeamTop", gateRoot, new Vector3(0f, halfHeight - beamWidth * 0.5f, 0f), new Vector3(size.x, beamWidth, beamWidth), laserMaterial, true);
        CreateLocalCube("BeamBottom", gateRoot, new Vector3(0f, -halfHeight + beamWidth * 0.5f, 0f), new Vector3(size.x, beamWidth, beamWidth), laserMaterial, true);
        CreateLocalCube("BeamLeft", gateRoot, new Vector3(-halfWidth + beamWidth * 0.5f, 0f, 0f), new Vector3(beamWidth, size.y, beamWidth), laserMaterial, true);
        CreateLocalCube("BeamRight", gateRoot, new Vector3(halfWidth - beamWidth * 0.5f, 0f, 0f), new Vector3(beamWidth, size.y, beamWidth), laserMaterial, true);
        CreateLocalCube("BeamMid", gateRoot, new Vector3(0f, 0f, 0f), new Vector3(size.x * 0.92f, beamWidth, beamWidth), laserMaterial, true);
        CreateLocalCube("BeamFill", gateRoot, Vector3.zero, new Vector3(size.x * 0.96f, size.y * 0.94f, beamWidth * 0.55f), laserMaterial, true);

        return gateRoot.gameObject.AddComponent<LaserBarrier>();
    }

    private void CreateWorldLabel(string text, Vector3 position, Vector3 forward, Color color)
    {
        GameObject labelObject = new GameObject("Label_" + text.Replace(" ", string.Empty));
        labelObject.transform.SetParent(root, false);
        labelObject.transform.position = position;
        labelObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.characterSize = 0.12f;
        label.fontSize = 48;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = color;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font != null)
        {
            label.font = font;
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = font.material;
            }
        }
    }

    private void BuildAdvancedChallengeSetpieces()
    {
        int level = GetDesignLevel();
        if (level < 2)
        {
            return;
        }

        BuildTrampolineSkyRoute(level);
        BuildMovingPlatformRhythmSection(level);
        BuildRotatingHazardCorridor(level);
        if (level >= 4)
        {
            BuildBossMechanismSection();
        }
    }

    private void BuildGravityAnchorZones(int anchorZoneCount)
    {
        int count = Mathf.Clamp(anchorZoneCount, 0, 8);
        if (count <= 0 || root == null || routePoints.Count < 2)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(0.26f, 0.92f, (i + 0.5f) / Mathf.Max(1f, count));
            Vector3 routePoint = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 center = routePoint + right * (side * (1.8f + (i % 3) * 0.7f));

            Transform zoneRoot = new GameObject("GravityAnchorZone_" + i).transform;
            zoneRoot.SetParent(root, false);
            zoneRoot.position = center;
            zoneRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

            BoxCollider trigger = zoneRoot.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.45f, 0f);
            trigger.size = new Vector3(7.2f, 2.8f, 7.2f);

            zoneRoot.gameObject.AddComponent<GravityAnchorZone>();

            CreateLocalCube("AnchorBase", zoneRoot, new Vector3(0f, 0.03f, 0f), new Vector3(5.8f, 0.12f, 5.8f), challengeNeonMaterial, false);
            CreateLocalCube("AnchorRingOuter", zoneRoot, new Vector3(0f, 0.16f, 0f), new Vector3(6.4f, 0.06f, 0.25f), challengeNeonMaterial, false);
            CreateLocalCube("AnchorRingInner", zoneRoot, new Vector3(0f, 0.16f, 0f), new Vector3(0.25f, 0.06f, 6.4f), challengeNeonMaterial, false);
            CreateLocalCube("AnchorCore", zoneRoot, new Vector3(0f, 0.52f, 0f), new Vector3(0.42f, 0.84f, 0.42f), switchMaterial, false);
            CreateWorldLabel("ANCHOR", zoneRoot.position + Vector3.up * 1.55f, forward, new Color(1f, 0.78f, 0.30f));
        }
    }

    private void BuildBossMechanismSection()
    {
        Vector3 center = EvaluateRoutePoint(0.92f);
        Vector3 forward = EvaluateRouteDirection(0.92f);
        Transform bossRoot = new GameObject("BossMechanismSection").transform;
        bossRoot.SetParent(root, false);
        bossRoot.position = center + Vector3.up * 0.02f;
        bossRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        CreateLocalCube("BossFloor", bossRoot, new Vector3(0f, -0.42f, 0f), new Vector3(18f, 0.84f, 34f), routeMaterial, true);
        CreateLocalCube("BossWallL", bossRoot, new Vector3(-8.8f, 3f, 0f), new Vector3(0.45f, 6f, 34f), wallMaterial, true);
        CreateLocalCube("BossWallR", bossRoot, new Vector3(8.8f, 3f, 0f), new Vector3(0.45f, 6f, 34f), wallMaterial, true);
        CreateLocalCube("BossCeiling", bossRoot, new Vector3(0f, 6.6f, 0f), new Vector3(18f, 0.72f, 34f), routeMaterial, true);

        GravitySwitchButton switchA = CreateGravitySwitch(
            "BossSwitch_A",
            bossRoot.TransformPoint(new Vector3(-4.4f, 1.2f, -10.4f)),
            forward);
        GravitySwitchButton switchB = CreateGravitySwitch(
            "BossSwitch_B",
            bossRoot.TransformPoint(new Vector3(4.4f, 5.9f, 2.4f)),
            forward);

        switchB.SetInteractable(false, "Boss chain: activate A first");
        CreateWorldLabel("BOSS A", switchA.transform.position + Vector3.up * 1.0f, forward, new Color(1f, 0.86f, 0.34f));
        CreateWorldLabel("BOSS B", switchB.transform.position + Vector3.up * 1.0f, forward, new Color(0.42f, 0.92f, 1f));

        LaserBarrier gateA = CreateLaserGate(bossRoot, "BossGate_A", new Vector3(0f, 3f, -2.8f), new Vector2(17f, 6f));
        LaserBarrier gateB = CreateLaserGate(bossRoot, "BossGate_B", new Vector3(0f, 3f, 9.8f), new Vector2(17f, 6f));
        gateA.ConfigureFeedbackType(MechanismFeedbackType.GateA);
        gateB.ConfigureFeedbackType(MechanismFeedbackType.GateB);
        gateA.LinkButton(switchA);
        gateB.LinkButton(switchB);

        SequentialMechanismChain chain = bossRoot.gameObject.AddComponent<SequentialMechanismChain>();
        chain.Configure(switchA, switchB, "Boss chain: unlock A before B");

        Transform anchor = new GameObject("BossAnchorZone").transform;
        anchor.SetParent(bossRoot, false);
        anchor.localPosition = new Vector3(0f, 0f, 4.4f);
        BoxCollider anchorTrigger = anchor.gameObject.AddComponent<BoxCollider>();
        anchorTrigger.isTrigger = true;
        anchorTrigger.center = new Vector3(0f, 1.5f, 0f);
        anchorTrigger.size = new Vector3(8.8f, 3f, 7.8f);
        anchor.gameObject.AddComponent<GravityAnchorZone>();
        CreateLocalCube("BossAnchorVisual", anchor, new Vector3(0f, 0.04f, 0f), new Vector3(8.4f, 0.08f, 7.4f), challengeNeonMaterial, false);

        CreateDropFailZone(bossRoot, "BossFailZone", new Vector3(0f, -2.1f, 0f), new Vector3(23f, 2.6f, 34f));

        crystalCandidates.Add(bossRoot.TransformPoint(new Vector3(-4.4f, 2.2f, -10.4f)));
        crystalCandidates.Add(bossRoot.TransformPoint(new Vector3(4.4f, 6.9f, 2.4f)));
        crystalCandidates.Add(bossRoot.TransformPoint(new Vector3(0f, 1.5f, 13.5f)));

        CreateAccentLight(bossRoot, "BossLight_A", new Vector3(-4.4f, 2.4f, -10.4f), new Color(1f, 0.82f, 0.34f), 3.0f, 9f);
        CreateAccentLight(bossRoot, "BossLight_B", new Vector3(4.4f, 6.8f, 2.4f), new Color(0.40f, 0.90f, 1f), 3.0f, 9f);
        CreateAccentLight(bossRoot, "BossLight_Exit", new Vector3(0f, 2.8f, 13.5f), new Color(0.45f, 1f, 0.72f), 3.2f, 10f);
    }

    private void BuildTrampolineSkyRoute(int level)
    {
        float anchorT = level == 2 ? 0.57f : 0.60f;
        Vector3 center = EvaluateRoutePoint(anchorT);
        Vector3 forward = EvaluateRouteDirection(anchorT);

        Transform sectionRoot = new GameObject("Challenge_TrampolineSkyRoute").transform;
        sectionRoot.SetParent(root, false);
        sectionRoot.position = center + Vector3.up * 0.02f;
        sectionRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        CreateLocalCube("SkyRouteBlocker", sectionRoot, new Vector3(0f, 3f, -22f), new Vector3(16f, 6.2f, 1.1f), wallMaterial, true);
        CreateLocalCube("SkyRouteBlockerGlow", sectionRoot, new Vector3(0f, 3.4f, -21.45f), new Vector3(14.8f, 0.24f, 0.10f), challengeNeonMaterial, false);

        CreateLocalCube("SkyRouteRailL", sectionRoot, new Vector3(-6.6f, 3.2f, 4f), new Vector3(0.32f, 6.2f, 64f), wallMaterial, true);
        CreateLocalCube("SkyRouteRailR", sectionRoot, new Vector3(6.6f, 3.2f, 4f), new Vector3(0.32f, 6.2f, 64f), wallMaterial, true);

        CreateBouncePad(
            "MegaBounce_A",
            sectionRoot,
            new Vector3(0f, 0.14f, -28f),
            level == 2 ? 20f : 22f,
            0.18f,
            level == 2 ? 10.8f : 12.2f
        );

        CreateChallengePlatform(sectionRoot, "SkyStep_1", new Vector3(0f, 7.4f, -9.2f), new Vector3(5.4f, 0.66f, 5.4f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "SkyStep_2", new Vector3(3.9f, 9.1f, -1.2f), new Vector3(4.2f, 0.66f, 4.2f), true, level == 2 ? 0.66f : 0.54f, 2.4f);
        CreateChallengePlatform(sectionRoot, "SkyStep_3", new Vector3(-3.9f, 10.8f, 7.4f), new Vector3(4.0f, 0.66f, 4.0f), true, level == 2 ? 0.62f : 0.50f, 2.2f);
        CreateChallengePlatform(sectionRoot, "SkyStep_4", new Vector3(0f, 12.4f, 16.2f), new Vector3(5.0f, 0.68f, 5.0f), false, 0f, 0f);

        if (level >= 3)
        {
            CreateChallengePlatform(sectionRoot, "SkyStep_Extra", new Vector3(4.4f, 14.4f, 22.8f), new Vector3(3.7f, 0.62f, 3.7f), true, 0.45f, 2.0f);
        }

        CreateBouncePad(
            "MegaBounce_B",
            sectionRoot,
            level >= 3 ? new Vector3(4.4f, 14.6f, 22.8f) : new Vector3(0f, 12.8f, 16.2f),
            level == 2 ? 18.6f : 20.2f,
            0.20f,
            level == 2 ? 9.2f : 10.4f
        );

        Vector3 finalLanding = level >= 3 ? new Vector3(0f, 20.2f, 35.0f) : new Vector3(0f, 18.4f, 29.8f);
        CreateChallengePlatform(sectionRoot, "SkyLanding", finalLanding, new Vector3(8.2f, 0.82f, 7.2f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "SkyDrop_1", finalLanding + new Vector3(0f, -3.4f, 7f), new Vector3(7.2f, 0.72f, 5.6f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "SkyDrop_2", finalLanding + new Vector3(0f, -7.0f, 14f), new Vector3(7.0f, 0.72f, 5.4f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "SkyDrop_3", finalLanding + new Vector3(0f, -10.6f, 21f), new Vector3(6.8f, 0.72f, 5.4f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "SkyDrop_4", finalLanding + new Vector3(0f, -13.2f, 27f), new Vector3(7.8f, 0.82f, 7.0f), false, 0f, 0f);

        CreateDropFailZone(sectionRoot, "SkyRouteFailZone", new Vector3(0f, 4.1f, 8f), new Vector3(13.5f, 5.6f, 66f));

        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(3.9f, 10.3f, -1.2f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(-3.9f, 12.0f, 7.4f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(finalLanding + new Vector3(0f, 1.5f, 0f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(finalLanding + new Vector3(1.9f, 1.5f, 2.1f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(finalLanding + new Vector3(-1.9f, 1.5f, -2.1f)));

        if (level >= 3)
        {
            crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(4.4f, 15.9f, 22.8f)));
        }

        CreateAccentLight(sectionRoot, "BounceLight_A", new Vector3(0f, 1.2f, -28f), new Color(0.95f, 0.34f, 1f), 3.9f, 11f);
        CreateAccentLight(sectionRoot, "BounceLight_B", level >= 3 ? new Vector3(4.4f, 15.2f, 22.8f) : new Vector3(0f, 13.4f, 16.2f), new Color(0.28f, 0.92f, 1f), 3.6f, 10f);
        CreateAccentLight(sectionRoot, "BounceLight_Final", finalLanding + new Vector3(0f, 1.3f, 0f), new Color(1f, 0.80f, 0.30f), 3.1f, 9.5f);

        CreateWorldLabel("MEGA BOUNCE", sectionRoot.TransformPoint(new Vector3(0f, 2.2f, -28f)), forward, new Color(0.94f, 0.52f, 1f));
    }

    private void BuildMovingPlatformRhythmSection(int level)
    {
        float anchorT = level == 2 ? 0.72f : 0.75f;
        Vector3 center = EvaluateRoutePoint(anchorT);
        Vector3 forward = EvaluateRouteDirection(anchorT);

        Transform sectionRoot = new GameObject("Challenge_MovingRhythm").transform;
        sectionRoot.SetParent(root, false);
        sectionRoot.position = center + Vector3.up * 0.02f;
        sectionRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        float difficulty01 = Mathf.InverseLerp(3f, 6f, currentConfig.MechanismCount);
        float moverDurationA = Mathf.Lerp(level == 2 ? 3.2f : 2.9f, level == 2 ? 2.3f : 1.95f, difficulty01);
        float moverDurationB = Mathf.Lerp(level == 2 ? 2.7f : 2.4f, level == 2 ? 2.0f : 1.7f, difficulty01);
        float gateWindowA = Mathf.Lerp(level == 2 ? 4.2f : 3.8f, level == 2 ? 3.0f : 2.55f, difficulty01);
        float gateWindowB = Mathf.Lerp(level == 2 ? 3.5f : 3.1f, level == 2 ? 2.45f : 2.15f, difficulty01);
        float switchCooldown = Mathf.Lerp(0.46f, 0.26f, difficulty01);

        float levelEase = level >= 4 ? 0.34f : (level == 3 ? 0.24f : 0.14f);
        moverDurationA = Mathf.Clamp(moverDurationA + levelEase, 2.2f, 4.6f);
        moverDurationB = Mathf.Clamp(moverDurationB + levelEase * 0.85f, 1.9f, 4.0f);
        gateWindowA = Mathf.Clamp(gateWindowA + levelEase * 1.8f, 2.8f, 5.6f);
        gateWindowB = Mathf.Clamp(gateWindowB + levelEase * 1.7f, 2.4f, 5.0f);
        switchCooldown = Mathf.Clamp(switchCooldown - levelEase * 0.10f, 0.20f, 0.60f);

        CreateLocalCube("RhythmBlocker", sectionRoot, new Vector3(0f, 4.3f, -22.2f), new Vector3(17.8f, 8.6f, 1.1f), wallMaterial, true);
        CreateLocalCube("RhythmBlockerGlow", sectionRoot, new Vector3(0f, 4.8f, -21.65f), new Vector3(15.2f, 0.26f, 0.10f), dangerMaterial, false);

        CreateChallengePlatform(sectionRoot, "RhythmApproach", new Vector3(-4.8f, 1.2f, -17.2f), new Vector3(8.6f, 0.72f, 6.2f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmStartDeck", new Vector3(-10.4f, 2.2f, -10.6f), new Vector3(7.2f, 0.72f, 9.0f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmMidDeckA", new Vector3(3.4f, 2.5f, -4.0f), new Vector3(6.0f, 0.72f, 7.2f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmPostGateADeck", new Vector3(8.8f, 2.8f, 6.6f), new Vector3(6.2f, 0.72f, 6.4f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmPreGateBDeck", new Vector3(9.8f, 3.0f, 10.8f), new Vector3(5.4f, 0.72f, 4.8f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmExitDeck", new Vector3(10.4f, 2.8f, 19.4f), new Vector3(7.4f, 0.72f, 7.8f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmReconnect_1", new Vector3(6.4f, 2.1f, 24.6f), new Vector3(6.0f, 0.72f, 5.4f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmReconnect_2", new Vector3(2.8f, 1.4f, 29.4f), new Vector3(5.6f, 0.72f, 5.2f), false, 0f, 0f);
        CreateChallengePlatform(sectionRoot, "RhythmReconnect_3", new Vector3(0f, 0.78f, 34.2f), new Vector3(6.2f, 0.78f, 6.2f), false, 0f, 0f);

        CreateLocalCube("RhythmRail_L", sectionRoot, new Vector3(-14f, 3.1f, 1.6f), new Vector3(0.36f, 4.2f, 46f), wallMaterial, true);
        CreateLocalCube("RhythmRail_R", sectionRoot, new Vector3(14f, 3.1f, 1.6f), new Vector3(0.36f, 4.2f, 46f), wallMaterial, true);

        CreateMovingPlatform(
            "RhythmMover_A",
            sectionRoot,
            new Vector3(-5.8f, 1.9f, -7.4f),
            new Vector3(2.2f, 2.5f, -4.0f),
            new Vector3(4.2f, 0.62f, 4.2f),
            moverDurationA);

        CreateMovingPlatform(
            "RhythmMover_B",
            sectionRoot,
            new Vector3(6.8f, 2.8f, 9.0f),
            new Vector3(10.8f, 3.0f, 11.4f),
            new Vector3(3.6f, 0.58f, 3.6f),
            moverDurationB);

        if (level >= 3)
        {
            CreateChallengePlatform(sectionRoot, "RhythmUpperDeck", new Vector3(3.8f, 5.8f, 11.2f), new Vector3(4.8f, 0.66f, 4.8f), false, 0f, 0f);
            CreateMovingPlatform(
                "RhythmMover_C",
                sectionRoot,
                new Vector3(8.6f, 3.5f, 10.0f),
                new Vector3(4.2f, 5.0f, 13.0f),
                new Vector3(3.6f, 0.58f, 3.6f),
                moverDurationB * 0.86f);
        }

        RhythmSwitch switchA = CreateRhythmSwitch(
            "RhythmSwitch_A",
            sectionRoot.TransformPoint(new Vector3(-10.4f, 2.85f, -12.9f)),
            forward);
        switchA.Configure(switchCooldown, "Switch A triggered");
        CreateWorldLabel("RHYTHM A", switchA.transform.position + Vector3.up * 1.1f, forward, new Color(1f, 0.80f, 0.34f));

        RhythmSwitch switchB = CreateRhythmSwitch(
            "RhythmSwitch_B",
            sectionRoot.TransformPoint(new Vector3(9.6f, 3.55f, 8.9f)),
            forward);
        switchB.Configure(Mathf.Max(0.2f, switchCooldown - 0.05f), "Switch B triggered");
        switchB.SetInteractable(false, "Trigger A gate first");
        CreateWorldLabel("RHYTHM B", switchB.transform.position + Vector3.up * 1.1f, forward, new Color(0.34f, 0.90f, 1f));

        RhythmGate gateA = CreateRhythmGate(sectionRoot, "RhythmGate_A", new Vector3(6.2f, 5.7f, 2.4f), new Vector2(6.8f, 6.0f));
        gateA.Configure(
            gateWindowA,
            Mathf.Clamp(gateWindowA * 0.24f, 0.55f, 1.05f),
            20f + difficulty01 * 5f,
            "rhythm_gate_a",
            "Gate A",
            new Color(0.98f, 0.72f, 0.28f));
        gateA.ConfigureFeedbackType(MechanismFeedbackType.RhythmGateA);
        gateA.LinkSwitch(switchA);

        RhythmGate gateB = CreateRhythmGate(sectionRoot, "RhythmGate_B", new Vector3(10.2f, 5.9f, 14.0f), new Vector2(7.2f, 6.2f));
        gateB.Configure(
            gateWindowB,
            Mathf.Clamp(gateWindowB * 0.28f, 0.55f, 1.00f),
            22f + difficulty01 * 5f,
            "rhythm_gate_b",
            "Gate B",
            new Color(0.30f, 0.90f, 1f));
        gateB.ConfigureFeedbackType(MechanismFeedbackType.RhythmGateB);

        RhythmGateChainController chainController = sectionRoot.gameObject.AddComponent<RhythmGateChainController>();
        chainController.Configure(switchA, switchB, gateA, gateB, "Trigger A gate first");

        CreateDropFailZone(sectionRoot, "RhythmFailZone", new Vector3(0f, -1.6f, 3.4f), new Vector3(31f, 3.0f, 46f));

        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(-10.4f, 3.7f, -12.9f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(-2.2f, 4.2f, -6.0f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(6.2f, 3.8f, 2.4f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(9.6f, 4.3f, 8.9f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(10.2f, 4.2f, 14.0f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(10.4f, 3.9f, 19.2f)));
        crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(2.8f, 2.5f, 29.4f)));
        if (level >= 3)
        {
            crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(3.8f, 6.9f, 11.2f)));
            crystalCandidates.Add(sectionRoot.TransformPoint(new Vector3(0.0f, 2.2f, 34.2f)));
        }

        CreateAccentLight(sectionRoot, "RhythmLight_Switch", new Vector3(-10.4f, 3.4f, -12.9f), new Color(0.85f, 0.90f, 1f), 2.8f, 8.5f);
        CreateAccentLight(sectionRoot, "RhythmLight_GateA", new Vector3(6.2f, 4.2f, 2.4f), new Color(1f, 0.74f, 0.34f), 3.0f, 8.8f);
        CreateAccentLight(sectionRoot, "RhythmLight_GateB", new Vector3(10.2f, 4.3f, 14.0f), new Color(0.30f, 0.90f, 1f), 3.1f, 9f);
        CreateAccentLight(sectionRoot, "RhythmLight_Rejoin", new Vector3(2.8f, 2.1f, 29.4f), new Color(0.26f, 0.88f, 1f), 2.6f, 8f);
    }

    private void BuildRotatingHazardCorridor(int level)
    {
        float anchorT = level == 2 ? 0.83f : 0.86f;
        Vector3 center = EvaluateRoutePoint(anchorT);
        Vector3 forward = EvaluateRouteDirection(anchorT);

        Transform corridorRoot = new GameObject("Challenge_RotatingCorridor").transform;
        corridorRoot.SetParent(root, false);
        corridorRoot.position = center + Vector3.up * 0.02f;
        corridorRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        CreateLocalCube("RotorBlocker", corridorRoot, new Vector3(0f, 3f, -21.5f), new Vector3(16f, 6.2f, 1.1f), wallMaterial, true);
        CreateLocalCube("RotorBlockerGlow", corridorRoot, new Vector3(0f, 3.45f, -20.95f), new Vector3(14.8f, 0.24f, 0.10f), dangerMaterial, false);

        CreateChallengePlatform(corridorRoot, "RotorStep_0", new Vector3(0f, 1.0f, -16.2f), new Vector3(8.6f, 0.72f, 5.8f), false, 0f, 0f);
        CreateChallengePlatform(corridorRoot, "RotorStep_1", new Vector3(0f, 2.2f, -10.6f), new Vector3(8.4f, 0.72f, 5.6f), false, 0f, 0f);
        CreateChallengePlatform(corridorRoot, "RotorBridge", new Vector3(0f, 3.4f, 0f), new Vector3(8.2f, 0.74f, 32f), false, 0f, 0f);
        CreateChallengePlatform(corridorRoot, "RotorExit_1", new Vector3(0f, 2.2f, 10.8f), new Vector3(8.0f, 0.72f, 5.4f), false, 0f, 0f);
        CreateChallengePlatform(corridorRoot, "RotorExit_2", new Vector3(0f, 1.0f, 16.4f), new Vector3(8.2f, 0.74f, 6.2f), false, 0f, 0f);

        CreateLocalCube("RotorRailL", corridorRoot, new Vector3(-4.3f, 4.5f, 0f), new Vector3(0.32f, 4.2f, 33f), wallMaterial, true);
        CreateLocalCube("RotorRailR", corridorRoot, new Vector3(4.3f, 4.5f, 0f), new Vector3(0.32f, 4.2f, 33f), wallMaterial, true);

        int rotorCount = level >= 4 ? 4 : 3;
        for (int i = 0; i < rotorCount; i++)
        {
            float z = Mathf.Lerp(-10.5f, 10.5f, rotorCount == 1 ? 0.5f : i / (rotorCount - 1f));
            Transform pivot = new GameObject("RotorPivot_" + i).transform;
            pivot.SetParent(corridorRoot, false);
            pivot.localPosition = new Vector3(0f, 4.2f, z);
            pivot.localRotation = Quaternion.identity;

            CreateLocalCube("RotorArm_A", pivot, Vector3.zero, new Vector3(8.5f, 0.34f, 0.58f), dangerMaterial, true);
            if (level >= 3 || i % 2 == 1)
            {
                Transform secondArm = CreateLocalCube("RotorArm_B", pivot, Vector3.zero, new Vector3(8.1f, 0.30f, 0.52f), dangerMaterial, true).transform;
                secondArm.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }

            CreateLocalPrimitive("RotorCore", PrimitiveType.Sphere, pivot, Vector3.zero, new Vector3(0.52f, 0.52f, 0.52f), challengeNeonMaterial, false);

            RotatingObstacle rotor = pivot.gameObject.AddComponent<RotatingObstacle>();
            float speed = level == 2
                ? 82f + i * 14f
                : 98f + i * 16f;
            float swingAngle = level == 2 ? 72f : 88f;
            rotor.Configure(speed, i % 2 == 1, swingAngle);
        }

        CreateDropFailZone(corridorRoot, "RotorFailZone_L", new Vector3(-7.8f, 3.8f, 0f), new Vector3(4.4f, 4.2f, 36f));
        CreateDropFailZone(corridorRoot, "RotorFailZone_R", new Vector3(7.8f, 3.8f, 0f), new Vector3(4.4f, 4.2f, 36f));

        crystalCandidates.Add(corridorRoot.TransformPoint(new Vector3(-2.2f, 5.4f, -8.6f)));
        crystalCandidates.Add(corridorRoot.TransformPoint(new Vector3(2.2f, 5.4f, 0f)));
        crystalCandidates.Add(corridorRoot.TransformPoint(new Vector3(-2.2f, 5.4f, 8.6f)));
        if (level >= 3)
        {
            crystalCandidates.Add(corridorRoot.TransformPoint(new Vector3(0f, 5.8f, 12.4f)));
        }

        CreateAccentLight(corridorRoot, "RotorLight_Entry", new Vector3(0f, 3.6f, -12f), new Color(1f, 0.35f, 0.28f), 2.8f, 8f);
        CreateAccentLight(corridorRoot, "RotorLight_Mid", new Vector3(0f, 4.2f, 0f), new Color(0.25f, 0.90f, 1f), 2.6f, 8f);
        CreateAccentLight(corridorRoot, "RotorLight_Exit", new Vector3(0f, 3.6f, 12f), new Color(1f, 0.82f, 0.34f), 2.8f, 8f);
    }

    private GameObject CreateChallengePlatform(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        bool collapsible,
        float collapseDelay,
        float respawnDelay)
    {
        Material material = collapsible && collapsePlatformMaterial != null
            ? collapsePlatformMaterial
            : platformMaterial;

        GameObject platform = CreateLocalCube(name, parent, localPosition, localScale, material, true);
        CreateLocalCube(
            name + "_Glow",
            parent,
            localPosition + new Vector3(0f, localScale.y * 0.5f + 0.04f, 0f),
            new Vector3(localScale.x * 0.64f, 0.06f, localScale.z * 0.64f),
            challengeNeonMaterial,
            false
        );

        if (collapsible)
        {
            TimedCollapsePlatform collapse = platform.AddComponent<TimedCollapsePlatform>();
            collapse.Configure(collapseDelay, respawnDelay);
        }

        return platform;
    }

    private BouncePad CreateBouncePad(
        string name,
        Transform parent,
        Vector3 localPosition,
        float bounceVelocity,
        float cooldown,
        float forwardBoost)
    {
        GameObject padRoot = new GameObject(name);
        padRoot.transform.SetParent(parent, false);
        padRoot.transform.localPosition = localPosition;
        padRoot.transform.localRotation = Quaternion.identity;

        BoxCollider trigger = padRoot.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.45f, 0f);
        trigger.size = new Vector3(4.2f, 1.1f, 4.2f);

        BouncePad pad = padRoot.AddComponent<BouncePad>();
        pad.Configure(bounceVelocity, cooldown, forwardBoost);

        CreateLocalPrimitive("PadBase", PrimitiveType.Cylinder, padRoot.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.62f, 0.10f, 1.62f), bouncePadMaterial, false);
        CreateLocalPrimitive("PadCore", PrimitiveType.Cylinder, padRoot.transform, new Vector3(0f, 0.20f, 0f), new Vector3(1.22f, 0.08f, 1.22f), challengeNeonMaterial, false);
        CreateLocalPrimitive("PadRing", PrimitiveType.Cylinder, padRoot.transform, new Vector3(0f, 0.30f, 0f), new Vector3(1.70f, 0.02f, 1.70f), challengeNeonMaterial, false);
        return pad;
    }

    private MovingPlatform CreateMovingPlatform(
        string name,
        Transform parent,
        Vector3 localStart,
        Vector3 localEnd,
        Vector3 localScale,
        float travelDuration)
    {
        Material material = movingPlatformMaterial != null ? movingPlatformMaterial : platformMaterial;
        GameObject platform = CreateLocalCube(name, parent, localStart, localScale, material, true);

        CreateLocalCube(
            name + "_Glow",
            platform.transform,
            new Vector3(0f, localScale.y * 0.5f + 0.04f, 0f),
            new Vector3(localScale.x * 0.62f, 0.06f, localScale.z * 0.62f),
            challengeNeonMaterial,
            false
        );

        Rigidbody body = platform.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = platform.AddComponent<Rigidbody>();
        }
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.useGravity = false;

        MovingPlatform mover = platform.AddComponent<MovingPlatform>();
        mover.Configure(localStart, localEnd, travelDuration, true, Random.Range(0f, 1f));
        return mover;
    }

    private RhythmSwitch CreateRhythmSwitch(string name, Vector3 position, Vector3 forward)
    {
        GameObject switchObj = new GameObject(name);
        switchObj.transform.SetParent(root, false);
        switchObj.transform.position = position;
        switchObj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        BoxCollider trigger = switchObj.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(2.1f, 1.15f, 2.1f);

        RhythmSwitch rhythmSwitch = switchObj.AddComponent<RhythmSwitch>();
        CreateLocalPrimitive("SwitchBase", PrimitiveType.Cylinder, switchObj.transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.68f, 0.11f, 0.68f), switchMaterial, false);
        CreateLocalPrimitive("SwitchCore", PrimitiveType.Sphere, switchObj.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.46f, 0.20f, 0.46f), challengeNeonMaterial, false);
        CreateLocalPrimitive("SwitchRing", PrimitiveType.Cylinder, switchObj.transform, new Vector3(0f, 0.30f, 0f), new Vector3(0.84f, 0.03f, 0.84f), bouncePadMaterial, false);
        return rhythmSwitch;
    }

    private RhythmGate CreateRhythmGate(Transform parent, string name, Vector3 localPosition, Vector2 size)
    {
        Transform gateRoot = new GameObject(name).transform;
        gateRoot.SetParent(parent, false);
        gateRoot.localPosition = localPosition;
        gateRoot.localRotation = Quaternion.identity;

        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        float beamWidth = 0.16f;

        CreateLocalCube("BeamTop", gateRoot, new Vector3(0f, halfHeight - beamWidth * 0.5f, 0f), new Vector3(size.x, beamWidth, beamWidth), dangerMaterial, true);
        CreateLocalCube("BeamBottom", gateRoot, new Vector3(0f, -halfHeight + beamWidth * 0.5f, 0f), new Vector3(size.x, beamWidth, beamWidth), dangerMaterial, true);
        CreateLocalCube("BeamLeft", gateRoot, new Vector3(-halfWidth + beamWidth * 0.5f, 0f, 0f), new Vector3(beamWidth, size.y, beamWidth), dangerMaterial, true);
        CreateLocalCube("BeamRight", gateRoot, new Vector3(halfWidth - beamWidth * 0.5f, 0f, 0f), new Vector3(beamWidth, size.y, beamWidth), dangerMaterial, true);
        CreateLocalCube("BeamMid", gateRoot, new Vector3(0f, 0f, 0f), new Vector3(size.x * 0.90f, beamWidth, beamWidth), dangerMaterial, true);
        CreateLocalCube("BeamFill", gateRoot, Vector3.zero, new Vector3(size.x * 0.95f, size.y * 0.92f, beamWidth * 0.50f), dangerMaterial, true);

        return gateRoot.gameObject.AddComponent<RhythmGate>();
    }

    private void CreateDropFailZone(Transform parent, string name, Vector3 localPosition, Vector3 localSize)
    {
        GameObject failZone = new GameObject(name);
        failZone.transform.SetParent(parent, false);
        failZone.transform.localPosition = localPosition;
        failZone.transform.localRotation = Quaternion.identity;

        BoxCollider trigger = failZone.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = localSize;
        failZone.AddComponent<KillZone>();
    }

    private void CreateAccentLight(Transform parent, string name, Vector3 localPosition, Color color, float intensity, float range)
    {
        GameObject lightObj = new GameObject(name);
        lightObj.transform.SetParent(parent, false);
        lightObj.transform.localPosition = localPosition;
        lightObj.transform.localRotation = Quaternion.identity;

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = Mathf.Clamp(intensity, 0.1f, 8f);
        light.range = Mathf.Clamp(range, 1f, 40f);
        light.shadows = LightShadows.None;
    }

    private void BuildPathCrystals(int totalCrystalsGoal)
    {
        int samples = Mathf.Max(totalCrystalsGoal * 3, 40);
        for (int i = 0; i < samples; i++)
        {
            float t = (i + 1f) / (samples + 2f);
            Vector3 routePoint = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float lane = ((i % 5) - 2) * 1.8f;
            float height = 1.25f;
            if (i % 11 == 0)
            {
                height += 1.6f;
            }

            crystalCandidates.Add(routePoint + right * lane + Vector3.up * height);
        }
    }

    private void BuildCheckpoints()
    {
        checkpointPositions.Clear();
        CheckpointPolicy policy = (CheckpointPolicy)Mathf.Clamp((int)currentConfig.CheckpointPolicy, 0, 2);

        if (IsTutorialLevel())
        {
            // Add an early tutorial safety checkpoint so early flip experiments don't hard reset progress.
            CreateCheckpointAt(0.10f);

            switch (policy)
            {
                case CheckpointPolicy.Dense:
                    CreateCheckpointSet(new[] { 0.18f, 0.36f, 0.54f, 0.72f, 0.88f });
                    break;
                case CheckpointPolicy.Sparse:
                    CreateCheckpointSet(new[] { 0.34f, 0.68f });
                    break;
                default:
                    CreateCheckpointSet(new[] { 0.24f, 0.48f, 0.72f, 0.90f });
                    break;
            }
            return;
        }

        int level = GetDesignLevel();

        if (level == 1)
        {
            // Ensure the first flip practice has a nearby retry anchor instead of full runback.
            CreateCheckpointAt(0.12f);

            switch (policy)
            {
                case CheckpointPolicy.Dense:
                    CreateCheckpointSet(new[] { 0.15f, 0.30f, 0.44f, 0.58f, 0.74f, 0.90f });
                    break;
                case CheckpointPolicy.Sparse:
                    CreateCheckpointSet(new[] { 0.24f, 0.54f, 0.84f });
                    break;
                default:
                    CreateCheckpointSet(new[] { 0.18f, 0.38f, 0.58f, 0.78f, 0.92f });
                    break;
            }
            return;
        }

        if (policy == CheckpointPolicy.Dense)
        {
            CreateCheckpointSet(new[] { 0.16f, 0.30f, 0.44f, 0.58f, 0.72f, 0.86f, 0.95f });
            return;
        }

        if (policy == CheckpointPolicy.Sparse)
        {
            if (level >= 4 || currentConfig.MapLength >= 320f)
            {
                CreateCheckpointSet(new[] { 0.30f, 0.62f, 0.90f });
            }
            else
            {
                CreateCheckpointSet(new[] { 0.34f, 0.68f });
            }
            return;
        }

        CreateCheckpointSet(new[] { 0.22f, 0.42f, 0.62f });
        if (level >= 2 || currentConfig.MapLength >= 220f)
        {
            CreateCheckpointAt(0.78f);
        }
        if (level >= 3 || currentConfig.MapLength >= 250f)
        {
            CreateCheckpointAt(0.90f);
        }
        if (level >= 4 || currentConfig.MapLength >= 320f)
        {
            CreateCheckpointAt(0.96f);
        }
    }

    private void CreateCheckpointSet(IReadOnlyList<float> points)
    {
        if (points == null)
        {
            return;
        }

        for (int i = 0; i < points.Count; i++)
        {
            CreateCheckpointAt(points[i]);
        }
    }

    private void CreateCheckpointAt(float t)
    {
        Vector3 point = EvaluateRoutePoint(t);
        if (IsTooCloseToAny(checkpointPositions, point, 4f))
        {
            return;
        }
        checkpointPositions.Add(point);

        Vector3 forward = EvaluateRouteDirection(t);

        GameObject checkpoint = new GameObject("Checkpoint");
        checkpoint.transform.SetParent(root, false);
        checkpoint.transform.position = point + Vector3.up * 0.1f;
        checkpoint.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        BoxCollider trigger = checkpoint.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.4f, 0f);
        trigger.size = new Vector3(4f, 2.8f, 4f);

        checkpoint.AddComponent<CheckpointZone>();

        GameObject markerBase = CreateLocalPrimitive(
            "CheckpointBase",
            PrimitiveType.Cylinder,
            checkpoint.transform,
            new Vector3(0f, 0.08f, 0f),
            new Vector3(1.35f, 0.08f, 1.35f),
            switchMaterial,
            false
        );
        DisableCollider(markerBase);

        GameObject markerCore = CreateLocalPrimitive(
            "CheckpointCore",
            PrimitiveType.Sphere,
            checkpoint.transform,
            new Vector3(0f, 0.65f, 0f),
            new Vector3(0.44f, 0.44f, 0.44f),
            switchMaterial,
            false
        );
        DisableCollider(markerCore);
    }

    private void BuildEnergyGateAndExit()
    {
        Vector3 gatePoint = EvaluateRoutePoint(0.93f);
        Vector3 forward = EvaluateRouteDirection(0.93f);

        Transform gateRoot = new GameObject("EnergyGateRoot").transform;
        gateRoot.SetParent(root, false);
        gateRoot.position = gatePoint + Vector3.up * 3f;
        gateRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject gateBarrier = CreateLocalCube("GateBarrier", gateRoot, Vector3.zero, new Vector3(17.2f, 6f, 0.7f), gateMaterial, true);
        gateBarrier.AddComponent<EnergyGate>();
        CreateLocalCube("GateFrameL", gateRoot, new Vector3(-8.8f, 0f, 0f), new Vector3(0.8f, 6.6f, 1f), exitMaterial, true);
        CreateLocalCube("GateFrameR", gateRoot, new Vector3(8.8f, 0f, 0f), new Vector3(0.8f, 6.6f, 1f), exitMaterial, true);
        CreateLocalCube("GateFrameTop", gateRoot, new Vector3(0f, 3.3f, 0f), new Vector3(18.4f, 0.8f, 1f), exitMaterial, true);

        Vector3 exitPoint = EvaluateRoutePoint(0.985f);
        Vector3 exitForward = EvaluateRouteDirection(0.985f);

        Transform exitRoot = new GameObject("ExitPortal").transform;
        exitRoot.SetParent(root, false);
        exitRoot.position = exitPoint + Vector3.up * 2f;
        exitRoot.rotation = Quaternion.LookRotation(exitForward, Vector3.up);

        CreateLocalCube("ExitFrameL", exitRoot, new Vector3(-2f, 0f, 0f), new Vector3(0.6f, 4f, 0.6f), exitMaterial, true);
        CreateLocalCube("ExitFrameR", exitRoot, new Vector3(2f, 0f, 0f), new Vector3(0.6f, 4f, 0.6f), exitMaterial, true);
        CreateLocalCube("ExitFrameTop", exitRoot, new Vector3(0f, 2f, 0f), new Vector3(4.5f, 0.6f, 0.6f), exitMaterial, true);
        CreateLocalCube("ExitGlow", exitRoot, new Vector3(0f, 0f, -0.2f), new Vector3(3.2f, 3.2f, 0.1f), routeAccentMaterial, false);

        GameObject trigger = new GameObject("ExitTrigger");
        trigger.transform.SetParent(exitRoot, false);
        trigger.transform.localPosition = new Vector3(0f, 0f, -0.4f);
        trigger.transform.localRotation = Quaternion.identity;

        BoxCollider collider = trigger.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(6f, 4f, 2.4f);
        trigger.AddComponent<ExitZone>();
    }

    private void BuildKillZone()
    {
        if (routePoints.Count == 0)
        {
            GameObject fallbackZone = new GameObject("KillZone");
            fallbackZone.transform.SetParent(root, false);
            fallbackZone.transform.position = new Vector3(0f, -26f, 0f);

            BoxCollider fallbackCollider = fallbackZone.AddComponent<BoxCollider>();
            fallbackCollider.isTrigger = true;
            fallbackCollider.size = new Vector3(220f, 12f, 220f);
            fallbackZone.AddComponent<KillZone>();
            return;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        for (int i = 0; i < routePoints.Count; i++)
        {
            Vector3 p = routePoints[i];
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        GameObject killZone = new GameObject("KillZone");
        killZone.transform.SetParent(root, false);
        killZone.transform.position = new Vector3((minX + maxX) * 0.5f, -26f, (minZ + maxZ) * 0.5f);

        BoxCollider collider = killZone.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3((maxX - minX) + 180f, 12f, (maxZ - minZ) + 180f);

        killZone.AddComponent<KillZone>();
    }

    private void BuildHintBoards()
    {
        if (IsTutorialLevel())
        {
            CreateHintBoard("Hint_Tutorial_Move", "Tutorial: WASD move, Space jump, Mouse look.", EvaluateRoutePoint(0.08f), EvaluateRouteDirection(0.08f));
            CreateHintBoard("Hint_Tutorial_Flip", "Press F to flip gravity and touch the switch.", EvaluateRoutePoint(0.36f), EvaluateRouteDirection(0.36f));
            CreateHintBoard("Hint_Tutorial_Exit", "Collect crystals, unlock gate, then reach the exit portal.", EvaluateRoutePoint(0.78f), EvaluateRouteDirection(0.78f));
            return;
        }

        int level = GetDesignLevel();
        if (level == 1)
        {
            return;
        }

        string stageHint;
        switch (level)
        {
            case 2:
                stageHint = "Level 2: trampoline + moving platform + dual rhythm gates + rotating hazards";
                break;
            case 3:
                stageHint = "Level 3: full challenge, denser hazards and strict jump rhythm";
                break;
            default:
                stageHint = "Level 4 Boss: compound chain + gravity anchor pressure section";
                break;
        }

        CreateHintBoard("Hint_Stage", stageHint, EvaluateRoutePoint(0.04f), EvaluateRouteDirection(0.04f));
        CreateHintBoard("Hint_1", "Collect enough crystals to unlock exit", EvaluateRoutePoint(0.16f), EvaluateRouteDirection(0.16f));
        CreateHintBoard("Hint_2", "Flip gravity to reach ceiling switches", EvaluateRoutePoint(0.32f), EvaluateRouteDirection(0.32f));
        if (currentConfig.MechanismCount >= 2)
        {
            CreateHintBoard("Hint_Chain", "Chain puzzle: activate Switch A first, then Switch B", EvaluateRoutePoint(0.48f), EvaluateRouteDirection(0.48f));
        }
        if (level >= 2)
        {
            CreateHintBoard("Hint_Bounce", "Mega bounce pads launch to sky path. Miss jump = reset.", EvaluateRoutePoint(0.60f), EvaluateRouteDirection(0.60f));
            CreateHintBoard("Hint_Rhythm", "Rhythm chain: A switch -> Gate A window -> B switch -> Gate B window.", EvaluateRoutePoint(0.72f), EvaluateRouteDirection(0.72f));
            CreateHintBoard("Hint_RhythmBar", "Top countdown bars show A/B gate windows in real time.", EvaluateRoutePoint(0.76f), EvaluateRouteDirection(0.76f));
            CreateHintBoard("Hint_Rotor", "Rotate corridor: read timing, then dash through.", EvaluateRoutePoint(0.82f), EvaluateRouteDirection(0.82f));
        }
        if (level >= 3 || currentConfig.GravityAnchorZoneCount > 0)
        {
            CreateHintBoard("Hint_Anchor", "Anchor zone: flip disabled inside. Reposition before entering.", EvaluateRoutePoint(0.56f), EvaluateRouteDirection(0.56f));
        }
        if (level >= 4)
        {
            CreateHintBoard("Hint_Boss", "Boss chain: clear A first, then B, then rush final gate.", EvaluateRoutePoint(0.93f), EvaluateRouteDirection(0.93f));
        }
        CreateHintBoard("Hint_3", "High platforms require jump or double jump", EvaluateRoutePoint(0.68f), EvaluateRouteDirection(0.68f));
        CreateHintBoard("Hint_4", "Gather more crystals for higher stars", EvaluateRoutePoint(0.90f), EvaluateRouteDirection(0.90f));
    }

    private void BuildHelloNpcs()
    {
        CreateHelloNpc("HelloNpc_A", 0.08f, -4.2f);
        CreateHelloNpc("HelloNpc_B", 0.22f, 4.2f);
    }

    private void CreateHelloNpc(string name, float routeT, float lateralOffset)
    {
        if (root == null)
        {
            return;
        }

        Vector3 point = EvaluateRoutePoint(routeT);
        Vector3 forward = EvaluateRouteDirection(routeT);
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        GameObject npcRoot = new GameObject(name);
        npcRoot.transform.SetParent(root, false);
        npcRoot.transform.position = point + right * lateralOffset;
        npcRoot.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);

        CapsuleCollider trigger = npcRoot.AddComponent<CapsuleCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.0f, 0f);
        trigger.height = 2.3f;
        trigger.radius = 0.62f;

        HelloNpcBehaviour npc = npcRoot.AddComponent<HelloNpcBehaviour>();
        npc.Configure("hello");

        CreateLocalPrimitive(
            "Body",
            PrimitiveType.Capsule,
            npcRoot.transform,
            new Vector3(0f, 0.9f, 0f),
            new Vector3(0.72f, 0.84f, 0.72f),
            playerSecondaryMaterial,
            false
        );
        CreateLocalPrimitive(
            "Head",
            PrimitiveType.Sphere,
            npcRoot.transform,
            new Vector3(0f, 1.76f, 0f),
            new Vector3(0.48f, 0.48f, 0.48f),
            playerPrimaryMaterial,
            false
        );
        CreateLocalPrimitive(
            "Eye",
            PrimitiveType.Cube,
            npcRoot.transform,
            new Vector3(0f, 1.76f, 0.23f),
            new Vector3(0.18f, 0.08f, 0.05f),
            playerGlowMaterial,
            false
        );
        CreateLocalPrimitive(
            "Feet",
            PrimitiveType.Cylinder,
            npcRoot.transform,
            new Vector3(0f, 0.06f, 0f),
            new Vector3(0.42f, 0.06f, 0.42f),
            switchMaterial,
            false
        );
    }

    private sealed class HelloNpcBehaviour : MonoBehaviour
    {
        [SerializeField] private string greeting = "hello";
        [SerializeField] private float cooldown = 1.25f;

        private float nextSpeakAt;
        private readonly HashSet<int> touchingPlayerIds = new HashSet<int>();

        public void Configure(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                greeting = text.Trim();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryTrackAndSpeak(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryTrackAndSpeak(other);
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

            touchingPlayerIds.Remove(player.GetInstanceID());
        }

        private void OnDisable()
        {
            touchingPlayerIds.Clear();
        }

        private void TryTrackAndSpeak(Collider other)
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

            int id = player.GetInstanceID();
            if (touchingPlayerIds.Add(id))
            {
                TrySpeak(player);
            }
        }

        private void TrySpeak(PlayerGravityMotor player)
        {
            if (player == null || Time.time < nextSpeakAt)
            {
                return;
            }

            nextSpeakAt = Time.time + Mathf.Max(0.2f, cooldown);
            RuntimeHUD.Instance?.ShowTransientMessage(greeting, 0.95f);
            Debug.Log("[HelloNpc] " + greeting);
        }
    }

    private void CreateHintBoard(string name, string text, Vector3 position, Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 boardPos = position + right * 8f + Vector3.up * 2.8f;

        GameObject board = CreateOrientedCube(
            name + "_Panel",
            boardPos,
            new Vector3(5f, 2f, 0.15f),
            -right,
            wallMaterial,
            root
        );
        DisableCollider(board);

        GameObject textObj = new GameObject(name + "_Text");
        textObj.transform.SetParent(root, false);
        textObj.transform.position = boardPos + (-right) * 0.12f;
        textObj.transform.rotation = Quaternion.LookRotation(-right, Vector3.up);

        TextMesh mesh = textObj.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 54;
        mesh.characterSize = 0.08f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(0.87f, 0.95f, 1f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        if (font != null)
        {
            mesh.font = font;
            MeshRenderer renderer = textObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = font.material;
            }
        }
    }

    private struct CrystalCandidateData
    {
        public Vector3 Position;
        public float Progress;
        public float DistanceFromRoute;
        public float HeightAboveRoute;
    }

    private int SpawnCrystals(int totalCount)
    {
        int targetCount = Mathf.Max(1, totalCount);
        const float minSpacingSqr = 3f;

        List<Vector3> uniqueCandidates = new List<Vector3>(crystalCandidates.Count);
        for (int i = 0; i < crystalCandidates.Count; i++)
        {
            TryAddCrystalPosition(uniqueCandidates, crystalCandidates[i], minSpacingSqr, true);
        }

        List<CrystalCandidateData> rankedCandidates = new List<CrystalCandidateData>(uniqueCandidates.Count);
        for (int i = 0; i < uniqueCandidates.Count; i++)
        {
            rankedCandidates.Add(BuildCrystalCandidateData(uniqueCandidates[i]));
        }

        rankedCandidates.Sort((a, b) =>
        {
            int progressComparison = a.Progress.CompareTo(b.Progress);
            if (progressComparison != 0)
            {
                return progressComparison;
            }

            int heightComparison = a.HeightAboveRoute.CompareTo(b.HeightAboveRoute);
            if (heightComparison != 0)
            {
                return heightComparison;
            }

            return a.DistanceFromRoute.CompareTo(b.DistanceFromRoute);
        });

        List<Vector3> finalPositions = new List<Vector3>(targetCount);
        int baselineQuota = Mathf.Clamp(Mathf.RoundToInt(targetCount * GetBaselineCrystalRatio()), 1, targetCount);

        for (int i = 0; i < rankedCandidates.Count && finalPositions.Count < baselineQuota; i++)
        {
            CrystalCandidateData candidate = rankedCandidates[i];
            if (!IsBaselineCrystal(candidate))
            {
                continue;
            }

            TryAddCrystalPosition(finalPositions, candidate.Position, minSpacingSqr, true);
        }

        for (int i = 0; i < rankedCandidates.Count && finalPositions.Count < targetCount; i++)
        {
            TryAddCrystalPosition(finalPositions, rankedCandidates[i].Position, minSpacingSqr, true);
        }

        int safety = 0;
        while (finalPositions.Count < targetCount && safety < 500)
        {
            safety++;
            float t = Random.Range(0.06f, 0.96f);
            Vector3 route = EvaluateRoutePoint(t);
            Vector3 forward = EvaluateRouteDirection(t);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.right;
            }

            float lateralRange;
            float maxHeight;
            if (IsTutorialLevel())
            {
                lateralRange = 3.8f;
                maxHeight = 2.8f;
            }
            else
            {
                int level = GetDesignLevel();
                lateralRange = level == 1 ? 4.8f : (level == 2 ? 6.0f : (level == 3 ? 7.2f : 8.2f));
                maxHeight = level == 1 ? 3.4f : (level == 2 ? 4.8f : (level == 3 ? 6.0f : 7.2f));
            }
            Vector3 extra = route +
                            right * Random.Range(-lateralRange, lateralRange) +
                            Vector3.up * Random.Range(1.25f, maxHeight);

            TryAddCrystalPosition(finalPositions, extra, minSpacingSqr, true);
        }

        // Final deterministic pass: relax spacing slightly to reduce "not enough crystals"
        // in extreme configs while keeping placements valid and reachable.
        if (finalPositions.Count < targetCount)
        {
            float relaxedSpacingSqr = minSpacingSqr * 0.36f;
            int deterministicSlots = Mathf.Max(16, targetCount * 6);
            for (int i = 0; i < deterministicSlots && finalPositions.Count < targetCount; i++)
            {
                float t = (i + 0.5f) / deterministicSlots;
                Vector3 route = EvaluateRoutePoint(t);
                Vector3 forward = EvaluateRouteDirection(t);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.right;
                }

                float laneBias = Mathf.Sin(i * 1.37f);
                float lateral = Mathf.Lerp(2.2f, 7.2f, Mathf.Abs(laneBias));
                float side = i % 2 == 0 ? -1f : 1f;
                float height = IsTutorialLevel() ? 1.45f : Mathf.Lerp(1.35f, 4.8f, Mathf.InverseLerp(1, 4, GetDesignLevel()));
                Vector3 candidate = route + right * side * lateral + Vector3.up * height;
                TryAddCrystalPosition(finalPositions, candidate, relaxedSpacingSqr, true);
            }
        }

        if (finalPositions.Count < targetCount)
        {
            Debug.LogWarning(
                "[LevelBuilder] Crystal placement capped at " + finalPositions.Count +
                "/" + targetCount +
                ". Consider lowering crystal target or raising map density.");
        }

        for (int i = 0; i < finalPositions.Count; i++)
        {
            CreateCrystal("Crystal_" + i, finalPositions[i]);
        }

        return finalPositions.Count;
    }

    private float GetBaselineCrystalRatio()
    {
        float ratio;
        if (IsTutorialLevel())
        {
            ratio = 0.90f;
        }
        else
        {
            int level = GetDesignLevel();
            ratio = level == 1 ? 0.84f : (level == 2 ? 0.74f : (level == 3 ? 0.68f : 0.64f));
        }
        if (currentConfig.MechanismCount >= 5)
        {
            ratio -= 0.04f;
        }

        return Mathf.Clamp(ratio, 0.45f, 0.9f);
    }

    private bool IsBaselineCrystal(CrystalCandidateData candidate)
    {
        float maxHeight;
        float maxRouteDistance;
        if (IsTutorialLevel())
        {
            maxHeight = 2.3f;
            maxRouteDistance = 5.4f;
        }
        else
        {
            int level = GetDesignLevel();
            maxHeight = level == 1 ? 2.8f : (level == 2 ? 3.2f : (level == 3 ? 3.8f : 4.2f));
            maxRouteDistance = level == 1 ? 6.5f : (level == 2 ? 7.2f : (level == 3 ? 8.2f : 9.0f));
        }
        return candidate.HeightAboveRoute <= maxHeight && candidate.DistanceFromRoute <= maxRouteDistance;
    }

    private CrystalCandidateData BuildCrystalCandidateData(Vector3 position)
    {
        float progress = EstimateRouteMetrics(position, out float distanceFromRoute, out float heightAboveRoute);
        return new CrystalCandidateData
        {
            Position = position,
            Progress = progress,
            DistanceFromRoute = distanceFromRoute,
            HeightAboveRoute = heightAboveRoute
        };
    }

    private float EstimateRouteMetrics(Vector3 point, out float distanceFromRoute, out float heightAboveRoute)
    {
        distanceFromRoute = point.magnitude;
        heightAboveRoute = Mathf.Max(0f, point.y);

        if (routePoints.Count < 2 || routeDistances.Count < 2)
        {
            return 0f;
        }

        float totalLength = routeDistances[routeDistances.Count - 1];
        if (totalLength <= 0.0001f)
        {
            return 0f;
        }

        float bestDistanceSqr = float.MaxValue;
        float bestAlongRoute = 0f;
        float bestHeight = 0f;

        for (int i = 0; i < routePoints.Count - 1; i++)
        {
            Vector3 a = routePoints[i];
            Vector3 b = routePoints[i + 1];
            Vector3 segment = b - a;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.0001f)
            {
                continue;
            }

            float localT = Mathf.Clamp01(Vector3.Dot(point - a, segment) / segmentLengthSqr);
            Vector3 closest = a + segment * localT;
            float distanceSqr = (point - closest).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            float segmentLength = Mathf.Sqrt(segmentLengthSqr);
            bestAlongRoute = routeDistances[i] + segmentLength * localT;
            bestHeight = point.y - closest.y;
        }

        distanceFromRoute = bestDistanceSqr < float.MaxValue ? Mathf.Sqrt(bestDistanceSqr) : point.magnitude;
        heightAboveRoute = Mathf.Max(0f, bestHeight);
        return Mathf.Clamp01(bestAlongRoute / totalLength);
    }

    private static bool IsTooCloseToAny(List<Vector3> positions, Vector3 candidate, float minDistanceSqr)
    {
        if (positions == null)
        {
            return false;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            if ((candidate - positions[i]).sqrMagnitude < minDistanceSqr)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryAddCrystalPosition(List<Vector3> positions, Vector3 candidate, float minDistanceSqr, bool validateBlocking)
    {
        if (positions == null)
        {
            return false;
        }

        if (!TryNormalizeCrystalPosition(candidate, out Vector3 normalized))
        {
            return false;
        }

        if (IsTooCloseToAny(positions, normalized, minDistanceSqr))
        {
            return false;
        }

        if (validateBlocking && IsCrystalPlacementBlocked(normalized, CrystalPlacementProbeRadius))
        {
            return false;
        }

        positions.Add(normalized);
        return true;
    }

    private bool TryNormalizeCrystalPosition(Vector3 candidate, out Vector3 normalized)
    {
        normalized = candidate;
        if (float.IsNaN(candidate.x) || float.IsNaN(candidate.y) || float.IsNaN(candidate.z))
        {
            return false;
        }

        if (float.IsInfinity(candidate.x) || float.IsInfinity(candidate.y) || float.IsInfinity(candidate.z))
        {
            return false;
        }

        normalized = ClampCrystalUnderCeiling(candidate);
        normalized.y = Mathf.Max(0.75f, normalized.y);
        return true;
    }

    private static bool IsCrystalPlacementBlocked(Vector3 position, float radius)
    {
        float probeRadius = Mathf.Max(0.05f, radius);
        return Physics.CheckSphere(position, probeRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private PlayerGravityMotor BuildPlayer(Vector3 spawnPoint)
    {
        GameObject player = new GameObject("Player");
        if (root != null)
        {
            player.transform.SetParent(root, false);
        }
        player.transform.position = spawnPoint;

        CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
        capsule.radius = 0.42f;
        capsule.height = 2f;
        capsule.center = Vector3.zero;
        capsule.material = GetPlayerPhysicsMaterial();

        player.AddComponent<Rigidbody>();

        Transform visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(player.transform, false);
        visualRoot.localPosition = Vector3.zero;

        Transform torso;
        Transform head;
        Transform flipRing;
        Renderer pulseRenderer;
        BuildPlayerVisuals(visualRoot, out torso, out head, out flipRing, out pulseRenderer);

        PlayerGravityMotor motor = player.AddComponent<PlayerGravityMotor>();
        PlayerVisualAvatar avatar = player.AddComponent<PlayerVisualAvatar>();
        avatar.Configure(visualRoot, torso, head, flipRing, pulseRenderer);

        motor.SetSpawnPoint(spawnPoint);
        return motor;
    }

    private PhysicsMaterial GetPlayerPhysicsMaterial()
    {
        if (playerPhysicsMaterial != null)
        {
            return playerPhysicsMaterial;
        }

        playerPhysicsMaterial = new PhysicsMaterial("PlayerFrictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        return playerPhysicsMaterial;
    }

    private void BuildPlayerVisuals(
        Transform visualRoot,
        out Transform torso,
        out Transform head,
        out Transform flipRing,
        out Renderer pulseRenderer)
    {
        switch (currentConfig.VisualTheme)
        {
            case PlayerVisualTheme.Mechanical:
                BuildMechanicalVisual(visualRoot, out torso, out head, out flipRing, out pulseRenderer);
                break;
            case PlayerVisualTheme.Minimal:
                BuildMinimalVisual(visualRoot, out torso, out head, out flipRing, out pulseRenderer);
                break;
            default:
                BuildAstronautVisual(visualRoot, out torso, out head, out flipRing, out pulseRenderer);
                break;
        }
    }

    private void BuildAstronautVisual(
        Transform visualRoot,
        out Transform torso,
        out Transform head,
        out Transform flipRing,
        out Renderer pulseRenderer)
    {
        torso = CreateLocalPrimitive(
            "Torso",
            PrimitiveType.Cube,
            visualRoot,
            new Vector3(0f, 0.05f, 0f),
            new Vector3(0.72f, 1.0f, 0.46f),
            playerPrimaryMaterial,
            false
        ).transform;

        head = CreateLocalPrimitive(
            "Head",
            PrimitiveType.Sphere,
            visualRoot,
            new Vector3(0f, 0.98f, 0f),
            new Vector3(0.46f, 0.46f, 0.46f),
            playerPrimaryMaterial,
            false
        ).transform;

        CreateLocalPrimitive("Visor", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.97f, 0.22f), new Vector3(0.28f, 0.18f, 0.06f), playerGlowMaterial, false);
        CreateLocalPrimitive("BackCore", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 0.26f, -0.25f), new Vector3(0.18f, 0.18f, 0.18f), playerGlowMaterial, false);
        CreateLocalPrimitive("Hip", PrimitiveType.Cube, visualRoot, new Vector3(0f, -0.58f, 0f), new Vector3(0.62f, 0.24f, 0.40f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("ArmL", PrimitiveType.Capsule, visualRoot, new Vector3(-0.44f, 0.2f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("ArmR", PrimitiveType.Capsule, visualRoot, new Vector3(0.44f, 0.2f, 0f), new Vector3(0.16f, 0.44f, 0.16f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("LegL", PrimitiveType.Capsule, visualRoot, new Vector3(-0.18f, -0.70f, 0f), new Vector3(0.20f, 0.40f, 0.20f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("LegR", PrimitiveType.Capsule, visualRoot, new Vector3(0.18f, -0.70f, 0f), new Vector3(0.20f, 0.40f, 0.20f), playerSecondaryMaterial, false);

        flipRing = CreateLocalPrimitive("FlipRing", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.46f, 0f), new Vector3(0.42f, 0.03f, 0.42f), playerGlowMaterial, false).transform;
        pulseRenderer = CreateLocalPrimitive("PulseCore", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 0.24f, -0.25f), new Vector3(0.15f, 0.15f, 0.15f), playerGlowMaterial, false).GetComponent<Renderer>();
    }

    private void BuildMechanicalVisual(
        Transform visualRoot,
        out Transform torso,
        out Transform head,
        out Transform flipRing,
        out Renderer pulseRenderer)
    {
        torso = CreateLocalPrimitive(
            "MechBody",
            PrimitiveType.Cube,
            visualRoot,
            new Vector3(0f, 0.05f, 0f),
            new Vector3(0.80f, 1.06f, 0.54f),
            playerPrimaryMaterial,
            false
        ).transform;

        head = CreateLocalPrimitive(
            "MechHead",
            PrimitiveType.Cube,
            visualRoot,
            new Vector3(0f, 1.04f, 0f),
            new Vector3(0.46f, 0.36f, 0.42f),
            playerPrimaryMaterial,
            false
        ).transform;

        CreateLocalPrimitive("MechVisor", PrimitiveType.Cube, visualRoot, new Vector3(0f, 1.03f, 0.24f), new Vector3(0.30f, 0.14f, 0.06f), playerGlowMaterial, false);
        CreateLocalPrimitive("ShoulderL", PrimitiveType.Cube, visualRoot, new Vector3(-0.50f, 0.38f, 0f), new Vector3(0.22f, 0.22f, 0.30f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("ShoulderR", PrimitiveType.Cube, visualRoot, new Vector3(0.50f, 0.38f, 0f), new Vector3(0.22f, 0.22f, 0.30f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("ArmL", PrimitiveType.Cube, visualRoot, new Vector3(-0.50f, -0.18f, 0f), new Vector3(0.16f, 0.50f, 0.20f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("ArmR", PrimitiveType.Cube, visualRoot, new Vector3(0.50f, -0.18f, 0f), new Vector3(0.16f, 0.50f, 0.20f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("LegL", PrimitiveType.Cube, visualRoot, new Vector3(-0.18f, -0.72f, 0f), new Vector3(0.20f, 0.42f, 0.22f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("LegR", PrimitiveType.Cube, visualRoot, new Vector3(0.18f, -0.72f, 0f), new Vector3(0.20f, 0.42f, 0.22f), playerSecondaryMaterial, false);
        CreateLocalPrimitive("BackPack", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.12f, -0.28f), new Vector3(0.36f, 0.58f, 0.20f), playerSecondaryMaterial, false);

        flipRing = CreateLocalPrimitive("FlipRing", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.42f, 0f), new Vector3(0.46f, 0.03f, 0.46f), playerGlowMaterial, false).transform;
        pulseRenderer = CreateLocalPrimitive("PulseCore", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 0.18f, -0.29f), new Vector3(0.14f, 0.14f, 0.14f), playerGlowMaterial, false).GetComponent<Renderer>();
    }

    private void BuildMinimalVisual(
        Transform visualRoot,
        out Transform torso,
        out Transform head,
        out Transform flipRing,
        out Renderer pulseRenderer)
    {
        torso = CreateLocalPrimitive(
            "CoreBody",
            PrimitiveType.Capsule,
            visualRoot,
            new Vector3(0f, 0f, 0f),
            new Vector3(0.70f, 0.95f, 0.70f),
            playerPrimaryMaterial,
            false
        ).transform;

        head = CreateLocalPrimitive(
            "CoreHead",
            PrimitiveType.Sphere,
            visualRoot,
            new Vector3(0f, 0.86f, 0f),
            new Vector3(0.36f, 0.36f, 0.36f),
            playerSecondaryMaterial,
            false
        ).transform;

        CreateLocalPrimitive("FrontBar", PrimitiveType.Cube, visualRoot, new Vector3(0f, 0.18f, 0.30f), new Vector3(0.38f, 0.06f, 0.06f), playerGlowMaterial, false);
        CreateLocalPrimitive("BackBar", PrimitiveType.Cube, visualRoot, new Vector3(0f, -0.08f, -0.30f), new Vector3(0.30f, 0.06f, 0.06f), playerGlowMaterial, false);
        CreateLocalPrimitive("Feet", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, -0.78f, 0f), new Vector3(0.35f, 0.08f, 0.35f), playerSecondaryMaterial, false);

        flipRing = CreateLocalPrimitive("FlipRing", PrimitiveType.Cylinder, visualRoot, new Vector3(0f, 1.18f, 0f), new Vector3(0.48f, 0.02f, 0.48f), playerGlowMaterial, false).transform;
        pulseRenderer = CreateLocalPrimitive("PulseCore", PrimitiveType.Sphere, visualRoot, new Vector3(0f, 0.10f, 0f), new Vector3(0.20f, 0.20f, 0.20f), playerGlowMaterial, false).GetComponent<Renderer>();
    }

    private GravityCameraFollow SetupCamera(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        GravityCameraFollow follow = camera.GetComponent<GravityCameraFollow>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<GravityCameraFollow>();
        }

        camera.fieldOfView = 66f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 620f;
        camera.renderingPath = RenderingPath.Forward;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.allowDynamicResolution = false;
        camera.useOcclusionCulling = true;
        camera.usePhysicalProperties = true;
        camera.sensorSize = new Vector2(36f, 20.25f);
        camera.focalLength = 42f;
        camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.DepthNormals | DepthTextureMode.MotionVectors;
        camera.transform.position = target.position + new Vector3(0f, 4f, -9f);
        camera.transform.rotation = Quaternion.LookRotation((target.position - camera.transform.position).normalized, Vector3.up);
        follow.SetTarget(target);

        ConfigureRenderQuality(camera);
        return follow;
    }

    private Light EnsureDirectionalLight()
    {
        Light directional = null;
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
            {
                directional = lights[i];
                break;
            }
        }

        if (directional == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
        }

        directional.intensity = 1.55f;
        directional.color = new Color(1f, 0.97f, 0.93f);
        directional.shadows = LightShadows.Soft;
        directional.shadowStrength = 0.80f;
        directional.shadowBias = 0.020f;
        directional.shadowNormalBias = 0.36f;
        directional.shadowNearPlane = 0.25f;
        directional.shadowCustomResolution = Mathf.Max(directional.shadowCustomResolution, 6144);
        directional.useColorTemperature = true;
        directional.colorTemperature = 5850f;
        directional.bounceIntensity = 0.96f;
        directional.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
        RenderSettings.sun = directional;
        return directional;
    }

    private void SetupScenicAtmosphere(Light sun)
    {
        if (root == null)
        {
            return;
        }

        ScenicAtmosphereController controller = root.GetComponent<ScenicAtmosphereController>();
        if (controller == null)
        {
            controller = root.gameObject.AddComponent<ScenicAtmosphereController>();
        }

        controller.Configure(
            sun,
            terrainMaterial,
            terrainSoilMaterial,
            terrainRockMaterial,
            mountainNearMaterial,
            mountainNearWarmMaterial,
            mountainFarMaterial,
            mountainFarWarmMaterial,
            cloudMaterial,
            skyboxMaterial,
            Camera.main != null ? Camera.main.GetComponent("CameraRenderEnhancer") as MonoBehaviour : null,
            cloudVisuals.ToArray()
        );
    }

    private void ConfigureRenderQuality(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 8);
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = Mathf.Max(QualitySettings.shadowCascades, 4);
        QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 260f);
        QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 10);
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 2.7f);
        QualitySettings.streamingMipmapsActive = false;
        QualitySettings.realtimeReflectionProbes = true;
        QualitySettings.billboardsFaceCameraPosition = true;
        QualitySettings.softParticles = true;
        QualitySettings.softVegetation = true;
        QualitySettings.maxQueuedFrames = Mathf.Clamp(QualitySettings.maxQueuedFrames, 1, 2);
        ScalableBufferManager.ResizeBuffers(1f, 1f);

        MonoBehaviour enhancer = EnsureRenderEnhancer(camera.gameObject);

        float profileIntensity = 1f;
        if (GameDirector.Instance != null)
        {
            profileIntensity = Mathf.Lerp(0.88f, 1.24f, Mathf.Clamp01(GameDirector.Instance.CurrentSettings.FeedbackIntensity * 0.5f));
        }

        if (enhancer != null)
        {
            MethodInfo configureMethod = enhancer.GetType().GetMethod("ConfigureProfile", new[] { typeof(float) });
            configureMethod?.Invoke(enhancer, new object[] { profileIntensity });
        }
    }

    private static MonoBehaviour EnsureRenderEnhancer(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        Component existing = target.GetComponent("CameraRenderEnhancer");
        if (existing is MonoBehaviour existingBehaviour)
        {
            return existingBehaviour;
        }

        System.Type enhancerType = FindTypeInLoadedAssemblies("CameraRenderEnhancer");
        if (enhancerType == null || !typeof(MonoBehaviour).IsAssignableFrom(enhancerType))
        {
            return null;
        }

        Component created = target.AddComponent(enhancerType);
        return created as MonoBehaviour;
    }

    private static System.Type FindTypeInLoadedAssemblies(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private GravitySwitchButton CreateGravitySwitch(string name, Vector3 position, Vector3 forward)
    {
        GameObject switchObj = new GameObject(name);
        switchObj.transform.SetParent(root, false);
        switchObj.transform.position = position;
        switchObj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        BoxCollider trigger = switchObj.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = Vector3.zero;
        trigger.size = new Vector3(1.8f, 1.0f, 1.8f);

        GravitySwitchButton button = switchObj.AddComponent<GravitySwitchButton>();
        CreateLocalPrimitive("SwitchBase", PrimitiveType.Cylinder, switchObj.transform, new Vector3(0f, -0.2f, 0f), new Vector3(0.62f, 0.10f, 0.62f), switchMaterial, false);
        CreateLocalPrimitive("SwitchCore", PrimitiveType.Sphere, switchObj.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.42f, 0.18f, 0.42f), switchMaterial, false);
        return button;
    }

    private void CreateCrystal(string name, Vector3 position)
    {
        position = ClampCrystalUnderCeiling(position);

        RuntimeObjectPool pool = RuntimeObjectPool.Instance;
        GameObject crystal = pool != null
            ? pool.Get(CrystalPoolKey, CreateCrystalPrefab, 360)
            : CreateCrystalPrefab();
        if (crystal == null)
        {
            return;
        }

        crystal.name = name;
        crystal.transform.SetParent(root, false);
        crystal.transform.position = position;
        crystal.transform.rotation = Quaternion.identity;
        crystal.transform.localScale = Vector3.one * 0.75f;

        Renderer renderer = crystal.GetComponent<Renderer>();
        if (renderer != null && crystalMaterial != null)
        {
            renderer.sharedMaterial = crystalMaterial;
        }

        Collider trigger = crystal.GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.enabled = true;
        }

        CrystalPickup pickup = crystal.GetComponent<CrystalPickup>();
        if (pickup == null)
        {
            pickup = crystal.AddComponent<CrystalPickup>();
        }
        pickup.ConfigurePool(pool, CrystalPoolKey);
        pickup.PrepareForSpawn();
    }

    private static GameObject CreateCrystalPrefab()
    {
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Collider collider = crystal.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        if (crystal.GetComponent<CrystalPickup>() == null)
        {
            crystal.AddComponent<CrystalPickup>();
        }

        return crystal;
    }

    private GameObject CreateCube(
        string name,
        Vector3 position,
        Vector3 scale,
        Material material,
        Transform parent,
        bool enableCollider)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = scale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        if (!enableCollider)
        {
            DisableCollider(cube);
        }

        return cube;
    }

    private GameObject CreateOrientedCube(
        string name,
        Vector3 position,
        Vector3 scale,
        Vector3 forward,
        Material material,
        Transform parent)
    {
        GameObject cube = CreateCube(name, position, scale, material, parent, true);
        if (forward.sqrMagnitude > 0.0001f)
        {
            cube.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
        return cube;
    }

    private GameObject CreateLocalCube(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        bool enableCollider)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;

        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        if (!enableCollider)
        {
            DisableCollider(cube);
        }

        return cube;
    }

    private GameObject CreateLocalPrimitive(
        string name,
        PrimitiveType primitiveType,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        bool enableCollider)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = Quaternion.identity;
        primitive.transform.localScale = localScale;

        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        if (!enableCollider)
        {
            DisableCollider(primitive);
        }

        return primitive;
    }

    private static void DisableCollider(GameObject obj)
    {
        Collider c = obj.GetComponent<Collider>();
        if (c != null)
        {
            c.enabled = false;
        }
    }

    private static void ConfigureSceneryRenderer(GameObject obj)
    {
        if (obj == null)
        {
            return;
        }

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private Vector3 EvaluateRoutePoint(float t)
    {
        t = Mathf.Clamp01(t);
        if (routePoints.Count == 0)
        {
            return Vector3.zero;
        }

        if (routePoints.Count == 1 || routeDistances.Count < 2)
        {
            return routePoints[0];
        }

        float totalLength = routeDistances[routeDistances.Count - 1];
        float target = totalLength * t;

        for (int i = 1; i < routeDistances.Count; i++)
        {
            if (target <= routeDistances[i])
            {
                float segmentStartDist = routeDistances[i - 1];
                float segmentLength = routeDistances[i] - segmentStartDist;
                float localT = segmentLength > 0.0001f ? (target - segmentStartDist) / segmentLength : 0f;
                return Vector3.Lerp(routePoints[i - 1], routePoints[i], localT);
            }
        }

        return routePoints[routePoints.Count - 1];
    }

    private Vector3 EvaluateRouteDirection(float t)
    {
        const float epsilon = 0.005f;
        Vector3 a = EvaluateRoutePoint(Mathf.Clamp01(t));
        Vector3 b = EvaluateRoutePoint(Mathf.Clamp01(t + epsilon));
        Vector3 direction = (b - a).normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            Vector3 c = EvaluateRoutePoint(Mathf.Clamp01(t - epsilon));
            direction = (a - c).normalized;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            if (routePoints.Count >= 2)
            {
                Vector3 tail = routePoints[routePoints.Count - 1] - routePoints[routePoints.Count - 2];
                if (tail.sqrMagnitude > 0.0001f)
                {
                    direction = tail.normalized;
                }
            }
        }
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }
        return direction;
    }

    private static Material CreateMaterial(
        string name,
        Color color,
        float metallic,
        float smoothness,
        Color emission = default)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }
        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = name,
            color = color
        };

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }
        if (material.HasProperty("_Glossiness"))
        {
            material.SetFloat("_Glossiness", smoothness);
        }
        if (emission != default && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }

        return material;
    }

    private static Material CreateSkyboxMaterial()
    {
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader == null)
        {
            return null;
        }

        Material material = new Material(skyShader)
        {
            name = "M_RuntimeSkybox"
        };

        if (material.HasProperty("_SunDisk"))
        {
            material.SetFloat("_SunDisk", 2f);
        }
        if (material.HasProperty("_SunSize"))
        {
            material.SetFloat("_SunSize", 0.038f);
        }
        if (material.HasProperty("_SunSizeConvergence"))
        {
            material.SetFloat("_SunSizeConvergence", 6f);
        }
        if (material.HasProperty("_AtmosphereThickness"))
        {
            material.SetFloat("_AtmosphereThickness", 0.92f);
        }
        if (material.HasProperty("_Exposure"))
        {
            material.SetFloat("_Exposure", 1.05f);
        }
        if (material.HasProperty("_SkyTint"))
        {
            material.SetColor("_SkyTint", new Color(0.62f, 0.75f, 0.90f, 1f));
        }
        if (material.HasProperty("_GroundColor"))
        {
            material.SetColor("_GroundColor", new Color(0.40f, 0.42f, 0.39f, 1f));
        }

        return material;
    }

    private void TrackRuntimeMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        runtimeMaterials.Add(material);
    }

    private void TrackRuntimeTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        runtimeTextures.Add(texture);
    }

    private void ApplyMaterialDetailTextures()
    {
        Texture2D routeTex = CreateProceduralTexture("T_RouteDetail", 128, new Color(0.20f, 0.22f, 0.26f), new Color(0.31f, 0.35f, 0.40f), 8.8f, 1.10f);
        Texture2D platformTex = CreateProceduralTexture("T_PlatformDetail", 128, new Color(0.20f, 0.48f, 0.40f), new Color(0.30f, 0.62f, 0.52f), 9.6f, 1.05f);
        Texture2D wallTex = CreateProceduralTexture("T_WallDetail", 128, new Color(0.25f, 0.28f, 0.34f), new Color(0.36f, 0.40f, 0.47f), 7.6f, 1.02f);
        Texture2D grassTex = CreateProceduralTexture("T_GrassDetail", 256, new Color(0.26f, 0.36f, 0.22f), new Color(0.37f, 0.52f, 0.32f), 12.2f, 0.96f);
        Texture2D soilTex = CreateProceduralTexture("T_SoilDetail", 256, new Color(0.34f, 0.24f, 0.16f), new Color(0.46f, 0.34f, 0.22f), 11.0f, 0.96f);
        Texture2D rockTex = CreateProceduralTexture("T_RockDetail", 256, new Color(0.25f, 0.25f, 0.26f), new Color(0.39f, 0.38f, 0.40f), 9.2f, 1.04f);
        Texture2D mountainTex = CreateProceduralTexture("T_MountainDetail", 256, new Color(0.33f, 0.37f, 0.42f), new Color(0.48f, 0.50f, 0.52f), 8.4f, 1.02f);

        Texture2D routeNormal = CreateProceduralNormalMap("T_RouteNormal", 128, 8.8f, 2.5f);
        Texture2D platformNormal = CreateProceduralNormalMap("T_PlatformNormal", 128, 9.6f, 2.2f);
        Texture2D wallNormal = CreateProceduralNormalMap("T_WallNormal", 128, 7.6f, 2.1f);
        Texture2D grassNormal = CreateProceduralNormalMap("T_GrassNormal", 256, 12.2f, 2.8f);
        Texture2D soilNormal = CreateProceduralNormalMap("T_SoilNormal", 256, 11.0f, 2.4f);
        Texture2D rockNormal = CreateProceduralNormalMap("T_RockNormal", 256, 9.2f, 2.9f);
        Texture2D mountainNormal = CreateProceduralNormalMap("T_MountainNormal", 256, 8.4f, 2.3f);

        ApplyMainTexture(routeMaterial, routeTex, new Vector2(6.2f, 16f), 8);
        ApplyMainTexture(routeAccentMaterial, routeTex, new Vector2(4.8f, 14f), 8);
        ApplyMainTexture(platformMaterial, platformTex, new Vector2(5.8f, 5.8f), 8);
        ApplyMainTexture(wallMaterial, wallTex, new Vector2(4.5f, 6.5f), 8);
        ApplyMainTexture(terrainMaterial, grassTex, new Vector2(44f, 44f), 8);
        ApplyMainTexture(terrainSoilMaterial, soilTex, new Vector2(38f, 38f), 8);
        ApplyMainTexture(terrainRockMaterial, rockTex, new Vector2(34f, 34f), 8);
        ApplyMainTexture(mountainNearMaterial, mountainTex, new Vector2(14f, 11f), 8);
        ApplyMainTexture(mountainNearWarmMaterial, mountainTex, new Vector2(14f, 11f), 8);
        ApplyMainTexture(mountainFarMaterial, mountainTex, new Vector2(11f, 9f), 8);
        ApplyMainTexture(mountainFarWarmMaterial, mountainTex, new Vector2(11f, 9f), 8);

        ApplyNormalTexture(routeMaterial, routeNormal, new Vector2(6.2f, 16f), 8, 0.62f);
        ApplyNormalTexture(routeAccentMaterial, routeNormal, new Vector2(4.8f, 14f), 8, 0.58f);
        ApplyNormalTexture(platformMaterial, platformNormal, new Vector2(5.8f, 5.8f), 8, 0.56f);
        ApplyNormalTexture(wallMaterial, wallNormal, new Vector2(4.5f, 6.5f), 8, 0.52f);
        ApplyNormalTexture(terrainMaterial, grassNormal, new Vector2(44f, 44f), 8, 0.66f);
        ApplyNormalTexture(terrainSoilMaterial, soilNormal, new Vector2(38f, 38f), 8, 0.58f);
        ApplyNormalTexture(terrainRockMaterial, rockNormal, new Vector2(34f, 34f), 8, 0.68f);
        ApplyNormalTexture(mountainNearMaterial, mountainNormal, new Vector2(14f, 11f), 8, 0.64f);
        ApplyNormalTexture(mountainNearWarmMaterial, mountainNormal, new Vector2(14f, 11f), 8, 0.60f);
        ApplyNormalTexture(mountainFarMaterial, mountainNormal, new Vector2(11f, 9f), 8, 0.56f);
        ApplyNormalTexture(mountainFarWarmMaterial, mountainNormal, new Vector2(11f, 9f), 8, 0.54f);
    }

    private Texture2D CreateProceduralTexture(
        string name,
        int size,
        Color colorA,
        Color colorB,
        float frequency,
        float contrast)
    {
        int texSize = Mathf.Clamp(size, 32, 512);
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, true, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };

        Color[] pixels = new Color[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        {
            float fy = y / (float)texSize;
            for (int x = 0; x < texSize; x++)
            {
                float fx = x / (float)texSize;
                float n0 = Mathf.PerlinNoise(fx * frequency + 0.27f, fy * frequency + 1.91f);
                float n1 = Mathf.PerlinNoise(fx * frequency * 1.85f + 3.73f, fy * frequency * 1.85f + 7.31f);
                float n2 = Mathf.PerlinNoise((fx + fy) * frequency * 0.9f + 9.23f, (fy - fx) * frequency * 0.9f + 2.57f);
                float blend = Mathf.Clamp01((n0 * 0.58f + n1 * 0.29f + n2 * 0.13f));
                blend = Mathf.Pow(blend, Mathf.Max(0.4f, contrast));
                Color c = Color.Lerp(colorA, colorB, blend);

                float micro = Mathf.PerlinNoise(fx * frequency * 6.6f + 1.14f, fy * frequency * 6.6f + 4.22f) * 0.08f - 0.04f;
                c.r = Mathf.Clamp01(c.r + micro);
                c.g = Mathf.Clamp01(c.g + micro);
                c.b = Mathf.Clamp01(c.b + micro);
                c.a = 1f;
                pixels[y * texSize + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true, false);
        TrackRuntimeTexture(tex);
        return tex;
    }

    private Texture2D CreateProceduralNormalMap(string name, int size, float frequency, float strength)
    {
        int texSize = Mathf.Clamp(size, 32, 512);
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, true, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };

        float invSize = 1f / texSize;
        float normalStrength = Mathf.Max(0.1f, strength);
        Color[] pixels = new Color[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        {
            float fy = y * invSize;
            int yp = (y + 1) % texSize;
            int ym = (y - 1 + texSize) % texSize;
            float fyp = yp * invSize;
            float fym = ym * invSize;

            for (int x = 0; x < texSize; x++)
            {
                float fx = x * invSize;
                int xp = (x + 1) % texSize;
                int xm = (x - 1 + texSize) % texSize;
                float fxp = xp * invSize;
                float fxm = xm * invSize;

                float hL = SampleHeightNoise(fxm, fy, frequency);
                float hR = SampleHeightNoise(fxp, fy, frequency);
                float hD = SampleHeightNoise(fx, fym, frequency);
                float hU = SampleHeightNoise(fx, fyp, frequency);

                float dX = (hR - hL) * normalStrength;
                float dY = (hU - hD) * normalStrength;
                Vector3 normal = new Vector3(-dX, -dY, 1f).normalized;

                pixels[y * texSize + x] = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f
                );
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true, false);
        TrackRuntimeTexture(tex);
        return tex;
    }

    private static float SampleHeightNoise(float fx, float fy, float frequency)
    {
        float n0 = Mathf.PerlinNoise(fx * frequency + 0.27f, fy * frequency + 1.91f);
        float n1 = Mathf.PerlinNoise(fx * frequency * 1.85f + 3.73f, fy * frequency * 1.85f + 7.31f);
        float n2 = Mathf.PerlinNoise((fx + fy) * frequency * 0.9f + 9.23f, (fy - fx) * frequency * 0.9f + 2.57f);
        return Mathf.Clamp01(n0 * 0.58f + n1 * 0.29f + n2 * 0.13f);
    }

    private static void ApplyMainTexture(Material material, Texture2D texture, Vector2 tiling, int aniso)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", tiling);
            material.mainTextureScale = tiling;
        }

        if (material.mainTexture != null)
        {
            material.mainTexture.wrapMode = TextureWrapMode.Repeat;
            material.mainTexture.filterMode = FilterMode.Trilinear;
            material.mainTexture.anisoLevel = Mathf.Clamp(aniso, 1, 16);
        }
    }

    private static void ApplyNormalTexture(Material material, Texture2D texture, Vector2 tiling, int aniso, float normalScale)
    {
        if (material == null || texture == null || !material.HasProperty("_BumpMap"))
        {
            return;
        }

        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_BumpMap", texture);
        material.SetTextureScale("_BumpMap", tiling);
        if (material.HasProperty("_BumpScale"))
        {
            material.SetFloat("_BumpScale", Mathf.Clamp(normalScale, 0f, 2f));
        }

        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Trilinear;
        texture.anisoLevel = Mathf.Clamp(aniso, 1, 16);
    }

    private void DisposeRuntimeMaterials()
    {
        if (runtimeMaterials.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }
        runtimeMaterials.Clear();
    }

    private void DisposeRuntimeTextures()
    {
        if (runtimeTextures.Count == 0)
        {
            return;
        }

        for (int i = 0; i < runtimeTextures.Count; i++)
        {
            if (runtimeTextures[i] != null)
            {
                Destroy(runtimeTextures[i]);
            }
        }
        runtimeTextures.Clear();
    }
}

