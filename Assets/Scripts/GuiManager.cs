using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add TMP Support
using Rhinotap.Toolkit;

public class GuiManager : Singleton<GuiManager>
{
    [SerializeField]
    private Image XpBar;

    [Header("Growth Icons")]
    [SerializeField]
    private Image[] growthIcons;
    [SerializeField]
    private Color completedColor = Color.white;
    [SerializeField]
    private Color currentColor = new Color(1f, 1f, 1f, 1f); // Full white
    [SerializeField]
    private Color lockedColor = Color.black; // Solid black for silhouette effect
    [SerializeField]
    private Sprite warningIconSprite; // Sprite for the warning icon on locked fishes



    [SerializeField]
    private GameObject pauseBtn;
    [SerializeField]
    private GameObject resumeBtn;
    [SerializeField]
    private GameObject pausedBg;

    [SerializeField]
    private GameObject ScoreScreen;
    [Header("Game Over Messages")]
    [SerializeField]
    [TextArea]
private string victoryMessage = "GbGrsaTr Gñk)anrYcCIvitkñúgvKÁenH";
    [SerializeField]
    [TextArea]
    private string defeatMessage = "BüayammþgeTot";
    
    [SerializeField]
    private Font messageFont;
    [SerializeField]
    private TMP_FontAsset messageFontTmp; // TMP Support

    [SerializeField]
    private Text ScoreText;
    private Text messageText;
    
    // Floating Text
    [Header("Floating Text")]
    [SerializeField]
    private GameObject floatingTextPrefab; 
    [SerializeField] 
    private int poolSize = 20;
    private Queue<GameObject> floatingTextPool = new Queue<GameObject>();

    private float targetXpFill = 0f;

