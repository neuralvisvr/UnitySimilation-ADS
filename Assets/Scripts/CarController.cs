using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using TMPro;
using UnityEngine.UI;

public class CarController : MonoBehaviour
{
    // ------------------------------------------------
    // 1) ONNX Model + Classification
    // ------------------------------------------------
    [Header("Barracuda Model Settings")]
    public NNModel onnxModel;
    public int imageSize = 56; // Resize input images to match training
    public string[] classLabels = { "Class0", "Class1", "Class2" }; // Optional reference

    [Header("Camera & Capture Settings")]
    public Camera captureCamera;
    public int imageResolutionX = 56;
    public int imageResolutionY = 56;

    private IWorker worker;
    private Model runtimeModel;

    // ------------------------------------------------
    // 2) WheelColliders + Movement Control
    // ------------------------------------------------
    [Header("Vehicle Wheel Settings")]
    public WheelCollider frontRightWheel;
    public WheelCollider frontLeftWheel;
    public WheelCollider rearRightWheel;
    public WheelCollider rearLeftWheel;

    // Control variables, now private and controlled by UI sliders
    private float motorTorque = 90f;        // Base torque for wheel movement
    private float maxSteerAngle = 40f;       // Max steering angle in degrees
    private int classificationFrequency = 10; // Frames between classifications

    // ------------------------------------------------
    // 3) Autonomous Mode UI
    // ------------------------------------------------




