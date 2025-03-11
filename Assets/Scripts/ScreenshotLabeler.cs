using UnityEngine;
using System.IO;
using System;

public class ScreenshotLabeler : MonoBehaviour
{
    public Camera carCamera;   // Assign the car's camera in the Inspector
    public bool takeScreenshot = true; // If true, capture screenshots; if false, only drive
    public bool useGrayscale = false; // Flag to capture grayscale (one-channel) or RGB (three-channel) images
    public int imageResolutionX = 224; // Resolution for the captured image (e.g., 224x224)
    public int imageResolutionY = 224;

    private int counter = 0;   // Counter for numbering the screenshots
    private string label = ""; // Current label based on key press

    // Base folder path
    public string baseFolderPath = @"C:\Unity_projects\vr_project\myproject-vr\fastapi-vr-backend\data\dataset\RCdata_ch1";

    private float screenshotInterval = 0.1f; // Frequency of screenshots (10 frames per second)
    private float timeSinceLastScreenshot = 0f;

    void Start()
    {
        // Ensure the camera is set; if not, use the main camera
        if (carCamera == null)
        {
            carCamera = Camera.main;
        }

        // Ensure directories exist for each key
        CreateDirectoryIfNotExists("Forward");
        CreateDirectoryIfNotExists("Left");
        CreateDirectoryIfNotExists("Right");
    }

    void Update()
    {
        // Track time since the last screenshot
        timeSinceLastScreenshot += Time.deltaTime;

        // Check for specific key combinations and set label
        if (Input.GetKey(KeyCode.Q))
        {
            if (Input.GetKey(KeyCode.UpArrow))
            {
                label = "Forward";
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                label = "Left";
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                label = "Right";
            }
            else
            {
                // Reset label if Q is pressed but not with I, J, or L
                label = "";
            }
        }
        else
        {
            // Reset label if Q is not pressed
            label = "";
        }

        // Capture screenshot if Q + specific key is pressed, and the screenshot interval has passed
        if (!string.IsNullOrEmpty(label) && timeSinceLastScreenshot >= screenshotInterval)
        {
            TakeScreenshot();
            timeSinceLastScreenshot = 0f; // Reset timer after taking a screenshot
        }
    }

    void TakeScreenshot()
    {
        // Determine folder path based on the current label
        string folderPath = Path.Combine(baseFolderPath, label);

        // Ensure the folder exists
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Generate a unique filename using a random number generator
        string randomId = Guid.NewGuid().ToString(); // Generates a globally unique identifier
        string filename = $"{label}_screenshot_{randomId}.png";

        // Create the RenderTexture with specified resolution
        RenderTexture renderTexture = new RenderTexture(imageResolutionX, imageResolutionY, 24);
        carCamera.targetTexture = renderTexture;

        // Determine texture format based on grayscale or RGB flag
        TextureFormat format = useGrayscale ? TextureFormat.R8 : TextureFormat.RGB24;
        Texture2D screenshot = new Texture2D(imageResolutionX, imageResolutionY, format, false);

        // Capture the screenshot from the camera's perspective
        carCamera.Render();
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, imageResolutionX, imageResolutionY), 0, 0);
        carCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        // Save the image based on the format
        SaveAsPng(screenshot, folderPath, filename);

        // No counter is needed as filenames are unique
    }


    void SaveAsPng(Texture2D screenshot, string folderPath, string filename)
    {
        string pngFilename = Path.Combine(folderPath, filename);

        // Convert to PNG
        byte[] pngData = screenshot.EncodeToPNG();

        // Save the PNG file
        File.WriteAllBytes(pngFilename, pngData);

        Debug.Log($"Image saved as {pngFilename}");
    }

    void CreateDirectoryIfNotExists(string label)
    {
        string path = Path.Combine(baseFolderPath, label);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