    // Start is called before the first frame update
    void Start()
    {
        // Simple fallback for ACTIVE objects only (Cheap, fixes dark screen if unassigned)
        if (pausedBg == null) pausedBg = GameObject.Find("PausedBG");
        if (ScoreScreen == null) ScoreScreen = GameObject.Find("ScoreScreen");

        // Ensure overlays are hidden at start (Fix for Black Screen)
        if (pausedBg != null) pausedBg.SetActive(false);
        if (ScoreScreen != null) ScoreScreen.SetActive(false);

        // Apply Font to ScoreText Template (if available) to fix "Default English" look
        if (ScoreText != null && messageFont != null)
        {
            ScoreText.font = messageFont;
        }

        InitializeFloatingTextPool();

        // Setup Message Text (Clone ScoreText)
        if (messageText == null && ScoreText != null)
        {
            GameObject msgObj = Instantiate(ScoreText.gameObject, ScoreText.transform.parent);
            msgObj.name = "MessageText";
            messageText = msgObj.GetComponent<Text>();
            
            if (messageFont != null)
            {
                messageText.font = messageFont;
            }

            messageText.text = "";
            // Optimize for long text
            messageText.resizeTextForBestFit = true;
            messageText.resizeTextMinSize = 10;
            messageText.resizeTextMaxSize = 60;
            messageText.alignment = TextAnchor.MiddleCenter;
            
            // Center the message text in the screen (Fill Parent)
            RectTransform rt = messageText.GetComponent<RectTransform>();
            if (rt != null)
            {
                 rt.anchorMin = Vector2.zero;
                 rt.anchorMax = Vector2.one;
                 rt.sizeDelta = Vector2.zero; 
                 rt.anchoredPosition = Vector2.zero;
            }

            msgObj.SetActive(false);
        }

        EventManager.StartListening("GameWin", () => {
             ShowGameMessage(victoryMessage);
        });

        EventManager.StartListening("GameLoss", () => {
             ShowGameMessage(defeatMessage);
        });

        EventManager.StartListening("GameStart", () => {
            SetXp(0, 1);
            HideScore();
            UpdateGrowthIcons(1); // Reset icons to level 1
        });
        

        EventManager.StartListening<bool>("gamePaused", (isPaused) => {
            TogglePauseBtn();
        });


        EventManager.StartListening<int>("GameOver", (score) => {
            ShowScore(score);
        });

        EventManager.StartListening<int>("onLevelUp", (level) => {
            UpdateGrowthIcons(level);
        });

        // Ensure layout is fixed at start
        FixPauseLayout();
        UpdateGrowthIcons(1); // Initial state
        
        // Ensure XP bar starts empty
        if (XpBar != null)
        {
             if (XpBar.type != Image.Type.Filled)
             {
                 XpBar.type = Image.Type.Filled;
                 XpBar.fillMethod = Image.FillMethod.Horizontal;
             }
             XpBar.fillAmount = 0f;
        }

        // Ensure UI overlays are hidden at start (Fix Black Screen)
        if (pausedBg != null) pausedBg.SetActive(false);
        if (ScoreScreen != null) ScoreScreen.SetActive(false);
        if (pauseBtn != null) pauseBtn.SetActive(true);
        if (resumeBtn != null) resumeBtn.SetActive(false);

        // Fix: Ensure Main UI Canvas is above Shark Warning Canvas (Order 999)
        if (pausedBg != null)
        {
            Canvas rootCanvas = pausedBg.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                // Ensure we are active to set this? No, component access is fine.
                // We want the Pause Menu to cover the Warning Icon.
                rootCanvas.sortingOrder = 2000; 
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (messageFontTmp == null)
        {
             // Try to find default TMP font
             messageFontTmp = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
    }
#endif

    private void FixPauseLayout()
    {
        // Fix Paused BG layout: Ensure it covers the entire screen and has no rounded corners
        if (pausedBg != null)
        {
            RectTransform rt = pausedBg.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Stretch to fill parent (Full Screen)
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                
                // Oversize it slightly to cover any potential edges/margins
                rt.offsetMin = new Vector2(-50f, -50f);
                rt.offsetMax = new Vector2(50f, 50f);
            }

            Image img = pausedBg.GetComponent<Image>();
            if (img != null)
            {
                // Remove sprite to eliminate border radius/rounded corners
                img.sprite = null; 
                // Ensure it's a black semi-transparent overlay
                img.color = new Color(0f, 0f, 0f, 0.4f);
            }
        }
    }

    public void SetXp(int currentXP, int maxXp, int currentLevel = 1, int maxLevels = 1)
    {
        if (XpBar == null) return;
        
        // Calculate progress within current level (0 to 1)
        float levelProgress = (float)currentXP / (float)maxXp;

        if (growthIcons != null && growthIcons.Length > 0)
        {
            // Determine Start Position
            float startPos = 0f;
            int startIdx = currentLevel - 1;
            
            // FIX: For Level 1, start at 0 (empty bar) instead of the first icon position
            if (currentLevel == 1)
            {
                startPos = 0f;
            }
            else if (startIdx >= 0 && startIdx < growthIcons.Length)
            {
                startPos = GetNormalizedPosition(growthIcons[startIdx].rectTransform);
            }
            else if (startIdx >= growthIcons.Length)
            {
                // If we are past the last icon, start from the last icon's position
                startPos = GetNormalizedPosition(growthIcons[growthIcons.Length - 1].rectTransform);
            }

            // Determine End Position
            float endPos = 1f; // Default to full bar if no next icon
            int endIdx = currentLevel;

            if (endIdx >= 0 && endIdx < growthIcons.Length)
            {
                endPos = GetNormalizedPosition(growthIcons[endIdx].rectTransform);
            }

            // Interpolate
            float finalFill = Mathf.Lerp(startPos, endPos, levelProgress);
            
            // Set Target for Smooth Animation
            targetXpFill = finalFill;
        }
        else
        {
            // Fallback to simple math if icons are missing
            float segmentSize = 1f / (float)maxLevels;
            float totalProgress = ((currentLevel - 1) * segmentSize) + (levelProgress * segmentSize);
            targetXpFill = totalProgress;
        }
    }

    private void InitializeFloatingTextPool()
    {
        Transform parent = null;
        if (XpBar != null) parent = XpBar.transform.parent;
        else if (ScoreText != null) parent = ScoreText.transform.parent;
        else parent = transform;

        GameObject template = (ScoreText != null) ? ScoreText.gameObject : null;
        if (template == null && floatingTextPrefab != null) template = floatingTextPrefab;

        if (template == null) return;

        floatingTextPool.Clear(); // Ensure pool is clean before init
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(template, parent);
            obj.name = "FloatingTextPool_" + i;
            obj.SetActive(false);
            
            // Add Outline for visibility (Feeding Frenzy style) - Removed per request for cleaner/smaller look
            /*
            if (obj.GetComponent<Outline>() == null)
            {
                 Outline outline = obj.AddComponent<Outline>();
                 outline.effectColor = Color.black;
                 outline.effectDistance = new Vector2(2, -2);
            }
            */

            // Ensure Font is applied (Legacy Text)
            if (messageFont != null)
            {
                Text t = obj.GetComponent<Text>();
                if (t != null) t.font = messageFont;
            }

            // Ensure Font is applied (TMP)
            TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                 if (messageFontTmp != null) tmp.font = messageFontTmp;
                 else if (tmp.font == null) 
                 {
                     // Fallback 1: Default Settings
                     tmp.font = TMP_Settings.defaultFontAsset;
                     
                     // Fallback 2: Load explicit resource if default is missing
                     if (tmp.font == null)
                     {
                         tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                     }
                 }
            }
            
            floatingTextPool.Enqueue(obj);
        }
    }

    private GameObject GetFloatingTextFromPool()
    {
        while (floatingTextPool.Count > 0)
        {
            GameObject obj = floatingTextPool.Dequeue();
            if (obj != null)
            {
                obj.SetActive(true);
                // Re-apply font in case it was lost/changed
                if (messageFont != null)
                {
                     Text t = obj.GetComponent<Text>();
                     if (t != null) t.font = messageFont;
                }

                // Re-apply font (TMP)
                TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                     if (messageFontTmp != null) tmp.font = messageFontTmp;
                     else if (tmp.font == null) 
                     {
                         tmp.font = TMP_Settings.defaultFontAsset;
                         if (tmp.font == null) tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                     }
                }

                return obj;
            }
        }

        // If pool is empty, fallback
        Transform parent = null;
        if (XpBar != null) parent = XpBar.transform.parent;
        else if (ScoreText != null) parent = ScoreText.transform.parent;
        else parent = transform;
        
        GameObject template = (ScoreText != null) ? ScoreText.gameObject : null;
        if (template == null && floatingTextPrefab != null) template = floatingTextPrefab;
        
        if (template != null)
        {
            GameObject objFallback = Instantiate(template, parent);
            objFallback.name = "FloatingXP_Fallback";
            objFallback.SetActive(true);

            // Assign Font (TMP)
            TextMeshProUGUI tmp = objFallback.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                 if (messageFontTmp != null) tmp.font = messageFontTmp;
                 else if (tmp.font == null) 
                 {
                     tmp.font = TMP_Settings.defaultFontAsset;
                     if (tmp.font == null) tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                 }
            }
            
            // Add Outline - Removed
            /*
            if (objFallback.GetComponent<Outline>() == null)
            {
                 Outline outline = objFallback.AddComponent<Outline>();
                 outline.effectColor = Color.black;
                 outline.effectDistance = new Vector2(2, -2);
            }
            */
            
            return objFallback;
        }
        return null;
    }

    private void ReturnFloatingTextToPool(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        floatingTextPool.Enqueue(obj);
    }

    public void ShowFloatingText(Vector3 worldPos, string text, Color color)
    {
        GameObject obj = GetFloatingTextFromPool();
        if (obj == null) 
        {
            return;
        }

        RectTransform rt = obj.GetComponent<RectTransform>();
        Text txt = obj.GetComponent<Text>();
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();

        // Ensure it's last sibling to be on top
        obj.transform.SetAsLastSibling();
        // Reset Scale (Will be animated)
        obj.transform.localScale = Vector3.one;

        if (txt != null)
        {
            txt.resizeTextForBestFit = false; 
            txt.text = text;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            // High quality trick: Large font size, scaled down object
            txt.fontSize = 48; // Was 24
            txt.fontStyle = FontStyle.Bold; 
            
            // Remove Shadow if it exists
            Shadow shadow = obj.GetComponent<Shadow>();
            if (shadow != null) Destroy(shadow);
        }
        
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 48; // Was 24
            tmp.fontStyle = FontStyles.Bold;
        }

        // Position
        if (Camera.main != null)
        {
            // Spawn slightly above the eaten fish (Offset Y)
            Vector3 offsetPos = worldPos + Vector3.up * 0.8f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(offsetPos);
            if (rt != null) 
            {
                rt.position = screenPos;
                // AUTO-FIX: Increase width to prevent text wrapping/shrinking (User Request)
                // Was likely 200, increasing to 400 to accommodate longer text like "÷100 BinÞú"
                rt.sizeDelta = new Vector2(400, 100); 
            }
        }
        
        // Start Animation
        StartCoroutine(AnimateFloatingText(obj, rt, txt, tmp));
    }

    private IEnumerator AnimateFloatingText(GameObject obj, RectTransform rt, Text txt, TextMeshProUGUI tmp)
    {
        float duration = 0.8f; 
        float elapsed = 0f;
        
        Vector3 startPos = (rt != null) ? rt.position : Vector3.zero;
        
        // Drift: Up and slightly random X
        float driftX = UnityEngine.Random.Range(-30f, 30f); 
        Vector3 endPos = startPos + Vector3.up * 100f + Vector3.right * driftX;

        // Scale Logic: Start tiny, target scale 0.3 (for high quality small text)
        // AUTO-FIX: Increased from 0.3 to 0.45 per user request "seem abit shrink"
        Vector3 targetScale = Vector3.one * 0.45f; 
        if(rt != null) rt.localScale = Vector3.zero; 

        Color startColor = Color.white;
        if (txt != null) startColor = txt.color;
        if (tmp != null) startColor = tmp.color;
        
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        while (elapsed < duration)
        {
            if (obj == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 1. Pop In (EaseOutBack - Cleaner, no double bounce)
            if (rt != null)
            {
                float scaleDuration = 0.3f;
                if (t < scaleDuration)
                {
                    float st = t / scaleDuration;
                    // Standard EaseOutBack
                    float c1 = 1.70158f;
                    float c3 = c1 + 1f;
                    float ease = 1f + c3 * Mathf.Pow(st - 1f, 3f) + c1 * Mathf.Pow(st - 1f, 2f);
                    
                    rt.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, ease);
                }
                else
                {
                    rt.localScale = targetScale;
                }
            }

            // 2. Position (Linear is smoother for floating)
            if (rt != null)
            {
                rt.position = Vector3.Lerp(startPos, endPos, t);
            }

            // 3. Fade Out (Last 50% for smoother exit)
            float fadeStart = 0.5f;
            Color currentColor = startColor;
            if (t > fadeStart)
            {
                float ft = (t - fadeStart) / (1f - fadeStart);
                currentColor = Color.Lerp(startColor, endColor, ft);
            }
            
            if (txt != null) txt.color = currentColor;
            if (tmp != null) tmp.color = currentColor;
            
            yield return null;
        }

        ReturnFloatingTextToPool(obj);
    }

    // Helper for "Pop" effect
    private float EvaluateEaseOutBack(float x) 
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1;
        return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2);
    }

    private float GetNormalizedPosition(RectTransform target)
    {
        if (XpBar == null || target == null) return 0f;
        
        RectTransform barRect = XpBar.rectTransform;
        Vector3[] corners = new Vector3[4];
        barRect.GetWorldCorners(corners);
        
        float startX = corners[0].x;
        float totalWidth = corners[2].x - corners[0].x;
        
        if (totalWidth <= 0) return 0f;

        float targetX = target.position.x;
        float normalized = (targetX - startX) / totalWidth;
        
        return Mathf.Clamp01(normalized);
    }

    private void UpdateGrowthIcons(int currentLevel)
    {
        if (growthIcons == null || growthIcons.Length == 0) return;

        for (int i = 0; i < growthIcons.Length; i++)
        {
            if (growthIcons[i] == null) continue;

            int iconLevel = i + 1;

            // --- Warning Icon Cleanup ---
            // We check for and destroy any existing "WarningIcon" objects to clean up the scene.
            Transform warningTrans = growthIcons[i].transform.Find("WarningIcon");
            if (warningTrans != null)
            {
                Destroy(warningTrans.gameObject);
            }
            // ---------------------------

            if (iconLevel < currentLevel)
            {
                // Past Levels: Completed Color (White)
                growthIcons[i].color = completedColor;
                growthIcons[i].transform.localScale = Vector3.one;
            }
            else if (iconLevel == currentLevel)
            {
                // Current Level: Highlighted (White) + Scaled Up (1.2x)
                growthIcons[i].color = currentColor;
                growthIcons[i].transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                // Future Levels: Locked (Solid Black Silhouette)
                growthIcons[i].color = lockedColor;
                growthIcons[i].transform.localScale = Vector3.one;
            }
        }
    }

    private void TogglePauseBtn()
    {
        if( pauseBtn == null || resumeBtn == null)
        {
            // Debug.Log("Missing pause/resume btns");
            return;
        }

        if( pauseBtn.activeSelf == true)
        {
            pauseBtn.SetActive(false);
            resumeBtn.SetActive(true);
            pausedBg.SetActive(true);
            FixPauseLayout();
            resumeBtn.transform.SetAsLastSibling();
        }else
        {
            pauseBtn.SetActive(true);
            resumeBtn.SetActive(false);
            pausedBg.SetActive(false);
        }
    }

    private void ShowScore(int score = 0)
    {
        if (ScoreScreen == null || ScoreText == null) return;
        ScoreScreen.SetActive(true);
        ScoreText.gameObject.SetActive(false);
        
        // Fix: Removed score display logic per user request
        /*
        if (messageText != null)
        {
            if (messageText.gameObject.activeSelf && !messageText.text.Contains("Score:"))
            {
                messageText.text += "\nScore: " + score.ToString();
            }
            else
            {
                messageText.text = "Score: " + score.ToString();
            }
            messageText.gameObject.SetActive(true);
        }
        */
    }

    private void ShowGameMessage(string message)
    {
        if (ScoreScreen == null) return;
        ScoreScreen.SetActive(true);
        if (ScoreText != null) ScoreText.gameObject.SetActive(false);
        
        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(true);
        }
    }

    private void HideScore()
    {
        if (ScoreScreen == null || ScoreText == null) return;
        ScoreScreen.SetActive(false);
        ScoreText.text = "0";
        if (messageText != null) messageText.gameObject.SetActive(false);
    }

    void Update()
    {
        // Smooth XP Logic
        if (XpBar != null && Mathf.Abs(XpBar.fillAmount - targetXpFill) > 0.001f)
        {
            XpBar.fillAmount = Mathf.Lerp(XpBar.fillAmount, targetXpFill, Time.deltaTime * 5f);
        }
    }

    // Helper to find UI objects even if inactive
    private GameObject FindUIObjectByName(params string[] names)
    {
        foreach (string name in names)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) return obj;
        }
        
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c.gameObject.scene.rootCount == 0) continue; 
            foreach (string name in names)
            {
                Transform t = FindDeepChild(c.transform, name);
                if (t != null) return t.gameObject;
            }
        }
        return null;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}



