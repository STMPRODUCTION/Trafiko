using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GraphGenerator))]
public class PlanarGraphEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GraphGenerator script = (GraphGenerator)target;

        if (GUILayout.Button("Generate Graph"))
        {
            script.Generate();
        }
    }
}
