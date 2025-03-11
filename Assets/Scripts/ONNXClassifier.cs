using UnityEngine;
using Unity.Barracuda;
using System.IO;
using System.Linq;
using System;
using UnityEngine.UI;       // For Button
using TMPro;               // For TMP_Text

public class ONNXAutonomousDriver : MonoBehaviour
{
    // ------------------------------------------------
    // 1) ONNX Model + Classification
    // ------------------------------------------------
    [Header("Barracuda Model Settings")]
    public NNModel onnxModel;
    public int imageSize = 56; // Resize input images to match training
    public string[] classLabels = { "Class0", "Class1", "Class2" }; // Optional, not strictly used for logic

    [Header("Camera & Capture Settings")]
    public Camera captureCamera;
    public int imageResolutionX = 224;
    public int imageResolutionY = 224;
    public string saveDirectory = @"D:\Unity_projects\myproject-vr\ADS\Assets\TestImages";

    private Model runtimeModel;
    private IWorker worker;

    // ------------------------------------------------
    // 2) WheelColliders + Movement Control
    // ------------------------------------------------
    [Header("Vehicle Wheel Settings")]
    public WheelCollider frontRightWheel;
    public WheelCollider frontLeftWheel;
    public WheelCollider rearRightWheel;
    public WheelCollider rearLeftWheel;

    [Tooltip("Controls the base forward torque applied to the rear wheels.")]
    public float motorTorque = 200f;

    [Tooltip("Maximum angle (in degrees) for steering the front wheels.")]
    public float maxSteerAngle = 30f;

    // ------------------------------------------------
    // 3) Autonomous vs Manual Toggle
    // ------------------------------------------------
    [Header("Autonomous Driving Setup")]
    public Transform initialPosition;            // If you want to reset your car
    public TMP_Text classificationResultText;    // UI Text to display classification result
    public Button startStopButton;               // UI Button to toggle autonomous mode

    [Tooltip("How often to run classification, in frames.")]
    [Range(1, 60)]
    public int classificationFrequency = 10;

    // Internal flags/variables
    private bool isAutonomousMode = false;
    private string currentCondition = "Stop";
    private int frameCount = 0;

    void Start()
    {
        // --- Initialize Barracuda Model ---
        if (onnxModel == null)
        {
            Debug.LogError("❌ ONNX model is missing! Please assign it in the Inspector.");
            enabled = false;
            return;
        }
        runtimeModel = ModelLoader.Load(onnxModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.CSharp, runtimeModel);
        Debug.Log("✅ ONNX Model Loaded Successfully!");

        // --- Camera Setup ---
        if (captureCamera == null)
        {
            captureCamera = Camera.main;
            if (captureCamera == null)
            {
                Debug.LogError("❌ No camera assigned or found!");
                enabled = false;
                return;
            }
        }

        // --- Ensure Save Directory Exists ---
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
            Debug.Log("📁 Created save directory: " + saveDirectory);
        }

