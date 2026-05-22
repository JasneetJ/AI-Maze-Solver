using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using TMPro;

public class MoveToGoal : Agent
{
    [HideInInspector] public Vector3 initialAgentPosition;

    [Header("Agent Settings")]
    [SerializeField] public float moveSpeed = 10f;

    [Header("Rewards")]
    [SerializeField] float goalReward = 1.0f;
    [SerializeField] float stepPenalty = -0.005f;
    [SerializeField] float wallPenalty = -0.005f;

    [Header("Debug")]
    [SerializeField] TextMeshProUGUI debugText;
    [SerializeField] bool showLocalVision = false;

    MazeGenerator mazeGenerator;

    int episodeCount = 0;
    int successCount = 0;
    int wallHitCount = 0;
    float episodeReward = 0f;
    float lastEpisodeReward = 0f;
    int stepCount = 0;
    int stuckTurns = 0;

    Vector3 targetVisualPosition;
    [Header("Visuals")]
    [SerializeField] float smoothMoveSpeed = 25f;

    public override void Initialize()
    {
        mazeGenerator = transform.parent.GetComponent<MazeGenerator>();
    }

    public override void OnEpisodeBegin()
    {
        lastEpisodeReward = episodeReward;
        episodeReward = 0f;
        wallHitCount = 0;
        stepCount = 0;
        stuckTurns = 0;
        episodeCount++;

        UpdateDebugText();
        
        mazeGenerator.GenerateNewMaze();

        MaxStep = mazeGenerator.rows * mazeGenerator.cols * 25;

        mazeGenerator.visitedGrid[mazeGenerator.agentTileRow, mazeGenerator.agentTileCol] = true;

        targetVisualPosition = transform.localPosition;
    }

