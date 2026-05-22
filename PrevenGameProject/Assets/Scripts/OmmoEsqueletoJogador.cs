using UnityEngine;

/// <summary>
/// OmmoEsqueletoJogador — Visualização do esqueleto do membro superior em tempo real.
///
/// Articulações:
///   Palma  → CuboSensor existente (controlado pelo OmmoDevice)
///   Cotovelo → esfera verde-limão (calculada live)
///   Ombro    → esfera laranja (Sensor B live)
///   Peito    → esfera azul (posição fixa pós-calibração)
///   Cabeça   → esfera branca (posição fixa pós-calibração)
///
/// Ossos (cilindros finos):
///   Palma↔Cotovelo · Cotovelo↔Ombro · Ombro↔Peito · Peito↔Cabeça
///
/// Proporções científicas (DIN 33402 / ISO 7250):
///   Total ombro→palma = 44% da altura (mão 10.8% + antebraço 14.6% + braço 18.6%)
///   Cotovelo fica a 57.7% da palma em direção ao ombro:
///       (10.8 + 14.6) / 44.0 = 0.577
///   Funciona para qualquer altura — usa a distância real medida na calibração.
/// </summary>
public class OmmoEsqueletoJogador : MonoBehaviour
{
    // ── Constante de proporção científica ─────────────────────────────
    // Cotovelo = palma + (ombro - palma) * PROPORCAO_COTOVELO
    // Derivado de: (hand 10.8% + forearm 14.6%) / total arm 44.0% = 0.577
    private const float PROPORCAO_COTOVELO = 0.577f;

    // ── Limiar de compensação postural (metros Unity = 10 cm) ─────────
    private const float LIMIAR_COMPENSACAO = 0.02f; // 2 cm

    // ── Espessura dos ossos e raio das esferas ────────────────────────
    private const float RAIO_OSSO   = 0.02f;  // 2 cm de raio → cilindros finos
    private const float RAIO_ESFERA = 0.08f;  // 8 cm de raio → articulações

    // ── Referências aos dispositivos ──────────────────────────────────
    private OmmoDevice _devicePalma;  // SIU A — palma (posição do sensor 0)
    private OmmoDevice _deviceOmbro;  // SIU B — ombro (posição do sensor 0)

    // ── Posições fixas (definidas na calibração) ──────────────────────
    private Vector3 _posPeito;
    private Vector3 _posCabeca;
    private Vector3 _posOmbroBase; // referência em repouso para deteção de compensação

    // ── Estado ────────────────────────────────────────────────────────
    private bool _calibrado = false;

    // ── GameObjects das articulações ──────────────────────────────────
    private GameObject _goCotovelo;
    private GameObject _goOmbro;
    private GameObject _goPeito;
    private GameObject _goCabeca;

    // ── GameObjects dos ossos (cilindros) ─────────────────────────────
    private GameObject _ossoPalmaCotovelo;
    private GameObject _ossoCotoveloOmbro;
    private GameObject _ossoOmbroPeito;
    private GameObject _ossoPeitoCabeca;

    // ── Materiais (para feedback de compensação) ──────────────────────
    private Material _matOmbro;
    private Material _matOmbroAlerta;

    // ── Propriedade pública ───────────────────────────────────────────

    /// <summary>Distância do ombro à posição base (em Unity units). 0.02 = 2 cm.</summary>
    public float CompensacaoOmbro { get; private set; }

    /// <summary>True quando o ombro ultrapassou o limiar de compensação postural.</summary>
    public bool OmbroCompensando => CompensacaoOmbro > LIMIAR_COMPENSACAO;

    // ── Inicialização ─────────────────────────────────────────────────

    /// <summary>
    /// Chamado pelo OmmoCalibracaoManager quando os dois dispositivos estão ativos.
    /// Cria os GameObjects do esqueleto (ainda invisíveis) e guarda as referências.
    /// </summary>
    public void Inicializar(OmmoDevice devicePalma, OmmoDevice deviceOmbro)
    {
        _devicePalma = devicePalma;
        _deviceOmbro = deviceOmbro;

        // Cor do cubo da palma (azul) e do ombro (laranja) para distinção visual
        ColorirSensor(devicePalma, new Color(0.3f, 0.7f, 1f));    // azul
        ColorirSensor(deviceOmbro, new Color(1f, 0.55f, 0.1f));   // laranja

        // Cria hierarquia de objetos sob este transform
        _goCotovelo = CriarEsfera("Cotovelo", new Color(0.6f, 1f, 0.2f));   // verde-limão
        _matOmbro       = CriarMaterial(new Color(1f, 0.55f, 0.1f));        // laranja
        _matOmbroAlerta = CriarMaterial(new Color(1f, 0.15f, 0.1f));        // vermelho alerta
        _goOmbro   = CriarEsfera("Ombro",  _matOmbro);
        _goPeito   = CriarEsfera("Peito",  new Color(0.3f, 0.7f, 1f));      // azul
        _goCabeca  = CriarEsfera("Cabeca", Color.white);

        _ossoPalmaCotovelo = CriarCilindro("Osso_Palma_Cotovelo", new Color(0.6f, 1f, 0.2f));
        _ossoCotoveloOmbro = CriarCilindro("Osso_Cotovelo_Ombro", new Color(1f, 0.55f, 0.1f));
        _ossoOmbroPeito    = CriarCilindro("Osso_Ombro_Peito",    new Color(0.3f, 0.7f, 1f));
        _ossoPeitoCabeca   = CriarCilindro("Osso_Peito_Cabeca",   Color.white);

        // Desativa tudo até à calibração estar completa
        AtivacaoEsqueleto(false);

        Debug.Log("[OmmoEsqueleto] Inicializado — aguarda calibração.");
    }

