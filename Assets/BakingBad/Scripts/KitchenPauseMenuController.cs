using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class KitchenPauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitToMainMenuButton;
    [SerializeField] private PrisonCookPlayerController playerController;
    [SerializeField] private KitchenOrderPrepUIController prepMenuController;

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool isPaused;
    private bool playerLockHeld;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    private void Awake()
    {
        EnsureReferences();
        WireButtons();
        SetMenuVisible(false);
        Time.timeScale = 1f;
    }

    private void OnDisable()
    {
        if (playerLockHeld && playerController != null)
        {
            playerController.RemoveInputLock();
            playerLockHeld = false;
        }

        Time.timeScale = 1f;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        bool toggleRequested = keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame;
        if (!toggleRequested)
        {
            return;
        }

        if (isPaused)
        {
            ResumeGame();
            return;
        }

        if (prepMenuController != null && prepMenuController.IsOpen)
        {
            return;
        }

        PauseGame();
#endif
    }

    public void PauseGame()
    {
        if (isPaused)
        {
            return;
        }

        EnsureReferences();
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetPlayerInputLocked(true);
        Time.timeScale = 0f;
        SetMenuVisible(true);
        isPaused = true;
    }

    public void ResumeGame()
    {
        if (!isPaused)
        {
            return;
        }

        Time.timeScale = 1f;
        SetPlayerInputLocked(false);
        SetMenuVisible(false);
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        isPaused = false;
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        SetPlayerInputLocked(false);
        playerLockHeld = false;
        isPaused = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void EnsureReferences()
    {
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PrisonCookPlayerController>();
        }

        if (prepMenuController == null)
        {
            prepMenuController = FindAnyObjectByType<KitchenOrderPrepUIController>();
        }
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(ResumeGame);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (quitToMainMenuButton != null)
        {
            quitToMainMenuButton.onClick.RemoveAllListeners();
            quitToMainMenuButton.onClick.AddListener(QuitToMainMenu);
        }
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(visible);
        }
    }

    private void SetPlayerInputLocked(bool locked)
    {
        EnsureReferences();

        if (playerController == null)
        {
            return;
        }

        if (locked && !playerLockHeld)
        {
            playerController.AddInputLock();
            playerLockHeld = true;
        }
        else if (!locked && playerLockHeld)
        {
            playerController.RemoveInputLock();
            playerLockHeld = false;
        }
    }
}
