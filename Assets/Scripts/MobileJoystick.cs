using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    public static MobileJoystick Instance;
    
    [Header("Settings")]
    public float handleRange = 1f;
    public bool hideOnDesktop = true;

    [Header("References")]
    public RectTransform background;
    public RectTransform handle;

    public Vector2 InputDirection { get; private set; }

    private Vector2 initialPos;
    private Canvas canvas;

    private void Awake()
    {
        Instance = this;
        
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null && transform.childCount > 0) handle = transform.GetChild(0).GetComponent<RectTransform>();
        
        canvas = GetComponentInParent<Canvas>();
        initialPos = background.anchoredPosition;

        // Determine if we should show or hide
        bool shouldShow = false;

        // Fix: Removed Input.touchSupported to prevent joystick from appearing on Desktop devices with touch screens
        if (Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld)
        {
            shouldShow = true;
        }
        else
        {
            // Desktop / Editor Logic
            #if UNITY_EDITOR
            // Check if Loader is forcing simulation
            MobileInputLoader loader = Object.FindFirstObjectByType<MobileInputLoader>();
            if (loader != null)
            {
                // If a Loader exists, we assume it manages our existence (or we are debugging).
                // This ensures visibility in Simulator where Input.touchSupported can be flaky.
                shouldShow = true;
            }
            #endif
        }

        if (hideOnDesktop && !shouldShow)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
            
            // AUTO-FIX: Enforce standard mobile sizing (User Request: Responsiveness & Size)
            // If the joystick is too small (old standard 250), upgrade it to 360.
            if (background != null && background.sizeDelta.x < 300)
            {
                background.sizeDelta = new Vector2(360, 360);
                // Fix: Move up to avoid safe area/home bar issues (was 150)
                background.anchoredPosition = new Vector2(200, 250); 
                
                if (handle != null)
                {
                    handle.sizeDelta = new Vector2(160, 160);
                }
                
                // Improve responsiveness: Reduce travel distance
                handleRange = 0.6f; 
                
                Debug.Log("MobileJoystick: Auto-upgraded size and responsiveness settings.");
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position))
        {
            position.x = (position.x / background.sizeDelta.x);
            position.y = (position.y / background.sizeDelta.y);

            InputDirection = new Vector2(position.x * 2, position.y * 2);
            InputDirection = (InputDirection.magnitude > 1.0f) ? InputDirection.normalized : InputDirection;

            // Move Handle
            if (handle != null)
            {
                handle.anchoredPosition = new Vector2(
                    InputDirection.x * (background.sizeDelta.x / 2) * handleRange,
                    InputDirection.y * (background.sizeDelta.y / 2) * handleRange
                );
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputDirection = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }
}
