using UnityEngine;
using UnityEngine.EventSystems;

public class MobileBoostButton : MonoBehaviour, IPointerDownHandler
{
    public static MobileBoostButton Instance;
    
    private int lastPressedFrame = -1;
    public bool WasPressedThisFrame => lastPressedFrame == Time.frameCount;
    
    private void Awake()
    {
        Instance = this;
        
        // AUTO-FIX: Enforce standard mobile sizing (Matched to Joystick 280x280)
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null && (Mathf.Abs(rt.sizeDelta.x - 280) > 1))
        {
            rt.sizeDelta = new Vector2(280, 280);
            rt.anchoredPosition = new Vector2(-200, 150); // Improved position
            
            // Also scale the icon if it exists (Child 0 usually)
            if (transform.childCount > 0)
            {
                RectTransform iconRt = transform.GetChild(0).GetComponent<RectTransform>();
                if (iconRt != null)
                {
                     // Ensure icon isn't too small
                     iconRt.sizeDelta = new Vector2(120, 120); // Scaled down slightly to fit 280
                }
            }
             Debug.Log("MobileBoostButton: Auto-upgraded size settings.");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastPressedFrame = Time.frameCount;
    }
}
