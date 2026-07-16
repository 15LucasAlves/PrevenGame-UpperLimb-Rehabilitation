using UnityEngine;

/// <summary>
/// OmmoEsqueletoJogador — Visualização do esqueleto do membro superior em tempo real.
///
/// Modo de 1 sensor (palma). Articulações:
///   Palma    → cubo do sensor (posição + orientação REAIS)
///   Cotovelo → esfera verde-limão — derivada da ORIENTAÇÃO medida do antebraço
///   Ombro    → esfera laranja translúcida (posição fixa pós-calibração, ESTIMADA)
///   Peito    → esfera azul (posição fixa pós-calibração, opcional)
///   Cabeça   → esfera branca (posição fixa pós-calibração, opcional)
///
/// Cotovelo (cinemática direta a partir do antebraço medido):
///   Lf = ComprimentoBraco * (14.6 / 44.0)  → antebraço
///   cotovelo = palma + (rotaçãoPalma * EixoAntebracoLocal) * Lf
///   → sem palpites; correto em qualquer plano de exercício (sagital, lateral, abdução).
///
/// O braço superior (cotovelo→ombro) é apenas ligação visual estimada e é
/// renderizado translúcido para não fingir precisão que não existe.
///
/// Compensação postural (proxy de 1 sensor): a palma não pode estar mais longe do
/// ombro do que o comprimento do braço; se estiver, o tronco/ombro avançou (batota).
/// </summary>
public class OmmoEsqueletoJogador : MonoBehaviour
{
    // ── Proporções (DIN 33402 / ISO 7250) ────────────────────────────
    private const float FRACAO_ANTEBRACO      = 14.6f / 44.0f; // antebraço (cotovelo→palma)
    private const float FRACAO_BRACO_SUPERIOR = 18.6f / 44.0f; // braço superior (cotovelo→ombro)

    // ── Limiar de compensação postural ────────────────────────────────
    // Excesso de alcance (palma além do comprimento do braço) que conta como batota.
    private const float LIMIAR_COMPENSACAO = 0.05f; // 5 cm

    // ── IK do antebraço ───────────────────────────────────────────────
    [Tooltip("Eixo LOCAL do sensor da palma que aponta da palma para o cotovelo " +
             "(ao longo do antebraço). Depende da montagem do sensor — confirmar em Play Mode.")]
    public Vector3 EixoAntebracoLocal = Vector3.back;

    // ── Geometria ─────────────────────────────────────────────────────
    private const float RAIO_OSSO   = 0.02f;
    private const float RAIO_ESFERA = 0.08f;

    // ── Dispositivos ──────────────────────────────────────────────────
    private OmmoDevice _devicePalma;

    // ── Posições fixas ────────────────────────────────────────────────
    private Vector3 _posPeito;
    private Vector3 _posCabeca;
    private Vector3 _posOmbroBase;

