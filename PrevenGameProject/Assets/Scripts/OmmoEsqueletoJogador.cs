using UnityEngine;

/// <summary>
/// OmmoEsqueletoJogador — Visualização do esqueleto do membro superior em tempo real.
///
/// Articulações:
///   Palma    → cubo do sensor A (controlado pelo OmmoDevice)
///   Cotovelo → esfera verde-limão (calculada por IK de 2 elos)
///   Ombro    → esfera laranja (Sensor B live ou posição fixa)
///   Peito    → esfera azul (posição fixa pós-calibração)
///   Cabeça   → esfera branca (posição fixa pós-calibração)
///
/// IK do cotovelo (2-link IK via lei dos cossenos):
///   Lu = comprimentoBraco * (18.6 / 44.0)  → braço superior
///   Lf = comprimentoBraco * (14.6 / 44.0)  → antebraço
///   Elbow hint: Vector3.back (cotovelo aponta para trás, para longe da base station)
/// </summary>
public class OmmoEsqueletoJogador : MonoBehaviour
{
    // ── Proporções (DIN 33402 / ISO 7250) ────────────────────────────
    private const float FRACAO_BRACO_SUPERIOR = 18.6f / 44.0f; // braço superior
    private const float FRACAO_ANTEBRACO      = 14.6f / 44.0f; // antebraço

    // ── Limiar de compensação postural ────────────────────────────────
    private const float LIMIAR_COMPENSACAO = 0.02f; // 2 cm

    // ── Geometria ─────────────────────────────────────────────────────
    private const float RAIO_OSSO   = 0.02f;
    private const float RAIO_ESFERA = 0.08f;

    // ── Dispositivos ──────────────────────────────────────────────────
    private OmmoDevice _devicePalma;
    private OmmoDevice _deviceOmbro;

    // ── Posições fixas ────────────────────────────────────────────────
    private Vector3 _posPeito;
    private Vector3 _posCabeca;
    private Vector3 _posOmbroBase;

    // ── Estado ────────────────────────────────────────────────────────
    private bool _calibrado = false;

    // ── GameObjects das articulações ──────────────────────────────────
    private GameObject _goCotovelo;
    private GameObject _goOmbro;
    private GameObject _goPeito;
    private GameObject _goCabeca;

    // ── GameObjects dos ossos ─────────────────────────────────────────
    private GameObject _ossoPalmaCotovelo;
    private GameObject _ossoCotoveloOmbro;
    private GameObject _ossoOmbroPeito;
    private GameObject _ossoPeitoCabeca;

    // ── Materiais ─────────────────────────────────────────────────────
    private Material _matOmbro;
    private Material _matOmbroAlerta;

    // ── Propriedades públicas — calibração ────────────────────────────

    public Vector3 PosPeito     { get; private set; }
    public Vector3 PosCabeca    { get; private set; }
    public Vector3 PosOmbroBase { get; private set; }
    public float   ComprimentoBraco { get; private set; }

    /// <summary>
    /// Direção horizontal do braço estendido para a frente (calibrada).
    /// Usada para orientar o exercício e o hint do cotovelo.
    /// </summary>
    public Vector3 DirecaoFrente { get; private set; } = Vector3.forward;

    public void DefinirComprimentoBraco(float comprimento)
    {
        ComprimentoBraco = comprimento;
        Debug.Log($"[OmmoEsqueleto] ComprimentoBraco = {comprimento:F2} units = {comprimento * 10f:F1} cm");
    }

    /// <summary>
    /// Guarda a direção frontal do paciente a partir da posição da palma
    /// quando o braço está estendido para a frente (passo BracoEstendido).
    /// Projeta no plano horizontal para eliminar componente vertical.
    /// </summary>
    public void DefinirDirecaoFrente(Vector3 posPalmaEstendida)
    {
        Vector3 dir = posPalmaEstendida - PosOmbroBase;
        Vector3 horizontal = new Vector3(dir.x, 0f, dir.z);
        if (horizontal.magnitude > 0.05f)
        {
            DirecaoFrente = horizontal.normalized;
            Debug.Log($"[OmmoEsqueleto] DirecaoFrente = {DirecaoFrente}");
        }
    }

    // ── Propriedades públicas — estado ────────────────────────────────

    public float CompensacaoOmbro { get; private set; }
    public bool  OmbroCompensando => CompensacaoOmbro > LIMIAR_COMPENSACAO;
    public bool  Calibrado        => _calibrado;

    // ── Posições live ─────────────────────────────────────────────────

    public Vector3 ObterPosPalmaAtual()
        => _devicePalma != null ? _devicePalma.ObterPosicaoSensor(0) : Vector3.zero;

    public Vector3 ObterPosOmbroAtual()
        => _deviceOmbro != null ? _deviceOmbro.ObterPosicaoSensor(0) : _posOmbroBase;

    // ── Inicialização ─────────────────────────────────────────────────

