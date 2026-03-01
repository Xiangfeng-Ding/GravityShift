#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class LevelConfigDatabaseAssetCreator
{
    private const string AssetFolder = "Assets/Resources";
    private const string AssetPath = AssetFolder + "/LevelConfigDatabase.asset";

    [MenuItem("Tools/GravityShift/Create Default Level Config Database")]
    public static void CreateAsset()
    {
        LevelConfigDatabase existing = AssetDatabase.LoadAssetAtPath<LevelConfigDatabase>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("LevelConfigDatabase already exists: " + AssetPath);
            return;
        }

        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        LevelConfigDatabase asset = ScriptableObject.CreateInstance<LevelConfigDatabase>();
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log("Created LevelConfigDatabase at: " + AssetPath);
    }
}
#endif
