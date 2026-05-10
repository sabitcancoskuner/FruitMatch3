using UnityEngine;

public class InGameUI : MonoBehaviour
{
    public static InGameUI Instance;

    [SerializeField] private GameObject gameWinPanel;
    [SerializeField] private GameObject gameLosePanel;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        LevelManager.Instance.OnGameWin += ActivateWinPanel;
        LevelManager.Instance.OnGameLose += ActivateLosePanel;
    }

    private void OnDisable()
    {
        LevelManager.Instance.OnGameWin -= ActivateWinPanel;
        LevelManager.Instance.OnGameLose -= ActivateLosePanel;
    }

    public void ActivateWinPanel()
    {
        GetComponent<Canvas>().sortingOrder = 10;
        gameWinPanel.SetActive(true);
    }

    public void ActivateLosePanel()
    {
        GetComponent<Canvas>().sortingOrder = 10;
        gameLosePanel.SetActive(true);
    }
}
