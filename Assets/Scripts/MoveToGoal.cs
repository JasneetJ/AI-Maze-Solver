using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using TMPro;

public class MoveToGoal : Agent
{
    [HideInInspector] public Vector3 initialAgentPosition;

    [Header("Movement")]
    [SerializeField] public float moveSpeed = 4f;

    [Header("Rewards")]
    [SerializeField] float goalReward = 50f;
    [SerializeField] float timePenalty = -0.001f;

    [Header("Debug")]
    [SerializeField] TextMeshProUGUI debugText;

    Transform goalTransform;
    Rigidbody2D rb;
    StaticMazeBuilder staticMazeBuilder;

    int episodeCount = 0;
    int successCount = 0;
    int wallHitCount = 0;
    float episodeReward = 0f;
    float lastEpisodeReward = 0f;
    int stepCount = 0;

    Vector2 lastMoveDir = Vector2.zero;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        goalTransform = transform.parent.Find("Goal");
        staticMazeBuilder = transform.parent.GetComponent<StaticMazeBuilder>();
    }

    public override void OnEpisodeBegin()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        lastEpisodeReward = episodeReward;
        episodeReward = 0f;
        wallHitCount = 0;
        stepCount = 0;
        episodeCount++;

        UpdateDebugText();
        staticMazeBuilder.RespawnEntities();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (goalTransform == null)
        {
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
            sensor.AddObservation(Vector2.zero);
            return;
        }

        Vector2 agentPos = transform.localPosition;
        Vector2 goalPos = goalTransform.localPosition;

        sensor.AddObservation(agentPos.normalized);
        sensor.AddObservation(goalPos.normalized);
        sensor.AddObservation((goalPos - agentPos).normalized);
    }

    private void FixedUpdate()
    {
        if (rb.linearVelocity.magnitude < 0.1f) return;

        Vector2 moveDir = rb.linearVelocity.normalized;
        float speed = rb.linearVelocity.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, moveDir, 0.5f);

        if (hit.collider != null)
        {
            float dot = Vector2.Dot(moveDir, hit.normal * -1f);
            float penalty = -0.005f * dot * (speed / moveSpeed);
            AddReward(penalty);
            episodeReward += penalty;

            if (dot > 0.9f) wallHitCount++;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAction = actions.DiscreteActions[0];
        Vector2 moveDir = lastMoveDir;

        switch (moveAction)
        {
            case 1: moveDir = Vector2.up; break;
            case 2: moveDir = Vector2.down; break;
            case 3: moveDir = Vector2.left; break;
            case 4: moveDir = Vector2.right; break;
            case 0: moveDir = lastMoveDir; break;
        }

        lastMoveDir = moveDir;
        rb.linearVelocity = moveDir * moveSpeed;

        AddReward(timePenalty);
        episodeReward += timePenalty;
        stepCount++;

        if (stepCount % 10 == 0)
            UpdateDebugText();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.W)) d[0] = 1;
        else if (Input.GetKey(KeyCode.S)) d[0] = 2;
        else if (Input.GetKey(KeyCode.A)) d[0] = 3;
        else if (Input.GetKey(KeyCode.D)) d[0] = 4;
        else d[0] = 0;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Goal"))
        {
            successCount++;
            episodeReward += goalReward;
            AddReward(goalReward);
            UpdateDebugText();
            EndEpisode();
        }
    }

    private void UpdateDebugText()
    {
        if (debugText == null) return;

        float successRate = 0f;
        if (episodeCount > 0)
        {
            successRate = (float)successCount / episodeCount * 100f;
        }
        float distToGoal = Vector2.Distance(transform.localPosition, goalTransform.localPosition);

        debugText.text =
            $"<b>{transform.parent.name}</b>\n" +
            $"Episode: {episodeCount}\n" +
            $"Success: {successCount} ({successRate}%)\n" +
            $"Last Reward: {lastEpisodeReward}\n" +
            $"Cur Reward: {episodeReward}\n" +
            $"Wall Hits: {wallHitCount}\n" +
            $"Steps: {stepCount}\n" +
            $"DistToGoal: {distToGoal}";
    }
}