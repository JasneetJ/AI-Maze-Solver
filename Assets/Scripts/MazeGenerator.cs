using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [Header("Maze Components")]
    [SerializeField] public GameObject wallPrefab;

    [Header("Key-Door System")]
    [SerializeField] public bool enableKeyDoor = true;
    [SerializeField] [Range(0f, 1f)] public float keyDoorChance = 0.0f;
    [SerializeField] GameObject keyPrefab;

    [HideInInspector] public bool hasKey = false;
    [HideInInspector] public int keyTileRow;
    [HideInInspector] public int keyTileCol;
    [HideInInspector] public int doorTileRow;
    [HideInInspector] public int doorTileCol;
    [HideInInspector] public int finalGoalRow;
    [HideInInspector] public int finalGoalCol;
    [HideInInspector] public GameObject keyObject;
    [HideInInspector] public GameObject doorObject;

    [Header("Traps")]
    [SerializeField] [Range(0f, 1f)] public float trapChance = 0.10f;

    [Header("Maze Settings")]
    [SerializeField] public int maxRows = 15;
    [SerializeField] public int maxCols = 15;
    [SerializeField] public int rows = 5;
    [SerializeField] public int cols = 5;
    [SerializeField] float cellSize = 10f;

    float wallThickness;

    Cell[,] grid; 
    GameObject wallContainer;
    Transform agentTransform;
    Transform goalTransform;

    public int[,] tileGrid; // Each tile has int type (e.g., wall, goal, empty)
    public bool[,] visitedGrid;

    public int agentTileRow;
    public int agentTileCol;
    public int goalTileRow;
    public int goalTileCol;

    public bool isEmptyRoom = false;
    public int startTileRow;
    public int startTileCol;

    public int maxTileRows
    {
        get { return 2 * maxRows + 1; }
    }
    public int maxTileCols
    {
        get { return 2 * maxCols + 1; }
    }

    public float TileSize
    {
        get { return cellSize / 2f; }
    }

    private class Cell
    {
        public int R, C;
        public bool Visited = false;
        public bool[] Walls = { true, true, true, true };

        public Cell(int r, int c)
        {
            R = r;
            C = c;
        }
    }

    private void Start()
    {
        if (wallPrefab != null)
        {
            wallThickness = wallPrefab.transform.localScale.y;
        }
        else
        {
            wallThickness = 1f;
        }

        agentTransform = transform.Find("Agent");
        goalTransform = transform.Find("Goal");

        Transform existingContainer = transform.Find("WallContainer");
        if (existingContainer != null)
        {
            wallContainer = existingContainer.gameObject;
        }

        GenerateNewMaze();
    }

    public void GenerateNewMaze()
    {
        if (Unity.MLAgents.Academy.IsInitialized)
        {
            float currentMazeSize = Unity.MLAgents.Academy.Instance.EnvironmentParameters.GetWithDefault("maze_size", rows);
            rows = (int)currentMazeSize;
            cols = (int)currentMazeSize;

            float emptyParam = Unity.MLAgents.Academy.Instance.EnvironmentParameters.GetWithDefault("is_empty_room", 0f);
            isEmptyRoom = (emptyParam > 0.5f);

            if (rows <= 3) { trapChance = 0.0f; keyDoorChance = 0.0f; }
            else if (rows == 4) { trapChance = 0.01f; keyDoorChance = 0.0f; }
            else if (rows == 5) { trapChance = 0.02f; keyDoorChance = 0.10f; }
            else if (rows == 7) { trapChance = 0.05f; keyDoorChance = 0.30f; }
            else if (rows == 10) { trapChance = 0.08f; keyDoorChance = 0.60f; }
            else if (rows == 12) { trapChance = 0.10f; keyDoorChance = 0.80f; }
            else if (rows >= 15) { trapChance = 0.12f; keyDoorChance = 1.00f; }
        }

        int mazePoolSize = 0;
        if (Unity.MLAgents.Academy.IsInitialized)
        {
            mazePoolSize = (int)Unity.MLAgents.Academy.Instance.EnvironmentParameters.GetWithDefault("maze_pool_size", 0f);
        }

        Random.State oldState = Random.state;
        if (mazePoolSize > 0)
        {
            int seed = Random.Range(0, mazePoolSize);
            Random.InitState(seed);
        }

        if (rows > maxRows) rows = maxRows;
        if (cols > maxCols) cols = maxCols;

        DestroyExistingMaze();
        InitializeGrid();

        if (isEmptyRoom)
        {
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    grid[r, c].Visited = true;
                    grid[r, c].Walls = new bool[] { 
                        r == 0,
                        c == cols - 1,
                        r == rows - 1,
                        c == 0
                    };
                }
            }
        }
        else
        {
            RunRecursiveBacktracker();
        }
        
        BuildTileGrid();
        InstantiateMazeObjects();

        if (mazePoolSize > 0)
        {
            Random.state = oldState;
        }
    }

    private void DestroyExistingMaze()
    {
        if (wallContainer != null)
        {
            Destroy(wallContainer);
            wallContainer = null;
        }
    }

    private void InitializeGrid()
    {
        grid = new Cell[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                grid[r, c] = new Cell(r, c);
            }
        }

        if (wallContainer == null)
        {
            wallContainer = new GameObject("WallContainer");
            wallContainer.transform.parent = this.transform;
            wallContainer.transform.localPosition = Vector3.zero;
        }
    }

    private void RunRecursiveBacktracker()
    {
        Stack<Cell> stack = new Stack<Cell>();
        Cell current = grid[0, 0];
        current.Visited = true;
        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            Cell next = GetUnvisitedNeighbor(current, out int wallToBreak, out int neighborWallToBreak);

            if (next != null)
            {
                current.Walls[wallToBreak] = false;
                next.Walls[neighborWallToBreak] = false;

                next.Visited = true;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    private Cell GetUnvisitedNeighbor(Cell cell, out int currentWallIndex, out int neighborWallIndex)
    {
        List<(int rOffset, int cOffset, int cWall, int nWall)> candidates = new List<(int, int, int, int)>
        {
            (-1, 0, 0, 2),
            (0, 1, 1, 3),
            (1, 0, 2, 0),
            (0, -1, 3, 1)
        };

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        currentWallIndex = -1;
        neighborWallIndex = -1;

        foreach (var (rOffset, cOffset, cWall, nWall) in candidates)
        {
            int nextR = cell.R + rOffset;
            int nextC = cell.C + cOffset;

            if (nextR >= 0 && nextR < rows && nextC >= 0 && nextC < cols)
            {
                Cell neighbor = grid[nextR, nextC];
                if (!neighbor.Visited)
                {
                    currentWallIndex = cWall;
                    neighborWallIndex = nWall;
                    return neighbor;
                }
            }
        }
        return null;
    }

    private void BuildTileGrid()
    {
        int tileRows = 2 * rows + 1;
        int tileCols = 2 * cols + 1;
        tileGrid = new int[tileRows, tileCols];
        visitedGrid = new bool[tileRows, tileCols];

        for (int r = 0; r < tileRows; r++)
            for (int c = 0; c < tileCols; c++)
                tileGrid[r, c] = 1;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int tr = 2 * r + 1;
                int tc = 2 * c + 1;
                tileGrid[tr, tc] = 0;

                Cell cell = grid[r, c];
                if (!cell.Walls[0]) tileGrid[tr - 1, tc] = 0;
                if (!cell.Walls[1]) tileGrid[tr, tc + 1] = 0;
                if (!cell.Walls[2]) tileGrid[tr + 1, tc] = 0;
                if (!cell.Walls[3]) tileGrid[tr, tc - 1] = 0;
            }
        }
    }

    public Vector3 GetWorldPositionFromTile(int tr, int tc)
    {
        float mazeWidth = cols * cellSize;
        float mazeHeight = rows * cellSize;
        Vector3 startOffset = new Vector3(-mazeWidth / 2f, mazeHeight / 2f, 1);
        return startOffset + new Vector3(tc * TileSize, -tr * TileSize, 0);
    }

    private void InstantiateMazeObjects()
    {
        if (wallContainer == null) return;

        float mazeWidth = cols * cellSize;
        float mazeHeight = rows * cellSize;
        Vector3 cellStartOffset = new Vector3(-mazeWidth / 2f + cellSize / 2f, mazeHeight / 2f - cellSize / 2f, 1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Cell cell = grid[r, c];
                Vector3 cellCenter = cellStartOffset + new Vector3(c * cellSize, -r * cellSize, 0);

                if (cell.Walls[0])
                {
                    Vector3 position = cellCenter + new Vector3(0, cellSize / 2f, 0);
                    InstantiateWall(position, Quaternion.identity);
                }

                if (cell.Walls[1])
                {
                    Vector3 position = cellCenter + new Vector3(cellSize / 2f, 0, 0);
                    InstantiateWall(position, Quaternion.Euler(0, 0, 90));
                }

                if (r == (rows - 1) && cell.Walls[2])
                {
                    Vector3 position = cellCenter + new Vector3(0, -cellSize / 2f, 0);
                    InstantiateWall(position, Quaternion.identity);
                }

                if (c == 0 && cell.Walls[3])
                {
                    Vector3 position = cellCenter + new Vector3(-cellSize / 2f, 0, 0);
                    InstantiateWall(position, Quaternion.Euler(0, 0, 90));
                }
            }
        }

        InstantiateBoundaryWalls(mazeWidth, mazeHeight, cellStartOffset);

        List<Vector2Int> emptyTiles = new List<Vector2Int>();
        int tileRows = 2 * rows + 1;
        int tileCols = 2 * cols + 1;
        for (int r = 1; r < tileRows - 1; r++)
        {
            for (int c = 1; c < tileCols - 1; c++)
            {
                if (tileGrid[r, c] == 0) emptyTiles.Add(new Vector2Int(r, c));
            }
        }

        if (emptyTiles.Count > 1 && agentTransform != null && goalTransform != null)
        {
            for (int i = 0; i < emptyTiles.Count; i++)
            {
                Vector2Int temp = emptyTiles[i];
                int randomIndex = Random.Range(i, emptyTiles.Count);
                emptyTiles[i] = emptyTiles[randomIndex];
                emptyTiles[randomIndex] = temp;
            }

            startTileRow = emptyTiles[0].x;
            startTileCol = emptyTiles[0].y;
            agentTileRow = startTileRow;
            agentTileCol = startTileCol;
            
            Vector3 agentPos = GetWorldPositionFromTile(agentTileRow, agentTileCol);
            agentPos.z = 1;
            
            agentTransform.gameObject.SetActive(true);
            agentTransform.localPosition = agentPos;

            MoveToGoal agentComponent = agentTransform.GetComponent<MoveToGoal>();
            if (agentComponent != null)
            {
                agentComponent.initialAgentPosition = agentPos;
            }

            int[,] distances = new int[tileRows, tileCols];
            
            for (int r = 0; r < tileRows; r++)
            {
                for (int c = 0; c < tileCols; c++)
                {
                    distances[r, c] = -1;
                }
            }

            Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();
            bfsQueue.Enqueue(new Vector2Int(startTileRow, startTileCol));
            distances[startTileRow, startTileCol] = 0;
            
            // Direction arrays for checking neighbor tiles (Up, Down, Left, Right)
            int[] rowDirections = { -1, 1, 0, 0 };
            int[] colDirections = { 0, 0, -1, 1 };
            
            int maximumDistanceFound = 0;
            
            while (bfsQueue.Count > 0)
            {
                Vector2Int currentTile = bfsQueue.Dequeue();
                
                // Check all 4 neighboring tiles
                for (int i = 0; i < 4; i++)
                {
                    int neighborRow = currentTile.x + rowDirections[i];
                    int neighborCol = currentTile.y + colDirections[i];
                    
                    // If the neighbor is an empty tile and hasn't been visited yet
                    if (tileGrid[neighborRow, neighborCol] == 0 && distances[neighborRow, neighborCol] == -1)
                    {
                        distances[neighborRow, neighborCol] = distances[currentTile.x, currentTile.y] + 1;
                        maximumDistanceFound = Mathf.Max(maximumDistanceFound, distances[neighborRow, neighborCol]);
                        bfsQueue.Enqueue(new Vector2Int(neighborRow, neighborCol));
                    }
                }
            }

            int minimumGoalDistance = Mathf.Max(1, (int)(maximumDistanceFound * 0.5f));
            int maximumGoalDistance = Mathf.Max(1, (int)(maximumDistanceFound * 0.85f));
            
            List<Vector2Int> candidateGoals = new List<Vector2Int>();
            
            foreach (Vector2Int tile in emptyTiles)
            {
                int distanceFromStart = distances[tile.x, tile.y];
                
                bool isFarEnough = distanceFromStart >= minimumGoalDistance;
                bool isNotTooFar = distanceFromStart <= maximumGoalDistance;
                bool isNotStartTile = (tile.x != startTileRow || tile.y != startTileCol);
                
                if (isFarEnough && isNotTooFar && isNotStartTile)
                {
                    candidateGoals.Add(tile);
                }
            }
            
            if (candidateGoals.Count > 0) {
                Vector2Int chosen = candidateGoals[Random.Range(0, candidateGoals.Count)];
                finalGoalRow = chosen.x;
                finalGoalCol = chosen.y;
            } else {
                finalGoalRow = emptyTiles[emptyTiles.Count - 1].x;
                finalGoalCol = emptyTiles[emptyTiles.Count - 1].y;
            }
            
            Vector3 goalPos = GetWorldPositionFromTile(finalGoalRow, finalGoalCol);
            goalPos.z = 1;
            goalTransform.gameObject.SetActive(true);
            goalTransform.localPosition = goalPos;

            hasKey = false;
            bool setupKeyDoor = false;

            if (enableKeyDoor && !isEmptyRoom && Random.value < keyDoorChance)
            {
                List<Vector2Int> path = GetShortestPathTiles(startTileRow, startTileCol, finalGoalRow, finalGoalCol);
                if (path != null && path.Count > 3)
                {
                    List<int> validDoorIndices = new List<int>();
                    for (int i = 1; i < path.Count - 1; i++)
                    {
                        if ((path[i].x + path[i].y) % 2 == 1) validDoorIndices.Add(i);
                    }
                    
                    if (validDoorIndices.Count > 0)
                    {
                        int doorIndex = validDoorIndices[Random.Range(0, validDoorIndices.Count)];
                        Vector2Int doorPos = path[doorIndex];
                        
                        doorTileRow = doorPos.x;
                        doorTileCol = doorPos.y;
                        tileGrid[doorTileRow, doorTileCol] = 1;
                        
                        Vector3 dWorldPos = GetWorldPositionFromTile(doorTileRow, doorTileCol);
                        dWorldPos.z = 1;
                        
                        if (wallPrefab != null)
                        {
                            Quaternion rotation;
                            if (doorTileRow % 2 == 0)
                            {
                                rotation = Quaternion.identity;
                            }
                            else
                            {
                                rotation = Quaternion.Euler(0, 0, 90);
                            }
                            doorObject = Instantiate(wallPrefab, wallContainer.transform);
                            doorObject.transform.localPosition = dWorldPos;
                            doorObject.transform.localRotation = rotation;
                            doorObject.transform.localScale = new Vector3(cellSize, wallThickness, 1);
                            SpriteRenderer sr = doorObject.GetComponent<SpriteRenderer>();
                            if (sr != null) sr.color = new Color(0.6f, 0.3f, 0f);
                        }

                        int keyIndex = doorIndex / 2;
                        if (keyIndex == 0 && doorIndex > 0) keyIndex = 1;
                        
                        if (keyIndex < path.Count)
                        {
                            Vector2Int kPos = path[keyIndex];
                            keyTileRow = kPos.x;
                            keyTileCol = kPos.y;
                            tileGrid[keyTileRow, keyTileCol] = 2;
                            
                            goalTileRow = keyTileRow;
                            goalTileCol = keyTileCol;
                            tileGrid[finalGoalRow, finalGoalCol] = 8;
                            
                            Vector3 kWorldPos = GetWorldPositionFromTile(keyTileRow, keyTileCol);
                            kWorldPos.z = 1;
                            if (keyPrefab != null)
                            {
                                keyObject = Instantiate(keyPrefab, wallContainer.transform);
                                keyObject.transform.localPosition = kWorldPos;
                                SpriteRenderer sr = keyObject.GetComponent<SpriteRenderer>();
                                if (sr != null) sr.color = Color.yellow;
                            }
                            setupKeyDoor = true;
                        }
                        else
                        {
                            tileGrid[doorTileRow, doorTileCol] = 0;
                            if (doorObject != null) Destroy(doorObject);
                        }
                    }
                }
            }

            if (!setupKeyDoor)
            {
                goalTileRow = finalGoalRow;
                goalTileCol = finalGoalCol;
                tileGrid[goalTileRow, goalTileCol] = 2;
                hasKey = true;
            }

            if (!isEmptyRoom)
            {
                for (int i = 0; i < emptyTiles.Count; i++) 
                {
                    Vector2Int tile = emptyTiles[i];
                    int tr = tile.x;
                    int tc = tile.y;
                    
                    if (tr == startTileRow && tc == startTileCol) continue;
                    if (tr == finalGoalRow && tc == finalGoalCol) continue;
                    if (setupKeyDoor && tr == keyTileRow && tc == keyTileCol) continue;
                    if (setupKeyDoor && tr == doorTileRow && tc == doorTileCol) continue;
                    if (tileGrid[tr, tc] != 0) continue;

                    if (Random.value < trapChance)
                    {
                        int trapType;
                        if (Random.value > 0.5f)
                        {
                            trapType = 4; // Sticky
                        }
                        else
                        {
                            trapType = 5; // Slippery
                        }
                        tileGrid[tr, tc] = trapType;

                        Vector3 pos = GetWorldPositionFromTile(tr, tc);
                        pos.z = 1;
                        
                        if (wallPrefab != null)
                        {
                            GameObject t = Instantiate(wallPrefab, wallContainer.transform);
                            t.transform.localPosition = new Vector3(pos.x, pos.y, pos.z + 0.1f);
                            t.transform.localScale = new Vector3(TileSize, TileSize, 1);
                            
                            Collider2D col2d = t.GetComponent<Collider2D>();
                            if (col2d != null) Destroy(col2d);
                            Collider col3d = t.GetComponent<Collider>();
                            if (col3d != null) Destroy(col3d);

                            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                            if (sr != null)
                            {
                                Color trapColor;
                                Color outlineColor;
                                if (trapType == 4)
                                {
                                    trapColor = new Color(0.8f, 0.4f, 0.0f, 0.15f);
                                    outlineColor = new Color(1.0f, 0.5f, 0.0f, 1f);
                                }
                                else
                                {
                                    trapColor = new Color(0.0f, 0.1f, 0.5f, 0.15f);
                                    outlineColor = new Color(0.2f, 0.6f, 1f, 1f);
                                }

                                sr.color = trapColor;
                                sr.sortingOrder = -2;

                                LineRenderer lr = t.AddComponent<LineRenderer>();
                                lr.material = sr.material;
                                lr.startColor = outlineColor;
                                lr.endColor = outlineColor;
                                lr.startWidth = TileSize * 0.1f;
                                lr.endWidth = TileSize * 0.1f;
                                lr.useWorldSpace = false;
                                lr.positionCount = 5;
                                lr.sortingOrder = -1;
                                
                                float half = 0.48f; 
                                lr.SetPosition(0, new Vector3(-half, -half, 0));
                                lr.SetPosition(1, new Vector3(-half, half, 0));
                                lr.SetPosition(2, new Vector3(half, half, 0));
                                lr.SetPosition(3, new Vector3(half, -half, 0));
                                lr.SetPosition(4, new Vector3(-half, -half, 0));
                            }
                        }
                    }
                }
            }
        }
    }

    private void InstantiateWall(Vector3 position, Quaternion rotation)
    {
        if (wallContainer == null) return;
        position.z = 1;

        GameObject wall = Instantiate(wallPrefab, wallContainer.transform);

        wall.transform.localPosition = position;
        wall.transform.localRotation = rotation;
        wall.transform.localScale = new Vector3(cellSize, wallThickness, 1);
    }

    private void InstantiateBoundaryWalls(float mazeWidth, float mazeHeight, Vector3 startOffset)
    {
        if (wallContainer == null) return;

        float halfMazeWidth = mazeWidth / 2f;
        float halfMazeHeight = mazeHeight / 2f;
        float halfThickness = wallThickness / 2f;

        Vector3 topPos = new Vector3(0, halfMazeHeight + halfThickness, 1);
        GameObject topWall = Instantiate(wallPrefab, wallContainer.transform);
        topWall.transform.localPosition = topPos;
        topWall.transform.localRotation = Quaternion.identity;
        topWall.transform.localScale = new Vector3(mazeWidth + wallThickness, wallThickness, 1);

        Vector3 bottomPos = new Vector3(0, -halfMazeHeight - halfThickness, 1);
        GameObject bottomWall = Instantiate(wallPrefab, wallContainer.transform);
        bottomWall.transform.localPosition = bottomPos;
        bottomWall.transform.localRotation = Quaternion.identity;
        bottomWall.transform.localScale = new Vector3(mazeWidth + wallThickness, wallThickness, 1);

        Vector3 leftPos = new Vector3(-halfMazeWidth - halfThickness, 0, 1);
        GameObject leftWall = Instantiate(wallPrefab, wallContainer.transform);
        leftWall.transform.localPosition = leftPos;
        leftWall.transform.localRotation = Quaternion.Euler(0, 0, 90);
        leftWall.transform.localScale = new Vector3(mazeHeight + wallThickness, wallThickness, 1);

        Vector3 rightPos = new Vector3(halfMazeWidth + halfThickness, 0, 1);
        GameObject rightWall = Instantiate(wallPrefab, wallContainer.transform);
        rightWall.transform.localPosition = rightPos;
        rightWall.transform.localRotation = Quaternion.Euler(0, 0, 90);
        rightWall.transform.localScale = new Vector3(mazeHeight + wallThickness, wallThickness, 1);
    }

    public void ResetAgentToStart()
    {
        if (visitedGrid != null)
        {
            for (int r = 0; r < visitedGrid.GetLength(0); r++)
            {
                for (int c = 0; c < visitedGrid.GetLength(1); c++)
                {
                    visitedGrid[r, c] = false;
                }
            }
        }

        agentTileRow = startTileRow;
        agentTileCol = startTileCol;
        
        if (agentTransform != null)
        {
            Vector3 agentPos = GetWorldPositionFromTile(agentTileRow, agentTileCol);
            agentPos.z = 1;
            agentTransform.localPosition = agentPos;

            MoveToGoal agentComponent = agentTransform.GetComponent<MoveToGoal>();
            if (agentComponent != null)
            {
                agentComponent.SyncVisualPosition();
            }
        }
    }

    public int GetShortestPathLength(int startR, int startC, int targetR, int targetC)
    {
        if (tileGrid == null) return -1;
        
        int totalRows = tileGrid.GetLength(0);
        int totalCols = tileGrid.GetLength(1);
        bool[,] visited = new bool[totalRows, totalCols];
        Queue<(int row, int col, int distance)> bfsQueue = new Queue<(int, int, int)>();

        bfsQueue.Enqueue((startR, startC, 0));
        visited[startR, startC] = true;

        // Direction arrays (Up, Down, Left, Right)
        int[] rowDirections = { -1, 1, 0, 0 };
        int[] colDirections = { 0, 0, -1, 1 };

        while (bfsQueue.Count > 0)
        {
            var currentTile = bfsQueue.Dequeue();
            
            // If we reached the target tile, return the distance taken to get here
            if (currentTile.row == targetR && currentTile.col == targetC)
            {
                return currentTile.distance;
            }

            // Check all 4 neighbor tiles
            for (int i = 0; i < 4; i++)
            {
                int neighborRow = currentTile.row + rowDirections[i];
                int neighborCol = currentTile.col + colDirections[i];

                // Ensure the neighbor is within the grid boundaries
                if (neighborRow >= 0 && neighborRow < totalRows && neighborCol >= 0 && neighborCol < totalCols)
                {
                    // If the tile hasn't been visited and is not a wall (type 1)
                    if (!visited[neighborRow, neighborCol] && tileGrid[neighborRow, neighborCol] != 1)
                    {
                        visited[neighborRow, neighborCol] = true;
                        bfsQueue.Enqueue((neighborRow, neighborCol, currentTile.distance + 1));
                    }
                }
            }
        }
        
        // Return -1 if no path could be found
        return -1;
    }

    public List<Vector2Int> GetShortestPathTiles(int startR, int startC, int targetR, int targetC)
    {
        int totalRows = tileGrid.GetLength(0);
        int totalCols = tileGrid.GetLength(1);
        
        bool[,] visited = new bool[totalRows, totalCols];
        Vector2Int[,] parentTile = new Vector2Int[totalRows, totalCols]; // Tracks the path back to the start
        Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();

        bfsQueue.Enqueue(new Vector2Int(startR, startC));
        visited[startR, startC] = true;

        // Direction arrays for Up, Down, Left, Right
        int[] rowDirections = { -1, 1, 0, 0 };
        int[] colDirections = { 0, 0, -1, 1 };
        
        bool pathFound = false;

        while (bfsQueue.Count > 0)
        {
            Vector2Int currentTile = bfsQueue.Dequeue();
            
            // Stop searching if we've reached the target
            if (currentTile.x == targetR && currentTile.y == targetC) 
            {
                pathFound = true;
                break;
            }

            // Check all 4 neighboring tiles
            for (int i = 0; i < 4; i++)
            {
                int neighborRow = currentTile.x + rowDirections[i];
                int neighborCol = currentTile.y + colDirections[i];

                // Ensure the neighbor is within the grid bounds
                if (neighborRow >= 0 && neighborRow < totalRows && neighborCol >= 0 && neighborCol < totalCols)
                {
                    // If the tile hasn't been visited and is not a wall (type 1)
                    if (!visited[neighborRow, neighborCol] && tileGrid[neighborRow, neighborCol] != 1)
                    {
                        visited[neighborRow, neighborCol] = true;
                        parentTile[neighborRow, neighborCol] = currentTile; // Store where it came from
                        bfsQueue.Enqueue(new Vector2Int(neighborRow, neighborCol));
                    }
                }
            }
        }

        if (!pathFound)
        {
            return null;
        }

        // Trace back from the target to the start to construct the path
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int traceStep = new Vector2Int(targetR, targetC);
        
        while (traceStep.x != startR || traceStep.y != startC)
        {
            path.Add(traceStep);
            traceStep = parentTile[traceStep.x, traceStep.y];
        }
        
        path.Add(new Vector2Int(startR, startC));
        path.Reverse(); // Revrerse so the path goes from start to target
        
        return path;
    }

    public List<Vector2Int> GetReachableTiles(int startR, int startC)
    {
        int totalRows = tileGrid.GetLength(0);
        int totalCols = tileGrid.GetLength(1);
        
        bool[,] visited = new bool[totalRows, totalCols];
        Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();
        List<Vector2Int> reachableTiles = new List<Vector2Int>();

        bfsQueue.Enqueue(new Vector2Int(startR, startC));
        visited[startR, startC] = true;

        // Direction arrays for Up, Down, Left, Right
        int[] rowDirections = { -1, 1, 0, 0 };
        int[] colDirections = { 0, 0, -1, 1 };

        while (bfsQueue.Count > 0)
        {
            Vector2Int currentTile = bfsQueue.Dequeue();
            reachableTiles.Add(currentTile);

            // Check all 4 neighboring tiles
            for (int i = 0; i < 4; i++)
            {
                int neighborRow = currentTile.x + rowDirections[i];
                int neighborCol = currentTile.y + colDirections[i];

                // Ensure the neighbor is within the grid boundaries
                if (neighborRow >= 0 && neighborRow < totalRows && neighborCol >= 0 && neighborCol < totalCols)
                {
                    // If the tile hasn't been visited and is not a wall (type 1)
                    if (!visited[neighborRow, neighborCol] && tileGrid[neighborRow, neighborCol] != 1)
                    {
                        visited[neighborRow, neighborCol] = true;
                        bfsQueue.Enqueue(new Vector2Int(neighborRow, neighborCol));
                    }
                }
            }
        }
        
        return reachableTiles;
    }

    public List<float> GetLocalObservation(int centerRow, int centerCol, int radius)
    {
        List<float> obs = new List<float>();
        // Determine the boundaries of the tile grid
        int tr = 0;
        int tc = 0;
        if (tileGrid != null)
        {
            tr = tileGrid.GetLength(0);
            tc = tileGrid.GetLength(1);
        }

        // Loop through the surrounding area to gather observations for the agent
        for (int r = centerRow - radius; r <= centerRow + radius; r++)
        {
            for (int c = centerCol - radius; c <= centerCol + radius; c++)
            {
                if (r >= 0 && r < tr && c >= 0 && c < tc)
                {
                    int type = tileGrid[r, c];
                    
                    // Check tile type and add observation flags
                    if (type == 1) { obs.Add(1f); } else { obs.Add(0f); }
                    if (type == 2) { obs.Add(1f); } else { obs.Add(0f); }
                    if (type == 4) { obs.Add(1f); } else { obs.Add(0f); }
                    if (type == 5) { obs.Add(1f); } else { obs.Add(0f); }
                    
                    // Check if the agent has already visited this tile
                    if (visitedGrid[r, c]) { obs.Add(1f); } else { obs.Add(0f); }
                }
                else
                {
                    // Treat out of bounds areas as walls to discourage moving there
                    obs.Add(1f);
                    obs.Add(0f);
                    obs.Add(0f);
                    obs.Add(0f);
                    obs.Add(0f);
                }
            }
        }
        return obs;
    }
}