using UnityEngine;
using UnityEngine.SceneManagement;

//This code was created by Google Gemini Pro 3.1 on 10/11/26

public class MainMenu : MonoBehaviour
{
    // The UI buttons will call this method and pass their specific ID (0-7)
    public void LoadEnvironment(int envIndex)
    {
        GameSettings.SelectedEnvironment = envIndex;

        // Change "MainGame" to exactly what your gameplay scene is named in the project
        SceneManager.LoadScene("OpenOcean");
    }
}