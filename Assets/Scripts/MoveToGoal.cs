using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class MoveToGoal : Agent
{
    [Header("Training Settings")]
    [SerializeField] private float maxSpeed = 8f;

    [Header("Reward Tuning")]
    [SerializeField] private float goalReward = 50f;
    [SerializeField] private float wallHitPenalty = -1.5f;
    [SerializeField] private float wallStayPenalty = -0.15f;
    [SerializeField] private float timePenalty = -0.002f;
    [SerializeField] private float stuckPenalty = -0.3f;

    Rigidbody2D rigidbody;
    Transform goalTransform;
    MazeGenerator mazeGenerator;

    public Vector3 initialAgentPosition = Vector3.zero;

    private int stepCount;
    private int maxSteps = 5000;
    private Vector3 lastPosition;
    private float lastDistanceToGoal;
    private int noProgressCounter = 0;
    private Vector2 lastDirection = Vector2.zero;
    private bool agentLost = false;

    public override void Initialize()
    {
        goalTransform = transform.parent.Find("Goal").transform;
        mazeGenerator = transform.parent.GetComponent<MazeGenerator>();
        rigidbody = GetComponent<Rigidbody2D>();
    }

    public override void OnEpisodeBegin()
    {
        if (mazeGenerator != null)
        {
            mazeGenerator.GenerateNewMaze();
        }

        // Reset physics
        rigidbody.linearVelocity = Vector2.zero;
        rigidbody.angularVelocity = 0f;

        // Reset tracking
        stepCount = 0;
        noProgressCounter = 0;
        lastPosition = transform.localPosition;
        lastDistanceToGoal = Vector3.Distance(transform.localPosition, goalTransform.localPosition);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Ray Perception Sensor adds observations automatically so we only need to add our custom observations here

        // Normalized direction to goal (2 values)
        Vector2 directionToGoal = (goalTransform.localPosition - transform.localPosition).normalized;
        sensor.AddObservation(directionToGoal);

        // Normalized distance to goal (1 value)
        float distance = Vector3.Distance(transform.localPosition, goalTransform.localPosition);
        sensor.AddObservation(distance / 100f); // Normalize based on expected max distance

        // Agent's normalized velocity (2 values)
        sensor.AddObservation(rigidbody.linearVelocity / maxSpeed);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        stepCount++;

        // Handle agent movement
        float moveX = actions.ContinuousActions[0];
        float moveY = actions.ContinuousActions[1];

        Vector2 inputDirection = new Vector2(moveX, moveY);
        if (inputDirection.magnitude >= 1f)
        {
            lastDirection = inputDirection.normalized;
        }
        Vector2 movement = lastDirection * maxSpeed;
        rigidbody.AddForce(movement);

        // Time penalty (helps w/ speed)
        AddReward(timePenalty);

        // Check every 50 steps if we've made progress, if not then
        float currentDistanceToGoal = Vector3.Distance(transform.localPosition, goalTransform.localPosition);
        if (stepCount % 50 == 0)
        {
            if (Mathf.Abs(currentDistanceToGoal - lastDistanceToGoal) < 1f)
            {
                noProgressCounter++;

                if (noProgressCounter >= 10)
                {
                    AddReward(stuckPenalty);
                    Debug.Log($"Stuck Penalty {stuckPenalty.ToString()}");
                    noProgressCounter = 0;
                }
            }
            else
            {
                noProgressCounter = 0;
            }

            lastDistanceToGoal = currentDistanceToGoal;
        }

        lastPosition = transform.localPosition;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
    }

    private void ChangeAgentColor(Color newColor)
    {
        GetComponent<SpriteRenderer>().color = newColor;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Goal"))
        {
            // Large reward for reaching goal
            AddReward(goalReward);
            Debug.Log($"Goal Reward {goalReward.ToString()}");

            // Bonus for reaching goal quickly
            float speedBonus = Mathf.Max(0, (maxSteps - stepCount) / (float)maxSteps) * 10f;
            AddReward(speedBonus);
            Debug.Log($"Speed Bonus {speedBonus.ToString()}");

            EndEpisode();
        }
    }

    /*private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(wallHitPenalty);
            Debug.Log($"Wall Hit Penalty {wallHitPenalty.ToString()}");
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(wallStayPenalty);
            Debug.Log($"Wall Stay Penalty {wallStayPenalty.ToString()}");
        }
    }*/
}