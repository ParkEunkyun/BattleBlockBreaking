using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleResultController : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "Scene_Lobby";

    public void GoToLobby()
    {
        BattleLoadoutSession.Clear();
        SceneManager.LoadScene(lobbySceneName);
    }
}