    [Header("UI elements")]
    public Slider motorTorqueSlider;       // Slider for motorTorque
    public TMP_Text motorTorqueText;       // Text to display motorTorque value
    public Slider maxSteerAngleSlider;     // Slider for maxSteerAngle
    public TMP_Text maxSteerAngleText;     // Text to display maxSteerAngle value
    public Slider classificationFrequencySlider; // Slider for classificationFrequency
    public TMP_Text classificationFrequencyText; // Text to display classificationFrequency value
    public Button startStopButton;        // UI Button to toggle autonomous mode
    public Button resetButton;
    private bool isAutonomousMode = false;
    private int frameCount = 0;
    [Header("Classification UI")]
    public TMP_Text decisionText;  // Displays ↑, →, ←
    public TMP_Text realFrequencyText; // Displays real classification frequency

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    // For real classification frequency measurement
    private float classificationTimer = 0f;
    private int classificationCountWindow = 0;
    private float measureInterval = 2f; // Update frequency measurement every 2 seconds

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetCarPosition);
        }
        // 1) Initialize ONNX Model
        if (onnxModel == null)
        {
            Debug.LogError("❌ ONNX model is missing! Please assign it in the Inspector.");
            enabled = false;
            return;
        }
        runtimeModel = ModelLoader.Load(onnxModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);
        Debug.Log("✅ ONNX Model Loaded with GPU (Compute) backend.");

        // 2) Ensure a valid camera
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

        // 3) Set up the UI button
        if (startStopButton != null)
        {
            startStopButton.onClick.AddListener(ToggleAutonomousMode);
            UpdateUI();
        }
        else
        {
            Debug.LogWarning("⚠️ No startStopButton assigned. Cannot toggle autonomous mode via UI.");
        }

        // 4) Set up sliders with listeners and initial values
        motorTorqueSlider.minValue = 50f; // Minimum speed
        motorTorqueSlider.maxValue = 200f;  // Maximum speed
        maxSteerAngleSlider.minValue = 15f; // Minimum speed
        maxSteerAngleSlider.maxValue = 60f;  // Maximum speed
        classificationFrequencySlider.minValue = 1f; // Minimum speed
        classificationFrequencySlider.maxValue = 30f;  // Maximum speed


        motorTorqueSlider.onValueChanged.AddListener(OnMotorTorqueChanged);
        maxSteerAngleSlider.onValueChanged.AddListener(OnMaxSteerAngleChanged);
        classificationFrequencySlider.onValueChanged.AddListener(OnClassificationFrequencyChanged);

        // Initialize slider values (triggers listeners to update text)
        motorTorqueSlider.value = motorTorque;
        maxSteerAngleSlider.value = maxSteerAngle;
        classificationFrequencySlider.value = classificationFrequency;
    }
    public void ResetCarPosition()
{
    transform.position = initialPosition;
    transform.rotation = initialRotation;
    StopMovement();
}
    void Update()
    {
        if (!isAutonomousMode)
        {
            HandleManualControls();
            return;
        }
        // Adjust classification frequency dynamically based on time speed
        AdjustClassificationFrequency();
        // Run classification based on slider-defined frequency
        frameCount++;
        if (frameCount >= classificationFrequency)
        {
            frameCount = 0;
            ClassifyAndDrive();
        }

        // Measure real classification frequency (updates every 2 seconds)
        classificationTimer += Time.unscaledDeltaTime;  // Uses real-time, unaffected by time scale
        if (classificationTimer >= measureInterval)
        {
            float realFrequency = classificationCountWindow / classificationTimer;
            realFrequencyText.text = $"{realFrequency:0.00}/sec";

            classificationTimer = 0f;
            classificationCountWindow = 0;
        }
    }
    private void AdjustClassificationFrequency()
    {
        float timeSpeed = Time.timeScale;

        // Base classification frequency from slider
        int baseFrequency = (int)classificationFrequencySlider.value;

        // Increase frequency proportionally to time speed
        classificationFrequency = Mathf.Clamp((int)(baseFrequency / timeSpeed), 1, 120); // Limit to prevent excessive updates

        // Update UI text
        classificationFrequencyText.text = $"{classificationFrequency}";
    }


    /// <summary>
    /// Toggles the autonomous driving mode on/off.
    /// </summary>
    void ToggleAutonomousMode()
    {
        isAutonomousMode = !isAutonomousMode;

        if (!isAutonomousMode)
        {
            StopMovement();
        }

        UpdateUI();
    }

    /// <summary>
    /// Updates UI text and button to reflect the autonomous state.
    /// </summary>
    void UpdateUI()
    {


        if (startStopButton != null)
        {
            startStopButton.GetComponentInChildren<TMP_Text>().text = isAutonomousMode ? "Stop" : "Start";
        }
    }

    /// <summary>
    /// Captures a frame, runs inference, and applies movement accordingly.
    /// </summary>
    private void ClassifyAndDrive()
    {
        Texture2D frame = CaptureCameraFrame();
        string predictedLabel = ClassifyFrame(frame);

        classificationCountWindow++; // Track classification count for real frequency
        UpdateUIDecision(predictedLabel);
        ApplyMovementCondition(predictedLabel);

        Destroy(frame);
    }

    /// <summary>
    /// Captures a frame from the camera.
    /// </summary>
    private Texture2D CaptureCameraFrame()
    {
        RenderTexture rt = new RenderTexture(imageResolutionX, imageResolutionY, 24);
        captureCamera.targetTexture = rt;
        captureCamera.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(imageResolutionX, imageResolutionY, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, imageResolutionX, imageResolutionY), 0, 0);
        tex.Apply();

        // Cleanup
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        rt.Release();
        Destroy(rt);

        return tex;
    }

    private string ClassifyFrame(Texture2D frame)
    {
        Texture2D flipped = FlipVertical(frame);
        using var inputTensor = Preprocess(flipped);
        worker.Execute(inputTensor);
        Tensor output = worker.PeekOutput();

        float[] logits = output.AsFloats();
        float[] probs = Softmax(logits);

        int bestIndex = probs.ToList().IndexOf(probs.Max());
        output.Dispose();

        return bestIndex switch
        {
            0 => "Forward",
            1 => "Left",
            2 => "Right",
            _ => "Unknown"
        };
    }

    private Tensor Preprocess(Texture2D src)
    {
        Texture2D resized = ResizeTexture(src, imageSize, imageSize);

        float[] data = new float[imageSize * imageSize];
        for (int y = 0; y < imageSize; y++)
        {
            for (int x = 0; x < imageSize; x++)
            {
                Color p = resized.GetPixel(x, y);
                float gray = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                data[y * imageSize + x] = (gray - 0.5f) / 0.5f;
            }
        }

        return new Tensor(1, imageSize, imageSize, 1, data);
    }

    private Texture2D ResizeTexture(Texture2D tex, int width, int height)
    {
        RenderTexture rt = new RenderTexture(width, height, 24);
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        rt.Release();
        Destroy(rt);

        return result;
    }

    private Texture2D FlipVertical(Texture2D src)
    {
        Texture2D flipped = new Texture2D(src.width, src.height);
        for (int y = 0; y < src.height; y++)
        {
            for (int x = 0; x < src.width; x++)
            {
                flipped.SetPixel(x, src.height - 1 - y, src.GetPixel(x, y));
            }
        }
        flipped.Apply();
        return flipped;
    }

    void HandleManualControls()
    {
        if (Input.GetKey(KeyCode.UpArrow))
        {
            MoveForward();
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            TurnLeft();
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            TurnRight();
        }
        else
        {
            StopMovement();
        }
    }

    private float[] Softmax(float[] logits)
    {
        float maxLogit = logits.Max();
        float[] exps = logits.Select(l => Mathf.Exp(l - maxLogit)).ToArray();
        float sumExp = exps.Sum();
        return exps.Select(e => e / sumExp).ToArray();
    }

    private void ApplyMovementCondition(string label)
    {
        switch (label)
        {
            case "Forward": MoveForward(); break;
            case "Right": TurnRight(); break;
            case "Left": TurnLeft(); break;
            default: StopMovement(); break;
        }
    }
    private void UpdateUIDecision(string label)
    {
        Color randomColor = new Color(Random.value, Random.value, Random.value); // Generate random color

        switch (label)
        {
            case "Forward":
                decisionText.text = "↑";
                break;
            case "Right":
                decisionText.text = "→";
                break;
            case "Left":
                decisionText.text = "←";
                break;
            default:
                decisionText.text = "-";
                break;
        }

        decisionText.color = randomColor; // Assign the random color
        decisionText.fontSize = 100;
    }
    private void MoveForward() => SetWheelTorque(motorTorque, 0);
    private void TurnRight() => SetWheelTorque(3 * motorTorque, maxSteerAngle);
    private void TurnLeft() => SetWheelTorque(3 * motorTorque, -maxSteerAngle);
    private void StopMovement() => SetWheelTorque(0, 0);

    private void SetWheelTorque(float torque, float angle)
    {
        rearLeftWheel.motorTorque = rearRightWheel.motorTorque = torque;
        frontLeftWheel.steerAngle = frontRightWheel.steerAngle = angle;
    }

    // Slider listener methods
    private void OnMotorTorqueChanged(float value)
    {
        motorTorque = value;
        motorTorqueText.text = value.ToString("F2"); // Display with 2 decimal places
    }

    private void OnMaxSteerAngleChanged(float value)
    {
        maxSteerAngle = value;
        maxSteerAngleText.text = value.ToString("F2"); // Display with 2 decimal places
    }

    private void OnClassificationFrequencyChanged(float value)
    {
        int inverted = (int)(classificationFrequencySlider.maxValue - value + 1);
        classificationFrequency = inverted; // now a lower number means more frequent classification
        classificationFrequencyText.text = value.ToString("F0");
        AdjustClassificationFrequency();
    }

    private void OnDestroy() => worker?.Dispose();
}