        // --- Button Setup ---
        if (startStopButton != null)
        {
            startStopButton.onClick.AddListener(ToggleAutonomousMode);
            startStopButton.GetComponentInChildren<TMP_Text>().text = "Start Autonomous";
        }
        else
        {
            Debug.LogWarning("No startStopButton assigned. Cannot toggle autonomous mode via UI.");
        }
    }

    void Update()
    {
        // If not in autonomous mode, allow manual driving with I/J/L keys.
        if (!isAutonomousMode)
        {
            HandleManualControls();
        }
        else
        {
            // In autonomous mode: run classification automatically on schedule
            frameCount++;
            if (frameCount >= classificationFrequency)
            {
                frameCount = 0;
                ClassifyFrameAndDrive();
            }
        }

        // Optionally, press P to classify manually at any moment
        if (Input.GetKeyDown(KeyCode.P))
        {
            ClassifyFrameAndDrive();
        }
    }

    /// <summary>
    /// Toggles the car between manual mode and autonomous mode.
    /// </summary>
    void ToggleAutonomousMode()
    {
        isAutonomousMode = !isAutonomousMode;
        if (isAutonomousMode)
        {
            // Switch to Autonomous
            if (startStopButton != null)
                startStopButton.GetComponentInChildren<TMP_Text>().text = "Stop Autonomous";
            Debug.Log("Autonomous Mode STARTED.");
        }
        else
        {
            // Switch to Manual
            if (startStopButton != null)
                startStopButton.GetComponentInChildren<TMP_Text>().text = "Start Autonomous";

            Debug.Log("Autonomous Mode STOPPED. Resetting car and stopping movement.");
            ResetCarPosition();
            StopMovement();
        }
    }

    /// <summary>
    /// Optionally reset the car’s position and rotation to 'initialPosition'.
    /// </summary>
    void ResetCarPosition()
    {
        if (initialPosition != null)
        {
            transform.position = initialPosition.position;
            transform.rotation = initialPosition.rotation;
            Debug.Log("Car reset to initial position.");
        }
    }

    /// <summary>
    /// Manual controls: I=Forward, J=Left, L=Right, else Stop
    /// </summary>
    void HandleManualControls()
    {
        if (Input.GetKey(KeyCode.I))
        {
            MoveForward();
        }
        else if (Input.GetKey(KeyCode.J))
        {
            TurnLeft();
        }
        else if (Input.GetKey(KeyCode.L))
        {
            TurnRight();
        }
        else
        {
            StopMovement();
        }
    }

    // -------------------------------------------------------------------
    // (A) Classification + Movement
    // -------------------------------------------------------------------
    /// <summary>
    /// Captures a frame, infers via ONNX, updates 'currentCondition',
    /// applies movement, and optionally saves the screenshot.
    /// </summary>
    void ClassifyFrameAndDrive()
    {
        Texture2D screenshot = CaptureScreenshot();

        // 1) Process classification
        string predictedLabel = ProcessCapturedImage(screenshot);

        // 2) Set driving condition
        currentCondition = predictedLabel;  // "Forward", "Left", or "Right" (or "Unknown")
        ApplyMovementCondition(currentCondition);

        // 3) Show classification in UI (optional)
        if (classificationResultText != null)
        {
            classificationResultText.text = $"Prediction: {predictedLabel}";
        }

        // 4) Save screenshot (optional)
        string filename = $"classified_{Guid.NewGuid()}.png";
        SaveAsPng(screenshot, saveDirectory, filename);
    }

    Texture2D CaptureScreenshot()
    {
        RenderTexture rt = new RenderTexture(imageResolutionX, imageResolutionY, 24);
        captureCamera.targetTexture = rt;
        captureCamera.Render();

        RenderTexture.active = rt;
        Texture2D screenshot = new Texture2D(imageResolutionX, imageResolutionY, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, imageResolutionX, imageResolutionY), 0, 0);
        screenshot.Apply();

        // Cleanup
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

        return screenshot;
    }

    /// <summary>
    /// Preprocess + ONNX inference => return "Forward", "Left", "Right", or "Unknown".
    /// </summary>
    string ProcessCapturedImage(Texture2D originalImage)
    {
        // Flip vertically for consistent orientation
        Texture2D flipped = FlipVertical(originalImage);

        // Preprocess (resize + grayscale + normalization)
        Tensor inputTensor = PreprocessImage(flipped);
        Tensor outputTensor = RunInference(inputTensor);

        // Extract logits, compute softmax
        float[] logits = outputTensor.AsFloats();
        float[] probabilities = Softmax(logits);

        // Find predicted index with max probability
        int predictedIndex = 0;
        float maxProb = probabilities[0];
        for (int i = 1; i < probabilities.Length; i++)
        {
            if (probabilities[i] > maxProb)
            {
                maxProb = probabilities[i];
                predictedIndex = i;
            }
        }

        // Map index => "Forward", "Left", "Right" (or "Unknown")
        string predictedLabel;
        if (predictedIndex == 0) predictedLabel = "Forward";
        else if (predictedIndex == 1) predictedLabel = "Left";
        else if (predictedIndex == 2) predictedLabel = "Right";
        else predictedLabel = "Unknown";

        Debug.Log($"🔎 Classification: {predictedLabel}, Index={predictedIndex}, Confidence={maxProb * 100:F2}%");

        // Clean up
        inputTensor.Dispose();
        outputTensor.Dispose();

        return predictedLabel;
    }

    Tensor PreprocessImage(Texture2D texture)
    {
        Texture2D resized = ResizeTexture(texture, imageSize, imageSize);

        // Convert to grayscale, normalized [-1, 1]
        float[] imageData = new float[imageSize * imageSize];
        for (int h = 0; h < imageSize; h++)
        {
            for (int w = 0; w < imageSize; w++)
            {
                Color pixel = resized.GetPixel(w, h);
                float gray = 0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b;
                float normalized = (gray - 0.5f) / 0.5f;
                imageData[h * imageSize + w] = normalized;
            }
        }

        return new Tensor(1, imageSize, imageSize, 1, imageData);
    }

    Tensor RunInference(Tensor inputTensor)
    {
        worker.Execute(inputTensor);
        return worker.PeekOutput();
    }

    Texture2D ResizeTexture(Texture2D texture, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 24);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.Blit(texture, rt);
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        return result;
    }

    Texture2D FlipVertical(Texture2D source)
    {
        Texture2D flipped = new Texture2D(source.width, source.height);
        for (int y = 0; y < source.height; y++)
        {
            for (int x = 0; x < source.width; x++)
            {
                flipped.SetPixel(x, source.height - 1 - y, source.GetPixel(x, y));
            }
        }
        flipped.Apply();
        return flipped;
    }

    float[] Softmax(float[] logits)
    {
        float maxLogit = logits.Max();
        float[] expLogits = logits.Select(l => Mathf.Exp(l - maxLogit)).ToArray();
        float sumExp = expLogits.Sum();
        return expLogits.Select(e => e / sumExp).ToArray();
    }

    void SaveAsPng(Texture2D screenshot, string folderPath, string filename)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        string filePath = Path.Combine(folderPath, filename);
        byte[] pngData = screenshot.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);
        Debug.Log($"💾 Saved screenshot as: {filePath}");
    }

    // -------------------------------------------------------------------
    // (B) Vehicle Movement Logic
    // -------------------------------------------------------------------
    void ApplyMovementCondition(string condition)
    {
        switch (condition)
        {
            case "Forward":
                MoveForward();
                break;
            case "Right":
                TurnRight();
                break;
            case "Left":
                TurnLeft();
                break;
            default:
                StopMovement();
                break;
        }
    }

    void MoveForward()
    {
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;
        rearLeftWheel.motorTorque = motorTorque;
        rearRightWheel.motorTorque = motorTorque;

        frontLeftWheel.steerAngle = 0f;
        frontRightWheel.steerAngle = 0f;
    }

    void TurnRight()
    {
        // Release brakes
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;

        // Base torque
        rearLeftWheel.motorTorque = motorTorque;
        rearRightWheel.motorTorque = motorTorque;

        // Steering
        frontLeftWheel.steerAngle = maxSteerAngle;
        frontRightWheel.steerAngle = maxSteerAngle;

        // Optional extra torque
        rearLeftWheel.motorTorque += 2 * motorTorque;
        rearRightWheel.motorTorque += 2 * motorTorque;
    }

    void TurnLeft()
    {
        // Release brakes
        rearLeftWheel.brakeTorque = 0f;
        rearRightWheel.brakeTorque = 0f;

        // Base torque
        rearLeftWheel.motorTorque = motorTorque;
        rearRightWheel.motorTorque = motorTorque;

        // Steering
        frontLeftWheel.steerAngle = -maxSteerAngle;
        frontRightWheel.steerAngle = -maxSteerAngle;

        // Optional extra torque
        rearLeftWheel.motorTorque += 2 * motorTorque;
        rearRightWheel.motorTorque += 2 * motorTorque;
    }

    void StopMovement()
    {
        rearLeftWheel.motorTorque = 0f;
        rearRightWheel.motorTorque = 0f;
        frontLeftWheel.steerAngle = 0f;
        frontRightWheel.steerAngle = 0f;

        // Optionally set some brake torque if you want a firm stop
        rearLeftWheel.brakeTorque = 500f;
        rearRightWheel.brakeTorque = 500f;
    }

    // Cleanup
    void OnDestroy()
    {
        if (worker != null) worker.Dispose();
    }
}
