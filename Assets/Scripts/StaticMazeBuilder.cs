using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class StaticMazeBuilder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] public GameObject wallPrefab;

    [Header("Maze Settings")]
    public int rows = 5;
    public int cols = 5;
    public float cellSize = 10f;
    [SerializeField] int mazeSeed = 42;

    float wallThickness;
    GameObject wallContainer;
    Transform agentTransform;
    Transform goalTransform;

    [HideInInspector] public List<Vector3> validSpawnPoints = new List<Vector3>();
    [HideInInspector] public List<Vector3> openSpawnPoints = new List<Vector3>();

    private class Cell
    {
        public int R, C;
        public bool Visited = false;
        public bool[] Walls = { true, true, true, true }; // top, right, bottom, left
        public Cell(int r, int c) { R = r; C = c; }
    }

    Cell[,] grid;

    private void Start()
    {
        wallThickness = wallPrefab != null ? wallPrefab.transform.localScale.y : 1f;
        agentTransform = transform.Find("Agent");
        goalTransform = transform.Find("Goal");
        BuildMaze();
        RespawnEntities();
    }

    public void BuildMaze()
    {
        Transform existing = transform.Find("StaticWalls");
        if (existing != null) DestroyImmediate(existing.gameObject);

        wallContainer = new GameObject("StaticWalls");
        wallContainer.transform.parent = transform;
        wallContainer.transform.localPosition = Vector3.zero;

        Random.InitState(mazeSeed);

        InitGrid();
        CarvePassages();
        PlaceWalls();
        SetSpawnPoints();
    }

    void InitGrid()
    {
        grid = new Cell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                grid[r, c] = new Cell(r, c);
    }

    void CarvePassages()
    {
        Stack<Cell> stack = new Stack<Cell>();
        Cell current = grid[0, 0];
        current.Visited = true;
        stack.Push(current);

        while (stack.Count > 0)
        {
            current = stack.Peek();
            Cell next = GetUnvisitedNeighbor(current, out int cWall, out int nWall);

            if (next != null)
            {
                current.Walls[cWall] = false;
                next.Walls[nWall] = false;
                next.Visited = true;
                stack.Push(next);
            }
            else
            {
                stack.Pop();
            }
        }
    }

    Cell GetUnvisitedNeighbor(Cell cell, out int currentWall, out int neighborWall)
    {
        var dirs = new List<(int dr, int dc, int cw, int nw)>
        {
            (-1,  0, 0, 2),
            ( 0,  1, 1, 3),
            ( 1,  0, 2, 0),
            ( 0, -1, 3, 1)
        };

        for (int i = dirs.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp;
        }

        currentWall = neighborWall = -1;

        foreach (var (dr, dc, cw, nw) in dirs)
        {
            int nr = cell.R + dr, nc = cell.C + dc;
            if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && !grid[nr, nc].Visited)
            {
                currentWall = cw;
                neighborWall = nw;
                return grid[nr, nc];
            }
        }
        return null;
    }

    void PlaceWalls()
    {
        float mazeW = cols * cellSize;
        float mazeH = rows * cellSize;
        Vector3 origin = new Vector3(-mazeW / 2f + cellSize / 2f, mazeH / 2f - cellSize / 2f, 1f);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Cell cell = grid[r, c];
                Vector3 center = origin + new Vector3(c * cellSize, -r * cellSize, 0);

                if (cell.Walls[0])
                    SpawnWall(center + new Vector3(0, cellSize / 2f, 0), Quaternion.identity);

                if (cell.Walls[1])
                    SpawnWall(center + new Vector3(cellSize / 2f, 0, 0), Quaternion.Euler(0, 0, 90));

                if (r == rows - 1 && cell.Walls[2])
                    SpawnWall(center + new Vector3(0, -cellSize / 2f, 0), Quaternion.identity);

                if (c == 0 && cell.Walls[3])
                    SpawnWall(center + new Vector3(-cellSize / 2f, 0, 0), Quaternion.Euler(0, 0, 90));
            }
        }

        PlaceBoundaryWalls(mazeW, mazeH);
    }

    void PlaceBoundaryWalls(float mazeW, float mazeH)
    {
        float hw = mazeW / 2f, hh = mazeH / 2f, ht = wallThickness / 2f;
        Vector3 scaleH = new Vector3(mazeW + wallThickness, wallThickness, 1);
        Vector3 scaleV = new Vector3(mazeH + wallThickness, wallThickness, 1);

        SpawnWall(new Vector3(0, hh + ht, 1), Quaternion.identity, scaleH); // top
        SpawnWall(new Vector3(0, -hh - ht, 1), Quaternion.identity, scaleH); // bottom
        SpawnWall(new Vector3(-hw - ht, 0, 1), Quaternion.Euler(0, 0, 90), scaleV); // left
        SpawnWall(new Vector3(hw + ht, 0, 1), Quaternion.Euler(0, 0, 90), scaleV); // right
    }

    void SpawnWall(Vector3 pos, Quaternion rot, Vector3? scale = null)
    {
        GameObject w = Instantiate(wallPrefab, wallContainer.transform);
        w.transform.localPosition = pos;
        w.transform.localRotation = rot;
        w.transform.localScale = scale ?? new Vector3(cellSize, wallThickness, 1);
    }

    void SetSpawnPoints()
    {
        float mazeW = cols * cellSize;
        float mazeH = rows * cellSize;
        Vector3 origin = new Vector3(-mazeW / 2f + cellSize / 2f, mazeH / 2f - cellSize / 2f, 1f);

        validSpawnPoints.Clear();
        openSpawnPoints.Clear();

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector3 p = origin + new Vector3(c * cellSize, -r * cellSize, 0);
                p.z = 1f;
                validSpawnPoints.Add(p);

                int openPassages = grid[r, c].Walls.Count(w => !w);
                if (openPassages >= 2)
                    openSpawnPoints.Add(p);
            }
        }
    }

    public void RespawnEntities()
    {
        if (openSpawnPoints.Count < 2)
        {
            MoveEntities(validSpawnPoints);
        }
        else
        {
            MoveEntities(openSpawnPoints);
        }
    }

    private void MoveEntities(List<Vector3> points)
    {
        if (points.Count < 2) return;

        Vector3 agentPos;
        Vector3 goalPos;
        int maxAttempts = 10;
        int attempts = 0;

        do
        {
            List<Vector3> shuffled = points.OrderBy(x => Random.value).Take(2).ToList();
            agentPos = shuffled[0];
            goalPos = shuffled[1];
            attempts++;

        } while (Vector3.Distance(agentPos, goalPos) < 5f && attempts < maxAttempts);

        if (agentTransform != null)
            agentTransform.localPosition = agentPos;

        if (goalTransform != null)
            goalTransform.localPosition = goalPos;
    }
}