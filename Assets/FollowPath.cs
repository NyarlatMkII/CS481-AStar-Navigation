using System.Collections.Generic;
using UnityEngine;

//This code was created by Google Gemini Pro 3.1 on 10/11/26

[System.Serializable]
public class FollowPath : Move
{
    public List<Vector3> waypoints;
    public int currentWpIndex = 0;

    // Increased to 100 (10 units) to ensure fast-moving entities never overshoot and spin
    public float waypointDistanceSq = 9f;

    // We create our own line variable so UnitAI.Decorate() cannot hijack it
    public LineRenderer pathLine;

    private float pathCheckTimer = 0f;

    public FollowPath(Entity ent, List<Vector3> path) : base(ent, path.Count > 0 ? path[0] : ent.position)
    {
        waypoints = path;
        this.line = null; // Hide the base line from UnitAI
    }

    public override void Init()
    {
        // Use LineMgr properly to prevent the NullReferenceException on second clicks
        pathLine = LineMgr.inst.CreateMoveLine(entity.position, entity.position);

        // Shrink the line thickness
        pathLine.startWidth = 0.5f;
        pathLine.endWidth = 0.5f;

        // We intentionally do NOT instantiate potentialLine here to remove the white artifact
    }

    public override void Tick()
    {
        if (currentWpIndex >= waypoints.Count) return;

        // --- NEW: Dynamic Rerouting ---
        // Increment timer to check for blockages twice a second instead of every frame
        pathCheckTimer += Time.deltaTime;
        if (pathCheckTimer > 0.5f)
        {
            pathCheckTimer = 0f;
            Vector3 currentWp = waypoints[currentWpIndex];
            Vector3 dirToWp = currentWp - entity.position;

            // If a wall is now blocking our direct line of sight to the current waypoint, we've been pushed off course
            if (Physics.Raycast(entity.position + Vector3.up * 0.5f, dirToWp.normalized, dirToWp.magnitude, AStarMgr.inst.obstacleMask))
            {
                // Recalculate a fresh path from our new current position to the final destination
                Vector3 finalDest = waypoints[waypoints.Count - 1];
                List<Vector3> newPath = AStarMgr.inst.FindPath(entity.position, finalDest);

                if (newPath.Count > 0)
                {
                    waypoints = newPath;
                    currentWpIndex = 0; // Reset index to follow the new path
                    UpdateVisualLine();
                    return; // Skip the rest of this frame's physics so the new path can settle
                }
            }
        }

        Vector3 pos2D = new Vector3(entity.position.x, 0, entity.position.z);
        Vector3 target2D = new Vector3(waypoints[currentWpIndex].x, 0, waypoints[currentWpIndex].z);

        // Increase the final stop radius slightly so the group can fit inside the finish zone
        float currentThresholdSq = (currentWpIndex == waypoints.Count - 1) ? 16f : waypointDistanceSq;

        if ((pos2D - target2D).sqrMagnitude < currentThresholdSq)
        {
            currentWpIndex++;
            if (currentWpIndex < waypoints.Count)
            {
                movePosition = waypoints[currentWpIndex];
            }
        }

        if (currentWpIndex < waypoints.Count)
        {
            // NEW: Turn off repulsion if this is the final waypoint so they can park safely
            bool useRepulsion = (currentWpIndex < waypoints.Count - 1);

            DHDS dhds = useRepulsion ? ComputeObstacleAvoidanceDHDS() : ComputeDHDS();

            entity.desiredHeading = dhds.dh;
            entity.desiredSpeed = dhds.ds;

            UpdateVisualLine();
        }
    }

    private DHDS ComputeObstacleAvoidanceDHDS()
    {
        DHDS baseDHDS = ComputePotentialDHDS();
        Vector3 moveVec = new Vector3(Mathf.Sin(baseDHDS.dh * Mathf.Deg2Rad), 0, Mathf.Cos(baseDHDS.dh * Mathf.Deg2Rad));
        moveVec *= baseDHDS.ds;

        // 1. Reduce the scan radius to 2f
        Collider[] nearbyWalls = Physics.OverlapSphere(entity.position, 1.5f, AStarMgr.inst.obstacleMask);
        Vector3 wallRepulsion = Vector3.zero;

        foreach (Collider wall in nearbyWalls)
        {
            Vector3 closestPoint = wall.ClosestPoint(entity.position);
            Vector3 pushDir = entity.position - closestPoint;
            pushDir.y = 0;

            float dist = pushDir.magnitude;
            // 2. Only apply pushback if the ship is within 2 units of the wall
            if (dist < 1.5f && dist > 0.01f)
            {
                // 3. Update the math to use the 2f threshold
                float force = (1.5f - dist) * 50f;
                wallRepulsion += pushDir.normalized * force;
            }
        }

        // 4. Combine the group-movement vector with the wall-pushback vector
        Vector3 finalVec = moveVec + wallRepulsion;

        // 5. Convert back to Unity's expected Heading and Speed format
        float heading = Mathf.Atan2(finalVec.x, finalVec.z) * Mathf.Rad2Deg;

        // Cap at 20f (our fixed max speed) to prevent physics explosions when heavily squeezed
        float speed = Mathf.Clamp(finalVec.magnitude, 0, 20f);

        // Pass the calculated values directly into the constructor
        return new DHDS(heading, speed);
    }

    private void UpdateVisualLine()
    {
        if (pathLine == null) return;

        // Dynamically shrink the line array as waypoints are reached
        int pointsLeft = waypoints.Count - currentWpIndex + 1;
        pathLine.positionCount = pointsLeft;

        // Point 0 always anchors to the entity's current position
        pathLine.SetPosition(0, entity.position + Vector3.up);

        // Subsequent points anchor to the remaining path nodes
        for (int i = currentWpIndex; i < waypoints.Count; i++)
        {
            int lineIndex = i - currentWpIndex + 1;
            pathLine.SetPosition(lineIndex, waypoints[i] + Vector3.up);
        }

        pathLine.gameObject.SetActive(entity.isSelected);
    }

    public override bool IsDone()
    {
        return currentWpIndex >= waypoints.Count;
    }

    public override void Stop()
    {
        entity.desiredSpeed = 0;
        // Safely destroy via LineMgr to prevent the line 78 crash
        if (pathLine != null) LineMgr.inst.DestroyLR(pathLine);
    }
}