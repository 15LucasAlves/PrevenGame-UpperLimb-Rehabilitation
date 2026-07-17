using UnityEngine;

/// <summary>
/// EntradaPressao — Serviço de pressão do jogo (sensor de força ligado por BLE via BLEManager).
///
/// Faz scan e ligação automática ao dispositivo e expõe:
///   • <see cref="ValorAtual"/> — leitura contínua (máx. dos dois canais), para a jogabilidade;
///   • <see cref="OnPressao"/> — evento "apertou" (flanco ascendente acima de Limiar + debounce);
///   • <see cref="Disponivel"/> — true com o dispositivo ligado.
///
/// A calibração é o primeiro consumidor (confirmar capturas); no futuro a pressão será
/// integrada nos minijogos. Sem BLE ligado nada falha — quem consome deve ter fallback
/// (a calibração aceita Enter). Os eventos do BLEManager já chegam no main thread.
/// </summary>
public class EntradaPressao : MonoBehaviour
{
    [Header("Ligação BLE")]
    [Tooltip("Substring do nome do sensor de pressão (o dispositivo anuncia-se como GRASP_x.y.z). " +
             "NUNCA deixar vazio num ambiente real — ligaria a qualquer dispositivo BLE por perto.")]
    public string FiltroNome = "GRASP";
    [Tooltip("Sem dispositivo ligado, repete o scan a cada N segundos (o anúncio BLE pode não estar ativo no primeiro).")]
    public float RescanSegundos = 10f;

    [Tooltip("INTERRUPTOR DE SEGURANÇA: desliga todo o BLE (o BLEDll nativo pode crashar o editor " +
             "ao configurar o UART). Com isto off, a captura fica por Enter/gatilho.")]
    public bool AtivarBle = true;
    [Tooltip("Tempo máximo de uma tentativa de ligação antes de voltar ao scan (segundos).")]
    public float TimeoutLigacao = 10f;

    [Header("Deteção de pressão")]
    [Tooltip("Zona morta de repouso: leituras com |valor| até este limiar contam como 'em repouso' " +
             "(o sensor emite valores negativos e positivos por desenho; [-0.5, 0.5] é resting). " +
             "Qualquer leitura fora da zona conta como pressão.")]
    public float Limiar = 0.5f;
    [Tooltip("Tempo mínimo entre duas pressões aceites (segundos).")]
    public float DebounceSegundos = 0.5f;
    [Tooltip("Escreve no log os valores recebidos (1×/segundo) — útil para afinar o Limiar.")]
    public bool LogValores = false;

    /// <summary>
    /// Leitura contínua do sensor em MÓDULO (máx. de |w1| e |w2|) — o dispositivo emite
    /// negativos e positivos, e só a magnitude interessa. 0 sem dispositivo.
    /// </summary>
    public float ValorAtual { get; private set; }

    /// <summary>True com o dispositivo de pressão ligado.</summary>
    public bool Disponivel { get; private set; }

    /// <summary>Emitido quando o utilizador faz pressão (flanco ascendente + debounce).</summary>
    public event System.Action OnPressao;

    private BLEManager _ble;
    private ulong _enderecoLigado;
    private ulong _enderecoTentativa;   // ligação em curso (uma de cada vez!)
    private bool  _aLigar;
    private float _fimTentativa;
    private bool  _acimaDoLimiar;
    private float _ultimaPressao = -999f;
    private float _ultimoLog     = -999f;
    private float _proximoScan   = -999f;

    void Start()
    {
        // Interruptor de segurança: o BLEDll nativo já matou o editor durante a
        // configuração do UART — em Modo Comando (testes sem hardware) ou com o
        // toggle desligado, o BLE nem arranca (Enter/gatilho continuam a funcionar).
        if (!AtivarBle ||
            (SessionManager.Instancia != null && SessionManager.Instancia.ModoComando))
        {
            Debug.Log("[EntradaPressao] BLE desativado (AtivarBle=false ou Modo Comando) — captura por Enter/gatilho.");
            return;
        }

        _ble = BLEManager.Instance;
        if (_ble == null)
        {
            Debug.LogWarning("[EntradaPressao] BLEManager não encontrado — pressão indisponível (usa Enter).");
            return;
        }

        _ble.OnDeviceFound        += AoEncontrarDispositivo;
        _ble.OnDeviceConnected    += AoLigar;
        _ble.OnDeviceDisconnected += AoDesligar;
        _ble.OnSensorData         += AoReceberDados;

        _ble.StartScan();
        _proximoScan = Time.unscaledTime + RescanSegundos;
        Debug.Log("[EntradaPressao] A procurar o sensor de pressão" +
                  (string.IsNullOrEmpty(FiltroNome) ? "..." : $" \"{FiltroNome}\"..."));
    }