    public void SyncVisualPosition()
    {
        targetVisualPosition = transform.localPosition;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (mazeGenerator == null || mazeGenerator.tileGrid == null)
        {
            return;
        }

        var localObs = mazeGenerator.GetLocalObservation(mazeGenerator.agentTileRow, mazeGenerator.agentTileCol, 2);
        foreach (var obs in localObs)
        {
            sensor.AddObservation(obs);
        }

        float relativeGoalX = (float)(mazeGenerator.goalTileCol - mazeGenerator.agentTileCol) / mazeGenerator.maxTileCols;
        float relativeGoalY = (float)(mazeGenerator.goalTileRow - mazeGenerator.agentTileRow) / mazeGenerator.maxTileRows;
        
        float dist = Vector2.Distance(new Vector2(mazeGenerator.agentTileCol, mazeGenerator.agentTileRow), new Vector2(mazeGenerator.goalTileCol, mazeGenerator.goalTileRow));
        float maxDist = Mathf.Sqrt(Mathf.Pow(mazeGenerator.maxTileCols * 2, 2) + Mathf.Pow(mazeGenerator.maxTileRows * 2, 2));

        sensor.AddObservation(relativeGoalX);
        sensor.AddObservation(relativeGoalY);
        sensor.AddObservation(dist / maxDist);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int moveAction = actions.DiscreteActions[0];

        if (moveAction == -1) return;

        if (stuckTurns > 0)
        {
            stuckTurns--;
            AddReward(stepPenalty);
            episodeReward += stepPenalty;
            stepCount++;
            if (stepCount % 10 == 0) UpdateDebugText();
            return;
        }

        int targetRow = mazeGenerator.agentTileRow;
        int targetCol = mazeGenerator.agentTileCol;

        int oldDist = -1;
        if (mazeGenerator != null && mazeGenerator.tileGrid != null)
        {
            oldDist = mazeGenerator.GetShortestPathLength(
                mazeGenerator.agentTileRow, mazeGenerator.agentTileCol,
                mazeGenerator.goalTileRow, mazeGenerator.goalTileCol);
        }

        int rDir = 0, cDir = 0;
        bool goalSwitched = false;

        switch (moveAction)
        {
            case 0: rDir = -1; break;
            case 1: rDir = 1; break;
            case 2: cDir = -1; break;
            case 3: cDir = 1; break;
        }

        if (rDir != 0 || cDir != 0)
        {
            targetRow += rDir;
            targetCol += cDir;

            if (targetRow >= 0 && targetRow < mazeGenerator.tileGrid.GetLength(0) && targetCol >= 0 && targetCol < mazeGenerator.tileGrid.GetLength(1))
            {
                int tileType = mazeGenerator.tileGrid[targetRow, targetCol];

                if (tileType == 1)
                {
                    wallHitCount++;
                    AddReward(wallPenalty);
                    episodeReward += wallPenalty;
                }
                else
                {
                    while (mazeGenerator.tileGrid[targetRow, targetCol] == 5)
                    {
                        int nextRow = targetRow + rDir;
                        int nextCol = targetCol + cDir;

                        if (nextRow >= 0 && nextRow < mazeGenerator.tileGrid.GetLength(0) && nextCol >= 0 && nextCol < mazeGenerator.tileGrid.GetLength(1))
                        {
                            int nextTileType = mazeGenerator.tileGrid[nextRow, nextCol];
                            if (nextTileType == 1)
                            {
                                break;
                            }
                            else
                            {
                                targetRow = nextRow;
                                targetCol = nextCol;
                                if (nextTileType == 4) break;
                                if (nextTileType == 2) break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    mazeGenerator.agentTileRow = targetRow;
                    mazeGenerator.agentTileCol = targetCol;

                    Vector3 newPos = mazeGenerator.GetWorldPositionFromTile(targetRow, targetCol);
                    newPos.z = 1f;
                    targetVisualPosition = newPos;
                    mazeGenerator.visitedGrid[targetRow, targetCol] = true;

                    int finalTileType = mazeGenerator.tileGrid[targetRow, targetCol];
                    if (finalTileType == 2)
                    {
                        if (mazeGenerator.enableKeyDoor && !mazeGenerator.hasKey)
                        {
                            mazeGenerator.hasKey = true;
                            goalSwitched = true;
                            
                            AddReward(goalReward * 0.5f);
                            episodeReward += goalReward * 0.5f;
                            
                            mazeGenerator.tileGrid[mazeGenerator.keyTileRow, mazeGenerator.keyTileCol] = 0;
                            if (mazeGenerator.keyObject != null) mazeGenerator.keyObject.SetActive(false);
                            
                            mazeGenerator.tileGrid[mazeGenerator.doorTileRow, mazeGenerator.doorTileCol] = 0;
                            if (mazeGenerator.doorObject != null) mazeGenerator.doorObject.SetActive(false);
                            
                            mazeGenerator.tileGrid[mazeGenerator.finalGoalRow, mazeGenerator.finalGoalCol] = 2;
                            mazeGenerator.goalTileRow = mazeGenerator.finalGoalRow;
                            mazeGenerator.goalTileCol = mazeGenerator.finalGoalCol;
                            
                            UpdateDebugText();
                        }
                        else
                        {
                            successCount++;
                            AddReward(goalReward);
                            episodeReward += goalReward;
                            UpdateDebugText();
                            EndEpisode();
                            return;
                        }
                    }
                    else if (finalTileType == 4)
                    {
                        stuckTurns = 1;
                    }
                }
            }
            else
            {
                wallHitCount++;
                AddReward(wallPenalty);
                episodeReward += wallPenalty;
            }
        }

        int newDist = -1;
        if (mazeGenerator != null && mazeGenerator.tileGrid != null)
        {
            newDist = mazeGenerator.GetShortestPathLength(
                mazeGenerator.agentTileRow, mazeGenerator.agentTileCol,
                mazeGenerator.goalTileRow, mazeGenerator.goalTileCol);
        }

        AddReward(stepPenalty);
        episodeReward += stepPenalty;

        if (!goalSwitched && oldDist != -1 && newDist != -1 && (rDir != 0 || cDir != 0))
        {
            if (newDist < oldDist)
            {
                AddReward(0.01f);
                episodeReward += 0.01f;
            }
            else if (newDist > oldDist)
            {
                AddReward(-0.01f);
                episodeReward -= 0.01f;
            }
        }

        stepCount++;

        if (stepCount % 10 == 0)
            UpdateDebugText();
    }

    int pendingAction = -1;

    private void Update()
    {
        if (transform.localPosition != targetVisualPosition)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetVisualPosition, Time.deltaTime * smoothMoveSpeed);
        }

        if (Input.GetKeyDown(KeyCode.W)) pendingAction = 0;
        else if (Input.GetKeyDown(KeyCode.S)) pendingAction = 1;
        else if (Input.GetKeyDown(KeyCode.A)) pendingAction = 2;
        else if (Input.GetKeyDown(KeyCode.D)) pendingAction = 3;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        d[0] = pendingAction;
        pendingAction = -1;
    }

    private void UpdateDebugText()
    {
        if (debugText == null) return;

        float successRate = 0f;
        if (episodeCount > 0)
        {
            successRate = (float)successCount / episodeCount * 100f;
        }
        
        int distToGoal = -1;
        if (mazeGenerator != null && mazeGenerator.tileGrid != null)
        {
            distToGoal = mazeGenerator.GetShortestPathLength(
                mazeGenerator.agentTileRow, mazeGenerator.agentTileCol,
                mazeGenerator.goalTileRow, mazeGenerator.goalTileCol);
        }

        debugText.text =
            $"<b>{transform.parent.name}</b>\n" +
            $"Episode: {episodeCount}\n" +
            $"Success: {successCount} ({successRate:F2}%)\n" +
            $"Last Reward: {lastEpisodeReward:F2}\n" +
            $"Cur Reward: {episodeReward:F2}\n" +
            $"Wall Hits: {wallHitCount}\n" +
            $"Steps: {stepCount}\n" +
            $"DFS Dist: {distToGoal}";
    }

    private void OnDrawGizmos()
    {
        if (!showLocalVision || !Application.isPlaying) return;
        if (mazeGenerator == null || mazeGenerator.tileGrid == null) return;

        Gizmos.matrix = mazeGenerator.transform.localToWorldMatrix;
        
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f); 
        
        Vector3 agentCenter = mazeGenerator.GetWorldPositionFromTile(mazeGenerator.agentTileRow, mazeGenerator.agentTileCol);
        agentCenter.z = 0f; 
        
        float visionWidth = mazeGenerator.TileSize * 2 * 5;
        Vector3 size = new Vector3(visionWidth, visionWidth, 0.1f);

        Gizmos.DrawWireCube(agentCenter, size);
    }
}