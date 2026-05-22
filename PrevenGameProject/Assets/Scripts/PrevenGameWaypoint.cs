using UnityEngine;
using System.Collections;

/// <summary>
/// PrevenGameWaypoint — Ponto-alvo do exercício de reabilitação.
///
/// Representa uma esfera que o sensor da palma tem de atingir.
/// Muda visualmente de estado conforme o progresso do exercício:
///   Inativo  → cinza semi-transparente (pontos futuros)
///   Ativo    → verde brilhante com pulsar (ponto atual a atingir)
///   Completo → flash branco → desaparece
///
/// Criado dinamicamente pelo PrevenGameManager em runtime.
/// </summary>
public class PrevenGameWaypoint : MonoBehaviour
{
    // ── Estados ───────────────────────────────────────────────────────
    public enum EstadoWaypoint { Inativo, Ativo, Completo }

    // ── Configuração ──────────────────────────────────────────────────
    [Tooltip("Raio de deteção do toque (Unity units). 0.15 = 15 cm.")]
    public float Raio = 0.15f;

    // ── Materiais ─────────────────────────────────────────────────────
    private Material _matInativo;
    private Material _matAtivo;
    private Material _matCompleto;

    // ── Estado interno ────────────────────────────────────────────────
    private EstadoWaypoint _estado = EstadoWaypoint.Inativo;
    private Renderer _renderer;
    private float _pulsarTimer = 0f;
    private bool _atingido = false;

    // ── Evento ────────────────────────────────────────────────────────
    public System.Action OnAtingido;

    // ── Unity ─────────────────────────────────────────────────────────

    void Awake()
    {
        _renderer = GetComponent<Renderer>();

        // Material inativo — cinza semi-transparente
        _matInativo = new Material(Shader.Find("Standard"));
        _matInativo.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        ConfigurarTransparencia(_matInativo);

        // Material ativo — verde brilhante
        _matAtivo = new Material(Shader.Find("Standard"));
        _matAtivo.color = new Color(0.2f, 0.9f, 0.3f, 0.85f);
        _matAtivo.EnableKeyword("_EMISSION");
        _matAtivo.SetColor("_EmissionColor", new Color(0f, 0.4f, 0.05f));
        ConfigurarTransparencia(_matAtivo);

        // Material completo — branco (flash temporário)
        _matCompleto = new Material(Shader.Find("Standard"));
        _matCompleto.color = Color.white;
        _matCompleto.EnableKeyword("_EMISSION");
        _matCompleto.SetColor("_EmissionColor", new Color(1f, 1f, 1f));

        if (_renderer) _renderer.material = _matInativo;
    }

    void Update()
    {
        // Efeito de pulsar no estado Ativo
        if (_estado == EstadoWaypoint.Ativo && _renderer)
        {
            _pulsarTimer += Time.deltaTime * 2.5f;
            float t = (Mathf.Sin(_pulsarTimer) + 1f) / 2f; // 0..1
            float emissao = Mathf.Lerp(0.05f, 0.55f, t);
            _matAtivo.SetColor("_EmissionColor", new Color(0f, emissao, 0.01f));
        }
    }

    // ── API pública ───────────────────────────────────────────────────

    /// <summary>Muda o estado visual do waypoint.</summary>
    public void SetEstado(EstadoWaypoint estado)
    {
        _estado = estado;
        _pulsarTimer = 0f;

        switch (estado)
        {
            case EstadoWaypoint.Inativo:
                if (_renderer) _renderer.material = _matInativo;
                gameObject.SetActive(true);
                break;

            case EstadoWaypoint.Ativo:
                if (_renderer) _renderer.material = _matAtivo;
                gameObject.SetActive(true);
                break;

            case EstadoWaypoint.Completo:
                StartCoroutine(AnimacaoConcluido());
                break;
        }
    }

    /// <summary>
    /// Verifica se a posição fornecida está dentro do raio de deteção.
    /// Retorna true apenas uma vez (dispara OnAtingido e muda para Completo).
    /// </summary>
    public bool VerificarToque(Vector3 posicao)
    {
        if (_atingido || _estado != EstadoWaypoint.Ativo) return false;

        if (Vector3.Distance(posicao, transform.position) <= Raio)
        {
            _atingido = true;
            SetEstado(EstadoWaypoint.Completo);
            OnAtingido?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>Repõe o waypoint para reutilização (nova repetição).</summary>
    public void Repor()
    {
        _atingido = false;
        SetEstado(EstadoWaypoint.Inativo);
    }

    // ── Animação de conclusão ─────────────────────────────────────────

    private IEnumerator AnimacaoConcluido()
    {
        if (_renderer) _renderer.material = _matCompleto;

        // Escala até 1.5× em 0.12s
        Vector3 escalaOriginal = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            transform.localScale = Vector3.Lerp(escalaOriginal, escalaOriginal * 1.5f, t);
            yield return null;
        }

        // Encolhe e desaparece em 0.18s
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.18f;
            transform.localScale = Vector3.Lerp(escalaOriginal * 1.5f, Vector3.zero, t);
            yield return null;
        }

        gameObject.SetActive(false);
        transform.localScale = escalaOriginal; // repõe para reutilização
    }

    // ── Gizmos de debug ───────────────────────────────────────────────

    void OnDrawGizmos()
    {
        Gizmos.color = _estado == EstadoWaypoint.Ativo
            ? new Color(0.2f, 0.9f, 0.3f, 0.4f)
            : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, Raio);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void ConfigurarTransparencia(Material mat)
    {
        mat.SetFloat("_Mode", 3f);                       // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }
}
