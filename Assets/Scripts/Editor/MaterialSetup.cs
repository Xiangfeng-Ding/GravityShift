using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Material Setup Module
/// Creates all materials needed for the game with proper colors and properties
/// </summary>
public static class MaterialSetup
{
    private const string MATERIAL_PATH = "Assets/Materials";
    
    public static void CreateAllMaterials()
    {
        // Ensure Materials folder exists
        if (!AssetDatabase.IsValidFolder(MATERIAL_PATH))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        
        // Player material (Blue)
        CreateMaterial("PlayerMaterial", new Color(0.2f, 0.5f, 1.0f, 1.0f));
        
        // Crystal material (Cyan/Glowing)
        CreateMaterial("CrystalMaterial", new Color(0.0f, 1.0f, 1.0f, 1.0f));
        
        // Ground material (Gray)
        CreateMaterial("GroundMaterial", new Color(0.5f, 0.5f, 0.5f, 1.0f));
        
        // Wall material (Dark Gray)
        CreateMaterial("WallMaterial", new Color(0.3f, 0.3f, 0.3f, 1.0f));
        
        // Checkpoint material (Green)
        CreateMaterial("CheckpointMaterial", new Color(0.2f, 1.0f, 0.2f, 1.0f));
        
        // Enemy material (Red)
        CreateMaterial("EnemyMaterial", new Color(1.0f, 0.2f, 0.2f, 1.0f));
        
        // Barrier material (Yellow, semi-transparent)
        CreateMaterial("BarrierMaterial", new Color(1.0f, 1.0f, 0.0f, 0.5f), true);
        
        // Platform material (Brown)
        CreateMaterial("PlatformMaterial", new Color(0.6f, 0.4f, 0.2f, 1.0f));
        
        // Hazard material (Dark Red)
        CreateMaterial("HazardMaterial", new Color(0.8f, 0.1f, 0.1f, 1.0f));
        
        // Exit Portal material (Purple)
        CreateMaterial("ExitPortalMaterial", new Color(0.8f, 0.2f, 1.0f, 1.0f));
        
        Debug.Log("[MaterialSetup] All materials created successfully");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private static void CreateMaterial(string name, Color color, bool transparent = false)
    {
        string path = $"{MATERIAL_PATH}/{name}.mat";
        
        // Check if material already exists
        Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existingMat != null)
        {
            Debug.Log($"[MaterialSetup] Material {name} already exists, updating...");
            existingMat.color = color;
            if (transparent)
            {
                existingMat.SetFloat("_Mode", 3); // Transparent mode
                existingMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                existingMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                existingMat.SetInt("_ZWrite", 0);
                existingMat.DisableKeyword("_ALPHATEST_ON");
                existingMat.EnableKeyword("_ALPHABLEND_ON");
                existingMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                existingMat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(existingMat);
            return;
        }
        
        // Create new material
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        
        if (transparent)
        {
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[MaterialSetup] Created material: {name}");
    }
}
