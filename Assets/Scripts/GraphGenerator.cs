// PlanarGraphGizmos.cs
// Generates a planar graph (no crossing edges) with straight lines.
// Each node has its own exclusion radius so others cannot spawn too close.
// Lines connect at cardinal directions (N, S, E, W).
// Attach to an empty GameObject.

using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GraphGenerator : MonoBehaviour
{
    [Header("Graph Settings")]
    [Min(3)] public int nodeCount = 15;
    [Range(1, 4)] public int maxDegree = 4;
    [Min(1)] public int extraEdges = 5;

    [Header("Layout")]
    public float areaRadius = 20f;
    public float minNodeRadius = 0.5f;
    public float maxNodeRadius = 2f;
    public float yHeight = 0f;

    [Header("Colors")]
    public Color nodeColor = Color.cyan;
    public Color edgeColor = Color.white;
    public Color connectionPointColor = Color.red;

    [Header("Generation")]
    public int seed = 1234;
    public bool autoRegenerate = true;
    [Header("Debug")]
    public bool showConnectionPoints = true;
    public bool connectToPorts = false;

    [SerializeField, HideInInspector] private List<Vector3> nodes = new();
    [SerializeField, HideInInspector] private List<float> radii = new();
    [SerializeField, HideInInspector] private List<Vector2Int> edges = new();
    [SerializeField, HideInInspector] private List<int> degrees = new();
    [SerializeField, HideInInspector] private List<CardinalDirection[]> nodeConnections = new();

    private HashSet<(int, int)> edgeSet;
    private System.Random rng;

    // Cardinal directions
    public enum CardinalDirection { North, East, South, West }

    void OnValidate()
    {
        if (autoRegenerate) Generate();
    }

#if UNITY_EDITOR
    [ContextMenu("Regenerate Now")]
#endif
    public void Generate()
    {
        rng = new System.Random(seed);
        nodes.Clear();
        radii.Clear();
        edges.Clear();
        degrees.Clear();
        nodeConnections.Clear();
        edgeSet = new();

        // Place nodes randomly with exclusion radius
        PlaceNodes();

        // Build spanning tree
        BuildSpanningTree();

        // Add extra edges
        AddExtraEdges();

        // Remove any intersecting edges
        RemoveIntersectingEdges();
    }

#if UNITY_EDITOR
    [ContextMenu("Connect To Ports")]
    public void ConnectToPorts()
    {
        connectToPorts = true;
        // Rebuild connections to use ports
        for (int i = 0; i < nodes.Count; i++)
        {
            RebuildNodeConnections(i);
        }
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Connect To Centers")]
    public void ConnectToCenters()
    {
        connectToPorts = false;
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void PlaceNodes()
    {
        int safety = 0;
        while (nodes.Count < nodeCount && safety++ < nodeCount * 1000)
        {
            float ang = (float)(rng.NextDouble() * Math.PI * 2);
            float rad = areaRadius * Mathf.Sqrt((float)rng.NextDouble());
            Vector3 candidate = new Vector3(Mathf.Cos(ang) * rad, yHeight, Mathf.Sin(ang) * rad);

            float r = Mathf.Lerp(minNodeRadius, maxNodeRadius, (float)rng.NextDouble());

            bool valid = true;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (Vector3.Distance(candidate, nodes[i]) < (r + radii[i]))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                nodes.Add(candidate);
                radii.Add(r);
                degrees.Add(0);
                nodeConnections.Add(new CardinalDirection[4]);
            }
        }

        if (nodes.Count < nodeCount)
        {
            Debug.LogWarning($"Could only place {nodes.Count}/{nodeCount} nodes without overlap. Try smaller radii or bigger area.");
        }
    }

    private void BuildSpanningTree()
    {
        for (int i = 1; i < nodes.Count; i++)
        {
            int best = -1;
            float bestDist = float.PositiveInfinity;
            for (int j = 0; j < i; j++)
            {
                if (!CanConnect(i, j)) continue;
                float d = (nodes[i] - nodes[j]).sqrMagnitude;
                if (d < bestDist && !WouldIntersect(i, j))
                {
                    bestDist = d;
                    best = j;
                }
            }
            if (best != -1) AddEdge(i, best);
        }
    }

    private void AddExtraEdges()
    {
        int attempts = 0;
        while (edges.Count < nodes.Count - 1 + extraEdges && attempts++ < nodes.Count * nodes.Count)
        {
            int a = rng.Next(0, nodes.Count);
            int b = rng.Next(0, nodes.Count);
            if (a == b) continue;
            if (!CanConnect(a, b)) continue;
            if (WouldIntersect(a, b)) continue;
            AddEdge(a, b);
        }
    }

    private bool CanConnect(int a, int b)
    {
        if (a == b) return false; // no self-loops
        if (degrees[a] >= maxDegree || degrees[b] >= maxDegree) return false;

        int u = Mathf.Min(a, b);
        int v = Mathf.Max(a, b);

        // Prevent duplicate edges
        if (edgeSet.Contains((u, v))) return false;

        return true;
    }

    private List<CardinalDirection> GetValidDirections(int fromNode, int toNode)
    {
        Vector3 toCenter = nodes[toNode];
        Vector3 fromCenter = nodes[fromNode];
        
        // Calculate the edge vector (from center to center)
        Vector3 edgeVector = (toCenter - fromCenter).normalized;
        
        // Define cardinal direction vectors
        Vector3[] cardinalVectors = {
            Vector3.forward,  // North
            Vector3.right,    // East
            Vector3.back,     // South
            Vector3.left      // West
        };
        
        List<CardinalDirection> validDirections = new List<CardinalDirection>();
        
        // Check each cardinal direction
        for (int i = 0; i < 4; i++)
        {
            CardinalDirection direction = (CardinalDirection)i;
            
            // Skip if this direction is already used
            bool directionUsed = false;
            for (int j = 0; j < degrees[fromNode]; j++)
            {
                if (nodeConnections[fromNode][j] == direction)
                {
                    directionUsed = true;
                    break;
                }
            }
            if (directionUsed) continue;
            
            // Calculate angle between edge vector and cardinal direction
            float angle = Vector3.Angle(edgeVector, cardinalVectors[i]);
            
            // If angle is <= 90 degrees, this is a valid direction
            if (angle <= 90f)
            {
                validDirections.Add(direction);
            }
        }
        
        return validDirections;
    }

    private bool WouldIntersect(int a, int b)
    {
        Vector3 startPoint, endPoint;
        
        if (connectToPorts)
        {
            // Get valid directions for both nodes
            List<CardinalDirection> validDirectionsA = GetValidDirections(a, b);
            List<CardinalDirection> validDirectionsB = GetValidDirections(b, a);
            
            // If no valid directions for either node, can't add edge
            if (validDirectionsA.Count == 0 || validDirectionsB.Count == 0) return true;
            
            // Choose the direction with smallest angle for each node
            CardinalDirection dirA = ChooseBestDirection(a, b, validDirectionsA);
            CardinalDirection dirB = ChooseBestDirection(b, a, validDirectionsB);
            
            // Get connection points
            startPoint = GetCardinalConnectionPoint(nodes[a], radii[a], dirA);
            endPoint = GetCardinalConnectionPoint(nodes[b], radii[b], dirB);
        }
        else
        {
            // Use node centers
            startPoint = nodes[a];
            endPoint = nodes[b];
        }
        
        // Check if edge passes through any other node's exclusion radius
        if (EdgePassesThroughNode(startPoint, endPoint, a, b))
        {
            return true;
        }
        
        // Check if the line intersects with any existing edge
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            int c = e.x, d = e.y;
            if (a == c || a == d || b == c || b == d) continue;
            
            Vector3 existingStart, existingEnd;
            
            if (connectToPorts)
            {
                // Get connection points for existing edge
                CardinalDirection existingDirC = GetCardinalDirectionForEdge(c, i);
                CardinalDirection existingDirD = GetCardinalDirectionForEdge(d, i);
                
                existingStart = GetCardinalConnectionPoint(nodes[c], radii[c], existingDirC);
                existingEnd = GetCardinalConnectionPoint(nodes[d], radii[d], existingDirD);
            }
            else
            {
                // Use node centers
                existingStart = nodes[c];
                existingEnd = nodes[d];
            }
            
            // Check if segments intersect in 2D (ignore Y axis)
            if (SegmentsIntersect(
                    new Vector2(startPoint.x, startPoint.z),
                    new Vector2(endPoint.x, endPoint.z),
                    new Vector2(existingStart.x, existingStart.z),
                    new Vector2(existingEnd.x, existingEnd.z)))
            {
                return true;
            }
        }
        return false;
    }

    private CardinalDirection GetCardinalDirectionForEdge(int nodeIndex, int edgeIndex)
    {
        int connectionIndex = 0;
        for (int i = 0; i <= edgeIndex; i++)
        {
            var edge = edges[i];
            if (edge.x == nodeIndex || edge.y == nodeIndex)
            {
                if (i == edgeIndex) break;
                connectionIndex++;
            }
        }
        
        if (connectionIndex < nodeConnections[nodeIndex].Length)
        {
            return nodeConnections[nodeIndex][connectionIndex];
        }
        
        return CardinalDirection.North; // fallback
    }

    private void AddEdge(int a, int b)
    {
        int u = Mathf.Min(a, b);
        int v = Mathf.Max(a, b);

        if (edgeSet.Contains((u, v))) return; // safety check

        if (connectToPorts)
        {
            // Get valid directions for both nodes
            List<CardinalDirection> validDirectionsA = GetValidDirections(a, b);
            List<CardinalDirection> validDirectionsB = GetValidDirections(b, a);
            
            // If no valid directions for either node, can't add edge
            if (validDirectionsA.Count == 0 || validDirectionsB.Count == 0) return;
            
            // Choose the direction with smallest angle for each node
            CardinalDirection dirA = ChooseBestDirection(a, b, validDirectionsA);
            CardinalDirection dirB = ChooseBestDirection(b, a, validDirectionsB);
            
            edgeSet.Add((u, v));
            edges.Add(new Vector2Int(u, v));

            nodeConnections[a][degrees[a]] = dirA;
            nodeConnections[b][degrees[b]] = dirB;
        }
        else
        {
            // Just add the edge without port connections
            edgeSet.Add((u, v));
            edges.Add(new Vector2Int(u, v));
            
            // Use default directions (will be ignored when not connecting to ports)
            nodeConnections[a][degrees[a]] = CardinalDirection.North;
            nodeConnections[b][degrees[b]] = CardinalDirection.North;
        }

        degrees[a]++; degrees[b]++;
    }

    private CardinalDirection ChooseBestDirection(int fromNode, int toNode, List<CardinalDirection> validDirections)
    {
        Vector3 toCenter = nodes[toNode];
        Vector3 fromCenter = nodes[fromNode];
        Vector3 edgeVector = (toCenter - fromCenter).normalized;
        
        // Define cardinal direction vectors
        Vector3[] cardinalVectors = {
            Vector3.forward,  // North
            Vector3.right,    // East
            Vector3.back,     // South
            Vector3.left      // West
        };
        
        CardinalDirection bestDirection = validDirections[0];
        float bestAngle = Vector3.Angle(edgeVector, cardinalVectors[(int)validDirections[0]]);
        
        // Find the direction with the smallest angle
        foreach (CardinalDirection direction in validDirections)
        {
            float angle = Vector3.Angle(edgeVector, cardinalVectors[(int)direction]);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestDirection = direction;
            }
        }
        
        return bestDirection;
    }

    private Vector3 GetCardinalConnectionPoint(Vector3 nodePos, float nodeRadius, CardinalDirection direction)
    {
        Vector3 offset = direction switch
        {
            CardinalDirection.North => Vector3.forward * nodeRadius,
            CardinalDirection.East => Vector3.right * nodeRadius,
            CardinalDirection.South => Vector3.back * nodeRadius,
            CardinalDirection.West => Vector3.left * nodeRadius,
            _ => Vector3.forward * nodeRadius
        };
        return nodePos + offset;
    }

    private void RemoveIntersectingEdges()
    {
        bool hasIntersections;
        int maxIterations = 100;
        int iterations = 0;
        
        do
        {
            hasIntersections = false;
            List<int> edgesToRemove = new List<int>();
            
            // Check all pairs of edges for intersections and node collisions
            for (int i = 0; i < edges.Count; i++)
            {
                var edge1 = edges[i];
                Vector3 start1, end1;
                
                if (connectToPorts)
                {
                    // Get connection points for edge1
                    CardinalDirection dir1A = GetCardinalDirectionForEdge(edge1.x, i);
                    CardinalDirection dir1B = GetCardinalDirectionForEdge(edge1.y, i);
                    start1 = GetCardinalConnectionPoint(nodes[edge1.x], radii[edge1.x], dir1A);
                    end1 = GetCardinalConnectionPoint(nodes[edge1.y], radii[edge1.y], dir1B);
                }
                else
                {
                    // Use node centers
                    start1 = nodes[edge1.x];
                    end1 = nodes[edge1.y];
                }
                
                // Check if this edge passes through any node it's not connected to
                if (EdgePassesThroughNode(start1, end1, edge1.x, edge1.y))
                {
                    hasIntersections = true;
                    if (!edgesToRemove.Contains(i)) edgesToRemove.Add(i);
                    continue;
                }
                
                for (int j = i + 1; j < edges.Count; j++)
                {
                    var edge2 = edges[j];
                    if (EdgesShareNode(edge1, edge2)) continue;
                    
                    Vector3 start2, end2;
                    
                    if (connectToPorts)
                    {
                        // Get connection points for edge2
                        CardinalDirection dir2A = GetCardinalDirectionForEdge(edge2.x, j);
                        CardinalDirection dir2B = GetCardinalDirectionForEdge(edge2.y, j);
                        start2 = GetCardinalConnectionPoint(nodes[edge2.x], radii[edge2.x], dir2A);
                        end2 = GetCardinalConnectionPoint(nodes[edge2.y], radii[edge2.y], dir2B);
                    }
                    else
                    {
                        // Use node centers
                        start2 = nodes[edge2.x];
                        end2 = nodes[edge2.y];
                    }
                    
                    // Check if segments intersect in 2D (ignore Y axis)
                    if (SegmentsIntersect(
                            new Vector2(start1.x, start1.z),
                            new Vector2(end1.x, end1.z),
                            new Vector2(start2.x, start2.z),
                            new Vector2(end2.x, end2.z)))
                    {
                        hasIntersections = true;
                        
                        // Count how many edges each edge intersects with
                        int intersections1 = CountIntersections(edge1);
                        int intersections2 = CountIntersections(edge2);
                        
                        // Calculate edge lengths
                        float length1 = Vector3.Distance(start1, end1);
                        float length2 = Vector3.Distance(start2, end2);
                        
                        // Decide which edge to remove
                        if (intersections1 > intersections2)
                        {
                            if (!edgesToRemove.Contains(i)) edgesToRemove.Add(i);
                        }
                        else if (intersections2 > intersections1)
                        {
                            if (!edgesToRemove.Contains(j)) edgesToRemove.Add(j);
                        }
                        else if (length1 > length2)
                        {
                            if (!edgesToRemove.Contains(i)) edgesToRemove.Add(i);
                        }
                        else
                        {
                            if (!edgesToRemove.Contains(j)) edgesToRemove.Add(j);
                        }
                    }
                }
            }
            
            // Remove the edges (in reverse order to maintain indices)
            edgesToRemove.Sort();
            edgesToRemove.Reverse();
            foreach (int index in edgesToRemove)
            {
                RemoveEdge(index);
            }
            
            iterations++;
        } while (hasIntersections && iterations < maxIterations);
        
        if (iterations >= maxIterations)
        {
            Debug.LogWarning("Reached maximum iterations while removing intersecting edges. Some intersections may remain.");
        }
    }
     
    // Add this method to rebuild connections after edge removal
    private void RebuildConnectionsAfterEdgeRemoval(int removedNode1, int removedNode2)
    {
        // Try to reconnect nodes that lost connections due to edge removal
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == removedNode1 || i == removedNode2) continue;
            
            // Try to connect to removedNode1 if possible
            if (degrees[removedNode1] < maxDegree && degrees[i] < maxDegree)
            {
                if (CanConnect(removedNode1, i) && !WouldIntersect(removedNode1, i))
                {
                    AddEdge(removedNode1, i);
                }
            }
            
            // Try to connect to removedNode2 if possible
            if (degrees[removedNode2] < maxDegree && degrees[i] < maxDegree)
            {
                if (CanConnect(removedNode2, i) && !WouldIntersect(removedNode2, i))
                {
                    AddEdge(removedNode2, i);
                }
            }
        }
    }

    private bool EdgesShareNode(Vector2Int edge1, Vector2Int edge2)
    {
        return edge1.x == edge2.x || edge1.x == edge2.y || edge1.y == edge2.x || edge1.y == edge2.y;
    }

    private int CountIntersections(Vector2Int edge)
    {
        int count = 0;
        Vector3 start, end;
        
        int edgeIndex = edges.IndexOf(edge);
        if (edgeIndex == -1) return 0;
        
        if (connectToPorts)
        {
            // Get connection points for this edge
            CardinalDirection dirA = GetCardinalDirectionForEdge(edge.x, edgeIndex);
            CardinalDirection dirB = GetCardinalDirectionForEdge(edge.y, edgeIndex);
            start = GetCardinalConnectionPoint(nodes[edge.x], radii[edge.x], dirA);
            end = GetCardinalConnectionPoint(nodes[edge.y], radii[edge.y], dirB);
        }
        else
        {
            // Use node centers
            start = nodes[edge.x];
            end = nodes[edge.y];
        }
        
        // Count node collisions
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == edge.x || i == edge.y) continue;
            
            Vector3 nodePos = nodes[i];
            float nodeRadius = radii[i];
            
            if (LineIntersectsCircle(new Vector2(start.x, start.z), 
                                    new Vector2(end.x, end.z), 
                                    new Vector2(nodePos.x, nodePos.z), 
                                    nodeRadius))
            {
                count++;
            }
        }
        
        // Count edge intersections
        foreach (var otherEdge in edges)
        {
            if (EdgesShareNode(edge, otherEdge)) continue;
            
            Vector3 otherStart, otherEnd;
            int otherEdgeIndex = edges.IndexOf(otherEdge);
            
            if (connectToPorts)
            {
                // Get connection points for other edge
                CardinalDirection otherDirA = GetCardinalDirectionForEdge(otherEdge.x, otherEdgeIndex);
                CardinalDirection otherDirB = GetCardinalDirectionForEdge(otherEdge.y, otherEdgeIndex);
                otherStart = GetCardinalConnectionPoint(nodes[otherEdge.x], radii[otherEdge.x], otherDirA);
                otherEnd = GetCardinalConnectionPoint(nodes[otherEdge.y], radii[otherEdge.y], otherDirB);
            }
            else
            {
                // Use node centers
                otherStart = nodes[otherEdge.x];
                otherEnd = nodes[otherEdge.y];
            }
            
            if (SegmentsIntersect(
                    new Vector2(start.x, start.z),
                    new Vector2(end.x, end.z),
                    new Vector2(otherStart.x, otherStart.z),
                    new Vector2(otherEnd.x, otherEnd.z)))
            {
                count++;
            }
        }
        
        return count;
    }

    private void RemoveEdge(int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= edges.Count) return;

        Vector2Int edge = edges[edgeIndex];
        int u = Mathf.Min(edge.x, edge.y);
        int v = Mathf.Max(edge.x, edge.y);

        // Remove from edge set
        edgeSet.Remove((u, v));

        // Remove from edges list
        edges.RemoveAt(edgeIndex);

        // Update degrees
        degrees[edge.x]--;
        degrees[edge.y]--;

        if (connectToPorts)
        {
            // Rebuild node connections for both nodes
            RebuildNodeConnections(edge.x);
            RebuildNodeConnections(edge.y);
            
            // Try to establish new connections after edge removal
            RebuildConnectionsAfterEdgeRemoval(edge.x, edge.y);
        }
    }

    private void RebuildNodeConnections(int nodeIndex)
    {
        if (!connectToPorts) return;

        // Clear existing connections
        for (int i = 0; i < 4; i++)
        {
            nodeConnections[nodeIndex][i] = CardinalDirection.North;
        }

        // Find all edges connected to this node and rebuild connections
        int connectionIndex = 0;
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (edge.x == nodeIndex || edge.y == nodeIndex)
            {
                int otherNodeIndex = edge.x == nodeIndex ? edge.y : edge.x;
                List<CardinalDirection> validDirections = GetValidDirections(nodeIndex, otherNodeIndex);
                
                if (validDirections.Count > 0 && connectionIndex < 4)
                {
                    CardinalDirection bestDir = ChooseBestDirection(nodeIndex, otherNodeIndex, validDirections);
                    nodeConnections[nodeIndex][connectionIndex++] = bestDir;
                }
            }
        }
    }

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        return (CCW(p1, q1, q2) != CCW(p2, q1, q2)) && (CCW(p1, p2, q1) != CCW(p1, p2, q2));
    }

    private static bool CCW(Vector2 a, Vector2 b, Vector2 c)
    {
        return (c.y - a.y) * (b.x - a.x) > (b.y - a.y) * (c.x - a.x);
    }
    // Add this method to check if an edge segment passes through any node's exclusion radius
    private bool EdgePassesThroughNode(Vector3 startPoint, Vector3 endPoint, int ignoreNode1, int ignoreNode2)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == ignoreNode1 || i == ignoreNode2) continue;
            
            Vector3 nodePos = nodes[i];
            float nodeRadius = radii[i];
            
            // Check if the line segment passes through the node's exclusion radius
            if (LineIntersectsCircle(new Vector2(startPoint.x, startPoint.z), 
                                    new Vector2(endPoint.x, endPoint.z), 
                                    new Vector2(nodePos.x, nodePos.z), 
                                    nodeRadius))
            {
                return true;
            }
        }
        return false;
    }

    // Method to check if a line segment intersects a circle
    private bool LineIntersectsCircle(Vector2 lineStart, Vector2 lineEnd, Vector2 circleCenter, float circleRadius)
    {
        // Vector from line start to circle center
        Vector2 d = lineEnd - lineStart;
        Vector2 f = lineStart - circleCenter;
        
        float a = Vector2.Dot(d, d);
        float b = 2 * Vector2.Dot(f, d);
        float c = Vector2.Dot(f, f) - circleRadius * circleRadius;
        
        float discriminant = b * b - 4 * a * c;
        
        if (discriminant < 0)
        {
            // No intersection
            return false;
        }
        else
        {
            discriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b - discriminant) / (2 * a);
            float t2 = (-b + discriminant) / (2 * a);
            
            // Check if either intersection point is within the line segment
            return (t1 >= 0 && t1 <= 1) || (t2 >= 0 && t2 <= 1);
        }
    }

    void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        // Draw edges
        Gizmos.color = edgeColor;
        for (int i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            
            if (connectToPorts)
            {
                // Get the cardinal directions for this edge
                CardinalDirection dirA = GetCardinalDirectionForEdge(e.x, i);
                CardinalDirection dirB = GetCardinalDirectionForEdge(e.y, i);
                
                Vector3 startPoint = GetCardinalConnectionPoint(nodes[e.x], radii[e.x], dirA);
                Vector3 endPoint = GetCardinalConnectionPoint(nodes[e.y], radii[e.y], dirB);
                
                // Draw straight line between connection points
                Gizmos.DrawLine(transform.TransformPoint(startPoint), transform.TransformPoint(endPoint));
            }
            else
            {
                // Draw straight line between node centers
                Gizmos.DrawLine(transform.TransformPoint(nodes[e.x]), transform.TransformPoint(nodes[e.y]));
            }
        }

        // Draw nodes
        for (int i = 0; i < nodes.Count; i++)
        {
            Gizmos.color = nodeColor;
            Gizmos.DrawSphere(transform.TransformPoint(nodes[i]), radii[i]);

            // Optional: show exclusion radius outline
            Gizmos.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.2f);
            Gizmos.DrawWireSphere(transform.TransformPoint(nodes[i]), radii[i]);

            // Show connection points
            if (showConnectionPoints && connectToPorts)
            {
                Gizmos.color = connectionPointColor;
                for (int j = 0; j < degrees[i]; j++)
                {
                    if (j < nodeConnections[i].Length)
                    {
                        Vector3 connectionPoint = GetCardinalConnectionPoint(nodes[i], radii[i], nodeConnections[i][j]);
                        Gizmos.DrawSphere(transform.TransformPoint(connectionPoint), 0.1f);
                    }
                }
            }
        }
    }
}