using UnityEngine;

public class CameraBreathing : MonoBehaviour
{
    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.localPosition;
    }

    void Update()
    {
        float breathingOffset = Mathf.Sin(Time.time * 0.5f) * 0.3f;
        transform.localPosition = new Vector3(
            initialPosition.x,
            initialPosition.y + breathingOffset,
            initialPosition.z
        );
    }
}
