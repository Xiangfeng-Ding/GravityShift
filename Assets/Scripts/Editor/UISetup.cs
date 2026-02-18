using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// UI Setup Module
/// Automatically creates all UI elements including menus, HUD, and multilingual support
/// </summary>
public static class UISetup
{
    public static void CreateAllUI()
    {
        CreateMainMenuUI();
        CreateGameHUD();
        CreatePauseMenuUI();
        
        Debug.Log("[UISetup] All UI elements created successfully");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private static void CreateMainMenuUI()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        
        GameObject canvas = GameObject.Find("MainMenuCanvas");
        if (canvas == null)
        {
            Debug.LogWarning("[UISetup] MainMenuCanvas not found");
            return;
        }
        
        // Clear existing UI
        foreach (Transform child in canvas.transform)
        {
            Object.DestroyImmediate(child.gameObject);
        }
        
        // Create title
        GameObject title = CreateText("Title", canvas.transform, new Vector2(0, 200), new Vector2(800, 100));
        Text titleText = title.GetComponent<Text>();
        titleText.text = "GRAVITY SHIFT BATTLE";
        titleText.fontSize = 48;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        
        // Create subtitle
        GameObject subtitle = CreateText("Subtitle", canvas.transform, new Vector2(0, 130), new Vector2(600, 50));
        Text subtitleText = subtitle.GetComponent<Text>();
        subtitleText.text = "3D Gravity Manipulation Platform Puzzle Game";
        subtitleText.fontSize = 20;
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.color = new Color(0.8f, 0.8f, 0.8f);
        
        // Create buttons
        CreateMenuButton("PlayButton", canvas.transform, new Vector2(0, 0), "PLAY", "LoadLevel1");
        CreateMenuButton("OptionsButton", canvas.transform, new Vector2(0, -80), "OPTIONS", "ShowOptions");
        CreateMenuButton("QuitButton", canvas.transform, new Vector2(0, -160), "QUIT", "QuitGame");
        
        // Create language selector in top right
        GameObject langPanel = new GameObject("LanguagePanel");
        langPanel.transform.SetParent(canvas.transform, false);
        RectTransform langRect = langPanel.AddComponent<RectTransform>();
        langRect.anchorMin = new Vector2(1, 1);
        langRect.anchorMax = new Vector2(1, 1);
        langRect.pivot = new Vector2(1, 1);
        langRect.anchoredPosition = new Vector2(-20, -20);
        langRect.sizeDelta = new Vector2(200, 150);
        
        CreateLanguageButton("EnglishButton", langPanel.transform, new Vector2(0, 0), "English");
        CreateLanguageButton("ChineseButton", langPanel.transform, new Vector2(0, -40), "中文");
        CreateLanguageButton("JapaneseButton", langPanel.transform, new Vector2(0, -80), "日本語");
        CreateLanguageButton("KoreanButton", langPanel.transform, new Vector2(0, -120), "한국어");
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UISetup] Main menu UI created");
    }
    
    private static void CreateGameHUD()
    {
        // This will be applied to all game level scenes
        string[] levelScenes = new string[]
        {
            "Assets/Scenes/Level1_Tutorial.unity",
            "Assets/Scenes/Level2_Platforms.unity",
            "Assets/Scenes/Level3_Hazards.unity",
            "Assets/Scenes/Level4_Mechanisms.unity",
            "Assets/Scenes/Level5_Final.unity"
        };
        
        foreach (string scenePath in levelScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            GameObject canvas = GameObject.Find("HUDCanvas");
            
            if (canvas == null) continue;
            
            // Clear existing UI
            foreach (Transform child in canvas.transform)
            {
                Object.DestroyImmediate(child.gameObject);
            }
            
            // Create energy bar background
            GameObject energyBG = CreatePanel("EnergyBarBackground", canvas.transform, 
                new Vector2(20, -20), new Vector2(200, 30), new Color(0.2f, 0.2f, 0.2f, 0.8f));
            energyBG.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            energyBG.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            energyBG.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
            
            // Create energy bar fill
            GameObject energyFill = CreatePanel("EnergyBarFill", energyBG.transform, 
                Vector2.zero, new Vector2(200, 30), new Color(0, 1, 1, 1));
            energyFill.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            energyFill.GetComponent<RectTransform>().anchorMax = Vector2.zero;
            energyFill.GetComponent<RectTransform>().pivot = Vector2.zero;
            
            // Create energy text
            GameObject energyText = CreateText("EnergyText", energyBG.transform, Vector2.zero, new Vector2(200, 30));
            energyText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            energyText.GetComponent<RectTransform>().anchorMax = Vector2.one;
            Text energyTextComp = energyText.GetComponent<Text>();
            energyTextComp.text = "Energy: 100";
            energyTextComp.alignment = TextAnchor.MiddleCenter;
            energyTextComp.fontSize = 18;
            energyTextComp.fontStyle = FontStyle.Bold;
            
            // Create crystal counter
            GameObject crystalPanel = CreatePanel("CrystalPanel", canvas.transform, 
                new Vector2(20, -60), new Vector2(150, 30), new Color(0.2f, 0.2f, 0.2f, 0.8f));
            crystalPanel.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            crystalPanel.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            crystalPanel.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
            
            GameObject crystalText = CreateText("CrystalText", crystalPanel.transform, Vector2.zero, new Vector2(150, 30));
            crystalText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            crystalText.GetComponent<RectTransform>().anchorMax = Vector2.one;
            Text crystalTextComp = crystalText.GetComponent<Text>();
            crystalTextComp.text = "Crystals: 0/5";
            crystalTextComp.alignment = TextAnchor.MiddleCenter;
            crystalTextComp.fontSize = 18;
            
            // Create score display
            GameObject scorePanel = CreatePanel("ScorePanel", canvas.transform, 
                new Vector2(20, -100), new Vector2(150, 30), new Color(0.2f, 0.2f, 0.2f, 0.8f));
            scorePanel.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            scorePanel.GetComponent<RectTransform>().anchorMax = new Vector2(0, 1);
            scorePanel.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
            
            GameObject scoreText = CreateText("ScoreText", scorePanel.transform, Vector2.zero, new Vector2(150, 30));
            scoreText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            scoreText.GetComponent<RectTransform>().anchorMax = Vector2.one;
            Text scoreTextComp = scoreText.GetComponent<Text>();
            scoreTextComp.text = "Score: 0";
            scoreTextComp.alignment = TextAnchor.MiddleCenter;
            scoreTextComp.fontSize = 18;
            
            // Create gravity indicator
            GameObject gravityText = CreateText("GravityIndicator", canvas.transform, new Vector2(0, -20), new Vector2(300, 50));
            gravityText.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1);
            gravityText.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
            gravityText.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
            Text gravityTextComp = gravityText.GetComponent<Text>();
            gravityTextComp.text = "Gravity: Down";
            gravityTextComp.alignment = TextAnchor.MiddleCenter;
            gravityTextComp.fontSize = 24;
            gravityTextComp.fontStyle = FontStyle.Bold;
            gravityTextComp.color = Color.yellow;
            
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        
        Debug.Log("[UISetup] Game HUD created for all levels");
    }
    
    private static void CreatePauseMenuUI()
    {
        // Pause menu will be created as a prefab that can be instantiated in any scene
        GameObject pauseMenu = new GameObject("PauseMenu");
        
        Canvas canvas = pauseMenu.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseMenu.AddComponent<UnityEngine.UI.CanvasScaler>();
        pauseMenu.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Create background overlay
        GameObject overlay = CreatePanel("Overlay", pauseMenu.transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.7f));
        overlay.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        overlay.GetComponent<RectTransform>().anchorMax = Vector2.one;
        
        // Create menu panel
        GameObject menuPanel = CreatePanel("MenuPanel", pauseMenu.transform, Vector2.zero, new Vector2(400, 500), new Color(0.1f, 0.1f, 0.1f, 0.95f));
        
        // Create title
        GameObject title = CreateText("PauseTitle", menuPanel.transform, new Vector2(0, 180), new Vector2(350, 60));
        Text titleText = title.GetComponent<Text>();
        titleText.text = "PAUSED";
        titleText.fontSize = 40;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        
        // Create buttons
        CreateMenuButton("ResumeButton", menuPanel.transform, new Vector2(0, 60), "RESUME", "ResumeGame");
        CreateMenuButton("RestartButton", menuPanel.transform, new Vector2(0, 0), "RESTART", "RestartLevel");
        CreateMenuButton("MainMenuButton", menuPanel.transform, new Vector2(0, -60), "MAIN MENU", "ReturnToMenu");
        CreateMenuButton("QuitButton", menuPanel.transform, new Vector2(0, -120), "QUIT", "QuitGame");
        
        pauseMenu.SetActive(false);
        
        // Save as prefab
        string prefabPath = "Assets/Prefabs/PauseMenu.prefab";
        PrefabUtility.SaveAsPrefabAsset(pauseMenu, prefabPath);
        Object.DestroyImmediate(pauseMenu);
        
        Debug.Log("[UISetup] Pause menu prefab created");
    }
    
    private static GameObject CreateText(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.color = Color.white;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        
        return textObj;
    }
    
    private static GameObject CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        
        Image image = panel.AddComponent<Image>();
        image.color = color;
        
        return panel;
    }
    
    private static void CreateMenuButton(string name, Transform parent, Vector2 position, string label, string functionName)
    {
        GameObject button = new GameObject(name);
        button.transform.SetParent(parent, false);
        
        RectTransform rect = button.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(300, 60);
        
        Image image = button.AddComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 1.0f, 1.0f);
        
        Button buttonComp = button.AddComponent<Button>();
        ColorBlock colors = buttonComp.colors;
        colors.normalColor = new Color(0.2f, 0.5f, 1.0f, 1.0f);
        colors.highlightedColor = new Color(0.3f, 0.6f, 1.0f, 1.0f);
        colors.pressedColor = new Color(0.1f, 0.4f, 0.9f, 1.0f);
        buttonComp.colors = colors;
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }
    
    private static void CreateLanguageButton(string name, Transform parent, Vector2 position, string label)
    {
        GameObject button = new GameObject(name);
        button.transform.SetParent(parent, false);
        
        RectTransform rect = button.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(180, 35);
        
        Image image = button.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        
        Button buttonComp = button.AddComponent<Button>();
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        
        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }
}
