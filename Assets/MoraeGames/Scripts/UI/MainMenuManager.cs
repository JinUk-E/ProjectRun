using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoraeGames.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        public void LoadLobbyScene()
        {
            Debug.Log("Loading Lobby Scene...");
            SceneManager.LoadScene("Lobby");
        }

        public void QuitGame()
        {
            Debug.Log("Quitting Game...");
            Application.Quit();
        }
    }
}
