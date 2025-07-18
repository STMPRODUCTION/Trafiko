using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiEnvironmentLoader : MonoBehaviour
{
    [SerializeField] private string sceneName = "Enviroment";
    [SerializeField] private int numberOfInstances = 4;
    [SerializeField] private float spacing = 50f; // Distance between environments
    
    void Awake()
    {
        LoadMultipleScenes();
    }
    
    void LoadMultipleScenes()
    {
        for (int i = 0; i < numberOfInstances; i++)
        {
            // Load scene additively
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }
        
        // After all scenes are loaded, position them
        StartCoroutine(PositionEnvironments());
    }
    
    System.Collections.IEnumerator PositionEnvironments()
    {
        yield return null; // Wait one frame for scenes to load
        
        int envIndex = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == sceneName && scene != SceneManager.GetActiveScene())
            {
                // Move all root objects in this scene
                GameObject[] rootObjects = scene.GetRootGameObjects();
                Vector3 offset = new Vector3(0, envIndex * spacing, 0);
                
                foreach (GameObject obj in rootObjects)
                {
                    obj.transform.position += offset;
                }
                envIndex++;
            }
        }
    }
}