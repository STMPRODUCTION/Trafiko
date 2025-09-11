using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GraphTraveler : MonoBehaviour
{
    [System.Serializable]
    public class Node { public float x, y, z; }

    [System.Serializable]
    public class Edge { public int x, y; }

    // nodeConnections element contains exactly the 4 port targets as in the file
    [System.Serializable]
    public class NodeConnections { public List<int> values; }

    [System.Serializable]
    public class GraphData
    {
        public List<Node> nodes;
        public List<float> radii;
        public List<Edge> edges;
        public List<int> degrees;
        public List<NodeConnections> nodeConnections;
        public bool connectToPorts;
    }

    [Header("Graph Settings")]
    public string graphFileName = "graph.cgraph"; // StreamingAssets

    [Header("Traveler Settings")]
    public GameObject travelerPrefab;
    public float moveSpeed = 5f;
    public bool rotateTowardsMovement = true;
    public bool loopPath = false;

    [Header("Debug")]
    public bool showPath = true;
    public Color pathColor = Color.green;
    public bool verbose = false;

    private GraphData graph;
    private GameObject travelerInstance;
    private List<Vector3> travelPositions = new List<Vector3>();
    private int currentTargetIndex = 0;
    private bool isMoving = false;

    void Start()
    {
        LoadGraph();
        if (graph == null)
        {
            Debug.LogError("Graph failed to load.");
            return;
        }
        if (graph.nodes == null || graph.edges == null)
        {
            Debug.LogError("Graph JSON missing nodes or edges.");
            return;
        }

        BuildTravelPositionsFromEdgesStrict();
        SpawnTraveler();
    }

    void Update()
    {
        if (travelerInstance == null || travelPositions.Count == 0 || !isMoving) return;
        MoveTraveler();
    }

    void LoadGraph()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, graphFileName);
        if (!File.Exists(filePath))
        {
            Debug.LogError("Graph file not found: " + filePath);
            return;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            graph = JsonUtility.FromJson<GraphData>(json);
            Debug.Log($"Graph loaded: nodes={graph.nodes?.Count ?? 0}, edges={graph.edges?.Count ?? 0}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error reading graph: " + e.Message);
            graph = null;
        }
    }

    // Ports: 0=North (forward), 1=South (back), 2=East (right), 3=West (left)
    Vector3 GetPortPosition(int nodeIndex, int portIndex)
    {
        Vector3 nodePos = NodeToVector3(graph.nodes[nodeIndex]);
        float radius = 1f;
        if (graph.radii != null && nodeIndex < graph.radii.Count) radius = graph.radii[nodeIndex];

        switch (portIndex)
        {
            case 0: return nodePos + new Vector3(0f, 0f, radius);
            case 1: return nodePos + new Vector3(0f, 0f, -radius);
            case 2: return nodePos + new Vector3(radius, 0f, 0f);
            case 3: return nodePos + new Vector3(-radius, 0f, 0f);
            default: return nodePos;
        }
    }

    // return port index p on node 'nodeIndex' such that nodeConnections[nodeIndex].values[p] == neighborIndex
    // returns -1 if not found or nodeConnections missing
    int FindPortExactly(int nodeIndex, int neighborIndex)
    {
        if (graph.nodeConnections == null) return -1;
        if (nodeIndex < 0 || nodeIndex >= graph.nodeConnections.Count) return -1;
        var vals = graph.nodeConnections[nodeIndex].values;
        if (vals == null) return -1;
        for (int p = 0; p < vals.Count && p < 4; p++)
        {
            if (vals[p] == neighborIndex) return p;
        }
        return -1;
    }

    // Validate edges once after loading the graph: returns the list of edges that have valid mutual ports
    List<Edge> ValidateEdgesWithPorts()
    {
        var valid = new List<Edge>();
        for (int ei = 0; ei < graph.edges.Count; ei++)
        {
            var e = graph.edges[ei];
            int a = e.x, b = e.y;
            if (a < 0 || a >= graph.nodes.Count || b < 0 || b >= graph.nodes.Count)
            {
                Debug.LogWarning($"Edge[{ei}] has invalid node ids {a},{b} - skipped.");
                continue;
            }

            int portAtoB = FindPortExactly(a, b);
            int portBtoA = FindPortExactly(b, a);
            if (portAtoB == -1 || portBtoA == -1)
            {
                Debug.LogWarning($"Edge[{ei}] skipped: missing mutual port mapping. node {a} ->{portAtoB}, node {b} ->{portBtoA}.");
                continue;
            }

            valid.Add(e);
        }

        Debug.Log($"Edges validated: {valid.Count}/{graph.edges.Count} usable (mutual port mapping).");
        return valid;
    }

    void BuildTravelPositionsFromEdgesStrict()
    {
        travelPositions.Clear();

        // get only validated edges
        var validEdges = ValidateEdgesWithPorts();
        Edge prevEdge = null;

        for (int ei = 0; ei < validEdges.Count; ei++)
        {
            var e = validEdges[ei];
            int a = e.x, b = e.y;

            // incoming neighbor for A is the other endpoint of prevEdge if prevEdge ends on A
            int incomingNeighborForA = -1;
            if (prevEdge != null)
            {
                if (prevEdge.x == a) incomingNeighborForA = prevEdge.y;
                else if (prevEdge.y == a) incomingNeighborForA = prevEdge.x;
            }

            // find ports strictly from file
            int incomingPortOnA = (incomingNeighborForA != -1) ? FindPortExactly(a, incomingNeighborForA) : -1;
            // if start of traversal and no prev edge, we can treat the incoming position as the port that points to b only if that entry exactly exists:
            if (incomingPortOnA == -1)
                incomingPortOnA = FindPortExactly(a, b);

            int outgoingPortOnA = FindPortExactly(a, b);
            int incomingPortOnB = FindPortExactly(b, a);

            // final guard: if any -1 (shouldn't happen because of ValidateEdgesWithPorts) skip edge
            if (incomingPortOnA == -1 || outgoingPortOnA == -1 || incomingPortOnB == -1)
            {
                Debug.LogWarning($"Edge {a}->{b} missing exact ports at build — skipping.");
                prevEdge = e;
                continue;
            }

            Vector3 pa_in  = GetPortPosition(a, incomingPortOnA);
            Vector3 pa_ctr = NodeToVector3(graph.nodes[a]);
            Vector3 pa_out = GetPortPosition(a, outgoingPortOnA);
            Vector3 pb_in  = GetPortPosition(b, incomingPortOnB);
            Vector3 pb_ctr = NodeToVector3(graph.nodes[b]);

            AddTravelPosition(pa_in);
            AddTravelPosition(pa_ctr);
            AddTravelPosition(pa_out);
            AddTravelPosition(pb_in);
            AddTravelPosition(pb_ctr);

            prevEdge = e;
        }

        Debug.Log($"Built {travelPositions.Count} travel positions (strict file-driven).");
    }


    bool ValidNodeIndex(int idx)
    {
        return graph.nodes != null && idx >= 0 && idx < graph.nodes.Count;
    }

    void AddTravelPosition(Vector3 p)
    {
        if (travelPositions.Count == 0 || Vector3.Distance(travelPositions[travelPositions.Count - 1], p) > 0.001f)
            travelPositions.Add(p);
    }

    void SpawnTraveler()
    {
        if (travelerPrefab == null)
        {
            Debug.LogError("Traveler prefab not assigned.");
            return;
        }
        if (travelPositions.Count == 0)
        {
            Debug.LogError("No travel positions were built. Check your .cgraph nodeConnections/edges.");
            return;
        }

        travelerInstance = Instantiate(travelerPrefab, travelPositions[0], Quaternion.identity);
        currentTargetIndex = 1;
        isMoving = travelPositions.Count > 1;
    }

    void MoveTraveler()
    {
        if (currentTargetIndex >= travelPositions.Count) return;

        Vector3 current = travelerInstance.transform.position;
        Vector3 target = travelPositions[currentTargetIndex];
        Vector3 next = Vector3.MoveTowards(current, target, moveSpeed * Time.deltaTime);
        travelerInstance.transform.position = next;

        if (rotateTowardsMovement && Vector3.Distance(current, target) > 0.001f)
        {
            Vector3 dir = (target - current).normalized;
            if (dir != Vector3.zero) travelerInstance.transform.rotation = Quaternion.LookRotation(dir);
        }

        if (Vector3.Distance(next, target) < 0.01f)
        {
            currentTargetIndex++;
            if (currentTargetIndex >= travelPositions.Count)
            {
                if (loopPath)
                {
                    currentTargetIndex = 1;
                    Debug.Log("Looping travel path.");
                }
                else
                {
                    isMoving = false;
                    Debug.Log("Travel complete.");
                }
            }
        }
    }

    Vector3 NodeToVector3(Node n) => new Vector3(n.x, n.y, n.z);

    void OnDrawGizmos()
    {
        if (!showPath || travelPositions == null || travelPositions.Count < 2) return;
        Gizmos.color = pathColor;
        for (int i = 0; i < travelPositions.Count - 1; i++)
        {
            Gizmos.DrawLine(travelPositions[i], travelPositions[i + 1]);
            Gizmos.DrawWireSphere(travelPositions[i], 0.12f);
        }
        Gizmos.DrawWireSphere(travelPositions[travelPositions.Count - 1], 0.12f);
    }

    // inspector controls
    public void StartMovement() => isMoving = true;
    public void StopMovement() => isMoving = false;
    public void ResetToStart()
    {
        if (travelPositions.Count > 0 && travelerInstance != null)
        {
            travelerInstance.transform.position = travelPositions[0];
            currentTargetIndex = 1;
            isMoving = travelPositions.Count > 1;
        }
    }
}
