using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

public class OmmoDevice : MonoBehaviour
{
    [Tooltip("Sensor Prefab.")]
    public GameObject SensorPrefab;

    private List<GameObject> _sensors = new List<GameObject>();

    private Task _dataTask;
    private CancellationTokenSource _source;
    private CancellationToken _token;

    // Referencial do espaço Ommo (o transform do BaseStation/OmmoRoot). As posições
    // dos sensores chegam relativas à base station física; aplicar o referencial
    // completo (posição+rotação) permite ancorar todo o espaço Ommo no mundo VR
    // (via QR code). Só pode ser lido no main thread.
    private Transform _referencial;
    private Vector3[] _sensorPositions;
    private Quaternion[] _sensorOrientations;
    private bool _primeirosDadosRecebidos = false;
    private string _nomeCache; // cache seguro para acesso em background threads

    // Filtros de posição — um por sensor (descarta zeros, throttle, jump filter)
    private OmmoSensorFilter[] _filtros;

    [Tooltip("Scale for 1 Unity value in centimeters.")]
    private float _unityScaleInCM;
    // [Header("Device Settings")]
    // public uint DeviceType = 0xFF;
    private uint _siuUuid = 0;
    private uint _portId = 0;
    public Ommo.DeviceFusionMode RequestedMode = Ommo.DeviceFusionMode.Default;

    [Header("Debug")]
    [Tooltip("Intervalo em segundos entre cada linha de debug na consola (0 = desativado).")]
    public float DebugIntervalSegundos = 0f;
    private float _debugTimer = 0f;

    // TODO: make this a singleton
    private Ommo.Client _client;


    private Ommo.DeviceDescriptor _device = null;

    // Awake corre imediatamente quando SetActive(true) é chamado, antes de qualquer Set*().
    // É aqui que _source/_token têm de ser inicializados porque SetDeviceDescriptor()
    // (que usa _token) é chamado no mesmo frame que SetActive(true), antes de Start().
    void Awake()
    {
        _source    = new CancellationTokenSource();
        _token     = _source.Token;
        _nomeCache = gameObject.name; // main thread — seguro
    }

    void Start()
    {
        Debug.Log("[OmmoDevice] Iniciado");
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < _sensors.Count; i++)
        {
            if (_sensors[i] != null)
            {
                _sensors[i].transform.position = TransformarPosicao(_sensorPositions[i]);
                _sensors[i].transform.rotation = TransformarRotacao(_sensorOrientations[i]);
            }
        }

        // ── Debug periódico na consola ────────────────────────────────
        if (DebugIntervalSegundos <= 0f || _sensorPositions == null) return;
        _debugTimer += Time.deltaTime;
        if (_debugTimer < DebugIntervalSegundos) return;
        _debugTimer = 0f;

