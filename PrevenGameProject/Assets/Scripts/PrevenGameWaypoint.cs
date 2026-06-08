using UnityEngine;
using System.Collections;

/// <summary>
/// PrevenGameWaypoint — Ponto-alvo do exercício de reabilitação.
///
/// Sistema de zonas de pontuação concêntricas:
///   A esfera central (zona interior) vale 100 %.
///   Cada anel adicional mais largo vale menos (ex: 75 %, 50 %).
///
/// Fluxo de deteção (entrada/saída):
///   1. A palma ENTRA na zona mais exterior → começa a rastrear a melhor distância.
///   2. Enquanto dentro → regista o ponto mais próximo do centro atingido.
///   3. Quando a palma SAI da zona mais exterior → determina o score e avança.
///
/// Isto obriga o jogador a realmente passar pelo alvo; entrar e sair
/// imediatamente com um toque exterior apenas vale 50 %.
/// </summary>
public class PrevenGameWaypoint : MonoBehaviour
{
    // ── Estados ───────────────────────────────────────────────────────
    public enum EstadoWaypoint { Inativo, Ativo, Completo }

    // ── Configuração ──────────────────────────────────────────────────
    [Tooltip("Raio de deteção da zona exterior (Unity units).")]
    public float Raio = 0.50f; // raio da zona mais exterior (fallback sem zonas)

    // ── Zonas de pontuação (definidas pelo manager via ConfigurarZonas) ─
    private float[]      _raios;       // interior → exterior
    private float[]      _pontuacoes;  // score de cada zona (ex: 1.0, 0.75, 0.5)
    private GameObject[] _aneis;       // esferas-anel filhas (para zonas além da interior)
    private bool         _zonasConfiguradas = false;
    private bool         _visivel = true; // false no modo Gamification (zonas só para pontuação)

    // ── Score do último toque ─────────────────────────────────────────
    public float UltimaPontuacao { get; private set; }

    // ── Estado interno ────────────────────────────────────────────────
    private EstadoWaypoint _estado      = EstadoWaypoint.Inativo;
    private Renderer       _renderer;
    private float          _pulsarTimer = 0f;
    private bool           _atingido    = false;

    // Rastreio de entrada/saída para a lógica de score
    private bool  _dentroDeZona    = false;
    private float _melhorDistancia = float.MaxValue;

