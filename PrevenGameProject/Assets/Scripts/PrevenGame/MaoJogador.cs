using UnityEngine;

/// <summary>
/// MaoJogador — O único objeto do jogador nas cenas de jogo: segue a pose do
/// sensor Ommo (na palma da mão). Substitui o antigo esqueleto visível
/// (<c>OmmoEsqueletoJogador</c>), que deixou de ser usado nas cenas.
///
/// Se não houver um visual atribuído, cria uma esfera simples. O visual pode
/// ser trocado por um modelo de mão/dardo pelo minijogo.
/// </summary>
public class MaoJogador : MonoBehaviour
{
    [Tooltip("Visual da mão (opcional — sem nada, cria uma esfera de 4 cm).")]
    public GameObject Visual;

    [Tooltip("Índice do sensor no dispositivo (0 = primeiro/único sensor).")]
    public int IndiceSensor = 0;

    /// <summary>Dispositivo atualmente seguido (null enquanto nenhum sensor liga).</summary>
    public OmmoDevice Device { get; private set; }

    /// <summary>True quando há dados de sensor a chegar.</summary>
    public bool Ativa => Device != null && Device.NumeroSensores > IndiceSensor;

    /// <summary>Posição mundo atual da mão (zero se sem sensor).</summary>
    public Vector3 Posicao => Ativa ? Device.ObterPosicaoSensor(IndiceSensor) : Vector3.zero;

    /// <summary>Rotação mundo atual da mão.</summary>
    public Quaternion Rotacao => Ativa ? Device.ObterRotacaoSensor(IndiceSensor) : Quaternion.identity;

    void Start()
    {
        if (Visual == null)
        {
            Visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Visual.name = "VisualMao";
            Visual.transform.localScale = Vector3.one * 0.04f;
            Destroy(Visual.GetComponent<Collider>());
            Visual.GetComponent<Renderer>().material.color = new Color(0.3f, 0.7f, 1f);
        }
        Visual.transform.SetParent(transform, false);
        Visual.SetActive(false);
    }

    void Update()
    {
        // Liga-se ao primeiro OmmoDevice que aparecer (recriado a cada cena).
        if (Device == null)
        {
            Device = FindObjectOfType<OmmoDevice>();
            if (Device == null) return;
        }

        bool ativa = Ativa;
        if (Visual != null && Visual.activeSelf != ativa) Visual.SetActive(ativa);
        if (!ativa) return;

        transform.SetPositionAndRotation(Posicao, Rotacao);
    }
}
