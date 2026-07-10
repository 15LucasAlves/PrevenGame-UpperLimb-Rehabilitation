using UnityEngine;

/// <summary>
/// MinigameController — Coordenador de uma cena de minijogo.
///
/// Lê o minijogo atual do <see cref="SessionManager"/>, reidrata o esqueleto a partir da
/// calibração persistida (sem recalibrar) quando o sensor liga, arranca o
/// <see cref="GamificationManager"/> e, ao terminar, regista o score e carrega o próximo.
/// </summary>
public class MinigameController : MonoBehaviour
{
    [Header("Referências")]
    public GamificationManager  Jogo;
    public OmmoEsqueletoJogador Esqueleto;
    public OmmoSensorManager    SensorManager;

    private bool _iniciado;

    void Start()
    {
        if (Jogo == null)          Jogo          = FindObjectOfType<GamificationManager>();
        if (Esqueleto == null)     Esqueleto     = FindObjectOfType<OmmoEsqueletoJogador>();
        if (SensorManager == null) SensorManager = FindObjectOfType<OmmoSensorManager>();

        if (Jogo != null) Jogo.OnConcluido += AoMinijogoConcluido;
    }

    void OnDestroy()
    {
        if (Jogo != null) Jogo.OnConcluido -= AoMinijogoConcluido;
    }

    void Update()
    {
        if (_iniciado) return;

        // Espera o sensor da palma ligar (re-bootstrap da cena) antes de reidratar o esqueleto.
        var device = FindObjectOfType<OmmoDevice>();
        if (device == null || device.NumeroSensores == 0) return;

        ReidratarEsqueleto(device);
        ArrancarMinijogo();
        _iniciado = true;
    }

    void ReidratarEsqueleto(OmmoDevice device)
    {
        if (Esqueleto == null) return;
        var sm = SessionManager.Instancia;

        Esqueleto.Inicializar(device);
        if (sm != null && sm.Calibrado)
            Esqueleto.AplicarCalibracao(sm.PosOmbro, sm.ComprimentoBraco, sm.DirecaoFrente);
        Esqueleto.AtivacaoEsqueleto(true);
    }

    void ArrancarMinijogo()
    {
        if (Jogo == null) return;
        var sm = SessionManager.Instancia;
        if (sm != null && sm.TemAtual)
        {
            var m = sm.Atual;
            Jogo.StartMinijogo(m.Tipo, m.RepsL, m.RepsR);
        }
        else
        {
            // Sem sessão (ex.: correr a cena isolada para testes) — 1 rep por braço.
            Jogo.StartMinijogo(ExerciciosWaypoints.TipoExercicio.FlexaoBraco, 1, 1);
        }
    }

    void AoMinijogoConcluido(float pctMedia)
    {
        var sm = SessionManager.Instancia;
        if (sm != null)
        {
            sm.RegistarScore(pctMedia);
            sm.CarregarProximo();
        }
    }
}
