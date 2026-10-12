using System.Collections.Generic;
using UnityEngine;

//This code was created by Google Gemini Pro 3.1 on 10/11/26

public class ObstacleMgr : MonoBehaviour
{
    public static ObstacleMgr inst;

    [Header("Spawn Bounds (Between Red/Yellow Zones)")]
    public Vector2 spawnMin = new Vector2(-40, -20);
    public Vector2 spawnMax = new Vector2(40, 20);

    [Header("Zone Planes")]
    public Transform yellowPlane;
    public Transform redPlane;

    [Header("Obstacle Prefabs")]
    public GameObject cylinderPrefab;
    public GameObject cubePrefab;
    public Transform obstacleParent;

    // Set this to 1-8 in the Inspector for testing before we build the menu
    [Header("Current Environment")]
    public int currentEnvIndex = 1;

    private List<GameObject> activeObstacles = new List<GameObject>();

    private void Awake()
    {
        inst = this;
    }

    void Start()
    {
        currentEnvIndex = GameSettings.SelectedEnvironment;

        // 1. Builds the walls and moves the planes
        GenerateEnvironment(currentEnvIndex);

        // 2. Force physics update and build the grid
        Physics.SyncTransforms();
        if (AStarMgr.inst != null)
        {
            AStarMgr.inst.CreateGrid();
        }

        // 3. Spawn the entities exactly where the yellow plane was moved
        if (GameMgr.inst != null)
        {
            GameMgr.inst.SpawnFleet(yellowPlane.position);
        }
    }

    private void Update()
    {
        // The assignment requires pressing "c" to generate a new randomized set of obstacles
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (currentEnvIndex <= 6) // Only randomize the first 6 environments
            {
                GenerateEnvironment(currentEnvIndex);
                Physics.SyncTransforms();
                AStarMgr.inst.CreateGrid(); // We will uncomment this when the A* script is ready
            }
        }
    }

    public void GenerateEnvironment(int envIndex)
    {
        ClearObstacles();
        currentEnvIndex = envIndex;

        // 1. Define standard coordinates for the random scatter maps (Cases 1-6)
        Vector3 defaultYellow = new Vector3(-50f, 0.05f, -20f);
        Vector3 defaultRed = new Vector3(50f, 0.05f, 20f);

        // 2. Apply defaults first so they reset when leaving the Office/A* maps
        if (yellowPlane != null && redPlane != null)
        {
            yellowPlane.position = defaultYellow;
            redPlane.position = defaultRed;
        }

        switch (envIndex)
        {
            case 1: SpawnRandomObstacles(cylinderPrefab, 20); break;
            case 2: SpawnRandomObstacles(cylinderPrefab, 30); break;
            case 3: SpawnRandomObstacles(cylinderPrefab, 100); break;
            case 4: SpawnRandomObstacles(cubePrefab, 20); break;
            case 5: SpawnRandomObstacles(cubePrefab, 30); break;
            case 6: SpawnRandomObstacles(cubePrefab, 100); break;
            case 7:
                // 3. Overwrite positions for the A* environment
                if (yellowPlane != null && redPlane != null)
                {
                    yellowPlane.position = new Vector3(-45f, 0.05f, 0f);
                    redPlane.position = new Vector3(45f, 0.05f, 0f);
                }
                BuildAStarEnvironment();
                break;
            case 8:
                // 4. Overwrite positions for the Office environment
                if (yellowPlane != null && redPlane != null)
                {
                    yellowPlane.position = new Vector3(-45f, 0.05f, 0f);
                    redPlane.position = new Vector3(45f, 0.05f, 0f);
                }
                BuildOfficeEnvironment();
                break;
        }
    }

    private void ClearObstacles()
    {
        foreach (GameObject obs in activeObstacles)
        {
            Destroy(obs);
        }
        activeObstacles.Clear();
    }

    private void SpawnRandomObstacles(GameObject shapePrefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = new Vector3(Random.Range(spawnMin.x, spawnMax.x), 0, Random.Range(spawnMin.y, spawnMax.y));
            GameObject obs = Instantiate(shapePrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360), 0), obstacleParent);

            float scaleX = Random.Range(2f, 8f);
            float scaleZ = shapePrefab == cubePrefab ? Random.Range(2f, 8f) : scaleX;

            obs.transform.localScale = new Vector3(scaleX, 5f, scaleZ);
            obs.layer = LayerMask.NameToLayer("Obstacles");
            activeObstacles.Add(obs);
        }
    }

    private void BuildAStarEnvironment()
    {
        // Top and bottom horizontal boundary walls (spanning the 100 X-width)
        CreateWall(new Vector3(0, 0, 20), new Vector3(100, 5, 2));
        CreateWall(new Vector3(0, 0, -20), new Vector3(100, 5, 2));

        // Center vertical barrier to force the path around (leaves a gap at the top and bottom)
        CreateWall(new Vector3(0, 0, -2), new Vector3(5, 5, 45));
    }

    private void BuildOfficeEnvironment()
    {
        // 1. Outer Boundary Walls
        // Top and Bottom
        CreateWall(new Vector3(0, 0, 20), new Vector3(102, 5, 2));
        CreateWall(new Vector3(0, 0, -20), new Vector3(102, 5, 2));

        // Left and Right (The right wall is made intentionally thicker to match the visual in the image)
        CreateWall(new Vector3(-50, 0, 0), new Vector3(2, 5, 42));
        CreateWall(new Vector3(50, 0, 0), new Vector3(6, 5, 42));

        // 2. Vertical Room Dividers
        // Generates 9 vertical walls to create 10 distinct rooms per side
        for (int x = -40; x <= 40; x += 10)
        {
            // Top dividers (leaves a gap near the corridor for the front caps)
            CreateWall(new Vector3(x, 0, 13f), new Vector3(2, 5, 12));
            // Bottom dividers
            CreateWall(new Vector3(x, 0, -13f), new Vector3(2, 5, 12));
        }

        // 3. Front Wall Caps (creating the doorways)
        // Generates the horizontal foot of the "L" shape to partially close each room
        for (int x = -50; x <= 50; x += 10)
        {
            // Mirror logic: Left side of the map points right, right side points left
            bool pointsRight = (x < 0);
            float capWidth = 3.5f;
            float capXOffset = capWidth / 2f;

            // Shift the center position of the cap based on the required direction
            float capX = pointsRight ? (x + capXOffset) : (x - capXOffset);

            // Top room caps (facing the central corridor)
            CreateWall(new Vector3(capX, 0, 6f), new Vector3(capWidth, 5, 2));

            // Bottom room caps (facing the central corridor)
            CreateWall(new Vector3(capX, 0, -6f), new Vector3(capWidth, 5, 2));
        }
    }

    private void CreateWall(Vector3 position, Vector3 scale)
    {
        GameObject wall = Instantiate(cubePrefab, position, Quaternion.identity, obstacleParent);
        wall.transform.localScale = scale;
        wall.layer = LayerMask.NameToLayer("Obstacles");
        activeObstacles.Add(wall);
    }
}