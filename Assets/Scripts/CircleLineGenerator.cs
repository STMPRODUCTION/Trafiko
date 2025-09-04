using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class CircleLineGenerator : EditorWindow
{
    [MenuItem("Tools/Circle Line Generator")]
    public static void ShowWindow()
    {
        GetWindow<CircleLineGenerator>("Circle Line Generator");
    }

    public float radius = 5f;
    public int lineCount = 10;
    public float minDistanceBetweenIntersections = 0.5f;
    public float nodeSize = 0.1f;
    
    private List<Vector2> points = new List<Vector2>();
    private List<Line> lines = new List<Line>();
    private List<Vector2> intersections = new List<Vector2>();
    
    private void OnGUI()
    {
        GUILayout.Label("Circle Line Generator", EditorStyles.boldLabel);
        
        radius = EditorGUILayout.FloatField("Circle Radius", radius);
        lineCount = EditorGUILayout.IntField("Number of Lines", lineCount);
        minDistanceBetweenIntersections = EditorGUILayout.FloatField("Min Distance Between Intersections", minDistanceBetweenIntersections);
        nodeSize = EditorGUILayout.FloatField("Node Size", nodeSize);
        
        if (GUILayout.Button("Generate"))
        {
            Generate();
        }
        
        if (GUILayout.Button("Clear"))
        {
            points.Clear();
            lines.Clear();
            intersections.Clear();
            SceneView.RepaintAll();
        }
    }
    
    private void Generate()
    {
        points.Clear();
        lines.Clear();
        intersections.Clear();
        
        // Generate points on the circle
        for (int i = 0; i < lineCount * 2; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        
        // Create lines by connecting points
        for (int i = 0; i < points.Count; i += 2)
        {
            if (i + 1 < points.Count)
            {
                lines.Add(new Line(points[i], points[i + 1]));
            }
        }
        
        // Find all intersections
        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                if (LineIntersection(lines[i], lines[j], out Vector2 intersection))
                {
                    // Check if intersection is too close to existing intersections
                    bool tooClose = false;
                    foreach (var existingIntersection in intersections)
                    {
                        if (Vector2.Distance(intersection, existingIntersection) < minDistanceBetweenIntersections)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    
                    if (!tooClose)
                    {
                        intersections.Add(intersection);
                    }
                }
            }
        }
        
        // Remove lines with no intersections
        lines.RemoveAll(line => !LineHasIntersections(line));
        
        SceneView.RepaintAll();
    }
    
    private bool LineHasIntersections(Line line)
    {
        foreach (var intersection in intersections)
        {
            // Check if the intersection point lies on the line segment
            if (PointOnLineSegment(line.start, line.end, intersection))
            {
                return true;
            }
        }
        return false;
    }
    
    private bool PointOnLineSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        // Check if point is on the line segment between a and b
        float cross = (point.y - a.y) * (b.x - a.x) - (point.x - a.x) * (b.y - a.y);
        if (Mathf.Abs(cross) > 0.01f) return false;
        
        float dot = (point.x - a.x) * (b.x - a.x) + (point.y - a.y) * (b.y - a.y);
        if (dot < 0) return false;
        
        float squaredLength = (b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y);
        if (dot > squaredLength) return false;
        
        return true;
    }
    
    private bool LineIntersection(Line line1, Line line2, out Vector2 intersection)
    {
        intersection = Vector2.zero;
        
        Vector2 a = line1.start;
        Vector2 b = line1.end;
        Vector2 c = line2.start;
        Vector2 d = line2.end;
        
        // Calculate determinants
        float det = (a.x - b.x) * (c.y - d.y) - (a.y - b.y) * (c.x - d.x);
        if (det == 0) return false; // Lines are parallel
        
        float t = ((a.x - c.x) * (c.y - d.y) - (a.y - c.y) * (c.x - d.x)) / det;
        float u = -((a.x - b.x) * (a.y - c.y) - (a.y - b.y) * (a.x - c.x)) / det;
        
        if (t < 0 || t > 1 || u < 0 || u > 1) return false; // Intersection is outside line segments
        
        intersection.x = a.x + t * (b.x - a.x);
        intersection.y = a.y + t * (b.y - a.y);
        
        return true;
    }
    
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }
    
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
    
    private void OnSceneGUI(SceneView sceneView)
    {
        // Draw circle
        Handles.color = Color.white;
        Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
        
        // Draw lines
        foreach (var line in lines)
        {
            Handles.color = Color.blue;
            Handles.DrawLine(line.start, line.end);
        }
        
        // Draw intersection nodes
        foreach (var intersection in intersections)
        {
            Handles.color = Color.red;
            Handles.DrawSolidDisc(intersection, Vector3.forward, nodeSize);
        }
        
        // Draw points on circle
        foreach (var point in points)
        {
            Handles.color = Color.green;
            Handles.DrawSolidDisc(point, Vector3.forward, nodeSize * 0.5f);
        }
    }
    
    private class Line
    {
        public Vector2 start;
        public Vector2 end;
        
        public Line(Vector2 start, Vector2 end)
        {
            this.start = start;
            this.end = end;
        }
    }
}