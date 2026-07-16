using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameFlowManager — Máquina de fases da cena hub "Menu".
///
/// Fases: Splash → Calibração (helpers guiam) → Seleção (tutorial + cards) → [Score].
/// A fase de entrada é decidida a partir do <see cref="SessionManager"/>:
///   • scores pendentes  → Score (voltou de uma sessão de minijogos)
///   • já calibrado       → Seleção (salta a calibração)
///   • caso contrário     → Splash → Calibração
///
/// Transições com fade (<see cref="ScreenFader"/>). O Ommo só é necessário na calibração.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public enum Fase { Splash, Calibracao, Selecao, Score }

    [Header("Managers")]
    public ScreenFader          Fader;
    public HelperDialogueManager Dialogo;
    public OmmoCalibracaoManager Calibracao;
    public MinigameSelectionUI   Selecao;

    [Header("Painéis")]
    public GameObject SplashPainel;   // firstMenu.png — clica para começar
    public GameObject CalibracaoPainel;
    public GameObject SelecaoPainel;
    public GameObject ScorePainel;
    public TMPro.TextMeshProUGUI ScoreTexto;

    [Header("Diálogos (opcional — se vazio, usa os textos por defeito)")]
    [Tooltip("Tutorial mostrado ao entrar na seleção.")]
    public DialogueSequence TutorialSelecaoSeq;
    [Tooltip("Comentário do score quando a média ≥ 75 %.")]
    public DialogueSequence ScoreAltoSeq;
    [Tooltip("Comentário do score quando a média está entre 15 % e 75 %.")]
    public DialogueSequence ScoreMedioSeq;
    [Tooltip("Comentário do score quando a média < 15 %.")]
    public DialogueSequence ScoreBaixoSeq;

    private Fase _fase;
    private bool _aguardaCliqueSplash;

    void Start()
    {
        SetPainel(SplashPainel, false);
        SetPainel(CalibracaoPainel, false);
        SetPainel(SelecaoPainel, false);
        SetPainel(ScorePainel, false);

        var sm = SessionManager.Instancia;
        if (sm != null && sm.TemScoresPendentes)      IniciarScore();
        else if (sm != null && sm.Calibrado)          IniciarSelecao();
        else                                          IniciarSplash();
    }

    void Update()
    {
        if (_aguardaCliqueSplash && Input.GetMouseButtonDown(0))
        {
            _aguardaCliqueSplash = false;
            if (Fader) Fader.FadeOut(DepoisDoSplash);
            else DepoisDoSplash();
        }
    }

    // ── Splash ────────────────────────────────────────────────────────
    void IniciarSplash()
    {
        _fase = Fase.Splash;
        GestorXR.Instancia?.ModoMonitor();
        SetPainel(SplashPainel, true);
        _aguardaCliqueSplash = true;
    }

    void DepoisDoSplash()
    {
        SetPainel(SplashPainel, false);
        var sm = SessionManager.Instancia;
        if (sm != null && sm.Calibrado) IniciarSelecao();
        else                            IniciarCalibracao();
        if (Fader) Fader.FadeIn();
    }

    // ── Calibração ────────────────────────────────────────────────────
    void IniciarCalibracao()
    {
        _fase = Fase.Calibracao;
        SetPainel(CalibracaoPainel, true);

        if (Calibracao != null)
        {
            Calibracao.Dialogo = Dialogo;
            Calibracao.OnCalibracaoConcluida += AoCalibracaoConcluida;
            Calibracao.IniciarCalibracao();
        }
        else AoCalibracaoConcluida();
    }

    void AoCalibracaoConcluida()
    {
        if (Calibracao != null) Calibracao.OnCalibracaoConcluida -= AoCalibracaoConcluida;
        if (Dialogo) Dialogo.Esconder();
        SetPainel(CalibracaoPainel, false);
        IniciarSelecao();
    }

    // ── Seleção ───────────────────────────────────────────────────────
    void IniciarSelecao()
    {
        _fase = Fase.Selecao;
        GestorXR.Instancia?.ModoMonitor(); // seleção é no monitor (o HMD mostra o placard)
        SetPainel(SelecaoPainel, true);
        if (Selecao) Selecao.Mostrar(Dialogo, FalasTutorial());
    }

    /// <summary>Falas do tutorial: usa a sequência atribuída no Inspector, ou o texto por defeito.</summary>
    List<HelperFala> FalasTutorial()
        => (TutorialSelecaoSeq != null && TutorialSelecaoSeq.TemFalas)
            ? TutorialSelecaoSeq.Falas
            : TutorialSelecaoDefault();

    /// <summary>Tutorial por defeito (fala o Patrick; ambos os helpers ficam visíveis).</summary>
    List<HelperFala> TutorialSelecaoDefault()
    {
        HelperId h = HelperId.Patrick;

        return new List<HelperFala>
        {
            Fala(h, HelperEmocao.Pleased,   "Boa! Agora escolhe os teus minijogos aqui."),
            Fala(h, HelperEmocao.Neutral,   "Passa o rato por cima de um card e ele mostra-te o exercício."),
            Fala(h, HelperEmocao.Neutral,   "Nos botões L e R escolhes as repetições para o braço esquerdo e direito."),
            Fala(h, HelperEmocao.Impressed, "Podes escolher vários exercícios ao mesmo tempo!"),
            Fala(h, HelperEmocao.Laugh,     "O nome do jogo e do exercício aparecem em cada card."),
            Fala(h, HelperEmocao.Pleased,   "Quando estiveres pronto, carrega em START. Vamos a isto!"),
        };
    }

    static HelperFala Fala(HelperId q, HelperEmocao e, string t)
        => new HelperFala { Quem = q, Emocao = e, Texto = t };

    // ── Score ─────────────────────────────────────────────────────────
    void IniciarScore()
    {
        _fase = Fase.Score;
        GestorXR.Instancia?.ModoMonitor(); // após os minijogos volta-se ao monitor
        SetPainel(ScorePainel, true);

        var sm = SessionManager.Instancia;
        var scores = sm != null ? sm.Scores : null;

        // Texto resumo (editável no builder). Helpers comentam por cima.
        if (ScoreTexto != null && scores != null)
        {
            var sb = new System.Text.StringBuilder("Resultados:\n");
            float total = 0f;
            foreach (var r in scores)
            {
                sb.AppendLine($"{ExerciciosWaypoints.Nome(r.Tipo)}: {r.PctMedia:F0} %");
                total += r.PctMedia;
            }
            float media = scores.Count > 0 ? total / scores.Count : 0f;
            sb.AppendLine($"\nMédia: {media:F0} %");
            ScoreTexto.text = sb.ToString();

            // SFX da banda do score (amazing/good/bad).
            if (scores.Count > 0) GestorAudio.Instancia?.TocarScoreBanda(media);
        }

        if (Dialogo != null)
            Dialogo.Reproduzir(FalasScore(scores), AoFecharScore);
    }

    /// <summary>Falas do score: usa a sequência atribuída (por faixa), ou o texto por defeito.</summary>
    List<HelperFala> FalasScore(IReadOnlyList<SessionManager.Resultado> scores)
    {
        float media = 0f;
        if (scores != null && scores.Count > 0)
        {
            foreach (var r in scores) media += r.PctMedia;
            media /= scores.Count;
        }

        DialogueSequence seq = media >= 75f ? ScoreAltoSeq
                             : media >= 45f ? ScoreMedioSeq
                             :                ScoreBaixoSeq;
        if (seq != null && seq.TemFalas) return seq.Falas;

        HelperId h = HelperId.Jane;
        HelperEmocao emocao = media >= 75f ? HelperEmocao.Impressed
                            : media >= 15f ? HelperEmocao.Pleased
                            :                HelperEmocao.Worried;
        string txt = media >= 75f ? "Excelente trabalho! Estás mesmo a melhorar."
                   : media >= 45f ? "Bom trabalho! Continua assim."
                   :                "Não faz mal — cada sessão conta. Vamos tentar outra vez!";

        return new List<HelperFala>
        {
            Fala(h, HelperEmocao.Neutral, "Vamos ver como te saíste..."),
            Fala(h, emocao, txt),
            Fala(h, HelperEmocao.Pleased, "Clica para voltar ao menu."),
        };
    }

    void AoFecharScore()
    {
        if (SessionManager.Instancia != null) SessionManager.Instancia.LimparScoresPendentes();
        SetPainel(ScorePainel, false);
        IniciarSelecao(); // volta à escolha de minijogos (já está calibrado)
    }

    // ── Helpers ───────────────────────────────────────────────────────
    static void SetPainel(GameObject go, bool ativo)
    {
        if (go != null) go.SetActive(ativo);
    }
}
