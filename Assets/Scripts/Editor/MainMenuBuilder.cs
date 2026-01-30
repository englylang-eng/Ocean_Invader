using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenuBuilder : MonoBehaviour
{
    [MenuItem("Ocean Invader/Build Main Menu UI")]
    public static void BuildMainMenu()
    {
        // 1. Create Canvas
        GameObject canvasObj = GameObject.Find("MainMenuCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("MainMenuCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Create EventSystem
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Fix: Ensure Main Camera & AudioListener exist
        GameObject camObj = GameObject.FindWithTag("MainCamera");
        if (camObj == null)
        {
            camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camObj.AddComponent<AudioListener>();
            Debug.Log("Created Main Camera with AudioListener.");
        }
        else
        {
            // Ensure existing camera has listener
            if (camObj.GetComponent<AudioListener>() == null)
            {
                camObj.AddComponent<AudioListener>();
                Debug.Log("Added AudioListener to existing Main Camera.");
            }
        }

        // 3. Background
        GameObject bgObj = GetOrCreateChild(canvasObj.transform, "Background");
        Image bgImage = GetOrAddComponent<Image>(bgObj);
        
        // Try to load custom background
        string bgPath = "Assets/Graphics/Backgrounds/menu_bg.png";
        
        // Auto-Fix Import Settings (Force Sprite & High Quality)
            TextureImporter importer = AssetImporter.GetAtPath(bgPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                // Ensure Single Sprite Mode for full backgrounds
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                
                // High Resolution Settings
                if (importer.maxTextureSize < 4096)
                {
                    importer.maxTextureSize = 4096;
                    changed = true;
                }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed; // Ensure no artifacts
                    changed = true;
                }
                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    changed = true;
                }
                
                if (changed)
                {
                    importer.SaveAndReimport();
                    Debug.Log("Auto-Fixed Background Import Settings to Sprite/Single/HighRes.");
                }
            }

        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
        if (bgSprite != null)
        {
            bgImage.sprite = bgSprite;
            bgImage.color = Color.white;
            
            // Fix: Add AspectRatioFitter to prevent stretching
            // "EnvelopeParent" makes it act like "background-size: cover"
            AspectRatioFitter fitter = GetOrAddComponent<AspectRatioFitter>(bgObj);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)bgSprite.texture.width / bgSprite.texture.height;
        }
        else
        {
            Debug.LogWarning($"Could not load background sprite at {bgPath}. Check if file exists and is a Sprite.");
            bgImage.color = new Color(0.0f, 0.4f, 0.6f, 1.0f); // Fallback Ocean Blue
        }
        
        SetFullScreen(bgObj.GetComponent<RectTransform>());

        // 4. Main Panel
        GameObject mainPanel = GetOrCreateChild(canvasObj.transform, "MainPanel");
        SetFullScreen(mainPanel.GetComponent<RectTransform>());

        // 5. Title (REMOVED as per request - Background contains title)
        GameObject titleObj = GameObject.Find("TitleText");
        if (titleObj != null)
        {
             // If we're rebuilding, remove old title if it exists
             Object.DestroyImmediate(titleObj);
        }

        // 6. Play Button (Center)
        // Load Circle Sprite
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/3rd Party/UI Elements/Extras/Circle128/circle128.png");
        
        GameObject playBtn = CreateRoundButton(mainPanel.transform, "PlayButton", new Vector2(0, 0), circleSprite, "Play");
        
        // 7. Settings Button (Bottom Right)
        GameObject settingsBtn = CreateRoundButton(mainPanel.transform, "SettingsButton", new Vector2(250, -150), circleSprite, "Settings");

        // 8. Quit Button (Bottom Left)
        GameObject quitBtn = CreateRoundButton(mainPanel.transform, "QuitButton", new Vector2(-250, -150), circleSprite, "Quit");

        // 9. Settings Panel
        GameObject settingsPanel = GetOrCreateChild(canvasObj.transform, "SettingsPanel");
        Image settingsBg = GetOrAddComponent<Image>(settingsPanel);
        settingsBg.color = new Color(0, 0, 0, 0.9f);
        SetFullScreen(settingsPanel.GetComponent<RectTransform>());
        settingsPanel.SetActive(false);

        // 10. Volume Slider
        GameObject sliderObj = GetOrCreateChild(settingsPanel.transform, "VolumeSlider");
        Slider slider = GetOrAddComponent<Slider>(sliderObj);
        RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
        sliderRT.sizeDelta = new Vector2(400, 40);
        
        // Slider Visuals (Background)
        GameObject sliderBg = GetOrCreateChild(sliderObj.transform, "Background");
        Image sliderBgImg = GetOrAddComponent<Image>(sliderBg);
        sliderBgImg.color = Color.grey;
        SetFullScreen(sliderBg.GetComponent<RectTransform>());
        slider.targetGraphic = sliderBgImg;
        
        // Slider Handle Area
        GameObject handleArea = GetOrCreateChild(sliderObj.transform, "Handle Slide Area");
        SetFullScreen(handleArea.GetComponent<RectTransform>());
        // Adjust handle area padding
        RectTransform haRT = handleArea.GetComponent<RectTransform>();
        haRT.offsetMin = new Vector2(10, 0);
        haRT.offsetMax = new Vector2(-10, 0);
        
        // Common Font Loader
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject handle = GetOrCreateChild(handleArea.transform, "Handle");
        Image handleImg = GetOrAddComponent<Image>(handle);
        handleImg.sprite = circleSprite;
        handleImg.color = Color.white;
        RectTransform handleRT = handle.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(40, 40);
        slider.handleRect = handleRT;

        // Volume Label
        GameObject volLabelObj = GetOrCreateChild(settingsPanel.transform, "VolumeLabel");
        Text volText = GetOrAddComponent<Text>(volLabelObj);
        volText.text = "Volume: 100%";
        volText.font = font;
        volText.fontSize = 40;
        volText.alignment = TextAnchor.MiddleCenter;
        volText.color = Color.white;
        RectTransform volRect = volLabelObj.GetComponent<RectTransform>();
        volRect.anchoredPosition = new Vector2(0, 50);

        // Controls Info
        GameObject controlsObj = GetOrCreateChild(settingsPanel.transform, "ControlsInfo");
        Text controlsText = GetOrAddComponent<Text>(controlsObj);
        controlsText.text = "CONTROLS:\nWASD / Arrows to Move\nSpace / Click to Boost";
        controlsText.font = font;
        controlsText.fontSize = 24;
        controlsText.alignment = TextAnchor.MiddleCenter;
        controlsText.color = Color.white;
        RectTransform controlsRect = controlsObj.GetComponent<RectTransform>();
        controlsRect.anchoredPosition = new Vector2(0, -50);
        controlsRect.sizeDelta = new Vector2(600, 100);

        // Back Button
        GameObject backBtn = CreateRoundButton(settingsPanel.transform, "BackButton", new Vector2(0, -200), circleSprite, "Back");

        // 11. Manager
        GameObject managerObj = GameObject.Find("MainMenuManager");
        if (managerObj == null) managerObj = new GameObject("MainMenuManager");
        MainMenuManager manager = GetOrAddComponent<MainMenuManager>(managerObj);

        // 12. Background Music
        string musicPath = "Assets/Audio/menu_theme.ogg";
        AudioClip musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
        
        if (musicClip != null)
        {
            AudioSource audioSource = GetOrAddComponent<AudioSource>(managerObj);
            audioSource.clip = musicClip;
            audioSource.loop = true;
            audioSource.playOnAwake = true;
            audioSource.volume = 0.5f; // Default volume
            if (!audioSource.isPlaying) audioSource.Play();
            
            // Assign to Manager
            manager.musicSource = audioSource;
            
            Debug.Log("Attached Menu Music: " + musicClip.name);
        }
        else
        {
            Debug.LogWarning($"Could not load menu music at {musicPath}");
        }

        // 13. Button Sound
        string buttonSoundPath = "Assets/Audio/button_sound_effect.ogg";
        AudioClip btnClip = AssetDatabase.LoadAssetAtPath<AudioClip>(buttonSoundPath);
        if (btnClip != null)
        {
            manager.buttonSoundClip = btnClip;
            Debug.Log("Attached Button Sound: " + btnClip.name);
        }
        else
        {
            Debug.LogWarning($"Could not load button sound at {buttonSoundPath}");
        }

        // Assign References
        manager.mainPanel = mainPanel;
        manager.settingsPanel = settingsPanel;
        manager.playButton = playBtn.GetComponent<Button>();
        manager.settingsButton = settingsBtn.GetComponent<Button>();
        manager.quitButton = quitBtn.GetComponent<Button>();
        manager.backButton = backBtn.GetComponent<Button>();
        manager.volumeSlider = slider;
        manager.volumeLabel = volText;
        
        EditorUtility.SetDirty(manager);
        Debug.Log("Main Menu UI Built Successfully!");
    }

    private static GameObject CreateRoundButton(Transform parent, string name, Vector2 position, Sprite sprite, string label)
    {
        GameObject btnObj = GetOrCreateChild(parent, name);
        Image img = GetOrAddComponent<Image>(btnObj);
        img.sprite = sprite;
        img.color = new Color(1f, 1f, 1f, 0.5f); // Semi-transparent
        Button btn = GetOrAddComponent<Button>(btnObj);
        
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180, 180);
        rect.anchoredPosition = position;

        // Clean up old Icon if switching back to text
        Transform iconChild = btnObj.transform.Find("Icon");
        if (iconChild != null) Object.DestroyImmediate(iconChild.gameObject);

        GameObject textObj = GetOrCreateChild(btnObj.transform, "Text");
        Text text = GetOrAddComponent<Text>(textObj);
        text.text = label;
        
        // Use default font
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        text.font = font;
        text.fontSize = 36; // Bigger size (was 28)
        text.fontStyle = FontStyle.Bold; // Make it Bold
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        SetFullScreen(textObj.GetComponent<RectTransform>());

        return btnObj;
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        GameObject obj;
        
        if (child == null)
        {
            obj = new GameObject(name);
            // If parent is part of Canvas, make sure child has RectTransform
            if (parent.GetComponentInParent<Canvas>() != null)
            {
                obj.AddComponent<RectTransform>();
            }
            obj.transform.SetParent(parent, false);
        }
        else
        {
            obj = child.gameObject;
            // Ensure RectTransform exists if it's supposed to be UI
            if (obj.transform is RectTransform == false && parent.GetComponentInParent<Canvas>() != null)
            {
                obj.AddComponent<RectTransform>();
            }
        }
        return obj;
    }

    private static T GetOrAddComponent<T>(GameObject obj) where T : Component
    {
        T comp = obj.GetComponent<T>();
        if (comp == null) comp = obj.AddComponent<T>();
        return comp;
    }

    private static void SetFullScreen(RectTransform rect)
    {
        if (rect == null)
        {
            Debug.LogError("SetFullScreen called with null RectTransform!");
            return;
        }
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
