using UnityEngine;

public class PhysicsZones : MonoBehaviour
{
    [Header("Objects")]
    GameObject stickyZone;
    GameObject slipperyZone;

    [Header("Settings")]
    [Range(1f, 10f)] float stickIntensity;
    [Range(1f, 10f)] float slipIntensity;

    float originalMoveSpeed = 0f;
    MoveToGoal moveToGoal;

    private void Start()
    {
        moveToGoal = FindAnyObjectByType<MoveToGoal>();
        originalMoveSpeed = moveToGoal.moveSpeed;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        switch (collision.tag)
        {
            case "Sticky Zone":
                moveToGoal.moveSpeed /= stickIntensity;
                break;
            case "Slippery Zone":
                moveToGoal.moveSpeed *= slipIntensity;
                break;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        moveToGoal.moveSpeed = originalMoveSpeed;
    }
}
