using UnityEngine;

/// <summary>
/// RastreadorCorpoJogador — Estima a posição dos ombros a partir da câmara VR.
///
/// Regras (decididas com o utilizador):
///   • O ombro segue a POSIÇÃO da câmara (se o jogador andar/deslocar-se, o ombro
///     acompanha — não se assume que está sentado).
///   • A ROTAÇÃO da cabeça é largamente ignorada: um olhar rápido para o lado NÃO
///     move o ombro. Mantém-se um "yaw de corpo" que só segue o yaw da cabeça
///     quando este se afasta mais de <see cref="DeadbandGraus"/> e, mesmo aí,
///     a uma velocidade limitada (<see cref="VelocidadeGrausPorSegundo"/>) —
///     só uma rotação sustentada do corpo re-orienta os ombros.
///
/// O offset cabeça→ombro por braço vem da calibração (headset posto), guardado em
/// <see cref="SessionManager.DadosBraco.OffsetOmbroLocalCabeca"/> no referencial
/// yaw-local. Sem VR (fallback), devolve as posições mundo calibradas.
/// </summary>
public class RastreadorCorpoJogador : MonoBehaviour
{
    [Tooltip("Desvio de yaw cabeça↔corpo (graus) a partir do qual o corpo começa a seguir a cabeça.")]
    public float DeadbandGraus = 30f;

    [Tooltip("Velocidade máxima (graus/s) a que o yaw de corpo segue a cabeça fora da deadband.")]
    public float VelocidadeGrausPorSegundo = 60f;

    [Tooltip("Esferas de debug nos ombros estimados (editor/desenvolvimento).")]
    public bool MostrarDebug = false;

    /// <summary>Yaw estimado do corpo (rotação só em Y).</summary>
    public Quaternion YawCorpo => Quaternion.Euler(0f, _yawCorpo, 0f);

    /// <summary>True quando há câmara VR ativa para derivar os ombros.</summary>
    public bool UsaVr => GestorXR.Instancia != null && GestorXR.Instancia.VrAtivo &&
                         GestorXR.Instancia.Cabeca != null;

    private float _yawCorpo;
    private bool  _inicializado;
    private GameObject _debugEsq, _debugDir;

    void Update()
    {
        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (cabeca == null) return;

        float yawCabeca = cabeca.eulerAngles.y;
        if (!_inicializado)
        {
            _yawCorpo     = yawCabeca;
            _inicializado = true;
        }

        // Fora da deadband, o corpo segue a cabeça a velocidade limitada até
        // voltar a ficar dentro da deadband (rotação sustentada = corpo rodou).
        float delta = Mathf.DeltaAngle(_yawCorpo, yawCabeca);
        if (Mathf.Abs(delta) > DeadbandGraus)
        {
            float alvo = yawCabeca - Mathf.Sign(delta) * DeadbandGraus;
            _yawCorpo = Mathf.MoveTowardsAngle(_yawCorpo, alvo,
                                               VelocidadeGrausPorSegundo * Time.deltaTime);
        }

        AtualizarDebug();
    }

    /// <summary>Alinha imediatamente o yaw de corpo com a cabeça (ex.: no início de um bloco).</summary>
    public void ReporYawCorpo()
    {
        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (cabeca != null) { _yawCorpo = cabeca.eulerAngles.y; _inicializado = true; }
    }

    /// <summary>
    /// Posição mundo estimada do ombro do braço pedido: posição da câmara + offset
    /// calibrado rodado pelo yaw de corpo. Fallback: posição mundo da calibração.
    /// </summary>
    public Vector3 ObterOmbroAtual(bool direito)
    {
        var sm = SessionManager.Instancia;
        if (sm == null) return Vector3.zero;
        var dados = sm.ObterBraco(direito);
        if (!dados.Valido) return Vector3.zero;

        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (dados.TemDadosCabeca && cabeca != null)
            return cabeca.position + YawCorpo * dados.OffsetOmbroLocalCabeca;

        return dados.PosOmbro;
    }

    /// <summary>Direção frente atual do braço pedido (yaw de corpo aplicado ao valor calibrado).</summary>
    public Vector3 ObterDirecaoFrenteAtual(bool direito)
    {
        var sm = SessionManager.Instancia;
        if (sm == null) return Vector3.forward;
        var dados = sm.ObterBraco(direito);
        if (!dados.Valido) return Vector3.forward;

        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (dados.TemDadosCabeca && cabeca != null)
            return (YawCorpo * dados.DirecaoFrenteLocal).normalized;

        return dados.DirecaoFrente;
    }

    // ── Debug ─────────────────────────────────────────────────────────
    void AtualizarDebug()
    {
        if (!MostrarDebug)
        {
            if (_debugEsq != null) { Destroy(_debugEsq); Destroy(_debugDir); _debugEsq = _debugDir = null; }
            return;
        }

        if (_debugEsq == null)
        {
            _debugEsq = CriarEsferaDebug("DebugOmbroEsq", Color.cyan);
            _debugDir = CriarEsferaDebug("DebugOmbroDir", Color.yellow);
        }
        _debugEsq.transform.position = ObterOmbroAtual(false);
        _debugDir.transform.position = ObterOmbroAtual(true);
    }

    GameObject CriarEsferaDebug(string nome, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nome;
        go.transform.localScale = Vector3.one * 0.05f;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material.color = cor;
        return go;
    }
}
