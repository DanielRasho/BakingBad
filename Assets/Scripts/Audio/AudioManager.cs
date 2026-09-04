using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips (WAV)")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip levelMusic;

    [Header("Nombres de escena")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string kitchenSceneName = "KitchenCell";

    [Header("Configuracion")]
    [SerializeField] private bool loopLevelMusic = true;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] private float fadeDuration = 0.75f;

    [Header("Sonidos de click de boton")]
    [SerializeField] private AudioClip menuButtonClickSound;
    [SerializeField] private AudioClip otherButtonClickSound;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private AudioSource _source;
    private AudioSource _sfxSource;
    private Coroutine _fadeRoutine;
    private string _currentSceneName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.volume = musicVolume;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.volume = sfxVolume;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == _currentSceneName) return;
        _currentSceneName = scene.name;

        if (scene.name == kitchenSceneName)
        {
            PlayTrack(levelMusic, loopLevelMusic);
        }
        else if (scene.name == mainMenuSceneName)
        {
            PlayTrack(menuMusic, true);
        }
    }

    private void PlayTrack(AudioClip clip, bool loop)
    {
        if (clip == null) return;
        if (_source.clip == clip && _source.isPlaying) return;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(CrossfadeTo(clip, loop));
    }

    private IEnumerator CrossfadeTo(AudioClip clip, bool loop)
    {
        float startVolume = _source.volume;

        // Fade out de la pista actual
        float t = 0f;
        while (_source.isPlaying && t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
            yield return null;
        }

        _source.Stop();
        _source.clip = clip;
        _source.loop = loop;
        _source.volume = 0f;
        _source.Play();

        // Fade in de la nueva pista
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(0f, musicVolume, t / fadeDuration);
            yield return null;
        }
        _source.volume = musicVolume;
    }

    public void SetVolume(float volume01)
    {
        musicVolume = Mathf.Clamp01(volume01);
        if (_fadeRoutine == null) _source.volume = musicVolume;
    }

    public void PlayMenuClickSound() => PlaySfx(menuButtonClickSound);

    public void PlayOtherClickSound() => PlaySfx(otherButtonClickSound);

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }
}