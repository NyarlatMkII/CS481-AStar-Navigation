using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//This code was modified with Google Gemini Pro 3.1 on 10/11/26

public class GameMgr : MonoBehaviour
{
    public static GameMgr inst;
    private GameInputs input;

    [Header("Office Patrol Settings")]
    private Entity officePatrolShip;
    private List<Vector3> officeRoomTargets = new List<Vector3>();
    private int currentRoomIndex = 0;
    private bool isOfficePatrolActive = false;
    private float patrolStartDelay = 0.5f; // NEW: Timer to allow initialization

    private void Awake()
    {
        inst = this;
        input = new GameInputs();
        input.Enable();
    }

    // Start is called before the first frame update
    public void SpawnFleet(Vector3 centerPos)
    {
        SelectionMgr.inst.ClearSelection();
        AIMgr.inst.isPotentialFieldsMovement = true;
        isOfficePatrolActive = false; // Reset in case of map reload

        // 1. Only spawn 1 ship if we are in the Office (Env 8), otherwise spawn 5
        int spawnCount = (GameSettings.SelectedEnvironment == 8) ? 1 : 5;

        for (int i = 0; i < spawnCount; i++)
        {
            float startX = centerPos.x + Random.Range(-2f, 2f);
            float startZ = centerPos.z + (i * 2f) - 4f;

            // If spawning just 1, lock it exactly to the center
            if (spawnCount == 1) { startX = centerPos.x; startZ = centerPos.z; }

            Vector3 spawnPos = new Vector3(startX, 0, startZ);
            Vector3 randomRotation = new Vector3(0, Random.Range(0, 360), 0);

            Entity ent = EntityMgr.inst.CreateEntity(EntityType.PilotVessel, spawnPos, randomRotation);
            ent.isSelected = false;

            // Save a reference to the lone ship
            if (spawnCount == 1) officePatrolShip = ent;
        }

        DistanceMgr.inst.Initialize();

        // 2. Kick off the auto-patrol
        if (GameSettings.SelectedEnvironment == 8)
        {
            SetupOfficePatrol();
        }
    }

    private void SetupOfficePatrol()
    {
        officeRoomTargets.Clear();
        currentRoomIndex = 0;
        patrolStartDelay = 0.5f; // Reset the timer every time we build the map

        for (float x = -45; x <= 45; x += 10)
            officeRoomTargets.Add(new Vector3(x, 0, 13f));

        // 2. Bottom row rooms: Loop right-to-left (X from 45 down to -45)
        for (float x = 45; x >= -45; x -= 10)
            officeRoomTargets.Add(new Vector3(x, 0, -13f));

        isOfficePatrolActive = true;

        if (officePatrolShip != null)
        {
            officePatrolShip.isSelected = true;
        }
    }

    private void SendToNextRoom()
    {
        if (currentRoomIndex >= officeRoomTargets.Count)
        {
            isOfficePatrolActive = false;
            return;
        }

        Vector3 target = officeRoomTargets[currentRoomIndex];
        List<Vector3> path = AStarMgr.inst.FindPath(officePatrolShip.position, target);

        // Sanity Check: Warn us if A* thinks the room is blocked
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"A* failed to find a path to Room {currentRoomIndex} at {target}!");
            return;
        }

        UnitAI ai = officePatrolShip.GetComponent<UnitAI>();
        if (ai != null)
        {
            FollowPath newCmd = new FollowPath(officePatrolShip, path);
            ai.SetCommand(newCmd);
        }
    }

    public Vector3 position;
    public float spread = 20;
    public float colNum = 10;
    public float initZ;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToMenu();
        }

        if (isOfficePatrolActive && officePatrolShip != null)
        {
            // 1. Initial delay to allow the ship and grid to fully settle
            if (patrolStartDelay > 0)
            {
                patrolStartDelay -= Time.deltaTime;
                if (patrolStartDelay <= 0)
                {
                    SendToNextRoom(); // Trigger the first movement
                }
                return; // Skip distance checking while waiting
            }

            // 2. Normal patrol distance checking
            if (currentRoomIndex < officeRoomTargets.Count)
            {
                Vector3 currentTarget = officeRoomTargets[currentRoomIndex];
                Vector3 pos2D = new Vector3(officePatrolShip.position.x, 0, officePatrolShip.position.z);
                Vector3 target2D = new Vector3(currentTarget.x, 0, currentTarget.z);

                if (Vector3.Distance(pos2D, target2D) < 3f)
                {
                    currentRoomIndex++;
                    SendToNextRoom();
                }
            }
        }
    }

    public void ReturnToMenu()
    {
        // Ensure this string matches the exact name you saved your menu scene as
        SceneManager.LoadScene("Menu");
    }
}
