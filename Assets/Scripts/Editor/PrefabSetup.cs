using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Prefab Setup Module
/// Automatically creates all game prefabs with proper components and references
/// </summary>
public static class PrefabSetup
{
    private const string PREFAB_PATH = "Assets/Prefabs";
    
    public static void CreateAllPrefabs()
    {
        // Ensure Prefabs folder exists
        if (!AssetDatabase.IsValidFolder(PREFAB_PATH))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        
        CreatePlayerPrefab();
        CreateCrystalPrefab();
        CreateCheckpointPrefab();
        CreateEnemyPrefab();
        CreateBarrierPrefab();
        CreatePlatformPrefab();
        CreatePressurePlatePrefab();
        CreateHazardZonePrefab();
        CreateExitPortalPrefab();
        
        Debug.Log("[PrefabSetup] All prefabs created successfully");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private static void CreatePlayerPrefab()
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Default");
        
        // Add visual representation
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "PlayerBody";
        body.transform.SetParent(player.transform);
        body.transform.localPosition = Vector3.zero;
        
        // Apply material
        Material playerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlayerMaterial.mat");
        if (playerMat != null)
        {
            body.GetComponent<Renderer>().material = playerMat;
        }
        
        // Remove default collider (CharacterController will handle collision)
        Object.DestroyImmediate(body.GetComponent<Collider>());
        
        // Add camera
        GameObject cameraObj = new GameObject("PlayerCamera");
        cameraObj.transform.SetParent(player.transform);
        cameraObj.transform.localPosition = new Vector3(0, 0.6f, 0);
        cameraObj.tag = "MainCamera";
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.fieldOfView = 60;
        cameraObj.AddComponent<AudioListener>();
        
        // Add components
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = Vector3.zero;
        
        // Add scripts (they will be assigned via GUID)
        AddScriptComponent(player, "PlayerController");
        AddScriptComponent(player, "GravityController");
        AddScriptComponent(player, "PlayerEnergy");
        
        SavePrefab(player, "Player");
    }
    
