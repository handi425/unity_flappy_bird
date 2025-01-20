using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1)] // Execute before other scripts to ensure proper initialization
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private UIDocument gameUI;
    [SerializeField] private UIDocument mainMenuUI;

    [Header("Audio")]
    [SerializeField] private AudioClip scoreSound;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Game References")]
    [SerializeField] private Bird birdPrefab;
    [SerializeField] private PipeSpawner pipeSpawnerPrefab;

    // Cached components
    private AudioSource audioSource;
    private Bird activeBird;
    private PipeSpawner activeSpawner;

    // Cached UI elements
    private Label scoreLabel;
    private Label finalScoreLabel;
    private Label highScoreLabel;
    private Label menuHighScoreLabel;
    private VisualElement gameOverPanel;
    private VisualElement mainMenuPanel;
    private Button restartButton;
    private Button playButton;

    // Game state
    private int currentScore;
    public bool IsGameActive { get; private set; }
    public bool IsGameOver { get; private set; }

    // Cached values
    private static readonly StyleScale normalScale = new StyleScale(new Vector2(1f, 1f));
    private static readonly StyleScale enlargedScale = new StyleScale(new Vector2(1.2f, 1.2f));
    private static readonly DisplayStyle flexStyle = DisplayStyle.Flex;
    private static readonly DisplayStyle noneStyle = DisplayStyle.None;
    private WaitForSeconds gameOverDelay;
    private readonly string highScoreKey = "HighScore";
    private string cachedScoreText = string.Empty;
    private string cachedHighScoreText = string.Empty;

    // Reusable UI animation values
    private StyleScale currentScoreScale;
    private IVisualElementScheduledItem scoreScaleAnimation;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeComponents();
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeGameObjects();
        SetupUI();
    }

    private void InitializeComponents()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        gameOverDelay = new WaitForSeconds(1f);
        cachedScoreText = "0";
        cachedHighScoreText = "High Score: 0";
        currentScoreScale = normalScale;
    }

    private void InitializeGameObjects()
    {
        // Initialize bird
        if (birdPrefab != null && activeBird == null)
        {
            activeBird = Instantiate(birdPrefab);
            activeBird.gameObject.SetActive(false);
        }

        // Initialize pipe spawner
        if (pipeSpawnerPrefab != null && activeSpawner == null)
        {
            activeSpawner = Instantiate(pipeSpawnerPrefab);
            activeSpawner.gameObject.SetActive(true);
        }
    }

    private void SetupUI()
    {
        SetupMainMenu();
        SetupGameUI();
        ShowMainMenu(true);
    }

    private void SetupMainMenu()
    {
        if (mainMenuUI == null || mainMenuUI.rootVisualElement == null) return;

        var root = mainMenuUI.rootVisualElement;
        mainMenuPanel = root.Q<VisualElement>("main-menu");
        playButton = root.Q<Button>("playButton");
        menuHighScoreLabel = root.Q<Label>("menuHighScore");

        if (playButton != null)
        {
            playButton.clicked += StartGame;
        }

        UpdateHighScoreDisplay();
    }

    private void SetupGameUI()
    {
        if (gameUI == null || gameUI.rootVisualElement == null) return;

        var root = gameUI.rootVisualElement;
        scoreLabel = root.Q<Label>("scoreLabel");
        gameOverPanel = root.Q<VisualElement>("gameOverPanel");
        restartButton = root.Q<Button>("restartButton");
        finalScoreLabel = root.Q<Label>("finalScoreLabel");
        highScoreLabel = root.Q<Label>("highScoreLabel");

        if (restartButton != null)
        {
            restartButton.clicked += RestartGame;
        }

        if (scoreLabel != null) scoreLabel.style.display = noneStyle;
        if (gameOverPanel != null) gameOverPanel.style.display = noneStyle;
    }

    private void ShowMainMenu(bool show)
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.style.display = show ? flexStyle : noneStyle;
        }

        if (scoreLabel != null)
        {
            scoreLabel.style.display = show ? noneStyle : flexStyle;
        }

        UpdateHighScoreDisplay();
    }

    private void UpdateHighScoreDisplay()
    {
        int highScore = PlayerPrefs.GetInt(highScoreKey, 0);
        cachedHighScoreText = $"High Score: {highScore}";

        if (menuHighScoreLabel != null)
        {
            menuHighScoreLabel.text = cachedHighScoreText;
        }
        if (highScoreLabel != null)
        {
            highScoreLabel.text = cachedHighScoreText;
        }
    }

    public void StartGame()
    {
        PlayButtonSound();
        IsGameActive = true;
        IsGameOver = false;
        currentScore = 0;
        UpdateScoreUI();
        ShowMainMenu(false);

        if (activeBird != null)
        {
            activeBird.gameObject.SetActive(true);
            activeBird.StartMoving();
        }

        if (activeSpawner != null)
        {
            activeSpawner.ResetSpawner();
            activeSpawner.enabled = true;
        }
    }

    public void AddScore()
    {
        if (IsGameOver || !IsGameActive) return;
        
        currentScore++;
        UpdateScoreUI();
        PlayScoreSound();
        
        if (currentScore % 10 == 0 && activeSpawner != null)
        {
            activeSpawner.IncreaseSpeed();
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreLabel == null) return;

        cachedScoreText = currentScore.ToString();
        scoreLabel.text = cachedScoreText;
        
        scoreScaleAnimation?.Pause();
        scoreLabel.style.scale = enlargedScale;
        scoreScaleAnimation = scoreLabel.schedule.Execute(() => 
        {
            scoreLabel.style.scale = normalScale;
        }).StartingIn(100);

        int highScore = PlayerPrefs.GetInt(highScoreKey, 0);
        if (currentScore > highScore)
        {
            PlayerPrefs.SetInt(highScoreKey, currentScore);
            UpdateHighScoreDisplay();
        }
    }

    public void GameOver()
    {
        if (IsGameOver || !IsGameActive) return;
        
        IsGameOver = true;
        IsGameActive = false;

        if (gameOverSound != null)
        {
            audioSource.PlayOneShot(gameOverSound);
        }

        if (activeSpawner != null)
        {
            activeSpawner.enabled = false;
        }

        StartCoroutine(ShowGameOverUIDelayed());
    }

    private IEnumerator ShowGameOverUIDelayed()
    {
        yield return gameOverDelay;
        
        if (gameOverPanel != null)
        {
            gameOverPanel.style.opacity = 0;
            gameOverPanel.style.display = flexStyle;
            
            float time = 0;
            while (time < 1f)
            {
                time += Time.deltaTime;
                gameOverPanel.style.opacity = time;
                yield return null;
            }
            
            if (finalScoreLabel != null)
            {
                finalScoreLabel.text = cachedScoreText;
            }
        }
    }

    private void RestartGame()
    {
        PlayButtonSound();
        ShowMainMenu(true);
        
        IsGameOver = false;
        IsGameActive = false;
        currentScore = 0;
        
        if (gameOverPanel != null)
        {
            gameOverPanel.style.display = noneStyle;
        }

        // Clean up before scene reload
        if (activeSpawner != null)
        {
            activeSpawner.ResetSpawner();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void PlayScoreSound()
    {
        if (audioSource != null && scoreSound != null)
        {
            audioSource.PlayOneShot(scoreSound);
        }
    }

    private void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}