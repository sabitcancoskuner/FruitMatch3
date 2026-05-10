using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName;

    public void PlayLevel(int levelID)
    {
        LevelManager.TargetLevelId = levelID;

        SceneManager.LoadScene(gameplaySceneName);
    }
}
