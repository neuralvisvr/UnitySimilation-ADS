using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.UI;

public class TrainingController : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public Button startTrainingButton;

    // Prefabs for visualizing spheres (train) and cubes (validation)
    public GameObject confusionMatrixPlane;
    public GameObject spherePrefab; // Shared for both loss and accuracy (train)
    public GameObject cubePrefab; // Shared for both loss and accuracy (validation)

    public Transform lossVisualizationParent;
    public Transform accuracyVisualizationParent;

    // Prefab for axis labels
    public GameObject axisLabelPrefab;

    public float xSpacing = 2.0f;  // Spacing between the epochs on the X-axis
    public float yScale = 10f;     // Scaling factor for the Y-axis

    // The URL of your FastAPI endpoint
    private string apiUrl = "http://127.0.0.1:8000/api/train";

    void Start()
    {
        startTrainingButton.onClick.AddListener(OnStartTrainingButtonClick);

        // Rotate the visualization parents 180 degrees around the Y-axis
        lossVisualizationParent.transform.rotation = Quaternion.Euler(0, 180, 0);
        accuracyVisualizationParent.transform.rotation = Quaternion.Euler(0, 180, 0);
    }

    void OnStartTrainingButtonClick()
    {
        StartCoroutine(SendPostRequest());
    }

    IEnumerator SendPostRequest()
    {
        UnityWebRequest request = UnityWebRequest.PostWwwForm(apiUrl, "");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
            statusText.text = "Error: " + request.error;
        }
        else
        {
            var responseText = request.downloadHandler.text;
            var responseData = JsonUtility.FromJson<APIResponse>(responseText);
            statusText.text = responseData.status;

            // Visualize loss and accuracy
            VisualizeMetrics(responseData.train_loss_history, responseData.val_loss_history, lossVisualizationParent, "Loss", Color.green, Color.red);
            VisualizeMetrics(responseData.train_acc_history, responseData.val_acc_history, accuracyVisualizationParent, "Accuracy", Color.blue, Color.yellow);

            // Load and visualize confusion matrix
            LoadImage(responseData.confusion_matrix_plot, confusionMatrixPlane);
        }
    }

    // Compact function to visualize both train/val metrics (loss or accuracy) in 3D, add axes, and labels
    void VisualizeMetrics(List<float> trainValues, List<float> valValues, Transform parent, string labelName, Color trainColor, Color valColor)
    {
        // Clear previous visualizations
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }

        float yMax = Mathf.Max(Mathf.Max(trainValues.ToArray()), Mathf.Max(valValues.ToArray()));
        float yMin = Mathf.Min(Mathf.Min(trainValues.ToArray()), Mathf.Min(valValues.ToArray()));

        if (Mathf.Approximately(yMax, yMin))
        {
            yMax += 1f;
        }

        // Create spheres for training values and cubes for validation values
        for (int i = 0; i < trainValues.Count; i++)
        {
            // Train values (Spheres)
            GameObject sphere = Instantiate(spherePrefab, parent);
            float trainXPosition = (i + 1) * xSpacing;
            float trainYPosition = ((trainValues[i] - yMin) / (yMax - yMin)) * yScale;
            sphere.transform.localPosition = new Vector3(trainXPosition, trainYPosition, 0);

            // Change color of sphere based on the value
            Renderer sphereRenderer = sphere.GetComponent<Renderer>();
            sphereRenderer.material.color = Color.Lerp(trainColor, Color.red, trainValues[i] / yMax);

            // Validation values (Cubes)
            if (i < valValues.Count)
            {
                GameObject cube = Instantiate(cubePrefab, parent);
                float valXPosition = (i + 1) * xSpacing;
                float valYPosition = ((valValues[i] - yMin) / (yMax - yMin)) * yScale;
                cube.transform.localPosition = new Vector3(valXPosition, valYPosition, 0f);

                // Change color of cube based on the value
                Renderer cubeRenderer = cube.GetComponent<Renderer>();
                cubeRenderer.material.color = Color.Lerp(valColor, Color.yellow, valValues[i] / yMax);
            }
        }

        AddAxesAndLabels(parent, trainValues.Count, yMax, yMin, labelName);
    }

    // Function to add axes and labels
    void AddAxesAndLabels(Transform parent, int epochCount, float yMax, float yMin, string labelName)
    {
        float axisLength = (epochCount - 1) * xSpacing;

        // Add labels for X-axis (Epochs)
        for (int i = 1; i <= epochCount; i++)
        {
            float xPos = i * xSpacing;
            CreateLabel(parent, new Vector3(xPos, -2f, 0), i.ToString());
        }

        float xMiddlePos = (epochCount - 1) * xSpacing / 2.0f;  // Calculate the middle position of the axis
        CreateLabel(parent, new Vector3(xMiddlePos, -3.5f, 0), "Epoch");  // Position the "Epoch" text below the numbers

        // Add labels for Y-axis (Loss/Accuracy values)
        CreateLabel(parent, new Vector3(-2f, yScale + 2f, 0), labelName);

        int numYLabels = 5;
        for (int i = 0; i <= numYLabels; i++)
        {
            float normalizedValue = (float)i / numYLabels;
            float yPos = normalizedValue * yScale;
            float yValue = Mathf.Lerp(yMin, yMax, normalizedValue);
            CreateLabel(parent, new Vector3(-2f, yPos, 0), yValue.ToString("F2"));
        }
    }

    // Helper function to create labels at the given position relative to the parent
    void CreateLabel(Transform parent, Vector3 localPosition, string text)
    {
        GameObject label = Instantiate(axisLabelPrefab, parent);
        label.transform.localPosition = localPosition;  // Use localPosition for relative positioning
        TextMeshPro tmp = label.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 15;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void LoadImage(string base64String, GameObject targetPlane)
    {
        byte[] imageBytes = System.Convert.FromBase64String(base64String);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageBytes);
        targetPlane.GetComponent<Renderer>().material.mainTexture = texture;
    }

    // Class to map the API response
    [System.Serializable]
    public class APIResponse
    {
        public string status;
        public List<float> train_loss_history;
        public List<float> val_loss_history;
        public List<float> train_acc_history;
        public List<float> val_acc_history;
        public string confusion_matrix_plot;
    }
}
