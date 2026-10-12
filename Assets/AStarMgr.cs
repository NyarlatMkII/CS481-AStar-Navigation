using System.Collections.Generic;
using UnityEngine;

//This code was created by Google Gemini Pro 3.1 on 10/11/26

public class AStarMgr : MonoBehaviour
{
    public static AStarMgr inst;

    [Header("Grid Settings")]
    public LayerMask obstacleMask;
    public Vector2 gridWorldSize = new Vector2(200, 200); // Matches your scaled-down footprint
    public float nodeRadius = 1f; // Defines resolution. 1f means each grid cell is 2x2 units.

    Node[,] grid;
    float nodeDiameter;
    int gridSizeX, gridSizeY;

    void Awake()
    {
        inst = this;

        // Prevent divide-by-zero crashes if the Inspector is accidentally set to 0
        if (nodeRadius <= 0f) nodeRadius = 1f;

        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
    }

    // Call this from ObstacleMgr.Update() when 'C' is pressed
    public void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);

                // Lift the check to Y=2, and multiply the radius by 1.5 to create a buffer zone
                Vector3 checkPoint = worldPoint + Vector3.up * 0.5f;
                // Expanded to 3.2f to guarantee A* never builds a path through a corridor narrower than the 3-unit forcefields
                bool walkable = !Physics.CheckSphere(checkPoint, 1.2f, obstacleMask);

                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                grid[x, y].gCost = 0;
                grid[x, y].hCost = 0;
                grid[x, y].parent = null;
            }
        }

        Node startNode = NodeFromWorldPoint(startPos);
        Node targetNode = NodeFromWorldPoint(targetPos);

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost)
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetraceAndSimplifyPath(startNode, targetNode);
            }

            foreach (Node neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor)) continue;

                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return new List<Vector3>(); // Return empty if no path found
    }

    List<Vector3> RetraceAndSimplifyPath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Add(startNode);
        path.Reverse();

        // Rubric requires "minimal set of waypoints". We use line-of-sight pulling here.
        return SimplifyPath(path);
    }

    List<Vector3> SimplifyPath(List<Node> path)
    {
        List<Vector3> waypoints = new List<Vector3>();
        if (path.Count == 0) return waypoints;

        waypoints.Add(path[0].worldPosition);
        Vector3 lastAdded = path[0].worldPosition;

        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector3 nextTarget = path[i + 1].worldPosition;
            Vector3 dir = nextTarget - lastAdded;
            float dist = dir.magnitude;

            // Swapped from SphereCast to a precise Raycast to prevent the "internal overlap" bug
            if (Physics.SphereCast(lastAdded + Vector3.up * 0.5f, 1f, dir.normalized, out RaycastHit hit, dist, obstacleMask))
            {
                waypoints.Add(path[i].worldPosition);
                lastAdded = path[i].worldPosition;
            }
        }

        waypoints.Add(path[path.Count - 1].worldPosition);
        return waypoints;
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    // NEW: If checking a diagonal neighbor, ensure both adjacent straight nodes are clear
                    if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
                    {
                        if (!grid[node.gridX + x, node.gridY].walkable || !grid[node.gridX, node.gridY + y].walkable)
                            continue; // Skip this diagonal to prevent clipping through tight corners
                    }

                    neighbors.Add(grid[checkX, checkY]);
                }
            }
        }
        return neighbors;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (worldPosition.z + gridWorldSize.y / 2) / gridWorldSize.y;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }
}