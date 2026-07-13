using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Qual dos helpers do jogo (Patrick à esquerda, Jane à direita no diálogo).</summary>
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

    // ── Calibração persistida (um conjunto por braço) ─────────────────
    /// <summary>Dados de calibração de UM braço — os braços podem ter comprimentos/ROM diferentes.</summary>
    [System.Serializable]
    public struct DadosBraco
    {
        public Vector3 PosOmbro;
        public float   ComprimentoBraco;
        public Vector3 DirecaoFrente;
        public bool    Valido;
    }

    public DadosBraco BracoDireito  { get; private set; }
    public DadosBraco BracoEsquerdo { get; private set; }

    /// <summary>Calibrado quando AMBOS os braços têm dados válidos.</summary>
    public bool Calibrado => BracoDireito.Valido && BracoEsquerdo.Valido;

    // Ponte temporária até ao rework do minijogo dos dardos: os scripts antigos
    // (MinigameController/GamificationManager) leem um conjunto único — espelham o braço direito.
    public Vector3 PosOmbro         { get; private set; }
    public float   ComprimentoBraco { get; private set; }
    public Vector3 DirecaoFrente    { get; private set; }

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
    /// <summary>Guarda a calibração de um braço (direito ou esquerdo).</summary>
    public void GuardarCalibracaoBraco(bool direito, Vector3 ombro, float comprimento, Vector3 direcao)
    {
        var dados = new DadosBraco
        {
            PosOmbro         = ombro,
            ComprimentoBraco = comprimento,
            DirecaoFrente    = direcao,
            Valido           = true,
        };

        if (direito)
        {
            BracoDireito = dados;
            // Ponte para os scripts antigos do minijogo (ver comentário nos campos).
            PosOmbro         = ombro;
            ComprimentoBraco = comprimento;
            DirecaoFrente    = direcao;
        }
        else BracoEsquerdo = dados;
    }

    /// <summary>Dados de calibração do braço pedido (direito = true).</summary>
    public DadosBraco ObterBraco(bool direito) => direito ? BracoDireito : BracoEsquerdo;

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
