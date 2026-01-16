using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MazeGenerator : MonoBehaviour
{
    [Header("Maze Components")]
    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject switchPrefab;

    [Header("Maze Settings")]
    [SerializeField] int rows = 10;
    [SerializeField] int cols = 10;
    [SerializeField] float cellSize = 10f;

    float wallThickness;

    Cell[,] grid; // Basically makes it have 2 indexes (e.g. array[1, 2] = "Hello";) so we can identify each cell with the row and column
    GameObject wallContainer;
    Transform agentTransform;
    Transform goalTransform;

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
        DestroyExistingMaze();
        InitializeGrid();
        RunRecursiveBacktracker();
        InstantiateMazeObjects();
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

    private void InstantiateMazeObjects()
    {
        if (wallContainer == null) return;

        float mazeWidth = cols * cellSize;
        float mazeHeight = rows * cellSize;
        List<Vector3> validSpawnPoints = new List<Vector3>();

        Vector3 startOffset = new Vector3(-mazeWidth / 2f + cellSize / 2f, mazeHeight / 2f - cellSize / 2f, 1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Cell cell = grid[r, c];
                Vector3 cellCenter = startOffset + new Vector3(c * cellSize, -r * cellSize, 0);

                validSpawnPoints.Add(cellCenter);

                if (cell.Walls[0]) // Top wall
                {
                    Vector3 position = cellCenter + new Vector3(0, cellSize / 2f, 0);
                    InstantiateWall(position, Quaternion.identity);
                }

                if (cell.Walls[1]) // Right wall
                {
                    Vector3 position = cellCenter + new Vector3(cellSize / 2f, 0, 0);
                    InstantiateWall(position, Quaternion.Euler(0, 0, 90));
                }

                if (r == (rows - 1) && cell.Walls[2]) // Bottom wall
                {
                    Vector3 position = cellCenter + new Vector3(0, -cellSize / 2f, 0);
                    InstantiateWall(position, Quaternion.identity);
                }

                if (c == 0 && cell.Walls[3]) // Left wall
                {
                    Vector3 position = cellCenter + new Vector3(-cellSize / 2f, 0, 0);
                    InstantiateWall(position, Quaternion.Euler(0, 0, 90));
                }
            }
        }

        InstantiateBoundaryWalls(mazeWidth, mazeHeight, startOffset);

        if (validSpawnPoints.Count > 1 && agentTransform != null && goalTransform != null)
        {
            validSpawnPoints = validSpawnPoints.OrderBy(_ => Random.value).ToList();

            Vector3 agentPos = validSpawnPoints[0];
            agentPos.z = 1;

            agentTransform.gameObject.SetActive(true);
            agentTransform.localPosition = agentPos;

            MoveToGoal agentComponent = agentTransform.GetComponent<MoveToGoal>();
            if (agentComponent != null)
            {
                agentComponent.initialAgentPosition = agentPos;
            }

            Vector3 goalPos = validSpawnPoints[validSpawnPoints.Count - 1];
            goalPos.z = 1;

            goalTransform.gameObject.SetActive(true);
            goalTransform.localPosition = goalPos;
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

        // Top Boundary Wall
        Vector3 topPos = new Vector3(0, halfMazeHeight + halfThickness, 1);
        GameObject topWall = Instantiate(wallPrefab, wallContainer.transform);
        topWall.transform.localPosition = topPos;
        topWall.transform.localRotation = Quaternion.identity;
        topWall.transform.localScale = new Vector3(mazeWidth + wallThickness, wallThickness, 1);

        // Bottom Boundary Wall
        Vector3 bottomPos = new Vector3(0, -halfMazeHeight - halfThickness, 1);
        GameObject bottomWall = Instantiate(wallPrefab, wallContainer.transform);
        bottomWall.transform.localPosition = bottomPos;
        bottomWall.transform.localRotation = Quaternion.identity;
        bottomWall.transform.localScale = new Vector3(mazeWidth + wallThickness, wallThickness, 1);

        // Left Boundary Wall
        Vector3 leftPos = new Vector3(-halfMazeWidth - halfThickness, 0, 1);
        GameObject leftWall = Instantiate(wallPrefab, wallContainer.transform);
        leftWall.transform.localPosition = leftPos;
        leftWall.transform.localRotation = Quaternion.Euler(0, 0, 90);
        leftWall.transform.localScale = new Vector3(mazeHeight + wallThickness, wallThickness, 1);

        // Right Boundary Wall
        Vector3 rightPos = new Vector3(halfMazeWidth + halfThickness, 0, 1);
        GameObject rightWall = Instantiate(wallPrefab, wallContainer.transform);
        rightWall.transform.localPosition = rightPos;
        rightWall.transform.localRotation = Quaternion.Euler(0, 0, 90);
        rightWall.transform.localScale = new Vector3(mazeHeight + wallThickness, wallThickness, 1);
    }
}