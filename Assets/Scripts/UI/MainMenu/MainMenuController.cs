using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string kitchenSceneName = "MainMap";
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartGame);
            ButtonSfx.Attach(startButton, SfxId.UiClick);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
            ButtonSfx.Attach(quitButton, SfxId.UiClick);
        }
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        DayProgress.StartNewGame();
        SceneManager.LoadScene(kitchenSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
