using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

/// <summary>
/// Scene Setup Module
/// Automatically populates scenes with game objects, lighting, and gameplay elements
/// </summary>
public static class SceneSetup
{
    private const string SCENE_PATH = "Assets/Scenes";
    
    public static void SetupAllScenes()
    {
        SetupMainMenuScene();
        SetupLevel1Scene();
        SetupLevel2Scene();
        SetupLevel3Scene();
        SetupLevel4Scene();
        SetupLevel5Scene();
        
        Debug.Log("[SceneSetup] All scenes configured successfully");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private static void SetupMainMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene($"{SCENE_PATH}/MainMenu.unity");
        
        // Clear existing objects except camera and light
        foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
        {
            if (obj.name != "Main Camera" && obj.name != "Directional Light")
            {
                Object.DestroyImmediate(obj);
            }
        }
        
        // Create GameManager
        GameObject gameManager = new GameObject("GameManager");
        AddScriptComponent(gameManager, "GameManager");
        AddScriptComponent(gameManager, "AudioManager");
        
        // Create UI Canvas (will be populated by UISetup)
        GameObject canvas = new GameObject("MainMenuCanvas");
        Canvas canvasComp = canvas.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetup] MainMenu scene configured");
    }
    
    private static void SetupLevel1Scene()
    {
        Scene scene = EditorSceneManager.OpenScene($"{SCENE_PATH}/Level1_Tutorial.unity");
        
        // Clear existing objects except camera and light
        ClearScene();
        
        // Load prefabs
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Crystal.prefab");
        GameObject checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Checkpoint.prefab");
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExitPortal.prefab");
        
        // Create GameManager
        GameObject gameManager = new GameObject("GameManager");
        AddScriptComponent(gameManager, "GameManager");
        AddScriptComponent(gameManager, "AudioManager");
        
        // Create ground
        GameObject ground = CreatePlatform("Ground", Vector3.zero, new Vector3(20, 1, 20));
        
        // Create walls
        CreatePlatform("Wall_North", new Vector3(0, 2.5f, 10), new Vector3(20, 5, 1));
        CreatePlatform("Wall_South", new Vector3(0, 2.5f, -10), new Vector3(20, 5, 1));
        CreatePlatform("Wall_East", new Vector3(10, 2.5f, 0), new Vector3(1, 5, 20));
        CreatePlatform("Wall_West", new Vector3(-10, 2.5f, 0), new Vector3(1, 5, 20));
        
        // Instantiate player
        if (playerPrefab != null)
        {
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.position = new Vector3(0, 2, -8);
        }
        
        // Create tutorial crystals
        if (crystalPrefab != null)
        {
            InstantiatePrefab(crystalPrefab, new Vector3(-5, 2, 0));
            InstantiatePrefab(crystalPrefab, new Vector3(0, 2, 5));
            InstantiatePrefab(crystalPrefab, new Vector3(5, 2, 0));
        }
        
        // Create checkpoint
        if (checkpointPrefab != null)
        {
            InstantiatePrefab(checkpointPrefab, new Vector3(0, 1, 0));
        }
        
        // Create platforms for gravity tutorial
        CreatePlatform("Platform1", new Vector3(-7, 3, 3), new Vector3(3, 0.5f, 3));
        CreatePlatform("Platform2", new Vector3(7, 3, 3), new Vector3(3, 0.5f, 3));
        CreatePlatform("Platform3", new Vector3(0, 6, 5), new Vector3(3, 0.5f, 3));
        
        // Create exit portal
        if (exitPrefab != null)
        {
            InstantiatePrefab(exitPrefab, new Vector3(0, 1, 8));
        }
        
        // Create HUD Canvas
        CreateHUDCanvas();
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetup] Level1_Tutorial scene configured");
    }
    
    private static void SetupLevel2Scene()
    {
        Scene scene = EditorSceneManager.OpenScene($"{SCENE_PATH}/Level2_Platforms.unity");
        ClearScene();
        
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Crystal.prefab");
        GameObject platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MovingPlatform.prefab");
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExitPortal.prefab");
        
        GameObject gameManager = new GameObject("GameManager");
        AddScriptComponent(gameManager, "GameManager");
        AddScriptComponent(gameManager, "AudioManager");
        
        CreatePlatform("Ground", Vector3.zero, new Vector3(30, 1, 30));
        CreatePlatform("Wall_North", new Vector3(0, 2.5f, 15), new Vector3(30, 5, 1));
        CreatePlatform("Wall_South", new Vector3(0, 2.5f, -15), new Vector3(30, 5, 1));
        CreatePlatform("Wall_East", new Vector3(15, 2.5f, 0), new Vector3(1, 5, 30));
        CreatePlatform("Wall_West", new Vector3(-15, 2.5f, 0), new Vector3(1, 5, 30));
        
        if (playerPrefab != null)
        {
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.position = new Vector3(0, 2, -12);
        }
        
        // Create moving platforms
        if (platformPrefab != null)
        {
            InstantiatePrefab(platformPrefab, new Vector3(-8, 3, 0));
            InstantiatePrefab(platformPrefab, new Vector3(0, 5, 5));
            InstantiatePrefab(platformPrefab, new Vector3(8, 3, 0));
        }
        
        // Create crystals on platforms
        if (crystalPrefab != null)
        {
            for (int i = 0; i < 5; i++)
            {
                InstantiatePrefab(crystalPrefab, new Vector3(Random.Range(-10f, 10f), 2 + i * 2, Random.Range(-10f, 10f)));
            }
        }
        
        if (exitPrefab != null)
        {
            InstantiatePrefab(exitPrefab, new Vector3(0, 1, 12));
        }
        
        CreateHUDCanvas();
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetup] Level2_Platforms scene configured");
    }
    
    private static void SetupLevel3Scene()
    {
        Scene scene = EditorSceneManager.OpenScene($"{SCENE_PATH}/Level3_Hazards.unity");
        ClearScene();
        
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Crystal.prefab");
        GameObject hazardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HazardZone.prefab");
        GameObject checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Checkpoint.prefab");
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExitPortal.prefab");
        
        GameObject gameManager = new GameObject("GameManager");
        AddScriptComponent(gameManager, "GameManager");
        AddScriptComponent(gameManager, "AudioManager");
        
        CreatePlatform("Ground", Vector3.zero, new Vector3(40, 1, 40));
        
        if (playerPrefab != null)
        {
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.position = new Vector3(-15, 2, -15);
        }
        
        // Create hazard zones
        if (hazardPrefab != null)
        {
            InstantiatePrefab(hazardPrefab, new Vector3(-5, 0.5f, -5));
            InstantiatePrefab(hazardPrefab, new Vector3(5, 0.5f, 5));
            InstantiatePrefab(hazardPrefab, new Vector3(0, 0.5f, 10));
        }
        
        // Create safe platforms
        CreatePlatform("SafePlatform1", new Vector3(-10, 1.5f, 0), new Vector3(5, 0.5f, 5));
        CreatePlatform("SafePlatform2", new Vector3(0, 1.5f, -5), new Vector3(5, 0.5f, 5));
        CreatePlatform("SafePlatform3", new Vector3(10, 1.5f, 0), new Vector3(5, 0.5f, 5));
        
        if (crystalPrefab != null)
        {
            InstantiatePrefab(crystalPrefab, new Vector3(-10, 3, 0));
            InstantiatePrefab(crystalPrefab, new Vector3(0, 3, -5));
            InstantiatePrefab(crystalPrefab, new Vector3(10, 3, 0));
            InstantiatePrefab(crystalPrefab, new Vector3(0, 3, 15));
        }
        
        if (checkpointPrefab != null)
        {
            InstantiatePrefab(checkpointPrefab, new Vector3(0, 1, 0));
        }
        
        if (exitPrefab != null)
        {
            InstantiatePrefab(exitPrefab, new Vector3(15, 1, 15));
        }
        
        CreateHUDCanvas();
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetup] Level3_Hazards scene configured");
    }
    
    private static void SetupLevel4Scene()
    {
        Scene scene = EditorSceneManager.OpenScene($"{SCENE_PATH}/Level4_Mechanisms.unity");
        ClearScene();
        
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Crystal.prefab");
        GameObject barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnergyBarrier.prefab");
        GameObject platePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PressurePlate.prefab");
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExitPortal.prefab");
        
        GameObject gameManager = new GameObject("GameManager");
        AddScriptComponent(gameManager, "GameManager");
        AddScriptComponent(gameManager, "AudioManager");
        
        CreatePlatform("Ground", Vector3.zero, new Vector3(35, 1, 35));
        
        if (playerPrefab != null)
        {
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.position = new Vector3(-12, 2, -12);
        }
        
        // Create barriers
        if (barrierPrefab != null)
        {
            InstantiatePrefab(barrierPrefab, new Vector3(0, 2, 0));
            InstantiatePrefab(barrierPrefab, new Vector3(5, 2, 5));
        }
        
        // Create pressure plates
        if (platePrefab != null)
        {
            InstantiatePrefab(platePrefab, new Vector3(-5, 1, -5));
            InstantiatePrefab(platePrefab, new Vector3(8, 1, -5));
        }
        
        if (crystalPrefab != null)
        {
            for (int i = 0; i < 6; i++)
            {
                InstantiatePrefab(crystalPrefab, new Vector3(Random.Range(-12f, 12f), 2, Random.Range(-12f, 12f)));
            }
        }
        
        if (exitPrefab != null)
        {
            InstantiatePrefab(exitPrefab, new Vector3(12, 1, 12));
        }
        
        CreateHUDCanvas();
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetup] Level4_Mechanisms scene configured");
    }
    
    private static void SetupLevel5Scene()
    {
        Scene scene = EditorSceneManager.OpenScene($"{SCENE_PATH}/Level5_Final.unity");
        ClearScene();
        
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Crystal.prefab");
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab");
        GameObject hazardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HazardZone.prefab");
        GameObject platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MovingPlatform.prefab");
        GameObject checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Checkpoint.prefab");
        GameObject exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExitPortal.prefab");
        
        GameObject gameManager = new GameObject("GameManager");
        AddScriptComponent(gameManager, "GameManager");
        AddScriptComponent(gameManager, "AudioManager");
        
        CreatePlatform("Ground", Vector3.zero, new Vector3(50, 1, 50));
        
        if (playerPrefab != null)
        {
            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            player.transform.position = new Vector3(-20, 2, -20);
        }
        
        // Create enemies
        if (enemyPrefab != null)
        {
            InstantiatePrefab(enemyPrefab, new Vector3(-10, 2, 0));
            InstantiatePrefab(enemyPrefab, new Vector3(10, 2, 0));
            InstantiatePrefab(enemyPrefab, new Vector3(0, 2, 10));
        }
        
        // Create hazards
        if (hazardPrefab != null)
        {
            InstantiatePrefab(hazardPrefab, new Vector3(-8, 0.5f, -8));
            InstantiatePrefab(hazardPrefab, new Vector3(8, 0.5f, 8));
        }
        
        // Create moving platforms
        if (platformPrefab != null)
        {
            InstantiatePrefab(platformPrefab, new Vector3(-12, 4, 5));
            InstantiatePrefab(platformPrefab, new Vector3(12, 4, -5));
        }
        
        // Create checkpoints
        if (checkpointPrefab != null)
        {
            InstantiatePrefab(checkpointPrefab, new Vector3(-10, 1, -10));
            InstantiatePrefab(checkpointPrefab, new Vector3(10, 1, 10));
        }
        
        // Create crystals
        if (crystalPrefab != null)
        {
            for (int i = 0; i < 10; i++)
            {
                InstantiatePrefab(crystalPrefab, new Vector3(Random.Range(-18f, 18f), 2 + Random.Range(0, 5), Random.Range(-18f, 18f)));
            }
        }
        
        if (exitPrefab != null)
        {
            InstantiatePrefab(exitPrefab, new Vector3(20, 1, 20));
        }
        
        CreateHUDCanvas();
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SceneSetup] Level5_Final scene configured");
    }
    
    private static GameObject CreatePlatform(string name, Vector3 position, Vector3 scale)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = name;
        platform.transform.position = position;
        platform.transform.localScale = scale;
        platform.layer = LayerMask.NameToLayer("Ground");
        
        Material groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GroundMaterial.mat");
        if (groundMat != null)
        {
            platform.GetComponent<Renderer>().material = groundMat;
        }
        
        return platform;
    }
    
    private static void InstantiatePrefab(GameObject prefab, Vector3 position)
    {
        if (prefab != null)
        {
            GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            obj.transform.position = position;
        }
    }
    
    private static void CreateHUDCanvas()
    {
        GameObject canvas = new GameObject("HUDCanvas");
        Canvas canvasComp = canvas.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }
    
    private static void ClearScene()
    {
        foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
        {
            if (obj.name != "Main Camera" && obj.name != "Directional Light")
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
    
    private static void AddScriptComponent(GameObject obj, string scriptName)
    {
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
                }
            }
        }
    }
}
