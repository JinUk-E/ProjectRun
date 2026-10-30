using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoraeGames.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                if (button.name == "QuitButton") button.gameObject.SetActive(false);
#endif
        }
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