    void Update()
    {
        if (_ble == null || Disponivel) return;

        // Tentativa de ligação a demorar demasiado — desiste e volta ao scan.
        if (_aLigar)
        {
            if (Time.unscaledTime >= _fimTentativa)
            {
                Debug.LogWarning("[EntradaPressao] Ligação demorou demasiado — a voltar ao scan.");
                _aLigar = false;
                _proximoScan = 0f; // re-scan imediato
            }
            return;
        }

        // O anúncio BLE pode não estar ativo no primeiro scan (dispositivo desligado,
        // a acordar, fora de alcance) — insiste periodicamente até ligar.
        if (Time.unscaledTime < _proximoScan) return;
        _proximoScan = Time.unscaledTime + RescanSegundos;
        Debug.Log("[EntradaPressao] Sem ligação — novo scan...");
        _ble.StartScan();
    }

    void OnDestroy()
    {
        if (_ble == null) return;
        _ble.OnDeviceFound        -= AoEncontrarDispositivo;
        _ble.OnDeviceConnected    -= AoLigar;
        _ble.OnDeviceDisconnected -= AoDesligar;
        _ble.OnSensorData         -= AoReceberDados;
    }

    // ── Ligação automática ────────────────────────────────────────────
    void AoEncontrarDispositivo(BLEManager.BLEDevice d)
    {
        // UMA ligação de cada vez: com o scan a apanhar dezenas de dispositivos
        // (telemóveis, headphones...), ligar a tudo congela/crasha a DLL nativa.
        if (Disponivel || _aLigar) return;
        if (!string.IsNullOrEmpty(FiltroNome) &&
            (d.Name == null || d.Name.IndexOf(FiltroNome, System.StringComparison.OrdinalIgnoreCase) < 0))
            return; // não é o sensor — ignora em silêncio

        Debug.Log($"[EntradaPressao] Sensor encontrado: {d.Name} ({d.AddressString}) — a ligar...");
        _aLigar            = true;
        _enderecoTentativa = d.Address;
        _fimTentativa      = Time.unscaledTime + TimeoutLigacao;
        _ble.StopScan(); // ligar com o scan ativo é instável no BLE do Windows
        _ble.Connect(d.Address);
    }

    void AoLigar(BLEManager.BLEDevice d)
    {
        if (Disponivel || d.Address != _enderecoTentativa) return;
        _aLigar         = false;
        _enderecoLigado = d.Address;
        Disponivel      = true;
        Debug.Log($"[EntradaPressao] Ligado a {d.Name} ({d.AddressString}).");
    }

    void AoDesligar(BLEManager.BLEDevice d)
    {
        if (_aLigar && d.Address == _enderecoTentativa)
        {
            // A tentativa falhou (o dispositivo recusou/caiu) — volta ao scan.
            _aLigar = false;
            _proximoScan = Time.unscaledTime + 2f;
            Debug.LogWarning("[EntradaPressao] Tentativa de ligação falhou — novo scan em breve.");
            return;
        }

        if (d.Address != _enderecoLigado) return;
        Disponivel     = false;
        ValorAtual     = 0f;
        _acimaDoLimiar = false;
        _proximoScan   = Time.unscaledTime + 2f;
        Debug.LogWarning("[EntradaPressao] Sensor de pressão desligado — a procurar de novo...");
    }

    // ── Dados do sensor ───────────────────────────────────────────────
    void AoReceberDados(BLEManager.BLEDevice d, float w1, float w2)
    {
        if (d.Address != _enderecoLigado) return;

        // Módulo: o sensor emite valores negativos e positivos por desenho — só a
        // magnitude interessa. Dentro de [-Limiar, +Limiar] é repouso; fora conta.
        ValorAtual = Mathf.Max(Mathf.Abs(w1), Mathf.Abs(w2));

        if (LogValores && Time.unscaledTime - _ultimoLog >= 1f)
        {
            _ultimoLog = Time.unscaledTime;
            Debug.Log($"[EntradaPressao] Leitura: {w1:F2} / {w2:F2} (|máx|={ValorAtual:F2}, limiar {Limiar:F2})");
        }

        // Flanco ascendente + debounce → uma "pressão". Apertar e manter conta uma vez.
        bool acima = ValorAtual >= Limiar;
        if (acima && !_acimaDoLimiar && Time.unscaledTime - _ultimaPressao >= DebounceSegundos)
        {
            _ultimaPressao = Time.unscaledTime;
            OnPressao?.Invoke();
        }

        // Histerese: só re-arma quando o valor desce bem abaixo do limiar — leituras com
        // ruído a oscilar à volta do limiar não geram pressões fantasma em cadeia.
        if (acima)                            _acimaDoLimiar = true;
        else if (ValorAtual < Limiar * 0.7f) _acimaDoLimiar = false;
    }
}
