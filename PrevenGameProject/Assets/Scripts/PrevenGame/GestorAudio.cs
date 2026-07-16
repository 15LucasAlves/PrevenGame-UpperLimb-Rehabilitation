using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GestorAudio — Serviço central de áudio do jogo (persistente, no OmmoBootstrap).
///
/// Duas fontes 2D:
///   • Música — loop de fundo. Toca a <see cref="MusicaHub"/> automaticamente
///     sempre que a cena Menu carrega (splash/calibração/seleção/score) e para
///     ao entrar numa cena de minijogo. Basta atribuir o clip (Inspector ou
///     builder — ficheiro "backgroundMusic.mp3" na pasta Sounds and SFX).
///   • SFX — one-shots (scores, progresso, blips de diálogo).
///
/// Catálogo (preenchido pelo builder a partir de
/// Assets/Prefabs/PrevenGameAssets/Sounds and SFX/):
///   score: amazing/good/badScoreSFX → bandas ≥75 / ≥45 / resto (TocarScoreBanda)
///   progresso: completedArm (fim de bloco de braço), completedExercise (tabela),
///              drumRoll (rufo — disponível para reveals)
///   diálogo: short/longDialogue_Jane/Patrick — escolhido pelo orador e pelo
///            comprimento do texto; com dedupe (a mesma linha aparece no monitor
///            E no EcraVR — só toca uma vez)
///   dardos: dartThrow + barAmbience — reservados para o rework do minijogo.
/// </summary>
[DisallowMultipleComponent]
public class GestorAudio : MonoBehaviour
{
    public static GestorAudio Instancia { get; private set; }

    [Header("Música de fundo")]
    [Tooltip("Música do hub (Menu). Toca em loop fora dos minijogos.")]
    public AudioClip MusicaHub;
    [Range(0f, 1f)] public float VolumeMusica = 0.35f;

    [Header("SFX — score (bandas iguais às falas: ≥75 alto, ≥45 médio)")]
    public AudioClip SfxScoreAlto;
    public AudioClip SfxScoreMedio;
    public AudioClip SfxScoreBaixo;

    [Header("SFX — progresso")]
    public AudioClip SfxBracoConcluido;
    public AudioClip SfxExercicioConcluido;
    public AudioClip SfxRufo;

    [Header("SFX — diálogo (blip por fala)")]
    public AudioClip SfxDialogoCurtoJane;
    public AudioClip SfxDialogoLongoJane;
    public AudioClip SfxDialogoCurtoPatrick;
    public AudioClip SfxDialogoLongoPatrick;
    [Tooltip("Falas com mais caracteres que isto usam o blip longo.")]
    public int LimiarDialogoLongo = 60;

    [Header("SFX — dardos (rework futuro)")]
    public AudioClip SfxLancamentoDardo;
    public AudioClip AmbienteBarDardos;

    [Range(0f, 1f)] public float VolumeSfx = 1f;

    private AudioSource _musica;
    private AudioSource _sfx;
    private AudioClip _ultimoDialogo;
    private float _ultimoDialogoTempo = -1f;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(this); return; }
        Instancia = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);

        _musica = gameObject.AddComponent<AudioSource>();
        _musica.loop = true;
        _musica.playOnAwake = false;
        _musica.spatialBlend = 0f;

        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.spatialBlend = 0f;

        SceneManager.sceneLoaded += AoCarregarCena;
    }

    void Start() => AplicarMusicaParaCena(SceneManager.GetActiveScene());

    void OnDestroy()
    {
        if (Instancia != this) return;
        SceneManager.sceneLoaded -= AoCarregarCena;
        Instancia = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo) => AplicarMusicaParaCena(cena);

    /// <summary>Música do hub fora dos minijogos: toca no Menu, para nas outras cenas.</summary>
    void AplicarMusicaParaCena(Scene cena)
    {
        if (cena.name == SessionManager.CenaMenu) TocarMusica(MusicaHub);
        else                                      PararMusica();
    }

    // ── Música ────────────────────────────────────────────────────────
    public void TocarMusica(AudioClip clip)
    {
        if (clip == null) { PararMusica(); return; }
        if (_musica.clip == clip && _musica.isPlaying) return;
        _musica.clip   = clip;
        _musica.volume = VolumeMusica;
        _musica.Play();
    }

    public void PararMusica()
    {
        if (_musica.isPlaying) _musica.Stop();
    }

    // ── SFX ───────────────────────────────────────────────────────────
    public void TocarSfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        _sfx.PlayOneShot(clip, volume * VolumeSfx);
    }

    /// <summary>SFX da banda do score (mesmos limiares das falas dos helpers).</summary>
    public void TocarScoreBanda(float mediaPct)
    {
        TocarSfx(mediaPct >= 75f ? SfxScoreAlto
               : mediaPct >= 45f ? SfxScoreMedio
               :                   SfxScoreBaixo);
    }

    /// <summary>
    /// Blip de diálogo pelo orador e comprimento da fala. Com dedupe: a mesma
    /// linha é mostrada no diálogo do monitor E no EcraVR quase em simultâneo —
    /// pedidos do mesmo clip em menos de 0.2 s tocam só uma vez.
    /// </summary>
    public void TocarDialogo(HelperId quem, string texto)
    {
        bool longo = texto != null && texto.Length > LimiarDialogoLongo;
        AudioClip clip = quem == HelperId.Jane
            ? (longo ? SfxDialogoLongoJane    : SfxDialogoCurtoJane)
            : (longo ? SfxDialogoLongoPatrick : SfxDialogoCurtoPatrick);
        if (clip == null) return;

        if (clip == _ultimoDialogo && Time.unscaledTime - _ultimoDialogoTempo < 0.2f) return;
        _ultimoDialogo      = clip;
        _ultimoDialogoTempo = Time.unscaledTime;
        TocarSfx(clip);
    }
}
