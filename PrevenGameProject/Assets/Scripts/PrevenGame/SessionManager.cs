using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Qual dos helpers do jogo. Um faz a calibração, o outro o tutorial da seleção.</summary>
public enum HelperId { Jane, Patrick }

/// <summary>
/// SessionManager — Estado persistente entre cenas (DontDestroyOnLoad).
///
/// Como cada minijogo é a sua própria cena (recarregada ao entrar/sair), a calibração, a fila
/// de minijogos selecionados e os scores têm de sobreviver às trocas de cena. Segue o padrão
/// de singleton persistente do <see cref="OmmoBootstrap"/>.
/// </summary>
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instancia { get; private set; }

    public const string CenaMenu = "Menu";

    // ── Calibração persistida ─────────────────────────────────────────
    public bool    Calibrado        { get; private set; }
    public Vector3 PosOmbro         { get; private set; }
    public float   ComprimentoBraco { get; private set; }
    public Vector3 DirecaoFrente    { get; private set; }

    /// <summary>Helper que guiou a calibração; o OUTRO dá o tutorial da seleção.</summary>
    public HelperId HelperCalibracao { get; set; } = HelperId.Jane;
    public HelperId HelperTutorial => HelperCalibracao == HelperId.Jane ? HelperId.Patrick : HelperId.Jane;

    // ── Fila de minijogos + scores ────────────────────────────────────
    public struct Minijogo
    {
        public ExerciciosWaypoints.TipoExercicio Tipo;
        public int    RepsL;   // repetições braço esquerdo
        public int    RepsR;   // repetições braço direito
        public string Cena;    // cena a carregar para este minijogo
    }

    public struct Resultado
    {
        public ExerciciosWaypoints.TipoExercicio Tipo;
        public float PctMedia;
    }

    private readonly List<Minijogo>  _fila   = new List<Minijogo>();
    private readonly List<Resultado> _scores = new List<Resultado>();
    private int _indice = -1;

    public IReadOnlyList<Resultado> Scores => _scores;

    /// <summary>True quando um minijogo terminou a fila e o Menu deve mostrar a fase Score.</summary>
    public bool TemScoresPendentes { get; private set; }

    public bool     TemAtual => _indice >= 0 && _indice < _fila.Count;
    public Minijogo Atual    => TemAtual ? _fila[_indice] : default;

    // ── Unity ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Calibração ────────────────────────────────────────────────────
    public void GuardarCalibracao(Vector3 ombro, float comprimento, Vector3 direcao)
    {
        PosOmbro         = ombro;
        ComprimentoBraco = comprimento;
        DirecaoFrente    = direcao;
        Calibrado        = true;
    }

    // ── Sessão de minijogos ───────────────────────────────────────────
    public void IniciarSessao(List<Minijogo> fila)
    {
        _fila.Clear();
        _fila.AddRange(fila);
        _scores.Clear();
        _indice = -1;
        TemScoresPendentes = false;
    }

    public void RegistarScore(float pct)
    {
        if (!TemAtual) return;
        _scores.Add(new Resultado { Tipo = _fila[_indice].Tipo, PctMedia = pct });
    }

    /// <summary>Carrega o próximo minijogo da fila; se não houver, volta ao Menu em fase Score.</summary>
    public void CarregarProximo()
    {
        _indice++;
        if (_indice < _fila.Count)
        {
            SceneManager.LoadScene(_fila[_indice].Cena);
        }
        else
        {
            TemScoresPendentes = true;
            SceneManager.LoadScene(CenaMenu);
        }
    }

    public void LimparScoresPendentes() => TemScoresPendentes = false;

    /// <summary>Aborta a sessão atual e volta ao Menu (ex.: botão Main Menu na pausa).</summary>
    public void VoltarAoMenu()
    {
        _fila.Clear();
        _scores.Clear();
        _indice = -1;
        TemScoresPendentes = false;
        SceneManager.LoadScene(CenaMenu);
    }
}
