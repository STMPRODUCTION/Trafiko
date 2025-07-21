using UnityEngine;

public class DebugSphereDrawer : MonoBehaviour
{
    [Header("Sphere Settings")]
    public Vector3 sphereCenter = Vector3.zero;
    public float radius = 5f;
    public Color sphereColor = Color.red;
    public bool showInEditor = true;
    public bool showInGame = false;
    
    [Header("Visual Quality")]
    [Range(8, 64)]
    public int segments = 32; // How detailed the sphere wireframe is

    void OnDrawGizmos()
    {
        if (!showInEditor) return;
        
        DrawWireframeSphere();
    }

    void OnDrawGizmosSelected()
    {
        if (!showInEditor) return;
        
        // Draw a more detailed version when selected
        Gizmos.color = new Color(sphereColor.r, sphereColor.g, sphereColor.b, 0.8f);
        DrawWireframeSphere();
    }

    void DrawWireframeSphere()
    {
        Gizmos.color = sphereColor;
        
        // Draw three circles (XY, XZ, YZ planes) to form a wireframe sphere
        DrawCircle(sphereCenter, radius, Vector3.forward); // XY plane
        DrawCircle(sphereCenter, radius, Vector3.right);   // YZ plane  
        DrawCircle(sphereCenter, radius, Vector3.up);      // XZ plane
        
        // Optional: Draw a solid sphere with transparency (uncomment if desired)
        // Gizmos.color = new Color(sphereColor.r, sphereColor.g, sphereColor.b, 0.1f);
        // Gizmos.DrawSphere(sphereCenter, radius);
    }

    void DrawCircle(Vector3 center, float radius, Vector3 normal)
    {
        Vector3 prevPoint = Vector3.zero;
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            Vector3 point = GetPointOnCircle(center, radius, angle, normal);
            
            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }
            
            prevPoint = point;
        }
    }

    Vector3 GetPointOnCircle(Vector3 center, float radius, float angle, Vector3 normal)
    {
        // Create two perpendicular vectors to the normal
        Vector3 tangent1 = Vector3.Cross(normal, Vector3.up);
        if (tangent1.magnitude < 0.1f)
            tangent1 = Vector3.Cross(normal, Vector3.right);
        tangent1 = tangent1.normalized;
        
        Vector3 tangent2 = Vector3.Cross(normal, tangent1).normalized;
        
        // Calculate point on circle
        return center + (tangent1 * Mathf.Cos(angle) + tangent2 * Mathf.Sin(angle)) * radius;
    }

    // Optional: Show sphere in game mode too
    void Update()
    {
        if (showInGame)
        {
            Debug.DrawLine(sphereCenter + Vector3.left * radius, sphereCenter + Vector3.right * radius, sphereColor);
            Debug.DrawLine(sphereCenter + Vector3.down * radius, sphereCenter + Vector3.up * radius, sphereColor);
            Debug.DrawLine(sphereCenter + Vector3.back * radius, sphereCenter + Vector3.forward * radius, sphereColor);
        }
    }
}