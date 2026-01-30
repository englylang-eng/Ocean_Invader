using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Add TextMeshPro Namespace

public class MainMenuManager : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("The name of the game scene to load")]
    [SerializeField] public string gameSceneName = "SampleScene";

    [Header("UI References")]
    [SerializeField] public GameObject mainPanel;
    [SerializeField] public GameObject settingsPanel;
    [SerializeField] public Slider volumeSlider;
    [SerializeField] public Text volumeLabel; // Reverted to Text for Legacy UI
    
    [Header("Buttons")]
    [SerializeField] public Button playButton;
    [SerializeField] public Button settingsButton;
    [SerializeField] public Button quitButton;
    [SerializeField] public Button backButton;

    [Header("Audio")]
    [SerializeField] public AudioClip buttonSoundClip;
    [SerializeField] public AudioSource musicSource; // Reference to Music Source
    private AudioSource sfxSource;

    private void Start()
    {
        // Auto-Link References if missing (Robustness)
        if (mainPanel == null) mainPanel = GameObject.Find("MainPanel");
        if (settingsPanel == null) settingsPanel = GameObject.Find("SettingsPanel");
        if (volumeSlider == null) volumeSlider = FindFirstObjectByType<Slider>(); // Simplification
        
        // Find Buttons if missing
        if (playButton == null && mainPanel != null) playButton = mainPanel.transform.Find("PlayButton")?.GetComponent<Button>();
        if (settingsButton == null && mainPanel != null) settingsButton = mainPanel.transform.Find("SettingsButton")?.GetComponent<Button>();
        if (backButton == null && settingsPanel != null) backButton = settingsPanel.transform.Find("BackButton")?.GetComponent<Button>();
        if (quitButton == null && mainPanel != null) quitButton = mainPanel.transform.Find("QuitButton")?.GetComponent<Button>();

        // Setup Audio Source for SFX
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // Setup Listeners and Hover Effects
        SetupButton(playButton, PlayGame);
        SetupButton(settingsButton, OpenSettings);
        SetupButton(backButton, CloseSettings);
        SetupButton(quitButton, QuitGame);

        // Initialize Volume
        if (volumeSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            AudioListener.volume = savedVolume;
            volumeSlider.value = savedVolume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            UpdateVolumeLabel(savedVolume);
        }

        // Ensure correct initial state
        ShowMain();
    }

    private void SetupButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners(); // Clean slate
            btn.onClick.AddListener(action);
            btn.onClick.AddListener(PlayButtonSound);
            
            // Auto-Add Hover Effect
            if (btn.gameObject.GetComponent<ButtonHoverEffect>() == null)
            {
                btn.gameObject.AddComponent<ButtonHoverEffect>();
            }
        }
    }

    public void ShowMain()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void PlayGame()
    {
        StartCoroutine(PlayGameRoutine());
    }

    private System.Collections.IEnumerator PlayGameRoutine()
    {
        // Kill menu music object entirely
        MenuMusic menuMusic = FindFirstObjectByType<MenuMusic>();
        if (menuMusic != null)
        {
            menuMusic.Kill();
        }

        // Wait for button click sound (played on sfxSource)
        yield return new WaitForSeconds(0.4f);

        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogError($"Scene '{gameSceneName}' not found! Please check Build Settings.");
        }
    }

    public void OpenSettings()
    {
        // Settings disabled for now per user request
        // if (mainPanel != null) mainPanel.SetActive(false);
        // if (settingsPanel != null) settingsPanel.SetActive(true);
        Debug.Log("Settings button clicked (Menu disabled)");
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    public void QuitGame()
    {
        StartCoroutine(QuitGameRoutine());
    }

    private System.Collections.IEnumerator QuitGameRoutine()
    {
        // Wait for sound to play (0.4s)
        yield return new WaitForSeconds(0.4f);

        Debug.Log("Quitting Game...");
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(value);
    }

    public void PlayButtonSound()
    {
        if (sfxSource != null && buttonSoundClip != null)
        {
            // Debug.Log("Playing Button Sound"); // Uncomment to debug
            sfxSource.volume = 1.0f; // Ensure volume is up (AudioListener controls master)
            sfxSource.PlayOneShot(buttonSoundClip);
        }
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeLabel != null)
        {
            volumeLabel.text = $"Volume: {Mathf.RoundToInt(value * 100)}%";
        }
    }
}
