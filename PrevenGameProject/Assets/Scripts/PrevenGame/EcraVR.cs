using UnityEngine;

/// <summary>
/// EcraVR — "Ecrã" world-space móvel onde vive a UI do paciente em VR que não é
/// head-locked: diálogo dos helpers (calibração, troca de braço), tabela de score
/// do minijogo e aviso de pausa. (A UI head-locked — demo do exercício + contador
/// de reps — vive no <see cref="HudVR"/>, presa à cabeça.)
///
/// Posicionamento: <see cref="Recentrar"/> coloca o ecrã a <see cref="Distancia"/>
/// metros à frente da cabeça, à altura dos olhos, só com yaw (sem inclinação).
/// Recentra automaticamente ao mostrar e com a tecla R (fisioterapeuta no PC).
/// Sem grab/controladores — o paciente tem o sensor Ommo na mão.
/// </summary>
public class EcraVR : MonoBehaviour
{
    [Tooltip("Diálogo dos helpers hospedado neste ecrã (instância própria, world-space).")]
    public HelperDialogueManager Dialogo;

    [Tooltip("Painel com o aviso 'Jogo em pausa' (espelho do PauseMenu do operador).")]
    public GameObject PainelPausa;

    [Tooltip("Texto da tabela de score do minijogo (preenchido pelo GestorMinijogo).")]
    public TMPro.TextMeshProUGUI TextoTabela;

    [Tooltip("Painel que contém a tabela de score.")]
    public GameObject PainelTabela;

    [Header("Posicionamento")]
    [Tooltip("Preso ao viewport: segue a câmara todos os frames (head-locked), como o HudVR. " +
             "Desligado, fica fixo no mundo e recentra-se com R.")]
    public bool SeguirCabeca = true;
    [Tooltip("Distância da cabeça ao ecrã (metros).")]
    public float Distancia = 1.4f;
    [Tooltip("Desvio vertical do centro do canvas face ao centro do olhar (metros; local à cabeça).")]
    public float DesvioVertical = 0f;
    [Tooltip("Escala do conteúdo (1 = tamanho original ~1.9 m de largura). Mais pequeno preso à cara.")]
    public float EscalaConteudo = 0.75f;
    [Tooltip("Altura mínima da cabeça (m) para a pose contar como válida — no primeiro " +
             "frame após criar o rig a cabeça ainda está em (0,0,0).")]
    public float AlturaCabecaMinima = 0.3f;

    void Awake()
    {
        // MULTIPLICA a escala existente (o builder serializa 0.001 no root para
        // converter os px do canvas em metros) — substituí-la tornaria o canvas
        // gigantesco e os helpers "invisíveis" por estarem a centenas de metros.
        transform.localScale *= EscalaConteudo;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) Recentrar();
    }

    void LateUpdate()
    {
        if (SeguirCabeca) SeguirCamera();
    }

    /// <summary>Cola o ecrã ao viewport (posição E rotação da cabeça, com offset local).</summary>
    void SeguirCamera()
    {
        var xr = GestorXR.Instancia;
        var cabeca = xr != null ? xr.Cabeca : null;
        if (cabeca == null) return;

        transform.SetPositionAndRotation(
            cabeca.TransformPoint(new Vector3(0f, DesvioVertical, Distancia)),
            cabeca.rotation);
    }

    /// <summary>Mostra/esconde o ecrã; posiciona-o logo à frente da cabeça.</summary>
    public void Mostrar(bool ativo)
    {
        gameObject.SetActive(ativo);
        if (!ativo) return;

        if (SeguirCabeca) SeguirCamera();
        else
        {
            // Modo fixo no mundo: o tracking pode ainda não ter dado a primeira pose
            // (cabeça em 0,0,0 logo após criar o rig) — recentra já e repete até a
            // pose ser válida, senão o ecrã fica ao nível do chão.
            Recentrar();
            StartCoroutine(RecentrarQuandoValido());
        }
    }

    System.Collections.IEnumerator RecentrarQuandoValido()
    {
        var xr = GestorXR.Instancia;
        float timeout = Time.unscaledTime + 5f;
        while (gameObject.activeSelf && Time.unscaledTime < timeout)
        {
            var cabeca = xr != null ? xr.Cabeca : null;
            if (cabeca != null && cabeca.position.y >= AlturaCabecaMinima)
            {
                Recentrar();
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>Mostra/esconde o aviso de pausa (chamado pelo PauseMenu do operador).</summary>
    public void MostrarPausa(bool ativo)
    {
        if (PainelPausa != null) PainelPausa.SetActive(ativo);
    }

    /// <summary>Coloca o ecrã à frente da cabeça do jogador (yaw-only).</summary>
    public void Recentrar()
    {
        var xr = GestorXR.Instancia;
        var cabeca = xr != null ? xr.Cabeca : null;
        if (cabeca == null) return;

        Vector3 frente = cabeca.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
        frente.Normalize();

        transform.position = cabeca.position + frente * Distancia + Vector3.up * DesvioVertical;
        transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
    }
}
