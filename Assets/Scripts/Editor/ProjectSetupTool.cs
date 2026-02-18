using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Automated Project Setup Tool
/// This tool automates the creation of prefabs, scenes, and UI elements to improve development efficiency
/// and ensure correct asset references throughout the project.
/// </summary>
public class ProjectSetupTool : EditorWindow
{
    private static string logFilePath = "Assets/Editor/SetupLog.txt";
    private Vector2 scrollPosition;
    private string setupLog = "";
    
    [MenuItem("Tools/Gravity Shift/Complete Project Setup")]
    public static void ShowWindow()
    {
        ProjectSetupTool window = GetWindow<ProjectSetupTool>("Project Setup Tool");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Gravity Shift - Automated Project Setup Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "This tool will automatically create all prefabs, materials, scenes, and UI elements for the Gravity Shift project.\n\n" +
            "Benefits:\n" +
            "• Ensures consistent asset references\n" +
            "• Eliminates manual configuration errors\n" +
            "• Speeds up development workflow\n" +
            "• Maintains project organization standards",
            MessageType.Info
        );
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Run Complete Setup", GUILayout.Height(40)))
        {
            RunCompleteSetup();
        }
        
        GUILayout.Space(10);
        
        GUILayout.Label("Individual Setup Steps:", EditorStyles.boldLabel);
        
        if (GUILayout.Button("1. Create Materials"))
        {
            MaterialSetup.CreateAllMaterials();
            LogSetup("Materials created successfully");
        }
        
        if (GUILayout.Button("2. Create Prefabs"))
        {
            PrefabSetup.CreateAllPrefabs();
            LogSetup("Prefabs created successfully");
        }
        
        if (GUILayout.Button("3. Setup Scenes"))
        {
            SceneSetup.SetupAllScenes();
            LogSetup("Scenes setup successfully");
        }
        
        if (GUILayout.Button("4. Create UI Elements"))
        {
            UISetup.CreateAllUI();
            LogSetup("UI elements created successfully");
        }
        
        GUILayout.Space(10);
        GUILayout.Label("Setup Log:", EditorStyles.boldLabel);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        EditorGUILayout.TextArea(setupLog, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    
    private void RunCompleteSetup()
    {
        setupLog = "";
        LogSetup("=== Starting Complete Project Setup ===");
        
        try
        {
            LogSetup("Step 1/4: Creating materials...");
            MaterialSetup.CreateAllMaterials();
            LogSetup("✓ Materials created");
            
            LogSetup("Step 2/4: Creating prefabs...");
            PrefabSetup.CreateAllPrefabs();
            LogSetup("✓ Prefabs created");
            
            LogSetup("Step 3/4: Setting up scenes...");
            SceneSetup.SetupAllScenes();
            LogSetup("✓ Scenes configured");
            
            LogSetup("Step 4/4: Creating UI elements...");
            UISetup.CreateAllUI();
            LogSetup("✓ UI elements created");
            
            LogSetup("=== Setup Complete! ===");
            LogSetup("Project is ready to run. Press Play to test.");
            
            EditorUtility.DisplayDialog("Setup Complete", 
                "All project assets have been created successfully!\n\nYou can now run the game.", 
                "OK");
        }
        catch (System.Exception e)
        {
            LogSetup("ERROR: " + e.Message);
            EditorUtility.DisplayDialog("Setup Error", 
                "An error occurred during setup:\n" + e.Message, 
                "OK");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private void LogSetup(string message)
    {
        setupLog += message + "\n";
        Debug.Log("[ProjectSetup] " + message);
        Repaint();
    }
}
