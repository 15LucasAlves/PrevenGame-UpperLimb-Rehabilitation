using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SelectionCard — Um card do ecrã de seleção (SELECT MINI GAME).
///
/// O fundo é o <c>selectionCard.png</c> (sem texto). Este componente coloca por cima o texto
/// editável no Unity: título do jogo, nome do exercício e as repetições L/R. As referências
/// são públicas para o utilizador poder trocar no Inspector.
///
/// Estilos (aplicados no builder): título 40 Poppins ExtraBold branco; nome do exercício 20
/// Poppins Medium #5F786C; números de reps 38 Poppins ExtraBold #84A495.
/// </summary>
public class SelectionCard : MonoBehaviour
{
    [Header("Exercício")]
    public ExerciciosWaypoints.TipoExercicio Tipo = ExerciciosWaypoints.TipoExercicio.FlexaoBraco;

    [Header("Textos (editáveis)")]
    public TextMeshProUGUI TituloJogo;      // ex.: "DARDOS"
    public TextMeshProUGUI NomeExercicio;   // ex.: "flexão frontal"
    public TextMeshProUGUI RepsLTexto;      // número (braço esquerdo)
    public TextMeshProUGUI RepsRTexto;      // número (braço direito)

    [Header("Imagem / seleção")]
    public Image  ImagemExercicio;      // animada no hover (CardAnimacaoHover)
    public Button BotaoSelecionar;      // o próprio card — alterna a seleção
    public Image  FundoCard;            // Image de fundo do card (troca de sprite ao selecionar)
    public Sprite SpriteNormal;         // selectionCard.png
    public Sprite SpriteSelecionado;    // selectionCardSelected.png

    [Header("Steppers")]
    public Button BotaoLMenos, BotaoLMais;
    public Button BotaoRMenos, BotaoRMais;

    [Header("Config")]
    public int RepsMin = 0;
    public int RepsMax = 20;

    private int  _repsL = 1;
    private int  _repsR = 1;
    private bool _selecionado;

    public bool Selecionado => _selecionado;
    public int  RepsL => _repsL;
    public int  RepsR => _repsR;

    void Start()
    {
        BotaoSelecionar?.onClick.AddListener(AlternarSelecao);
        BotaoLMenos?.onClick.AddListener(() => AlterarReps(ref _repsL, -1));
        BotaoLMais?.onClick.AddListener(() => AlterarReps(ref _repsL, +1));
        BotaoRMenos?.onClick.AddListener(() => AlterarReps(ref _repsR, -1));
        BotaoRMais?.onClick.AddListener(() => AlterarReps(ref _repsR, +1));

        if (NomeExercicio != null && string.IsNullOrEmpty(NomeExercicio.text))
            NomeExercicio.text = ExerciciosWaypoints.Nome(Tipo);

        AtualizarUI();
    }

    void AlternarSelecao()
    {
        _selecionado = !_selecionado;
        AtualizarUI();
    }

    void AlterarReps(ref int reps, int delta)
    {
        reps = Mathf.Clamp(reps + delta, RepsMin, RepsMax);
        AtualizarUI();
    }

    void AtualizarUI()
    {
        if (RepsLTexto) RepsLTexto.text = _repsL.ToString();
        if (RepsRTexto) RepsRTexto.text = _repsR.ToString();

        if (FundoCard)
        {
            var alvo = _selecionado && SpriteSelecionado != null ? SpriteSelecionado : SpriteNormal;
            if (alvo != null) FundoCard.sprite = alvo;
        }
    }
}
