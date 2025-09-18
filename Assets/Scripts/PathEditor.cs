using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class PathEditorWindow : EditorWindow
{
    [System.Serializable]
    public class PathData
    {
        public List<Vector3> points = new List<Vector3>();
    }

    private PathData pathData = new PathData();
    private GameObject newPointObj;

    [MenuItem("Tools/Path Editor")]
    public static void ShowWindow()
    {
        GetWindow<PathEditorWindow>("Path Editor");
    }

    private void OnGUI()
    {
        GUILayout.Label("Path Creator", EditorStyles.boldLabel);

        // Drag and drop new GameObject
        newPointObj = (GameObject)EditorGUILayout.ObjectField("New Point Object:", newPointObj, typeof(GameObject), true);

        if (GUILayout.Button("Add Point from GameObject") && newPointObj != null)
        {
            pathData.points.Add(newPointObj.transform.position);
            newPointObj = null;
        }

        GUILayout.Space(10);

        // Show current points
        GUILayout.Label("Current Points:");
        for (int i = 0; i < pathData.points.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            pathData.points[i] = EditorGUILayout.Vector3Field($"Point {i}", pathData.points[i]);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                pathData.points.RemoveAt(i);
            }
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Clear Path"))
        {
            pathData.points.Clear();
        }

        if (GUILayout.Button("Save Path (.cgraph)"))
        {
            SavePath();
        }
    }

    private void SavePath()
    {
        string json = JsonUtility.ToJson(pathData, true);
        string path = EditorUtility.SaveFilePanel("Save Path Data", Application.dataPath, "path", "cgraph");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            Debug.Log($"Path saved to {path}");
        }
    }

    // Draws the path in Scene view
    private void OnSceneGUI(SceneView sceneView)
    {
        if (pathData.points.Count > 1)
        {
            Handles.color = Color.green;
            for (int i = 0; i < pathData.points.Count - 1; i++)
            {
                Handles.DrawLine(pathData.points[i], pathData.points[i + 1]);
            }
        }
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
}
