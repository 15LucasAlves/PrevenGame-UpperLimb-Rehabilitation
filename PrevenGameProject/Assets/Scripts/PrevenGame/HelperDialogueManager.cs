using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Emoções dos helpers (índice estável — mapeado para os PNGs no builder).</summary>
public enum HelperEmocao { Neutral, Pleased, Impressed, Laugh, Surprised, Worried, Disappointed }

/// <summary>
/// HelperDialogueManager — Mostra um personagem-ajudante (Jane/Patrick, por emoção) com um
/// balão de fala e texto escrito letra a letra (máquina de escrever).
///
/// Modos:
///   • <see cref="MostrarLinha"/> — uma linha fixa (ex.: instrução de calibração).
///   • <see cref="Reproduzir(DialogueSequence, System.Action)"/> / <see cref="Reproduzir(IEnumerable{HelperFala}, System.Action)"/>
///     — reproduz uma sequência; o clique completa a escrita e, se já completa, avança.
///
/// Cada <see cref="HelperFala"/> escolhe o personagem e a emoção, pelo que cada balão pode ter
/// uma emoção diferente. Texto do balão: Poppins Medium, 40, #231F20 (definido no builder).
/// </summary>
public class HelperDialogueManager : MonoBehaviour
{
    [Header("Referências UI")]
    [Tooltip("Conteúdo do diálogo (personagem + balão). Liga/desliga com o diálogo.")]
    public GameObject      Painel;
    public Image           HelperImagem;
    public Image           BalaoImagem;
    public TextMeshProUGUI TextoBalao;

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
    public void MostrarLinha(HelperId quem, HelperEmocao emocao, string texto)
    {
        _emSequencia = false;
        if (Painel) Painel.SetActive(true);
        Aplicar(quem, emocao, texto);
    }

    public void Esconder()
    {
        _emSequencia = false;
        if (Painel) Painel.SetActive(false);
    }

    // ── Modo sequência (tutorial / score) ─────────────────────────────
    public void Reproduzir(DialogueSequence seq, System.Action aoTerminar = null)
        => Reproduzir(seq != null ? seq.Falas : null, aoTerminar);

    public void Reproduzir(IEnumerable<HelperFala> falas, System.Action aoTerminar = null)
    {
        _sequencia  = falas != null ? new List<HelperFala>(falas) : new List<HelperFala>();
        _idxSeq     = 0;
        _aoTerminar = aoTerminar;

        if (_sequencia.Count == 0) { aoTerminar?.Invoke(); return; }

        _emSequencia = true;
        if (Painel) Painel.SetActive(true);
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
            cb?.Invoke();
            return;
        }
        MostrarAtual();
    }

    void MostrarAtual()
    {
        var f = _sequencia[_idxSeq];
        Aplicar(f.Quem, f.Emocao, f.Texto);
    }

    // ── Aplicar + máquina de escrever ─────────────────────────────────
    void Aplicar(HelperId quem, HelperEmocao emocao, string texto)
    {
        if (HelperImagem)
        {
            var sprite = SpritePara(quem, emocao);
            if (sprite != null) { HelperImagem.sprite = sprite; HelperImagem.enabled = true; }
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