    /// <summary>
    /// Define uma posição fixa de articulação após captura na calibração.
    /// Nomes válidos: "Peito", "Cabeca"
    /// </summary>
    public void DefinirPosicaoFixa(string nome, Vector3 posicao)
    {
        switch (nome)
        {
            case "Peito":
                _posPeito = posicao;
                if (_goPeito) _goPeito.transform.position = posicao;
                Debug.Log($"[OmmoEsqueleto] Peito definido: {posicao}");
                break;
            case "Cabeca":
                _posCabeca = posicao;
                if (_goCabeca) _goCabeca.transform.position = posicao;
                Debug.Log($"[OmmoEsqueleto] Cabeça definida: {posicao}");
                break;
        }
    }

    /// <summary>Ativa/desativa a visualização do esqueleto.</summary>
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
            // Guarda a posição base do ombro para deteção de compensação
            _posOmbroBase = _deviceOmbro.ObterPosicaoSensor(0);
            Debug.Log($"[OmmoEsqueleto] ✅ Esqueleto ativo | Ombro base: {_posOmbroBase}");
        }
    }

    // ── Update ────────────────────────────────────────────────────────

    void Update()
    {
        if (!_calibrado || _devicePalma == null || _deviceOmbro == null) return;

        // Posições live dos sensores
        Vector3 posPalma = _devicePalma.ObterPosicaoSensor(0);
        Vector3 posOmbro = _deviceOmbro.ObterPosicaoSensor(0);

        // Cotovelo: proporção científica — 57.7% da palma até ao ombro
        Vector3 posCotovelo = posPalma + (posOmbro - posPalma) * PROPORCAO_COTOVELO;

        // Atualiza posições das articulações live
        if (_goCotovelo) _goCotovelo.transform.position = posCotovelo;
        if (_goOmbro)    _goOmbro.transform.position    = posOmbro;
        // Peito e Cabeça são fixos — posição já definida em DefinirPosicaoFixa()

        // Atualiza cilindros
        AtualizarCilindro(_ossoPalmaCotovelo, posPalma,   posCotovelo);
        AtualizarCilindro(_ossoCotoveloOmbro, posCotovelo, posOmbro);
        AtualizarCilindro(_ossoOmbroPeito,    posOmbro,   _posPeito);
        AtualizarCilindro(_ossoPeitoCabeca,   _posPeito,  _posCabeca);

        // Deteção de compensação postural
        CompensacaoOmbro = Vector3.Distance(posOmbro, _posOmbroBase);
        if (_matOmbro && _matOmbroAlerta && _goOmbro)
        {
            var renderer = _goOmbro.GetComponent<Renderer>();
            if (renderer)
                renderer.material = OmbroCompensando ? _matOmbroAlerta : _matOmbro;
        }
    }

    // ── Helpers de geometria ──────────────────────────────────────────

    /// <summary>
    /// Posiciona e orienta um cilindro para ligar dois pontos.
    /// O cilindro Unity tem Y como eixo local e height=2 por defeito.
    /// </summary>
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
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.3f);
        return mat;
    }

    private static void SetAtivo(GameObject go, bool ativo)
    {
        if (go != null) go.SetActive(ativo);
    }

    /// <summary>
    /// Aplica uma cor a todos os Renderers dos sensores filhos de um OmmoDevice.
    /// Cria um novo Material para não partilhar com outros dispositivos.
    /// </summary>
    private static void ColorirSensor(OmmoDevice device, Color cor)
    {
        if (device == null) return;
        for (int i = 0; i < device.NumeroSensores; i++)
        {
            var t = device.ObterTransformSensor(i);
            if (t == null) continue;
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            var mat = new Material(Shader.Find("Standard")) { color = cor };
            mat.SetFloat("_Metallic",    0.05f);
            mat.SetFloat("_Glossiness",  0.5f);
            foreach (var r in renderers)
                r.material = mat;
        }
    }
}
