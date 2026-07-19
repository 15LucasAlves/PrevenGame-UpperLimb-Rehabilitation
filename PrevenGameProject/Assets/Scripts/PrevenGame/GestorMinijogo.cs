using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GestorMinijogo — Coordenador de UMA cena de minijogo. Dono do loop de jogo:
///
///   1. Garante o ModoVR e o tracking Ommo (1 sensor).
///   2. Lê o minijogo atual do <see cref="SessionManager"/> (tipo + reps L/R).
///   3. Corre os blocos por braço — ESQUERDO primeiro, depois direito (blocos
///      com 0 reps são saltados) — anunciando a troca no <see cref="EcraVR"/>
///      (Jane = esquerdo, Patrick = direito) e repondo o contador do HUD.
///   4. Por repetição: gera os waypoints com o ombro LIVE
///      (<see cref="RastreadorCorpoJogador"/> + <see cref="ExerciciosWaypoints.Gerar"/>)
///      e delega a rep ao <see cref="MinijogoBase"/>; recolhe a % de cada rep.
///   5. No fim: tabela de score no EcraVR (pct por rep + médias), confirmação por
///      pressão BLE ou Enter, e hand-off <c>RegistarScore(média global)</c> +
///      <c>CarregarProximo()</c>.
///
/// Espelha ainda a pausa do operador (ESC/PauseMenu, Time.timeScale = 0) no EcraVR.
/// </summary>
public class GestorMinijogo : MonoBehaviour
{
    [Header("Referências (auto-encontradas se vazias)")]
    public MinijogoBase Minijogo;
    public EcraVR Ecra;
    public MaoJogador Mao;
    public RastreadorCorpoJogador Rastreador;
    public OmmoSensorManager SensorManager;
    public EntradaPressao Pressao;

    [Header("Ritmo")]
    [Tooltip("Segundos que a fala de anúncio do braço fica visível antes da 1.ª rep.")]
    public float SegundosAnuncioBraco = 3f;
    [Tooltip("Segundos de intervalo entre repetições.")]
    public float SegundosEntreReps = 1f;
    [Tooltip("Segundos que o feedback final fica visível antes de voltar ao Menu (score no monitor).")]
    public float SegundosMostrarResultado = 4f;

    enum Estado { AguardarSensor, AnunciarBraco, EmRep, EntreReps, Tabela, Concluido }

    struct Bloco { public bool Direito; public int Reps; }

    private Estado _estado = Estado.AguardarSensor;
    private readonly List<Bloco> _blocos = new List<Bloco>();
    private int _iBloco;
    private int _rep;                 // 1-based dentro do bloco
    private float _timer;
    private bool  _pressaoPendente;
    private bool  _pausaVisivel;
    private bool  _ecraVisivelAntesPausa;
    private ExerciciosWaypoints.TipoExercicio _tipo;
    private readonly List<float> _pctsEsq = new List<float>();
    private readonly List<float> _pctsDir = new List<float>();
    private HudVR _hud;

    // ── Arranque ──────────────────────────────────────────────────────
    void Start()
    {
        if (Minijogo      == null) Minijogo      = FindObjectOfType<MinijogoBase>();
        if (Ecra          == null) Ecra          = FindObjectOfType<EcraVR>(true);
        if (Mao           == null) Mao           = FindObjectOfType<MaoJogador>();
        if (Rastreador    == null) Rastreador    = FindObjectOfType<RastreadorCorpoJogador>();
        if (SensorManager == null) SensorManager = FindObjectOfType<OmmoSensorManager>();
        if (Pressao       == null) Pressao       = FindObjectOfType<EntradaPressao>();

        if (Minijogo == null)
        {
            Debug.LogError("[GestorMinijogo] Sem MinijogoBase na cena — nada para correr.");
            enabled = false;
            return;
        }

        // Minijogos correm em VR (se falhar, continua no monitor com fallback de calibração).
        GestorXR.ObterOuCriar().ModoVR();

        // Em Modo Comando não há hardware Ommo — o tracking nem arranca.
        if (!EmModoComando && SensorManager != null) SensorManager.IniciarTracking(1);
        if (Pressao != null) Pressao.OnPressao += AoPressao;

        Minijogo.OnRepConcluida += AoRepConcluida;

        // Fila do SessionManager (fallback para teste da cena isolada).
        var sm = SessionManager.Instancia;
        int repsL = 1, repsR = 1;
        _tipo = ExerciciosWaypoints.TipoExercicio.FlexaoBraco;
        if (sm != null && sm.TemAtual)
        {
            _tipo = sm.Atual.Tipo;
            repsL = sm.Atual.RepsL;
            repsR = sm.Atual.RepsR;
        }
        else Debug.LogWarning("[GestorMinijogo] Sem sessão ativa — a usar FlexaoBraco 1L/1R (teste).");

        if (repsL > 0) _blocos.Add(new Bloco { Direito = false, Reps = repsL }); // esquerdo primeiro
        if (repsR > 0) _blocos.Add(new Bloco { Direito = true,  Reps = repsR });
        if (_blocos.Count == 0) _blocos.Add(new Bloco { Direito = true, Reps = 1 });
    }

