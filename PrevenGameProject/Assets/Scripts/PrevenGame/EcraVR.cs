using UnityEngine;

/// <summary>
/// EcraVR — "Ecrã" world-space onde vive a UI do paciente em VR: painel de
/// instruções (calibração e anúncios do minijogo), tabela de score e aviso de
/// pausa. (A UI head-locked — demo do exercício + contador de reps — vive no
/// <see cref="HudVR"/>.)
///
/// Estilo "menu de jogo VR": um painel com o background da calibração que
/// SEGUE o centro da câmara com um lerp suave (lazy-follow) sobre passthrough —
/// nada de personagens presos à cara do jogador.
/// </summary>
public class EcraVR : MonoBehaviour
{
    [Header("Painel de instruções (estilo menu VR)")]
    [Tooltip("Painel com o background da calibração onde as instruções aparecem.")]
    public GameObject PainelInstrucoes;
    [Tooltip("Texto das instruções (calibração, anúncios de braço, etc.).")]
    public TMPro.TextMeshProUGUI TextoInstrucoes;

    [Header("Outros painéis")]
    [Tooltip("Painel com o aviso 'Jogo em pausa' (espelho do PauseMenu do operador).")]
    public GameObject PainelPausa;
    [Tooltip("Texto da tabela de score do minijogo (preenchido pelo GestorMinijogo).")]
    public TMPro.TextMeshProUGUI TextoTabela;
    [Tooltip("Painel que contém a tabela de score.")]
    public GameObject PainelTabela;

    [Header("Posicionamento (lazy-follow)")]
    [Tooltip("Distância da cabeça ao ecrã (metros).")]
    public float Distancia = 1.2f;
    [Tooltip("Desvio vertical do centro do painel face ao centro do olhar (metros; local à cabeça).")]
    public float DesvioVertical = 0f;
    [Tooltip("Velocidade do lerp de perseguição (maior = cola mais depressa).")]
    public float VelocidadeSeguir = 5f;
    [Tooltip("Escala do conteúdo (1 = tamanho desenhado).")]
    public float EscalaConteudo = 1f;
    [Tooltip("Altura mínima da cabeça (m) para a pose contar como válida — no primeiro " +
             "frame após criar o rig a cabeça ainda está em (0,0,0).")]
    public float AlturaCabecaMinima = 0.3f;

    void Awake()
    {
        // MULTIPLICA a escala existente (o builder serializa 0.001 no root para
        // converter os px do canvas em metros).
        transform.localScale *= EscalaConteudo;
    }

    void LateUpdate() => Seguir(instantaneo: false);

    /// <summary>
    /// Persegue o ponto à frente do centro da câmara com lerp (lazy-follow),
    /// sempre virado para o jogador. <paramref name="instantaneo"/> salta o lerp
    /// (usado ao mostrar, para não deslizar desde a pose antiga).
    /// </summary>
    void Seguir(bool instantaneo)
    {
        var xr = GestorXR.Instancia;
        var cabeca = xr != null ? xr.Cabeca : null;
        if (cabeca == null || cabeca.position.y < AlturaCabecaMinima) return;

        Vector3 alvoPos = cabeca.TransformPoint(new Vector3(0f, DesvioVertical, Distancia));
        Vector3 paraEcra = alvoPos - cabeca.position;
        Quaternion alvoRot = paraEcra.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(paraEcra.normalized, Vector3.up)
            : transform.rotation;

        if (instantaneo)
        {
            transform.SetPositionAndRotation(alvoPos, alvoRot);
            return;
        }

        float t = 1f - Mathf.Exp(-VelocidadeSeguir * Time.deltaTime); // suave e independente de fps
        transform.position = Vector3.Lerp(transform.position, alvoPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, alvoRot, t);
    }

    // ── API ───────────────────────────────────────────────────────────
    /// <summary>Mostra/esconde o ecrã; ao mostrar posiciona-o já à frente da cabeça.</summary>
    public void Mostrar(bool ativo)
    {
        gameObject.SetActive(ativo);
        if (ativo) Seguir(instantaneo: true);
    }

    /// <summary>Mostra uma instrução no painel (ativa o ecrã e o painel se preciso).</summary>
    public void MostrarTexto(string texto)
    {
        if (!gameObject.activeSelf) Mostrar(true);
        if (PainelInstrucoes != null) PainelInstrucoes.SetActive(true);
        if (TextoInstrucoes != null) TextoInstrucoes.text = texto ?? "";
    }

    /// <summary>Esconde só o painel de instruções (a tabela/pausa podem continuar).</summary>
    public void EsconderTexto()
    {
        if (PainelInstrucoes != null) PainelInstrucoes.SetActive(false);
    }

    /// <summary>Mostra/esconde o aviso de pausa (chamado pelo espelho de pausa).</summary>
    public void MostrarPausa(bool ativo)
    {
        if (PainelPausa != null) PainelPausa.SetActive(ativo);
    }
}