    // ── Estado ────────────────────────────────────────────────────────
    private bool _calibrado  = false;
    // True se o passo Peito foi calibrado (opcional — apenas visual)
    private bool _temPeito   = false;

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
        Debug.Log($"[OmmoEsqueleto] ComprimentoBraco = {comprimento:F2} units = {comprimento * 100f:F1} cm");
    }

    /// <summary>
    /// Reidrata a calibração a partir de valores persistidos (ex.: numa cena de minijogo,
    /// sem repetir a captura interativa). Chamar após <see cref="Inicializar"/>.
    /// </summary>
    public void AplicarCalibracao(Vector3 posOmbro, float comprimento, Vector3 direcao)
    {
        DefinirPosicaoFixa("Ombro", posOmbro);
        DefinirComprimentoBraco(comprimento);
        DirecaoFrente = direcao == Vector3.zero ? Vector3.forward : direcao.normalized;
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

    public Vector3 ObterPosOmbroAtual() => _posOmbroBase;

    // ── Inicialização ─────────────────────────────────────────────────

    public void Inicializar(OmmoDevice devicePalma)
    {
        _devicePalma = devicePalma;

        ColorirSensor(devicePalma, new Color(0.3f, 0.7f, 1f));

        // Antebraço (palma→cotovelo) = MEDIDO → sólido e brilhante.
        _goCotovelo        = CriarEsfera("Cotovelo", new Color(0.6f, 1f, 0.2f));
        _ossoPalmaCotovelo = CriarCilindro("Osso_Palma_Cotovelo", new Color(0.6f, 1f, 0.2f));

        // Braço superior (cotovelo→ombro) e ombro = ESTIMADOS → translúcidos.
        _matOmbro          = CriarMaterialTranslucido(new Color(1f, 0.55f, 0.1f), 0.45f);
        _matOmbroAlerta    = CriarMaterial(new Color(1f, 0.15f, 0.1f)); // alerta opaco para destacar
        _goOmbro           = CriarEsfera("Ombro", _matOmbro);
        _ossoCotoveloOmbro = CriarCilindroMat("Osso_Cotovelo_Ombro",
                                 CriarMaterialTranslucido(new Color(1f, 0.55f, 0.1f), 0.35f));

        // Tronco (peito/cabeça) — apenas visual, fixo pós-calibração.
        _goPeito         = CriarEsfera("Peito",  new Color(0.3f, 0.7f, 1f));
        _goCabeca        = CriarEsfera("Cabeca", Color.white);
        _ossoOmbroPeito  = CriarCilindro("Osso_Ombro_Peito",  new Color(0.3f, 0.7f, 1f));
        _ossoPeitoCabeca = CriarCilindro("Osso_Peito_Cabeca", Color.white);

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
                _temPeito = true;
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
        SetAtivo(_ossoPalmaCotovelo, ativo);
        SetAtivo(_ossoCotoveloOmbro, ativo);
        // Peito/Cabeça só ativados se o passo de calibração correspondente foi feito
        SetAtivo(_goPeito,           ativo && _temPeito);
        SetAtivo(_goCabeca,          ativo && _temPeito);
        SetAtivo(_ossoOmbroPeito,    ativo && _temPeito);
        SetAtivo(_ossoPeitoCabeca,   ativo && _temPeito);

        if (ativo)
            Debug.Log($"[OmmoEsqueleto] ✅ Ativo | Ombro base: {_posOmbroBase}");
    }

    // ── Update ────────────────────────────────────────────────────────

    void Update()
    {
        if (!_calibrado || _devicePalma == null) return;

        Vector3 posPalma = _devicePalma.ObterPosicaoSensor(0);

        // Cotovelo a partir da ORIENTAÇÃO real do antebraço (sensor da palma),
        // não de um palpite — correto em qualquer plano de exercício.
        Vector3 posCotovelo = CalcularPosCotovelo(posPalma);

        // Ombro: em vez de fixo (o que obrigaria o osso do braço superior a esticar
        // quando a palma avança), o ombro FLUTUA mantendo o braço superior no seu
        // comprimento anatómico real (Lu). Move-se ao longo da direção cotovelo→ombro-base.
        Vector3 posOmbro = CalcularPosOmbro(posCotovelo);

        if (_goCotovelo) _goCotovelo.transform.position = posCotovelo;
        if (_goOmbro)    _goOmbro.transform.position    = posOmbro;

        AtualizarCilindro(_ossoPalmaCotovelo, posPalma,    posCotovelo); // medido
        AtualizarCilindro(_ossoCotoveloOmbro, posCotovelo, posOmbro);    // estimado
        if (_temPeito)
        {
            AtualizarCilindro(_ossoOmbroPeito,  posOmbro,  _posPeito);
            AtualizarCilindro(_ossoPeitoCabeca, _posPeito, _posCabeca);
        }

        // Compensação postural (proxy de 1 sensor): a palma não pode estar mais
        // longe do ombro do que o comprimento do braço. O excesso = avanço do tronco.
        CompensacaoOmbro = Mathf.Max(0f, Vector3.Distance(posPalma, _posOmbroBase) - ComprimentoBraco);
        if (_matOmbro && _matOmbroAlerta && _goOmbro)
        {
            var r = _goOmbro.GetComponent<Renderer>();
            if (r) r.material = OmbroCompensando ? _matOmbroAlerta : _matOmbro;
        }
    }

    // ── Cotovelo (cinemática direta a partir do antebraço medido) ─────

    /// <summary>
    /// Posição do cotovelo a partir da orientação REAL do sensor da palma.
    ///
    /// O antebraço é um corpo rígido medido: a sua direção no mundo é
    /// rotaçãoPalma * EixoAntebracoLocal. O cotovelo fica a um comprimento de
    /// antebraço (Lf) da palma ao longo desse eixo. Sem palpites de hint, pelo
    /// que funciona igual em flexão sagital, lateral e abdução.
    /// </summary>
    private Vector3 CalcularPosCotovelo(Vector3 posPalma)
    {
        float bracoTotal = ComprimentoBraco > 0.05f ? ComprimentoBraco : 0.44f;
        float Lf         = bracoTotal * FRACAO_ANTEBRACO; // antebraço (cotovelo→palma)

        Vector3 eixo = _devicePalma.ObterRotacaoSensor(0) * EixoAntebracoLocal;
        if (eixo.sqrMagnitude < 1e-6f)
            eixo = -DirecaoFrente; // fallback se orientação ainda não chegou

        return posPalma + eixo.normalized * Lf;
    }

    /// <summary>
    /// Posição do ombro mantendo o braço superior (cotovelo→ombro) no seu comprimento
    /// anatómico real, em vez de esticar o osso até ao ombro-base fixo. O ombro flutua
    /// a partir do cotovelo na direção do ombro-base — uma posição mais favorável que
    /// evita o aspeto "esticado" quando a palma avança para fora do ROM.
    /// </summary>
    private Vector3 CalcularPosOmbro(Vector3 posCotovelo)
    {
        float bracoTotal = ComprimentoBraco > 0.05f ? ComprimentoBraco : 0.44f;
        float Lu         = bracoTotal * FRACAO_BRACO_SUPERIOR; // braço superior (cotovelo→ombro)

        Vector3 dirOmbro = _posOmbroBase - posCotovelo;
        if (dirOmbro.sqrMagnitude < 1e-6f)
            dirOmbro = -DirecaoFrente; // fallback degenerado

        return posCotovelo + dirOmbro.normalized * Lu;
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
        => CriarCilindroMat(nome, CriarMaterial(cor));

    private GameObject CriarCilindroMat(string nome, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Esqueleto_" + nome;
        go.transform.SetParent(transform, false);
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        go.GetComponent<Renderer>().material = mat;
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

    /// <summary>Material translúcido — usado para as partes ESTIMADAS do esqueleto.</summary>
    private static Material CriarMaterialTranslucido(Color cor, float alpha)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(cor.r, cor.g, cor.b, alpha);
        mat.SetFloat("_Mode", 3f); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.SetFloat("_Metallic",   0f);
        mat.SetFloat("_Glossiness", 0.3f);
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
            // Contorno Fresnel: realça o objeto controlado pelo Ommo. Fallback para
            // Standard caso o shader não esteja disponível.
            var shader = Shader.Find("PrevenGame/RimGlow") ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = cor };
            if (mat.HasProperty("_RimColor"))
            {
                mat.SetColor("_RimColor", Color.white);
                mat.SetFloat("_RimPower", 3f);
                mat.SetFloat("_RimIntensity", 2.2f);
            }
            mat.SetFloat("_Metallic",   0.05f);
            mat.SetFloat("_Glossiness", 0.5f);
            foreach (var r in renderers)
                r.material = mat;
        }
    }
}