    public void Inicializar(OmmoDevice devicePalma, OmmoDevice deviceOmbro)
    {
        _devicePalma = devicePalma;
        _deviceOmbro = deviceOmbro;

        ColorirSensor(devicePalma, new Color(0.3f, 0.7f, 1f));
        if (deviceOmbro != null)
            ColorirSensor(deviceOmbro, new Color(1f, 0.55f, 0.1f));

        _goCotovelo = CriarEsfera("Cotovelo", new Color(0.6f, 1f, 0.2f));
        _matOmbro       = CriarMaterial(new Color(1f, 0.55f, 0.1f));
        _matOmbroAlerta = CriarMaterial(new Color(1f, 0.15f, 0.1f));
        _goOmbro   = CriarEsfera("Ombro",  _matOmbro);
        _goPeito   = CriarEsfera("Peito",  new Color(0.3f, 0.7f, 1f));
        _goCabeca  = CriarEsfera("Cabeca", Color.white);

        _ossoPalmaCotovelo = CriarCilindro("Osso_Palma_Cotovelo", new Color(0.6f, 1f, 0.2f));
        _ossoCotoveloOmbro = CriarCilindro("Osso_Cotovelo_Ombro", new Color(1f, 0.55f, 0.1f));
        _ossoOmbroPeito    = CriarCilindro("Osso_Ombro_Peito",    new Color(0.3f, 0.7f, 1f));
        _ossoPeitoCabeca   = CriarCilindro("Osso_Peito_Cabeca",   Color.white);

        AtivacaoEsqueleto(false);
        Debug.Log("[OmmoEsqueleto] Inicializado — aguarda calibração.");
    }

    public void DefinirPosicaoFixa(string nome, Vector3 posicao)
    {
        switch (nome)
        {
            case "Ombro":
                _posOmbroBase = posicao;
                PosOmbroBase  = posicao;
                if (_goOmbro) _goOmbro.transform.position = posicao;
                Debug.Log($"[OmmoEsqueleto] Ombro fixo: {posicao}");
                break;
            case "Peito":
                _posPeito = posicao;
                PosPeito  = posicao;
                if (_goPeito) _goPeito.transform.position = posicao;
                Debug.Log($"[OmmoEsqueleto] Peito: {posicao}");
                break;
            case "Cabeca":
                _posCabeca = posicao;
                PosCabeca  = posicao;
                if (_goCabeca) _goCabeca.transform.position = posicao;
                Debug.Log($"[OmmoEsqueleto] Cabeça: {posicao}");
                break;
        }
    }

    public void AtivacaoEsqueleto(bool ativo)
    {
        _calibrado = ativo;

        SetAtivo(_goCotovelo,        ativo);
        SetAtivo(_goOmbro,           ativo);
        SetAtivo(_goPeito,           ativo);
        SetAtivo(_goCabeca,          ativo);
        SetAtivo(_ossoPalmaCotovelo, ativo);
        SetAtivo(_ossoCotoveloOmbro, ativo);
        SetAtivo(_ossoOmbroPeito,    ativo);
        SetAtivo(_ossoPeitoCabeca,   ativo);

        if (ativo && _deviceOmbro != null)
        {
            _posOmbroBase = _deviceOmbro.ObterPosicaoSensor(0);
            PosOmbroBase  = _posOmbroBase;
            Debug.Log($"[OmmoEsqueleto] ✅ Ativo | Ombro base: {_posOmbroBase}");
        }
    }

    // ── Update ────────────────────────────────────────────────────────

    void Update()
    {
        if (!_calibrado || _devicePalma == null) return;

        Vector3 posPalma = _devicePalma.ObterPosicaoSensor(0);
        Vector3 posOmbro = _deviceOmbro != null
            ? _deviceOmbro.ObterPosicaoSensor(0)
            : _posOmbroBase;

        // IK de 2 elos: cotovelo calculado por lei dos cossenos
        // Hint: cotovelo aponta para trás relativamente à direção frontal calibrada
        Vector3 posCotovelo = CalcularPosCotovelo(posOmbro, posPalma, ComprimentoBraco, -DirecaoFrente);

        if (_goCotovelo) _goCotovelo.transform.position = posCotovelo;
        if (_goOmbro)    _goOmbro.transform.position    = posOmbro;

        AtualizarCilindro(_ossoPalmaCotovelo, posPalma,    posCotovelo);
        AtualizarCilindro(_ossoCotoveloOmbro, posCotovelo, posOmbro);
        AtualizarCilindro(_ossoOmbroPeito,    posOmbro,    _posPeito);
        AtualizarCilindro(_ossoPeitoCabeca,   _posPeito,   _posCabeca);

        // Compensação postural (só com 2 sensores — ombro live)
        if (_deviceOmbro != null)
        {
            CompensacaoOmbro = Vector3.Distance(posOmbro, _posOmbroBase);
            if (_matOmbro && _matOmbroAlerta && _goOmbro)
            {
                var r = _goOmbro.GetComponent<Renderer>();
                if (r) r.material = OmbroCompensando ? _matOmbroAlerta : _matOmbro;
            }
        }
    }

