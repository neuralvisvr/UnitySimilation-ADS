using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add this for TextMeshPro

public class TimeSpeedUIController : MonoBehaviour
{
    public Slider timeSlider;          // Reference to the UI Slider
    public TMP_Text speedLabel;        // Reference to the TMP_Text for displaying speed

    void Start()
    {
        // Initialize time scale to 1 (normal speed) by default
        Time.timeScale = 1f;

        // Set up the slider
        if (timeSlider != null)
        {
            timeSlider.minValue = 0.1f; // Minimum speed
            timeSlider.maxValue = 20f;  // Maximum speed
            timeSlider.value = Time.timeScale; // Start at 1
            timeSlider.onValueChanged.AddListener(UpdateTimeSpeed); // Link to method
        }
        else
        {
            Debug.LogError("Time Slider not assigned! Attach a UI Slider in the Inspector.");
        }

        // Initialize the label
        UpdateSpeedLabel(Time.timeScale);
    }

    void UpdateTimeSpeed(float newSpeed)
    {
        // Update Time.timeScale and the label
        Time.timeScale = newSpeed;
        UpdateSpeedLabel(newSpeed);
    }

    void UpdateSpeedLabel(float speed)
    {
        // Display the speed value with 1 decimal place, e.g., "Speed: 1.0x"
        if (speedLabel != null)
        {
            speedLabel.text = $"{speed:F1}x";
        }
    }

    void OnDisable()
    {
        // Reset to normal speed when disabled
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        // Clean up the listener
        if (timeSlider != null)
        {
            timeSlider.onValueChanged.RemoveListener(UpdateTimeSpeed);
        }
    }
}