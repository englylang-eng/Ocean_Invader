using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class MobileInputSetup : EditorWindow
{
    [MenuItem("Tools/Setup Mobile Input")]
    public static void Setup()
    {
        // 0. Cleanup existing MobileInputCanvas to avoid duplicates
        GameObject existing = GameObject.Find("MobileInputCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        // 1. Create Prefab Structure
        GameObject root = new GameObject("MobileInputCanvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above everything else
        
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        root.AddComponent<GraphicRaycaster>();

        // 2. Create Joystick Background
        GameObject bg = new GameObject("JoystickBackground");
        bg.transform.SetParent(root.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.sprite = GetCircleSprite(); // Use same flat circle as Boost Button
        bgImage.color = new Color(1f, 1f, 1f, 0.15f); // Soft white transparency
        
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f); // Bottom Left
        bgRect.anchorMax = new Vector2(0f, 0f);
        bgRect.pivot = new Vector2(0f, 0f);
        // IMPROVED SIZING:
        // Reduced size per user request for easier handling
        bgRect.anchoredPosition = new Vector2(200, 250); 
        bgRect.sizeDelta = new Vector2(280, 280); 

        // 3. Create Joystick Handle
        GameObject handle = new GameObject("JoystickHandle");
        handle.transform.SetParent(bg.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.sprite = GetCircleSprite(); // Use custom circle sprite
        handleImage.color = Color.white; // Pure opaque white
        handleImage.raycastTarget = false; // Ensure input goes to background
        
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(120, 120); // Smaller handle

        // 4. Add MobileJoystick Component
        MobileJoystick joystick = bg.AddComponent<MobileJoystick>();
        joystick.background = bgRect;
        joystick.handle = handleRect;
        // Reduce range to 0.5f for quicker response
        joystick.handleRange = 0.5f; 
        joystick.hideOnDesktop = true;

        // 5. Create Boost Button (Background)
        GameObject boostBtn = new GameObject("BoostButton");
        boostBtn.transform.SetParent(root.transform, false);
        Image boostBg = boostBtn.AddComponent<Image>();
        boostBg.sprite = GetCircleSprite();
        boostBg.color = new Color(1f, 1f, 1f, 0.15f); // Same soft white transparency as Joystick
        
        RectTransform boostRect = boostBtn.GetComponent<RectTransform>();
        boostRect.anchorMin = new Vector2(1f, 0f); // Bottom Right
        boostRect.anchorMax = new Vector2(1f, 0f);
        boostRect.pivot = new Vector2(1f, 0f);
        // Large target for action button
        boostRect.anchoredPosition = new Vector2(-200, 150); 
        boostRect.sizeDelta = new Vector2(280, 280); // Resized to match Joystick
        
        // Add Script
        boostBtn.AddComponent<MobileBoostButton>();
        
        // Boost Icon (Directly on Background, No Face)
        GameObject boostIcon = new GameObject("BoostIcon");
        boostIcon.transform.SetParent(boostBtn.transform, false);
        Image boostIconImg = boostIcon.AddComponent<Image>();
        boostIconImg.raycastTarget = false; // Don't block input
        boostIconImg.preserveAspect = true; // Ensure icon doesn't stretch and stays centered

        
        // Load Icon
        string iconPath = "Assets/Graphics/speed_boost_icon.png";
        Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (iconSprite != null)
        {
            boostIconImg.sprite = iconSprite;
            boostIconImg.color = Color.white; // White Icon
        }
        else
        {
            Debug.LogWarning($"Speed boost icon not found at {iconPath}");
        }
        
        RectTransform iconRect = boostIcon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-10f, 0f); // Slight left offset for visual centering
        iconRect.sizeDelta = new Vector2(120, 120); // Larger size since no face container

        // 6. Save as Prefab in Resources for easier loading
        string resourcesPath = "Assets/Resources";
        if (!System.IO.Directory.Exists(resourcesPath))
        {
            System.IO.Directory.CreateDirectory(resourcesPath);
        }
        string prefabPath = resourcesPath + "/MobileInputCanvas.prefab";
        
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root); // Remove from scene after saving

        Debug.Log("MobileInputCanvas Prefab created at: " + prefabPath);

        // 6. Attach Loader to Scene
        GuiManager guiManager = GameObject.FindFirstObjectByType<GuiManager>();
        if (guiManager != null)
        {
            MobileInputLoader loader = guiManager.gameObject.GetComponent<MobileInputLoader>();
            if (loader == null)
            {
                loader = guiManager.gameObject.AddComponent<MobileInputLoader>();
            }
            
            loader.mobileInputPrefab = prefab;
            loader.simulateMobileInEditor = false; // Disable by default (Auto-detect only)
            Debug.Log("MobileInputLoader attached to GuiManager and configured.");
            EditorUtility.SetDirty(guiManager.gameObject);
        }
        else
        {
            Debug.LogError("GuiManager not found in scene! Could not attach MobileInputLoader.");
        }

        // 7. Cleanup old objects
        MobileJoystick[] existingJoysticks = GameObject.FindObjectsByType<MobileJoystick>(FindObjectsSortMode.None);
        foreach (var j in existingJoysticks)
        {
            if (PrefabUtility.GetPrefabAssetType(j.gameObject) == PrefabAssetType.NotAPrefab)
            {
                DestroyImmediate(j.transform.root.gameObject);
                Debug.Log("Cleaned up existing MobileJoystick in scene.");
            }
        }
    }

    private static Sprite GetCircleSprite()
    {
        string dir = "Assets/Resources";
        string path = dir + "/JoystickCircle.png";
        
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

        if (!System.IO.File.Exists(path))
        {
            int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = (size / 2f) - 4;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius - dist + 1.0f); // Simple AA
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.Refresh();
            
            // Set Import Settings
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
        
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
