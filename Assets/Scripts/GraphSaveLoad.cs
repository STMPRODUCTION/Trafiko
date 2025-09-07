using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class GraphEditorWindow : EditorWindow
{
    private GraphGenerator graphGenerator;
    private string filePath = "Assets/SavedGraph.cgraph";
    
    [MenuItem("Tools/Graph Editor")]
    public static void ShowWindow()
    {
        GetWindow<GraphEditorWindow>("Graph Editor");
    }
    
    void OnGUI()
    {
        GUILayout.Label("Graph File Operations", EditorStyles.boldLabel);
        
        graphGenerator = (GraphGenerator)EditorGUILayout.ObjectField("Graph Generator", graphGenerator, typeof(GraphGenerator), true);
        
        GUILayout.Label("File Path:");
        filePath = EditorGUILayout.TextField(filePath);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Browse File Path"))
        {
            string newPath = EditorUtility.SaveFilePanel("Save Graph", Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath), "cgraph");
            if (!string.IsNullOrEmpty(newPath))
            {
                filePath = newPath;
            }
        }
        
        GUILayout.Space(5);
        
        EditorGUI.BeginDisabledGroup(graphGenerator == null);
        {
            if (GUILayout.Button("Save Graph")) SaveGraph();
            if (GUILayout.Button("Load Graph")) LoadGraph();
        }
        EditorGUI.EndDisabledGroup();
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Browse For Load"))
        {
            string loadPath = EditorUtility.OpenFilePanel("Load Graph", Path.GetDirectoryName(filePath), "cgraph");
            if (!string.IsNullOrEmpty(loadPath))
            {
                filePath = loadPath;
                if (graphGenerator != null) LoadGraph();
            }
        }
    }
    
    private void SaveGraph()
    {
        if (graphGenerator == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Graph Generator first.", "OK");
            return;
        }
        
        try
        {
            GraphSaveData saveData = new GraphSaveData
            {
                nodes = new List<Vector3>(graphGenerator.nodes),
                radii = new List<float>(graphGenerator.radii),
                edges = new List<Vector2Int>(graphGenerator.edges),
                degrees = new List<int>(graphGenerator.degrees),
                connectToPorts = graphGenerator.connectToPorts,
                nodeConnections = new List<IntArrayWrapper>()
            };

            // Convert CardinalDirection[] -> IntArrayWrapper
            if (graphGenerator.nodeConnections != null)
            {
                foreach (var connectionArray in graphGenerator.nodeConnections)
                {
                    IntArrayWrapper wrapper = new IntArrayWrapper();
                    wrapper.values = connectionArray != null 
                        ? System.Array.ConvertAll(connectionArray, c => (int)c)
                        : System.Array.Empty<int>();
                    saveData.nodeConnections.Add(wrapper);
                }
            }

            string jsonData = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(filePath, jsonData);
            
            EditorUtility.DisplayDialog("Success", $"Graph saved to: {filePath}", "OK");
            Debug.Log($"Graph saved with {saveData.nodeConnections.Count} nodeConnections.");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to save graph: {e.Message}", "OK");
            Debug.LogError(e);
        }
    }
    
    private void LoadGraph()
    {
        if (graphGenerator == null)
        {
            EditorUtility.DisplayDialog("Error", "Please assign a Graph Generator first.", "OK");
            return;
        }
        
        if (!File.Exists(filePath))
        {
            EditorUtility.DisplayDialog("Error", $"File not found: {filePath}", "OK");
            return;
        }
        
        try
        {
            string jsonData = File.ReadAllText(filePath);
            GraphSaveData saveData = JsonUtility.FromJson<GraphSaveData>(jsonData);
            
            graphGenerator.nodes = new List<Vector3>(saveData.nodes);
            graphGenerator.radii = new List<float>(saveData.radii);
            graphGenerator.edges = new List<Vector2Int>(saveData.edges);
            graphGenerator.degrees = new List<int>(saveData.degrees);
            graphGenerator.connectToPorts = saveData.connectToPorts;

            // Convert IntArrayWrapper -> CardinalDirection[]
            graphGenerator.nodeConnections = new List<GraphGenerator.CardinalDirection[]>();
            if (saveData.nodeConnections != null)
            {
                foreach (var wrapper in saveData.nodeConnections)
                {
                    if (wrapper != null && wrapper.values != null)
                    {
                        GraphGenerator.CardinalDirection[] arr = new GraphGenerator.CardinalDirection[wrapper.values.Length];
                        for (int i = 0; i < wrapper.values.Length; i++)
                            arr[i] = (GraphGenerator.CardinalDirection)wrapper.values[i];
                        graphGenerator.nodeConnections.Add(arr);
                    }
                    else
                    {
                        graphGenerator.nodeConnections.Add(new GraphGenerator.CardinalDirection[0]);
                    }
                }
            }

            // Edge set rebuild
            graphGenerator.edgeSet ??= new HashSet<(int, int)>();
            graphGenerator.edgeSet.Clear();
            foreach (var edge in graphGenerator.edges)
            {
                int u = Mathf.Min(edge.x, edge.y);
                int v = Mathf.Max(edge.x, edge.y);
                graphGenerator.edgeSet.Add((u, v));
            }

            EditorUtility.DisplayDialog("Success", $"Graph loaded from: {filePath}", "OK");
            SceneView.RepaintAll();
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to load graph: {e.Message}", "OK");
            Debug.LogError(e);
        }
    }

    // --- helpers ---
    private GraphGenerator.CardinalDirection GetBestCardinalDirection(Vector3 direction)
    {
        float angleNorth = Vector3.Angle(direction, Vector3.forward);
        float angleEast = Vector3.Angle(direction, Vector3.right);
        float angleSouth = Vector3.Angle(direction, Vector3.back);
        float angleWest = Vector3.Angle(direction, Vector3.left);

        float minAngle = Mathf.Min(angleNorth, angleEast, angleSouth, angleWest);
        
        if (minAngle == angleNorth) return GraphGenerator.CardinalDirection.North;
        if (minAngle == angleEast) return GraphGenerator.CardinalDirection.East;
        if (minAngle == angleSouth) return GraphGenerator.CardinalDirection.South;
        return GraphGenerator.CardinalDirection.West;
    }
    
    [System.Serializable]
    public class IntArrayWrapper
    {
        public int[] values;
    }
    
    [System.Serializable]
    public class GraphSaveData
    {
        public List<Vector3> nodes = new();
        public List<float> radii = new();
        public List<Vector2Int> edges = new();
        public List<int> degrees = new();
        public List<IntArrayWrapper> nodeConnections = new(); // ✅ Unity serializable
        public bool connectToPorts;
    }
}
