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
    [Tooltip("Substring do nome do dispositivo a ligar (vazio = liga ao primeiro encontrado).")]
    public string FiltroNome = "";

    [Header("Deteção de pressão")]
    [Tooltip("Valor a partir do qual a leitura conta como 'apertou'. Afinar com LogValores ligado.")]
    public float Limiar = 0.5f;
    [Tooltip("Tempo mínimo entre duas pressões aceites (segundos).")]
    public float DebounceSegundos = 0.5f;
    [Tooltip("Escreve no log os valores recebidos (1×/segundo) — útil para afinar o Limiar.")]
    public bool LogValores = true;

    /// <summary>Leitura contínua do sensor (máx. dos dois canais). 0 sem dispositivo.</summary>
    public float ValorAtual { get; private set; }

    /// <summary>True com o dispositivo de pressão ligado.</summary>
    public bool Disponivel { get; private set; }

    /// <summary>Emitido quando o utilizador faz pressão (flanco ascendente + debounce).</summary>
    public event System.Action OnPressao;

    private BLEManager _ble;
    private ulong _enderecoLigado;
    private bool  _acimaDoLimiar;
    private float _ultimaPressao = -999f;
    private float _ultimoLog     = -999f;

    void Start()
    {
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
        Debug.Log("[EntradaPressao] A procurar o sensor de pressão" +
                  (string.IsNullOrEmpty(FiltroNome) ? "..." : $" \"{FiltroNome}\"..."));
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
        if (Disponivel) return;
        if (!string.IsNullOrEmpty(FiltroNome) &&
            (d.Name == null || d.Name.IndexOf(FiltroNome, System.StringComparison.OrdinalIgnoreCase) < 0))
            return;

        Debug.Log($"[EntradaPressao] Dispositivo encontrado: {d.Name} ({d.AddressString}) — a ligar...");
        _ble.Connect(d.Address);
    }

    void AoLigar(BLEManager.BLEDevice d)
    {
        if (Disponivel) return;
        _enderecoLigado = d.Address;
        Disponivel = true;
        _ble.StopScan();
        Debug.Log($"[EntradaPressao] Ligado a {d.Name} ({d.AddressString}).");
    }

    void AoDesligar(BLEManager.BLEDevice d)
    {
        if (d.Address != _enderecoLigado) return;
        Disponivel     = false;
        ValorAtual     = 0f;
        _acimaDoLimiar = false;
        Debug.LogWarning("[EntradaPressao] Sensor de pressão desligado — a procurar de novo...");
        _ble.StartScan();
    }

    // ── Dados do sensor ───────────────────────────────────────────────
    void AoReceberDados(BLEManager.BLEDevice d, float w1, float w2)
    {
        if (d.Address != _enderecoLigado) return;

        ValorAtual = Mathf.Max(w1, w2);

        if (LogValores && Time.unscaledTime - _ultimoLog >= 1f)
        {
            _ultimoLog = Time.unscaledTime;
            Debug.Log($"[EntradaPressao] Leitura: {w1:F2} / {w2:F2} (limiar {Limiar:F2})");
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
