using UnityEngine;

/// <summary>
/// OmmoSensorFilter — Filtra e estabiliza a posição de um sensor Ommo.
///
/// Aplica quatro camadas de proteção contra dados erráticos do SIU:
///
///   1. Descarta (0,0,0) — o firmware envia este valor em caso de falha;
///      o sensor nunca está fisicamente na origem da BaseStation.
///
///   2. Throttle — aceita no máximo AmostrasPorSegundo amostras/s;
///      leituras em excesso são ignoradas (reduz carga de processamento).
///
///   3. Valida saltos — descarta posições cujo deslocamento face à
///      amostra anterior excede MaxVelocidade / AmostrasPorSegundo;
///      elimina teletransportes impossíveis causados por erros pontuais.
///
///   4. Lag de 1 amostra — devolve sempre a amostra ANTERIOR à última
///      aceite; um spike de 1 frame nunca chega ao consumidor porque,
///      quando o spike seria retornado, já está confirmado como inválido
///      pela leitura seguinte (que restabeleceu a posição normal).
///
/// Não é um MonoBehaviour — instanciado diretamente por OmmoDevice.
/// Thread-safe para leituras: PosicaoFiltrada é lida pelo main thread
/// enquanto TentarAtualizar é chamado pelo thread gRPC; os campos são
/// do tipo valor (Vector3, bool, long) o que garante atomicidade em x86.
/// </summary>
public class OmmoSensorFilter
{
    // ── Configuração ──────────────────────────────────────────────────

    /// <summary>
    /// Velocidade máxima esperada do sensor em Unity units/s.
    /// 1 Unity unit = 10 cm. Predefinição: 20.0 u/s ≈ 200 cm/s ≈ 2 m/s
    /// (cobre movimentos rápidos de braço em reabilitação).
    /// </summary>
    public float MaxVelocidade = 20.0f;

    /// <summary>
    /// Taxa de aceitação alvo (amostras/segundo).
    /// Leituras que cheguem mais rapidamente são descartadas.
    /// Predefinição: 20 sps (~50 ms por amostra).
    /// </summary>
    public int AmostrasPorSegundo = 20;

    // ── Estado interno ────────────────────────────────────────────────

    private Vector3 _posConfirmada;       // posição devolvida ao consumidor
    private Vector3 _posPendente;         // última leitura aceite (ainda não promovida)
    private bool    _inicializado = false;
    private long    _ultimaAmostraTicks = 0L;

    // Mecanismo de escape: se o jump filter rejeitar N amostras consecutivas,
    // reseta a âncora — impede travamento permanente por leitura inicial errada.
    private int _rejeicoesConsecutivas = 0;
    private const int _limiteRejeicoes = 4;

    // ── Propriedades públicas ─────────────────────────────────────────

    /// <summary>
    /// Posição filtrada mais recente.
    /// Corresponde à penúltima leitura aceite (lag de 1 amostra).
    /// Devolve Vector3.zero enquanto não chegou ainda nenhuma leitura válida.
    /// </summary>
    public Vector3 PosicaoFiltrada => _posConfirmada;

    /// <summary>True após a primeira leitura válida ter sido processada.</summary>
    public bool Inicializado => _inicializado;

    // ── Lógica de filtragem ───────────────────────────────────────────

    /// <summary>
    /// Tenta integrar uma nova leitura do sensor.
    /// Deve ser chamado a partir do thread do stream gRPC.
    /// </summary>
    /// <param name="novaPosicao">Posição em espaço relativo (sem _basePosition).</param>
    /// <returns>
    /// True se PosicaoFiltrada foi atualizado (nova amostra aceite e promovida).
    /// False se a leitura foi descartada (PosicaoFiltrada permanece inalterado).
    /// </returns>
    public bool TentarAtualizar(Vector3 novaPosicao)
    {
        // ── 1. Descarta (0,0,0) ──────────────────────────────────────
        // sqrMagnitude evita raiz quadrada; limiar 1e-6 ≈ 0.001 cm
        if (novaPosicao.sqrMagnitude < 1e-6f)
            return false;

        // ── 2. Throttle ───────────────────────────────────────────────
        long agora        = System.DateTime.UtcNow.Ticks;
        long intervalo    = System.TimeSpan.TicksPerSecond / AmostrasPorSegundo;
        if (_inicializado && (agora - _ultimaAmostraTicks) < intervalo)
            return false;

        _ultimaAmostraTicks = agora;

        // ── 3. Primeira leitura válida ─────────────────────────────────
        if (!_inicializado)
        {
            _posConfirmada = novaPosicao;
            _posPendente   = novaPosicao;
            _inicializado  = true;
            return true;
        }

        // ── 4. Valida salto ───────────────────────────────────────────
        // Distância máxima admissível entre duas amostras consecutivas
        float maxSalto = MaxVelocidade / AmostrasPorSegundo;
        if (Vector3.Distance(novaPosicao, _posPendente) > maxSalto)
        {
            _rejeicoesConsecutivas++;
            if (_rejeicoesConsecutivas >= _limiteRejeicoes)
            {
                // Escape: âncora travada por leitura inicial errada —
                // reseta _posPendente para a posição atual e recomeça.
                _posPendente           = novaPosicao;
                _rejeicoesConsecutivas = 0;
            }
            return false; // descarta (mesmo após reset, aguarda próxima amostra)
        }
        _rejeicoesConsecutivas = 0;

        // ── 5. Promove (lag de 1 amostra) ─────────────────────────────
        // Ao devolver _posConfirmada (a anterior), um spike de exatamente
        // 1 amostra nunca será retornado: quando seria promovido a
        // _posConfirmada, a leitura seguinte já o descartou como inválido.
        _posConfirmada = _posPendente;
        _posPendente   = novaPosicao;
        return true;
    }

    /// <summary>Repõe o filtro ao estado inicial (útil após reconexão do sensor).</summary>
    public void Reset()
    {
        _inicializado          = false;
        _posConfirmada         = Vector3.zero;
        _posPendente           = Vector3.zero;
        _ultimaAmostraTicks    = 0L;
        _rejeicoesConsecutivas = 0;
    }
}