    void OnDestroy()
    {
        if (Minijogo != null) Minijogo.OnRepConcluida -= AoRepConcluida;
        if (Pressao  != null) Pressao.OnPressao       -= AoPressao;
    }

    // ── Loop ──────────────────────────────────────────────────────────
    void Update()
    {
        EspelharPausa();
        if (Time.timeScale == 0f) return;

        switch (_estado)
        {
            case Estado.AguardarSensor:
                if (Mao != null && Mao.Ativa)
                {
                    _hud = HudVR.ObterOuCriar();
                    IniciarBloco(0);
                }
                break;

            case Estado.AnunciarBraco:
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    if (Ecra != null) Ecra.Mostrar(false);
                    IniciarRep();
                }
                break;

            case Estado.EntreReps:
                _timer -= Time.deltaTime;
                if (_timer <= 0f) IniciarRep();
                break;

            case Estado.Tabela:
                // Sem confirmação: o feedback fica uns segundos e volta-se ao Menu
                // (o score detalhado aparece no MONITOR; o HMD mostra o placard
                // "Remove os óculos" automaticamente em ModoMonitor).
                _timer -= Time.deltaTime;
                if (_timer <= 0f) Concluir();
                break;
        }
    }

    void IniciarBloco(int indice)
    {
        _iBloco = indice;
        _rep    = 1;
        var bloco = _blocos[_iBloco];

        // O yaw de corpo alinha-se com a cabeça no início de cada bloco.
        if (Rastreador != null) Rastreador.ReporYawCorpo();

        // Em Modo Comando, a mão passa a ser o comando do lado do braço em jogo.
        if (Mao != null) Mao.DefinirMaoComando(bloco.Direito);

        // HUD: demo do exercício + contador reposto para este braço.
        if (_hud != null)
        {
            _hud.Mostrar(true);
            _hud.DefinirExercicio(_tipo);
            _hud.DefinirReps(_rep, bloco.Reps);
        }

        // Anúncio no painel do EcraVR.
        if (Ecra != null)
        {
            Ecra.Mostrar(true);
            if (Ecra.PainelTabela != null) Ecra.PainelTabela.SetActive(false);
            string lado = bloco.Direito ? "direito" : "esquerdo";
            Ecra.MostrarTexto($"Agora com o braço {lado}! Segue os alvos com a mão.");
        }

        _timer  = SegundosAnuncioBraco;
        _estado = Estado.AnunciarBraco;
    }

    void IniciarRep()
    {
        var bloco = _blocos[_iBloco];
        var sm = SessionManager.Instancia;
        var dados = sm != null ? sm.ObterBraco(bloco.Direito) : default;

        // Ombro/direção LIVE no início de cada rep (segue a câmara; estável durante a rep).
        Vector3 ombro  = Rastreador != null ? Rastreador.ObterOmbroAtual(bloco.Direito) : dados.PosOmbro;
        Vector3 frente = Rastreador != null ? Rastreador.ObterDirecaoFrenteAtual(bloco.Direito) : dados.DirecaoFrente;

        var cfg = new MinijogoBase.ConfigRep
        {
            Tipo             = _tipo,
            BracoDireito     = bloco.Direito,
            RepAtual         = _rep,
            TotalReps        = bloco.Reps,
            Waypoints        = ExerciciosWaypoints.Gerar(_tipo, ombro, dados.ComprimentoBraco, frente, bloco.Direito),
            Mao              = Mao,
            Rastreador       = Rastreador,
            ComprimentoBraco = dados.ComprimentoBraco,
        };

        if (_hud != null) _hud.DefinirReps(_rep, bloco.Reps);
        _estado = Estado.EmRep;
        Minijogo.PrepararRep(cfg);
    }

    void AoRepConcluida(float pct)
    {
        if (_estado != Estado.EmRep) return;

        var bloco = _blocos[_iBloco];
        (bloco.Direito ? _pctsDir : _pctsEsq).Add(pct);
        Debug.Log($"[GestorMinijogo] Rep {_rep}/{bloco.Reps} braço {(bloco.Direito ? "dir" : "esq")}: {pct:F0} %");

        if (_rep < bloco.Reps)
        {
            _rep++;
            _timer  = SegundosEntreReps;
            _estado = Estado.EntreReps;
        }
        else if (_iBloco + 1 < _blocos.Count)
        {
            // Bloco de um braço concluído — SFX e passa ao outro braço.
            GestorAudio.Instancia?.TocarSfx(GestorAudio.Instancia.SfxBracoConcluido);
            IniciarBloco(_iBloco + 1);
        }
        else
        {
            MostrarTabela();
        }
    }

    // ── Fim do minijogo (feedback curto; a tabela detalhada fica só no log) ──
    void MostrarTabela()
    {
        _estado = Estado.Tabela;
        GestorAudio.Instancia?.TocarSfx(GestorAudio.Instancia.SfxExercicioConcluido);
        if (_hud != null) _hud.Mostrar(false);

        Debug.Log($"[GestorMinijogo] Resultados:\n{ConstruirTextoTabela()}");

        _timer = SegundosMostrarResultado;

        if (Ecra != null)
        {
            Ecra.Mostrar(true);
            if (Ecra.PainelTabela != null) Ecra.PainelTabela.SetActive(false);

            // Só o feedback; o resultado detalhado vê-se no monitor a seguir.
            // Com mais minijogos na fila, o jogador FICA de óculos postos.
            float media = MediaGlobal();
            string elogio = media >= 75f ? "Excelente trabalho!"
                          : media >= 45f ? "Bom trabalho!"
                          :                "Boa tentativa — continua!";
            bool haProximo = SessionManager.Instancia != null && SessionManager.Instancia.TemProximo;
            Ecra.MostrarTexto($"{elogio}\n" + (haProximo
                ? "Espera enquanto carregamos o próximo exercício…"
                : "Remove os óculos para veres o teu resultado."));
        }
    }

    string ConstruirTextoTabela()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(ExerciciosWaypoints.Nome(_tipo));
        sb.AppendLine();
        AcrescentarBraco(sb, "Braço esquerdo", _pctsEsq);
        AcrescentarBraco(sb, "Braço direito",  _pctsDir);
        sb.AppendLine();
        sb.AppendLine($"Média: {MediaGlobal():F0} %");
        return sb.ToString();
    }

    static void AcrescentarBraco(System.Text.StringBuilder sb, string titulo, List<float> pcts)
    {
        if (pcts.Count == 0) return;
        sb.AppendLine($"{titulo}:");
        float soma = 0f;
        for (int i = 0; i < pcts.Count; i++)
        {
            sb.AppendLine($"  Rep {i + 1} — {pcts[i]:F0} %");
            soma += pcts[i];
        }
        sb.AppendLine($"  Média — {soma / pcts.Count:F0} %");
    }

    /// <summary>Média de % entre TODAS as repetições (os dois braços).</summary>
    float MediaGlobal()
    {
        float soma = 0f; int n = 0;
        foreach (var p in _pctsEsq) { soma += p; n++; }
        foreach (var p in _pctsDir) { soma += p; n++; }
        return n > 0 ? soma / n : 0f;
    }

    void Concluir()
    {
        if (_estado == Estado.Concluido) return;
        _estado = Estado.Concluido;

        Minijogo.Terminar();
        if (Ecra != null) Ecra.Mostrar(false);
        if (_hud != null) _hud.Mostrar(false);

        var sm = SessionManager.Instancia;
        if (sm != null)
        {
            sm.RegistarScore(MediaGlobal());
            sm.CarregarProximo(); // próximo minijogo ou Menu (fase Score)
        }
        else Debug.LogWarning("[GestorMinijogo] Sem SessionManager — fim sem hand-off (teste isolado).");
    }

    // ── Entradas / pausa ──────────────────────────────────────────────
    static bool EmModoComando =>
        SessionManager.Instancia != null && SessionManager.Instancia.ModoComando;

    void AoPressao()
    {
        if (_estado == Estado.Tabela) _pressaoPendente = true;
    }

    /// <summary>Espelha a pausa do operador (Time.timeScale = 0) no EcraVR do paciente.</summary>
    void EspelharPausa()
    {
        bool pausado = Time.timeScale == 0f;
        if (pausado == _pausaVisivel || Ecra == null) return;
        _pausaVisivel = pausado;

        if (pausado)
        {
            _ecraVisivelAntesPausa = Ecra.gameObject.activeSelf;
            Ecra.Mostrar(true);
            Ecra.MostrarPausa(true);
        }
        else
        {
            Ecra.MostrarPausa(false);
            if (!_ecraVisivelAntesPausa) Ecra.gameObject.SetActive(false);
        }
    }
}
