using UnityEngine;
using Rhinotap.Toolkit;

public class MobileInputLoader : MonoBehaviour
{
    public GameObject mobileInputPrefab;
    
    [Tooltip("If true, the joystick will be shown in the Editor even if not in Simulator mode.")]
    public bool simulateMobileInEditor = false;

    private GameObject instantiatedControls;

    void Start()
    {
        // Listen for Pause Event
        EventManager.StartListening<bool>("gamePaused", OnGamePaused);

        bool isMobile = Application.isMobilePlatform;
        
        // Check SystemInfo for handheld devices (Robust check for WebGL/Desktop)
        if (UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld)
        {
            isMobile = true;
        }

        // Check for Touch Support (Handles Simulator & Touch Laptops)
        // Fix: Removed simple Input.touchSupported check because it returns true on many desktop devices
        // We rely on Application.isMobilePlatform or explicit simulation
        if (!Application.isEditor && Input.touchSupported && (Application.isMobilePlatform || UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld))
        {
            isMobile = true;
        }
        
        // Extended check for Editor/Simulator
        #if UNITY_EDITOR
        // Only override if explicitly requested (allows desktop testing without joystick)
        if (simulateMobileInEditor) isMobile = true;
        #endif
        
        if (isMobile)
        {
            // Ensure EventSystem exists (Critical for Mobile UI Input)
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Fallback: Load from Resources if prefab is missing
            if (mobileInputPrefab == null)
            {
                mobileInputPrefab = Resources.Load<GameObject>("MobileInputCanvas");
            }
            
            if (mobileInputPrefab != null && MobileJoystick.Instance == null)
            {
                instantiatedControls = Instantiate(mobileInputPrefab);
            }
            else if (mobileInputPrefab == null)
            {
                Debug.LogError("MobileInputLoader: MobileInputCanvas prefab is missing! Please run Tools > Setup Mobile Input.");
            }
        }
        else
        {
            // FORCE CLEANUP: Ensure no mobile controls exist on Desktop
            if (MobileJoystick.Instance != null)
            {
                // Destroy the entire canvas/root of the joystick
                Destroy(MobileJoystick.Instance.transform.root.gameObject);
                Debug.Log("MobileInputLoader: Removed Mobile Joystick for Desktop platform.");
            }
        }
    }

    private void OnDestroy()
    {
        // Safe check to prevent errors when quitting the application
        if (EventManager.HasInstance)
        {
            EventManager.StopListening<bool>("gamePaused", OnGamePaused);
        }
    }

    private void OnGamePaused(bool isPaused)
    {
        if (instantiatedControls != null)
        {
            instantiatedControls.SetActive(!isPaused);
        }
    }
}
