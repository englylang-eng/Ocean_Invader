using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class FontFixer
{
    static FontFixer()
    {
        // Subscribe to update to check occasionally, or just on load
        EditorApplication.hierarchyChanged += CheckForMissingFonts;
    }

    [MenuItem("Tools/Fix Missing Fonts")]
    public static void CheckForMissingFonts()
    {
        // Find default font
        TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (defaultFont == null)
        {
            // Try loading by GUID if path fails (LiberationSans SDF is standard)
            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            }
        }

        if (defaultFont == null)
        {
            Debug.LogWarning("[FontFixer] Could not find 'LiberationSans SDF'. Skipping auto-fix.");
            return;
        }

        // Fix Scene Objects
        TextMeshProUGUI[] uiTexts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int fixedCount = 0;
        foreach (var txt in uiTexts)
        {
            if (txt.font == null)
            {
                txt.font = defaultFont;
                EditorUtility.SetDirty(txt);
                fixedCount++;
            }
        }

        TextMeshPro[] worldTexts = Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var txt in worldTexts)
        {
            if (txt.font == null)
            {
                txt.font = defaultFont;
                EditorUtility.SetDirty(txt);
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"[FontFixer] Auto-assigned default font to {fixedCount} text components with missing fonts.");
        }
    }
}
