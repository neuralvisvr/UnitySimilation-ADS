using UnityEngine;

public class WheelRotation : MonoBehaviour
{
    public float rotationSpeed = 200f;  // Adjust rotation speed as needed

    void Update()
    {
        // Rotate the wheel around its local X-axis
        transform.Rotate(- Vector3.up * rotationSpeed * Time.deltaTime);
    }
}