    // ── IK do cotovelo ────────────────────────────────────────────────

    /// <summary>
    /// IK de 2 elos: calcula a posição do cotovelo dada a posição do ombro e da palma.
    ///
    /// Usa a lei dos cossenos para encontrar o ângulo de flexão, depois projeta o
    /// cotovelo num plano perpendicular ao eixo ombro→palma usando o vetor hint.
    ///
    /// Hint = Vector3.back → cotovelo aponta para longe da base station (atrás do player).
    /// </summary>
    private static Vector3 CalcularPosCotovelo(Vector3 ombro, Vector3 palma, float comprimentoBraco, Vector3 hint)
    {
        // Fallback se comprimento não calibrado
        float bracoTotal = comprimentoBraco > 0.05f ? comprimentoBraco : 0.44f;

        float Lu = bracoTotal * FRACAO_BRACO_SUPERIOR; // braço superior (ombro→cotovelo)
        float Lf = bracoTotal * FRACAO_ANTEBRACO;      // antebraço (cotovelo→palma)

        Vector3 dir = palma - ombro;
        float d = dir.magnitude;

        if (d < 0.001f) return (ombro + palma) * 0.5f;

        Vector3 dirNorm = dir / d;

        // Limite do alcance: braço totalmente estendido ou dobrado ao máximo
        float maxAlcance = Lu + Lf;
        float minAlcance = Mathf.Abs(Lu - Lf);
        float dEfetivo   = Mathf.Clamp(d, minAlcance + 0.001f, maxAlcance - 0.001f);

        // Lei dos cossenos: distância ao longo do eixo até à base da perpendicular
        float a = (dEfetivo * dEfetivo + Lu * Lu - Lf * Lf) / (2f * dEfetivo);
        // Altura do cotovelo acima do eixo ombro→palma
        float h = Mathf.Sqrt(Mathf.Max(0f, Lu * Lu - a * a));

        // Ponto projetado ao longo do eixo (limite ao comprimento real da direção)
        Vector3 pontoBase = ombro + dirNorm * Mathf.Min(a, d);

        if (h < 0.001f) return pontoBase; // braço esticado — cotovelo no eixo

        // Normaliza o hint; fallback se paralelo ao eixo do braço
        if (hint.magnitude < 0.01f) hint = Vector3.back;
        hint = hint.normalized;
        if (Mathf.Abs(Vector3.Dot(dirNorm, hint)) > 0.98f)
            hint = Vector3.right;

        // Componente do hint perpendicular ao eixo do braço
        Vector3 perpHint = (hint - Vector3.Dot(hint, dirNorm) * dirNorm).normalized;

        return pontoBase + perpHint * h;
    }

    // ── Helpers de geometria ──────────────────────────────────────────

    private static void AtualizarCilindro(GameObject cil, Vector3 a, Vector3 b)
    {
        if (cil == null) return;
        float dist = Vector3.Distance(a, b);
        if (dist < 0.001f) { cil.SetActive(false); return; }
        cil.SetActive(true);
        cil.transform.position   = (a + b) / 2f;
        cil.transform.localScale = new Vector3(RAIO_OSSO * 2f, dist / 2f, RAIO_OSSO * 2f);
        cil.transform.rotation   = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
    }

    // ── Criação de objetos visuais ────────────────────────────────────

    private GameObject CriarEsfera(string nome, Color cor)
        => CriarEsfera(nome, CriarMaterial(cor));

    private GameObject CriarEsfera(string nome, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Esqueleto_" + nome;
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * (RAIO_ESFERA * 2f);
        Object.DestroyImmediate(go.GetComponent<SphereCollider>());
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    private GameObject CriarCilindro(string nome, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Esqueleto_" + nome;
        go.transform.SetParent(transform, false);
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        go.GetComponent<Renderer>().material = CriarMaterial(cor);
        return go;
    }

    private static Material CriarMaterial(Color cor)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = cor;
        mat.SetFloat("_Metallic",    0f);
        mat.SetFloat("_Glossiness",  0.3f);
        return mat;
    }

    private static void SetAtivo(GameObject go, bool ativo)
    {
        if (go != null) go.SetActive(ativo);
    }

    private static void ColorirSensor(OmmoDevice device, Color cor)
    {
        if (device == null) return;
        for (int i = 0; i < device.NumeroSensores; i++)
        {
            var t = device.ObterTransformSensor(i);
            if (t == null) continue;
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            var mat = new Material(Shader.Find("Standard")) { color = cor };
            mat.SetFloat("_Metallic",   0.05f);
            mat.SetFloat("_Glossiness", 0.5f);
            foreach (var r in renderers)
                r.material = mat;
        }
    }
}
