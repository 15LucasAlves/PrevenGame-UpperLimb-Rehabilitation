using UnityEngine;

/// <summary>
/// MaoJogador — O único objeto do jogador nas cenas de jogo: segue a pose do
/// sensor Ommo (na palma da mão). Substitui o antigo esqueleto visível
/// (<c>OmmoEsqueletoJogador</c>), que deixou de ser usado nas cenas.
///
/// Em <see cref="SessionManager.ModoComando"/> (F3 na calibração — testes sem o
/// hardware Ommo), segue em vez disso o hand anchor do comando do Quest
/// (<see cref="DefinirMaoComando"/> escolhe o lado; o OVRCameraRig atualiza os
/// anchors sozinho).
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

    [Tooltip("Em Modo Comando: usar o comando direito (true) ou esquerdo (false).")]
    public bool MaoDireita = true;

    /// <summary>Dispositivo atualmente seguido (null enquanto nenhum sensor liga).</summary>
    public OmmoDevice Device { get; private set; }

    /// <summary>Em Modo Comando escolhe o lado do comando (chamado por calibração/minijogo por bloco).</summary>
    public void DefinirMaoComando(bool direita) => MaoDireita = direita;

    static bool ModoComando =>
        SessionManager.Instancia != null && SessionManager.Instancia.ModoComando;

    /// <summary>Anchor do comando ativo (null se o VR não está pronto).</summary>
    Transform AnchorComando
    {
        get
        {
            var xr = GestorXR.Instancia;
            if (xr == null || xr.Rig == null) return null;
            return MaoDireita ? xr.Rig.rightHandAnchor : xr.Rig.leftHandAnchor;
        }
    }

    /// <summary>True quando há dados a chegar (sensor Ommo ou comando em Modo Comando).</summary>
    public bool Ativa => ModoComando
        ? AnchorComando != null && ComandoLigado
        : Device != null && Device.NumeroSensores > IndiceSensor;

    /// <summary>O comando do lado ativo está ligado/tracked? (Comando adormecido daria posições nulas.)</summary>
    bool ComandoLigado =>
        OVRInput.IsControllerConnected(MaoDireita ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);

    /// <summary>Posição mundo atual da mão (zero se sem fonte).</summary>
    public Vector3 Posicao
    {
        get
        {
            if (ModoComando) { var a = AnchorComando; return a != null ? a.position : Vector3.zero; }
            return Ativa ? Device.ObterPosicaoSensor(IndiceSensor) : Vector3.zero;
        }
    }

    /// <summary>Rotação mundo atual da mão.</summary>
    public Quaternion Rotacao
    {
        get
        {
            if (ModoComando) { var a = AnchorComando; return a != null ? a.rotation : Quaternion.identity; }
            return Ativa ? Device.ObterRotacaoSensor(IndiceSensor) : Quaternion.identity;
        }
    }

    void Start()
    {
        // Sem visual por defeito: o jogador não precisa de feedback de objetos
        // (na calibração sente o comando/sensor; no minijogo o visual é o dardo).
        // Um visual só aparece se for atribuído no Inspector.
        if (Visual != null)
        {
            Visual.transform.SetParent(transform, false);
            Visual.SetActive(false);
        }
    }

    void Update()
    {
        // Em Modo Comando a fonte é o hand anchor do rig — NÃO depende de haver
        // OmmoDevice (sem este guard, o transform nunca seguia o comando e o
        // dardo ficava pendurado na origem).
        if (!ModoComando && Device == null)
        {
            // Liga-se ao primeiro OmmoDevice que aparecer (recriado a cada cena).
            Device = FindObjectOfType<OmmoDevice>();
            if (Device == null) return;
        }

        // Os cubos dos sensores são escondidos na fonte (OmmoDevice) — o visual
        // do jogador é só o campo Visual (GRASP) ou o dardo do minijogo.
        bool ativa = Ativa;
        if (Visual != null && Visual.activeSelf != ativa) Visual.SetActive(ativa);
        if (!ativa) return;

        transform.SetPositionAndRotation(Posicao, Rotacao);
    }
}
