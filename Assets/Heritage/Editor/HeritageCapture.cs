using UnityEditor;
using UnityEditor.SceneManagement;

namespace VirtualHeritage.Editor
{
    public static class HeritageCapture
    {
        [MenuItem("Heritage/Capture Preview Shots")]
        public static void Run()
        {
            HeritageProject.Configure();
            EditorSceneManager.OpenScene("Assets/Heritage/Scenes/Heritage.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
