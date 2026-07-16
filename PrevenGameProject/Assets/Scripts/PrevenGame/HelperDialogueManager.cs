using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Emoções dos helpers (índice estável — mapeado para os PNGs no builder).</summary>
public enum HelperEmocao { Neutral, Pleased, Impressed, Laugh, Surprised, Worried, Disappointed }

/// <summary>
/// HelperDialogueManager — Mostra os DOIS personagens-ajudantes em simultâneo (Patrick em
/// baixo à esquerda, Jane em baixo à direita) com um balão de fala apontado a quem fala e
/// texto escrito letra a letra (máquina de escrever).
///
/// Modos:
///   • <see cref="MostrarLinha"/> — uma linha fixa (ex.: instrução de calibração).
///   • <see cref="Reproduzir(DialogueSequence, System.Action)"/> / <see cref="Reproduzir(IEnumerable{HelperFala}, System.Action)"/>
///     — reproduz uma sequência; o clique completa a escrita e, se já completa, avança.
///
/// Cada <see cref="HelperFala"/> escolhe o orador e a emoção. A emoção muda só no orador; o
/// outro personagem mantém a última (ambos começam Neutral em cada conversa). Para forçar a
/// emoção do não-orador (ex.: pôr os dois Neutral na fala final) usa <see cref="DefinirEmocao"/>.
/// Quando fala a Jane o balão é espelhado horizontalmente para o bico apontar para ela — o
/// texto é contra-espelhado para ficar direito.
/// </summary>
public class HelperDialogueManager : MonoBehaviour
{
    [Header("Referências UI")]
    [Tooltip("Conteúdo do diálogo (personagens + balão). Liga/desliga com o diálogo.")]
    public GameObject      Painel;
    public Image           ImagemPatrick;   // canto inferior esquerdo
    public Image           ImagemJane;      // canto inferior direito
    public Image           BalaoImagem;
    public TextMeshProUGUI TextoBalao;
    [Tooltip("Linha secundária opcional (ex.: dica), numa caixa própria abaixo do texto principal.")]
    public TextMeshProUGUI TextoBalaoSub;

    [Header("Posição do balão por orador (âncora topo-centro)")]
    public Vector2 PosBalaoPatrick = new Vector2( 120f, -120f);
    public Vector2 PosBalaoJane    = new Vector2(-120f, -120f);

    [Header("Modo VR (EcraVR) — menos clutter; defaults false = instância do monitor intocada")]
    [Tooltip("Mostra APENAS o personagem que está a falar (o outro desaparece).")]
    public bool MostrarApenasOrador = false;
    [Tooltip("Ancora o balão ao canto inferior do orador em vez do topo-centro. " +
             "PosBalaoPatrick/Jane passam a ser offsets a partir do canto inferior respetivo.")]
    public bool BalaoJuntoAoOrador = false;

    [Header("Máquina de escrever")]
    [Tooltip("Velocidade de escrita em caracteres por segundo. 0 = texto instantâneo.")]
    public float VelocidadeEscrita = 40f;

    [Header("Sprites por emoção (índice = HelperEmocao)")]
    public Sprite[] SpritesJane;
    public Sprite[] SpritesPatrick;

    // Sequência ativa
    private List<HelperFala> _sequencia;
    private int              _idxSeq;
    private System.Action    _aoTerminar;
    private bool             _emSequencia;

    // Escrita
    private Coroutine _typewriter;
    private bool      _aEscrever;

    void Awake()
    {
        if (Painel) Painel.SetActive(false);
    }