    private static void CreateCrystalPrefab()
    {
        GameObject crystal = new GameObject("Crystal");
        crystal.tag = "Crystal";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "CrystalMesh";
        visual.transform.SetParent(crystal.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.5f;
        visual.transform.rotation = Quaternion.Euler(45, 45, 0);
        
        Material crystalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CrystalMaterial.mat");
        if (crystalMat != null)
        {
            visual.GetComponent<Renderer>().material = crystalMat;
        }
        
        BoxCollider collider = visual.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        
        AddScriptComponent(crystal, "CrystalPickup");
        
        SavePrefab(crystal, "Crystal");
    }
    
    private static void CreateCheckpointPrefab()
    {
        GameObject checkpoint = new GameObject("Checkpoint");
        checkpoint.tag = "Checkpoint";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "CheckpointMesh";
        visual.transform.SetParent(checkpoint.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(2f, 0.2f, 2f);
        
        Material checkpointMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CheckpointMaterial.mat");
        if (checkpointMat != null)
        {
            visual.GetComponent<Renderer>().material = checkpointMat;
        }
        
        MeshCollider collider = visual.GetComponent<MeshCollider>();
        collider.convex = true;
        collider.isTrigger = true;
        
        AddScriptComponent(checkpoint, "Checkpoint");
        
        SavePrefab(checkpoint, "Checkpoint");
    }
    
    private static void CreateEnemyPrefab()
    {
        GameObject enemy = new GameObject("Enemy");
        enemy.tag = "Enemy";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "EnemyBody";
        visual.transform.SetParent(enemy.transform);
        visual.transform.localPosition = Vector3.zero;
        
        Material enemyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/EnemyMaterial.mat");
        if (enemyMat != null)
        {
            visual.GetComponent<Renderer>().material = enemyMat;
        }
        
        SphereCollider collider = visual.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        
        AddScriptComponent(enemy, "EnemyAI");
        
        SavePrefab(enemy, "Enemy");
    }
    
    private static void CreateBarrierPrefab()
    {
        GameObject barrier = new GameObject("EnergyBarrier");
        barrier.tag = "Barrier";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "BarrierMesh";
        visual.transform.SetParent(barrier.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(1f, 3f, 0.2f);
        
        Material barrierMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BarrierMaterial.mat");
        if (barrierMat != null)
        {
            visual.GetComponent<Renderer>().material = barrierMat;
        }
        
        BoxCollider collider = visual.GetComponent<BoxCollider>();
        collider.isTrigger = false;
        
        AddScriptComponent(barrier, "EnergyBarrier");
        
        SavePrefab(barrier, "EnergyBarrier");
    }
    
    private static void CreatePlatformPrefab()
    {
        GameObject platform = new GameObject("MovingPlatform");
        platform.tag = "Platform";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "PlatformMesh";
        visual.transform.SetParent(platform.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(3f, 0.5f, 3f);
        
        Material platformMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlatformMaterial.mat");
        if (platformMat != null)
        {
            visual.GetComponent<Renderer>().material = platformMat;
        }
        
        AddScriptComponent(platform, "MovingPlatform");
        
        SavePrefab(platform, "MovingPlatform");
    }
    
    private static void CreatePressurePlatePrefab()
    {
        GameObject plate = new GameObject("PressurePlate");
        plate.tag = "Mechanism";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "PlateMesh";
        visual.transform.SetParent(plate.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(1.5f, 0.1f, 1.5f);
        
        Material platformMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PlatformMaterial.mat");
        if (platformMat != null)
        {
            visual.GetComponent<Renderer>().material = platformMat;
        }
        
        MeshCollider collider = visual.GetComponent<MeshCollider>();
        collider.convex = true;
        collider.isTrigger = true;
        
        AddScriptComponent(plate, "PressurePlate");
        
        SavePrefab(plate, "PressurePlate");
    }
    
    private static void CreateHazardZonePrefab()
    {
        GameObject hazard = new GameObject("HazardZone");
        hazard.tag = "Hazard";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "HazardMesh";
        visual.transform.SetParent(hazard.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(5f, 0.5f, 5f);
        
        Material hazardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/HazardMaterial.mat");
        if (hazardMat != null)
        {
            visual.GetComponent<Renderer>().material = hazardMat;
        }
        
        BoxCollider collider = visual.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        
        AddScriptComponent(hazard, "HazardZone");
        
        SavePrefab(hazard, "HazardZone");
    }
    
    private static void CreateExitPortalPrefab()
    {
        GameObject portal = new GameObject("ExitPortal");
        portal.tag = "Exit";
        
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "PortalMesh";
        visual.transform.SetParent(portal.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(2f, 0.5f, 2f);
        
        Material portalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ExitPortalMaterial.mat");
        if (portalMat != null)
        {
            visual.GetComponent<Renderer>().material = portalMat;
        }
        
        MeshCollider collider = visual.GetComponent<MeshCollider>();
        collider.convex = true;
        collider.isTrigger = true;
        
        AddScriptComponent(portal, "ExitPortal");
        
        SavePrefab(portal, "ExitPortal");
    }
    
    private static void AddScriptComponent(GameObject obj, string scriptName)
    {
        // Find script by name
        string[] guids = AssetDatabase.FindAssets($"t:Script {scriptName}");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null)
            {
                System.Type scriptType = script.GetClass();
                if (scriptType != null)
                {
                    obj.AddComponent(scriptType);
                    Debug.Log($"[PrefabSetup] Added {scriptName} to {obj.name}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[PrefabSetup] Script {scriptName} not found");
        }
    }
    
    private static void SavePrefab(GameObject obj, string name)
    {
        string path = $"{PREFAB_PATH}/{name}.prefab";
        
        // Delete existing prefab if it exists
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
        
        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Object.DestroyImmediate(obj);
        Debug.Log($"[PrefabSetup] Created prefab: {name}");
    }
}
