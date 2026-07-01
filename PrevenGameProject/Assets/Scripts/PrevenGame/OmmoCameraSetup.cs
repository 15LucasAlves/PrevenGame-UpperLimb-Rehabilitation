using UnityEngine;

/// <summary>
/// Posiciona a câmara principal relativamente ao OMBRO calibrado, lateral e acima
/// (aproximadamente no sítio da cabeça do paciente) para uma boa visualização do exercício.
///
/// O ombro só é conhecido após a calibração, por isso a câmara é reposicionada UMA vez
/// quando <see cref="OmmoEsqueletoJogador.Calibrado"/> fica true. Antes disso mantém a pose
/// que tiver na cena. Os offsets são em unidades Unity (1 u = 10 cm) e tunáveis no Inspector.
/// </summary>
public class OmmoCameraSetup : MonoBehaviour
{
    [Header("Offsets relativos ao ombro (unidades Unity)")]
    [Tooltip("Altura acima do ombro (≈ altura da cabeça).")]
    public float Altura = 3f;
    [Tooltip("Deslocamento lateral (Cross(up, frente)); inverte o sinal para o outro lado.")]
    public float Lateral = 3f;
    [Tooltip("Recuo para trás do paciente (ao longo de -DirecaoFrente).")]
    public float Recuo = 0.5f;

    [Header("Alvo do olhar (relativo ao ombro)")]
    [Tooltip("Quão à frente do ombro a câmara olha (zona do braço).")]
    public float OlharFrente = 2f;
    [Tooltip("Ajuste vertical do alvo do olhar.")]
    public float OlharCima = -0.5f;

    private OmmoEsqueletoJogador _esqueleto;
    private bool _posicionada;

    void Start()
    {
        _esqueleto = FindObjectOfType<OmmoEsqueletoJogador>();
    }

    void Update()
    {
        if (_posicionada || _esqueleto == null || !_esqueleto.Calibrado) return;

        Vector3 posOmbro = _esqueleto.PosOmbroBase;
        Vector3 frente   = _esqueleto.DirecaoFrente;
        if (frente == Vector3.zero) frente = Vector3.forward;
        Vector3 lateral  = Vector3.Cross(Vector3.up, frente).normalized;

        transform.position = posOmbro
                           + Vector3.up * Altura
                           + lateral    * Lateral
                           - frente     * Recuo;
        transform.LookAt(posOmbro + frente * OlharFrente + Vector3.up * OlharCima);

        _posicionada = true;
    }
}
