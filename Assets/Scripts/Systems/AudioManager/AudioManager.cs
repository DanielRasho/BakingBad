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
    [SerializeField] private string kitchenSceneName = "MainMap";

    [Header("Configuracion")]
    [SerializeField] private bool loopLevelMusic = true;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] private float fadeDuration = 0.75f;

    [Header("Sonidos de click de boton")]
    [Tooltip("Click de UI general")]
    [SerializeField] private AudioClip menuButtonClickSound;
    [Tooltip("Click de la GUI de cocina")]
    [SerializeField] private AudioClip otherButtonClickSound;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("SFX - UI")]
    [SerializeField] private SfxCue uiClick = new SfxCue();
    [SerializeField] private SfxCue kitchenUiClick = new SfxCue();

    [Header("SFX - Cocina")]
    [Tooltip("Clicks de engranaje en orden. A la derecha suenan 1, 2, 3, 4, 1...; a la izquierda 4, 3, 2, 1, 4...")]
    [SerializeField] private SfxCue pieceRotate = new SfxCue();

    [Header("SFX - Ordenes")]
    [Tooltip("Papel al recoger una orden")]
    [SerializeField] private SfxCue orderPickup = new SfxCue(0.96f, 1.04f);
    [Tooltip("Al colocar la orden en la GUI de cocina para activarla.")]
    [SerializeField] private SfxCue orderActivate = new SfxCue();
    [Tooltip("Capa de papel de la entrega correcta. Vacio = usa el papel de 'Order Pickup'.")]
    [SerializeField] private SfxCue orderDeliveredPaper = new SfxCue(0.96f, 1.04f);
    [Tooltip("Capa de dinero de la entrega correcta. Suena un instante despues del papel.")]
    [SerializeField] private SfxCue orderDeliveredMoney = new SfxCue(1f, 1f, 0.12f);
    [Tooltip("Error al entregar el pastel al prisionero equivocado.")]
    [SerializeField] private SfxCue orderDeliveredWrong = new SfxCue();

    [Header("SFX - Voces")]
    [Tooltip("Cuantos efectos pueden sonar al mismo tiempo.")]
    [SerializeField, Range(2, 16)] private int sfxVoiceCount = 8;

    private AudioSource _source;
    private AudioSource _sfxSource;
    private AudioSource[] _sfxVoices;
    private int _nextVoice;
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

        _sfxVoices = new AudioSource[Mathf.Max(2, sfxVoiceCount)];
        for (int i = 0; i < _sfxVoices.Length; i++)
        {
            AudioSource voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
            voice.ignoreListenerPause = true;
            _sfxVoices[i] = voice;
        }
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

    public void SetSfxVolume(float volume01)
    {
        sfxVolume = Mathf.Clamp01(volume01);
        if (_sfxSource != null) _sfxSource.volume = sfxVolume;
    }

    public static void Play(SfxId id)
    {
        if (Instance != null) Instance.PlaySfx(id);
    }

    public void PlaySfx(SfxId id)
    {
        switch (id)
        {
            case SfxId.UiClick:
                PlayCue(uiClick, menuButtonClickSound);
                break;
            case SfxId.KitchenUiClick:
                PlayCue(kitchenUiClick, otherButtonClickSound);
                break;
            case SfxId.PieceRotate:
                PlayCue(pieceRotate, null, 1);
                break;
            case SfxId.OrderActivate:
                PlayCue(orderActivate);
                break;
            case SfxId.OrderPickup:
                PlayCue(orderPickup);
                break;
            case SfxId.OrderDeliveredCorrect:
                PlayCue(orderDeliveredPaper.HasClips ? orderDeliveredPaper : orderPickup);
                PlayCue(orderDeliveredMoney);
                break;
            case SfxId.OrderDeliveredWrong:
                PlayCue(orderDeliveredWrong);
                break;
        }
    }

    public static void PlayPieceRotate(bool clockwise)
    {
        if (Instance != null) Instance.PlayCue(Instance.pieceRotate, null, clockwise ? 1 : -1);
    }

    public void PlayMenuClickSound() => PlaySfx(SfxId.UiClick);

    public void PlayOtherClickSound() => PlaySfx(SfxId.KitchenUiClick);

    private void PlayCue(SfxCue cue, AudioClip fallbackClip = null, int sequenceDirection = 0)
    {
        if (cue == null || _sfxVoices == null) return;
        if (!cue.HasClips && fallbackClip == null) return;

        if (!cue.TryMarkPlayed(Time.unscaledTime)) return;

        AudioClip clip = sequenceDirection != 0 ? cue.NextClipInSequence(sequenceDirection) : cue.NextClip();
        if (clip == null) clip = fallbackClip;
        if (clip == null) return;

        AudioSource voice = GetFreeVoice();
        voice.clip = clip;
        voice.pitch = cue.NextPitch();
        voice.volume = cue.Volume * sfxVolume;

        if (cue.Delay > 0f) voice.PlayDelayed(cue.Delay);
        else voice.Play();
    }

    private AudioSource GetFreeVoice()
    {
        for (int i = 0; i < _sfxVoices.Length; i++)
        {
            int index = (_nextVoice + i) % _sfxVoices.Length;
            if (!_sfxVoices[index].isPlaying)
            {
                _nextVoice = (index + 1) % _sfxVoices.Length;
                return _sfxVoices[index];
            }
        }

        AudioSource stolen = _sfxVoices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _sfxVoices.Length;
        stolen.Stop();
        return stolen;
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