    void Update()
    {
        if (!_emSequencia) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (_aEscrever) CompletarEscrita(); // 1º clique: revela tudo
            else            Avancar();          // 2º clique: próxima fala
        }
    }

    // ── Modo linha fixa (calibração) ──────────────────────────────────
    /// <param name="subtexto">Linha secundária opcional, mostrada na caixa própria (sem máquina de escrever).</param>
    public void MostrarLinha(HelperId quem, HelperEmocao emocao, string texto, string subtexto = null)
    {
        _emSequencia = false;
        AbrirPainel();
        Aplicar(quem, emocao, texto, subtexto);
    }

    public void Esconder()
    {
        _emSequencia = false;
        if (Painel) Painel.SetActive(false);
    }

    // ── Modo sequência (intro / tutorial / score) ─────────────────────
    public void Reproduzir(DialogueSequence seq, System.Action aoTerminar = null)
        => Reproduzir(seq != null ? seq.Falas : null, aoTerminar);

    public void Reproduzir(IEnumerable<HelperFala> falas, System.Action aoTerminar = null)
    {
        _sequencia  = falas != null ? new List<HelperFala>(falas) : new List<HelperFala>();
        _idxSeq     = 0;
        _aoTerminar = aoTerminar;

        if (_sequencia.Count == 0) { aoTerminar?.Invoke(); return; }

        _emSequencia = true;
        AbrirPainel();
        MostrarAtual();
    }

    public void Avancar()
    {
        _idxSeq++;
        if (_idxSeq >= _sequencia.Count)
        {
            _emSequencia = false;
            if (Painel) Painel.SetActive(false);
            var cb = _aoTerminar; _aoTerminar = null;
            // Se o callback rebentar, o erro aparece no Console em vez de matar o fluxo
            // em silêncio com o painel escondido.
            try { cb?.Invoke(); }
            catch (System.Exception e) { Debug.LogException(e); }
            return;
        }
        MostrarAtual();
    }

    void MostrarAtual()
    {
        var f = _sequencia[_idxSeq];
        Aplicar(f.Quem, f.Emocao, f.Texto);
    }

    // ── Emoções dos personagens ───────────────────────────────────────
    /// <summary>Força a emoção de um personagem sem mexer no balão (ex.: "Jane também Neutral").</summary>
    public void DefinirEmocao(HelperId quem, HelperEmocao emocao)
    {
        var img = quem == HelperId.Jane ? ImagemJane : ImagemPatrick;
        if (img == null) return;
        var sprite = SpritePara(quem, emocao);
        img.sprite  = sprite;
        img.color   = Color.white;          // a Image nasce quase transparente no builder
        img.enabled = sprite != null;
    }

    /// <summary>Ambos os personagens voltam a Neutral (estado de arranque de cada conversa).</summary>
    public void ReporEmocoes()
    {
        DefinirEmocao(HelperId.Jane,    HelperEmocao.Neutral);
        DefinirEmocao(HelperId.Patrick, HelperEmocao.Neutral);

        // Em modo só-orador ninguém aparece até a primeira fala definir quem fala.
        if (MostrarApenasOrador)
        {
            if (ImagemJane    != null) ImagemJane.enabled    = false;
            if (ImagemPatrick != null) ImagemPatrick.enabled = false;
        }
    }

    /// <summary>Abre o painel; se estava fechado, a conversa começa com ambos Neutral.</summary>
    void AbrirPainel()
    {
        if (Painel == null) return;
        if (!Painel.activeSelf)
        {
            Painel.SetActive(true);
            ReporEmocoes();
        }
    }

    // ── Aplicar + máquina de escrever ─────────────────────────────────
    void Aplicar(HelperId quem, HelperEmocao emocao, string texto, string subtexto = null)
    {
        DefinirEmocao(quem, emocao);

        // Modo só-orador (VR): esconde quem não está a falar.
        if (MostrarApenasOrador)
        {
            var outro = quem == HelperId.Jane ? ImagemPatrick : ImagemJane;
            if (outro != null) outro.enabled = false;
        }

        AplicarOrador(quem);

        // Blip de diálogo (curto/longo pelo tamanho da fala; dedupe no GestorAudio
        // para a linha espelhada monitor+EcraVR tocar só uma vez).
        if (!string.IsNullOrEmpty(texto))
            GestorAudio.Instancia?.TocarDialogo(quem, texto);

        // Subtexto na caixa própria: instantâneo, escondido quando vazio.
        if (TextoBalaoSub != null)
        {
            bool temSub = !string.IsNullOrEmpty(subtexto);
            TextoBalaoSub.text = temSub ? subtexto : "";
            TextoBalaoSub.gameObject.SetActive(temSub);
        }

        if (TextoBalao == null) return;

        TextoBalao.text = texto ?? "";
        if (_typewriter != null) { StopCoroutine(_typewriter); _typewriter = null; }

        if (VelocidadeEscrita > 0f && Application.isPlaying && !string.IsNullOrEmpty(texto))
            _typewriter = StartCoroutine(Escrever());
        else
        {
            TextoBalao.maxVisibleCharacters = int.MaxValue;
            _aEscrever = false;
        }
    }

    /// <summary>Aponta o balão ao orador: reposiciona e espelha (texto contra-espelhado).</summary>
    void AplicarOrador(HelperId quem)
    {
        if (BalaoImagem == null) return;
        bool jane = quem == HelperId.Jane;

        var rt = BalaoImagem.rectTransform;

        // Modo VR: balão ancorado ao canto inferior do orador (pivot no canto
        // interior; com o flip da Jane o balão estende-se para DENTRO do ecrã).
        if (BalaoJuntoAoOrador)
        {
            rt.anchorMin = rt.anchorMax = jane ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
        }

        rt.anchoredPosition = jane ? PosBalaoJane : PosBalaoPatrick;
        rt.localScale       = new Vector3(jane ? -1f : 1f, 1f, 1f);

        // Os textos são filhos do balão: espelhá-los outra vez devolve-os direitos.
        var contraFlip = new Vector3(jane ? -1f : 1f, 1f, 1f);
        if (TextoBalao    != null) TextoBalao.rectTransform.localScale    = contraFlip;
        if (TextoBalaoSub != null) TextoBalaoSub.rectTransform.localScale = contraFlip;
    }

    IEnumerator Escrever()
    {
        _aEscrever = true;
        TextoBalao.ForceMeshUpdate();
        int total = TextoBalao.textInfo.characterCount;
        TextoBalao.maxVisibleCharacters = 0;

        float delay = 1f / Mathf.Max(1f, VelocidadeEscrita);
        for (int i = 0; i <= total; i++)
        {
            TextoBalao.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(delay);
        }

        TextoBalao.maxVisibleCharacters = int.MaxValue;
        _aEscrever  = false;
        _typewriter = null;
    }

    void CompletarEscrita()
    {
        if (_typewriter != null) { StopCoroutine(_typewriter); _typewriter = null; }
        if (TextoBalao) TextoBalao.maxVisibleCharacters = int.MaxValue;
        _aEscrever = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────
    Sprite SpritePara(HelperId quem, HelperEmocao emocao)
    {
        var arr = quem == HelperId.Jane ? SpritesJane : SpritesPatrick;
        int i = (int)emocao;
        if (arr == null || arr.Length == 0) return null;
        if (i < 0 || i >= arr.Length) i = 0;
        return arr[i];
    }
}