    // ── Materiais ─────────────────────────────────────────────────────
    private Material _matInativo;
    private Material _matAtivo;
    private Material _matCompleto;

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
        if (_estado == EstadoWaypoint.Ativo && _renderer && _visivel)
        {
            _pulsarTimer += Time.deltaTime * 2.5f;
            float t = (Mathf.Sin(_pulsarTimer) + 1f) / 2f; // 0..1
            float emissao = Mathf.Lerp(0.05f, 0.55f, t);
            _matAtivo.SetColor("_EmissionColor", new Color(0f, emissao, 0.01f));
        }
    }

    // ── API pública — Zonas ───────────────────────────────────────────

    /// <summary>
    /// Configura as zonas de pontuação concêntricas.
    /// Deve ser chamado pelo PrevenGameManager imediatamente após criar o waypoint.
    /// </summary>
    /// <param name="raios">Raios de deteção, interior→exterior (ex: 0.20, 0.35, 0.50).</param>
    /// <param name="pontuacoes">Score de cada zona, interior→exterior (ex: 1.0, 0.75, 0.50).</param>
    /// <param name="cores">Cor visual de cada zona.</param>
    /// <param name="escalaParent">EscalaEsfera do waypoint principal (para calcular localScale dos anéis).</param>
    public void ConfigurarZonas(float[] raios, float[] pontuacoes, Color[] cores, float escalaParent, bool visivel = true)
    {
        if (raios == null || raios.Length == 0) return;

        _visivel    = visivel;
        _raios      = raios;
        _pontuacoes = pontuacoes;
        Raio        = raios[raios.Length - 1]; // raio máximo para gizmos

        if (!visivel)
        {
            // Modo Gamification: as zonas servem só de pontuação (entrada/saída),
            // sem qualquer visual. Desliga a esfera principal e não cria anéis.
            if (_renderer) _renderer.enabled = false;
            _zonasConfiguradas = true;
            return;
        }

        // A esfera principal (índice 0) mantém o seu visual.
        // Criamos anéis filhos para os índices 1, 2, …
        int numAneis = raios.Length - 1;
        _aneis = new GameObject[numAneis];

        for (int i = 0; i < numAneis; i++)
        {
            int    zonaIdx = i + 1;
            float  raioZona = raios[zonaIdx];
            Color  corZona  = (cores != null && zonaIdx < cores.Length) ? cores[zonaIdx] : Color.yellow;

            var anel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anel.name = $"Anel_{zonaIdx}_{(int)(pontuacoes[zonaIdx] * 100)}pct";
            Destroy(anel.GetComponent<SphereCollider>());
            anel.transform.SetParent(transform, false);

            // O pai tem localScale = escalaParent; para obter diâmetro-mundo = raioZona*2:
            // child.localScale = (raioZona * 2) / escalaParent
            float scaleFilho = (raioZona * 2f) / escalaParent;
            anel.transform.localScale = Vector3.one * scaleFilho;

            // Material semi-transparente
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(corZona.r, corZona.g, corZona.b, 0.18f);
            ConfigurarTransparencia(mat);
            anel.GetComponent<Renderer>().material = mat;

            anel.SetActive(false); // visível apenas no estado Ativo
            _aneis[i] = anel;
        }

        _zonasConfiguradas = true;
    }

    // ── API pública — Estado ──────────────────────────────────────────

    /// <summary>Muda o estado visual do waypoint.</summary>
    public void SetEstado(EstadoWaypoint estado)
    {
        _estado      = estado;
        _pulsarTimer = 0f;

        switch (estado)
        {
            case EstadoWaypoint.Inativo:
                if (_renderer && _visivel) _renderer.material = _matInativo;
                gameObject.SetActive(true);
                SetAneisAtivos(false);
                ResetRastreio();
                break;

            case EstadoWaypoint.Ativo:
                if (_renderer && _visivel) _renderer.material = _matAtivo;
                gameObject.SetActive(true);
                SetAneisAtivos(true);
                break;

            case EstadoWaypoint.Completo:
                SetAneisAtivos(false);
                if (_visivel) StartCoroutine(AnimacaoConcluido());
                break;
        }
    }

    /// <summary>
    /// Verifica se a palma está dentro/fora das zonas de pontuação.
    /// Retorna true no frame em que a palma SAIU depois de ter entrado —
    /// nesse momento UltimaPontuacao fica disponível.
    /// </summary>
    public bool VerificarToque(Vector3 posicao)
    {
        if (_atingido || _estado != EstadoWaypoint.Ativo) return false;

        float dist = Vector3.Distance(posicao, transform.position);

        // Raio máximo: último elemento do array (ou Raio como fallback)
        float raioMax = _zonasConfiguradas ? _raios[_raios.Length - 1] : Raio;

        if (dist <= raioMax)
        {
            // Dentro da área — atualiza melhor distância ao centro
            _dentroDeZona    = true;
            _melhorDistancia = Mathf.Min(_melhorDistancia, dist);
        }
        else if (_dentroDeZona)
        {
            // Saiu da área — determina score e conclui waypoint
            _dentroDeZona   = false;
            UltimaPontuacao = _zonasConfiguradas
                ? CalcularPontuacao(_melhorDistancia)
                : 1f; // fallback sem zonas = 100 %
            _melhorDistancia = float.MaxValue;

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
        UltimaPontuacao = 0f;
        ResetRastreio();
        SetEstado(EstadoWaypoint.Inativo);
    }

    // ── Cálculo de pontuação ──────────────────────────────────────────

    /// <summary>Devolve o score correspondente à melhor distância atingida.</summary>
    private float CalcularPontuacao(float melhorDist)
    {
        // Percorre do interior (score mais alto) para o exterior
        for (int i = 0; i < _raios.Length; i++)
        {
            if (melhorDist <= _raios[i])
                return _pontuacoes[i];
        }
        return 0f; // nunca deve chegar aqui se chamado após entrar na área
    }

    // ── Helpers internos ──────────────────────────────────────────────

    private void ResetRastreio()
    {
        _dentroDeZona    = false;
        _melhorDistancia = float.MaxValue;
    }

    private void SetAneisAtivos(bool ativo)
    {
        if (_aneis == null) return;
        foreach (var anel in _aneis)
            if (anel != null) anel.SetActive(ativo);
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
        if (_zonasConfiguradas && _raios != null)
        {
            // Mostra cada zona com cor proporcional ao score
            for (int i = 0; i < _raios.Length; i++)
            {
                float t = (float)i / Mathf.Max(1, _raios.Length - 1);
                Gizmos.color = Color.Lerp(
                    new Color(0.2f, 0.9f, 0.3f, 0.35f),  // verde (100%)
                    new Color(1f, 0.4f, 0.1f, 0.15f),     // laranja (50%)
                    t);
                Gizmos.DrawWireSphere(transform.position, _raios[i]);
            }
        }
        else
        {
            Gizmos.color = _estado == EstadoWaypoint.Ativo
                ? new Color(0.2f, 0.9f, 0.3f, 0.4f)
                : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, Raio);
        }
    }

    // ── Helpers estáticos ─────────────────────────────────────────────

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