        var sb = new System.Text.StringBuilder();
        sb.Append($"[OmmoDevice] {gameObject.name} | {_sensorPositions.Length} sensor(es)\n");
        for (int i = 0; i < _sensorPositions.Length; i++)
        {
            Vector3  pos = TransformarPosicao(_sensorPositions[i]);
            Vector3  posCM = _sensorPositions[i] * _unityScaleInCM; // local (Ommo) em cm para facilitar leitura
            Quaternion rot = _sensorOrientations[i];
            sb.Append($"  S{i} | pos Unity: ({pos.x:F3}, {pos.y:F3}, {pos.z:F3})" +
                      $"  cm: ({posCM.x:F1}, {posCM.y:F1}, {posCM.z:F1})" +
                      $"  rot: ({rot.x:F2}, {rot.y:F2}, {rot.z:F2}, {rot.w:F2})\n");
        }
        Debug.Log(sb.ToString());
    }

    public void SetUnityScaleInCM(float unityScaleInCM)
    {
        _unityScaleInCM = unityScaleInCM;
    }

    public void SetClient(Ommo.Client client)
    {
        _client = client;
    }

    /// <summary>
    /// Define o referencial do espaço Ommo (transform do BaseStation/OmmoRoot).
    /// As posições/rotações dos sensores passam a ser expressas neste referencial —
    /// mover/rodar o referencial (ex.: ancorar no QR) move todo o espaço Ommo.
    /// </summary>
    public void DefinirReferencial(Transform referencial)
    {
        _referencial = referencial;
    }

    private Vector3 TransformarPosicao(Vector3 local)
        => _referencial != null ? _referencial.TransformPoint(local) : local;

    private Quaternion TransformarRotacao(Quaternion local)
        => _referencial != null ? _referencial.rotation * local : local;

    public void SetDeviceDescriptor(Ommo.DeviceDescriptor device)
    {
        _device = device;

        int sensorCount = _device.SensorUnitDescriptors.Count;
        Debug.Log("SetDeviceDescriptor - sensorCount " + sensorCount);

        // O clone do template traz um CuboSensor HERDADO que nunca é movido
        // (ficava parado na cena) — limpa os filhos antes de criar os sensores.
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        for (int i = 0; i < sensorCount; i++)
        {
            Vector3 posInicial = _referencial != null ? _referencial.position : Vector3.zero;
            _sensors.Add(Instantiate(SensorPrefab, posInicial, Quaternion.identity, gameObject.transform));
        }

        _sensorPositions    = new Vector3[sensorCount];
        _sensorOrientations = new Quaternion[sensorCount];

        // Inicializa um filtro por sensor
        _filtros = new OmmoSensorFilter[sensorCount];
        for (int i = 0; i < sensorCount; i++)
            _filtros[i] = new OmmoSensorFilter();

        _siuUuid = _device.SiuUuid;
        _portId = _device.PortId;
        StartDataStream(_device.SiuUuid, _device.PortId);
        Debug.Log("Starting Data Stream" + _device.SiuUuid + ":" + _device.PortId);

    }

    private void OnDestroy()
    {
        Debug.Log("Device destroyed");
        _source.Cancel();
        try 
        {
            _dataTask?.Wait(1000); // Wait up to 1 second for clean shutdown
        }
        catch (OperationCanceledException) { }
        _source.Dispose();
    }

    private void StartDataStream(uint uuid, uint portId)
    {
        Debug.Log("Starting Data Stream" + _siuUuid + ":" + _portId);
        // Create a request message to get the data we want
        Ommo.TrackingDeviceDataStreamRequest request = new Ommo.TrackingDeviceDataStreamRequest
        {
            // Header field to include UUID, PortId and DeviceType to identify the device
            FieldMask = Ommo.RawDataFieldMask.OMMO_SIU_UUID | Ommo.RawDataFieldMask.OMMO_PORT_ID | Ommo.RawDataFieldMask.OMMO_DEVICE_TYPE,
            SiuUuid = uuid,
            PortId = portId,
            // Report as soon as data becomes available
            ReportInterval = 0,
            // Buffer 10 samples before overwriting if client is too slow to process
            BufferDepth = 10,
            // Use Default fusion mode (highest available level)
            RequestedFusionMode = RequestedMode
        };

        _dataTask = Task.Factory.StartNew(async () => {
            try 
            {
                await _client.StreamTrackingDeviceData(request, _token, ProcessTrackingDeviceData);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Data stream cancelled");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in data stream: {ex.Message}");
            }
            //_client.StreamTrackingDeviceData(request, _token, ProcessTrackingDeviceData).Wait(_token);
        }, TaskCreationOptions.LongRunning);
    }


    // ── Acessores públicos para scripts do jogo ───────────────────────

    /// <summary>Número de sensores neste dispositivo (0 antes de SetDeviceDescriptor).</summary>
    public int NumeroSensores => _sensors.Count;

    /// <summary>
    /// Posição do sensor no espaço mundo Unity (através do referencial do BaseStation).
    /// Devolve Vector3.zero se o índice for inválido ou não houver dados ainda.
    /// </summary>
    public Vector3 ObterPosicaoSensor(int indice)
    {
        if (_sensorPositions == null || indice < 0 || indice >= _sensorPositions.Length)
            return Vector3.zero;
        return TransformarPosicao(_sensorPositions[indice]);
    }

    /// <summary>
    /// Rotação do sensor no espaço mundo Unity (através do referencial do BaseStation).
    /// Devolve Quaternion.identity se o índice for inválido.
    /// </summary>
    public Quaternion ObterRotacaoSensor(int indice)
    {
        if (_sensorOrientations == null || indice < 0 || indice >= _sensorOrientations.Length)
            return Quaternion.identity;
        return TransformarRotacao(_sensorOrientations[indice]);
    }

    /// <summary>Transform do GameObject filho que representa o sensor (para referência visual).</summary>
    public Transform ObterTransformSensor(int indice)
    {
        if (indice < 0 || indice >= _sensors.Count || _sensors[indice] == null)
            return null;
        return _sensors[indice].transform;
    }

    // ─────────────────────────────────────────────────────────────────

    public void ProcessTrackingDeviceData(Ommo.TrackingDeviceData mes)
    {
        // Log único na primeira chegada de dados — confirma que o stream gRPC está ativo
        if (!_primeirosDadosRecebidos)
        {
            _primeirosDadosRecebidos = true;
            string nome = _nomeCache ?? "OmmoDevice"; // _nomeCache é safe em background threads
            UnityMainThreadDispatcher.Enqueue(() =>
                Debug.Log($"[OmmoDevice] ✅ {nome} — primeiros dados recebidos! " +
                          $"Posições={mes.Positions.Count} Quaterniões={mes.Quaternions.Count}"));
        }
        lock (this)
        {
            //Debug.Log("ProcessTrackingDeviceData - lock");
            // Check both sensor positions and ensure we don't exceed the # of sensors on the device
            for (int i = 0; i < _sensorPositions.Length && i < mes.Positions.Count; i++)
            {
                //Debug.Log("ProcessTrackingDeviceData - for " + i);
                // Converte eixos (Ommo → Unity) e normaliza para Unity units
                Vector3 rawPos = new Vector3(
                    mes.Positions[i].X,
                    mes.Positions[i].Z,
                    mes.Positions[i].Y) / _unityScaleInCM;

                // Filtra: descarta (0,0,0), throttle, jump filter, lag 1 amostra
                if (_filtros != null && i < _filtros.Length)
                {
                    if (_filtros[i].TentarAtualizar(rawPos))
                        _sensorPositions[i] = _filtros[i].PosicaoFiltrada;
                    // Se false → _sensorPositions[i] mantém o último valor válido
                }
                else
                {
                    // Fallback sem filtro (caso _filtros não esteja inicializado)
                    _sensorPositions[i] = rawPos;
                }

                // Simple conversion into Unity rotation
                // Quaternion from service is to rotate from sensor frame into base station frame
                // So we take the inverse as Unity frame is the Base station frame (with Y and Z axis swapped)
                // We swap Y and Z values again to match Ommo Base station coordinate frame to Unity coordinate frame
                _sensorOrientations[i] = Quaternion.Inverse(new Quaternion(mes.Quaternions[i].X, mes.Quaternions[i].Z, mes.Quaternions[i].Y, mes.Quaternions[i].W));
            }
        }
    }
}
