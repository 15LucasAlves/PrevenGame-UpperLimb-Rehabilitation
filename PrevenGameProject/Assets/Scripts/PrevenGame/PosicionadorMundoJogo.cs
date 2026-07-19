using System.Collections;
using UnityEngine;

/// <summary>
/// PosicionadorMundoJogo — Constrói o mundo do minijogo À VOLTA do jogador.
///
/// O jogador está FISICAMENTE ancorado ao Ommo/QR — o rig VR e a BaseStation
/// nunca se movem do mundo real. Em vez de teleportar o jogador para a cena,
/// é a RAIZ do mundo (o FBX do bar inteiro) que é rodada e transladada para
/// que o "aro 1" fique centrado em frente à cara do jogador, à distância de
/// lançamento, com o alvo virado para ele — o alinhamento fica PARALELO à
/// direção dos óculos.
///
/// A orientação vem da relação AUTORAL da cena: a direção da câmara do FBX
/// (<see cref="MarcadorVista"/>) para o aro 1 é, por construção, a vista
/// correta do alvo — o mundo roda até essa direção coincidir com o forward
/// horizontal da cabeça. (O forward do próprio aro não é fiável — os eixos do
/// Blender chegam rodados.)
///
/// Sem tocar no rig, na BaseStation nem no <see cref="AlinhadorOmmoQr"/> —
/// o alinhamento QR continua ativo e a mão Ommo continua colada ao jogador.
/// </summary>
public class PosicionadorMundoJogo : MonoBehaviour
{
    [Tooltip("Raiz do mundo a posicionar (o root do FBX do bar). Vazio = topmost parent do \"aro 1\".")]
    public Transform RaizMundo;

    [Tooltip("Aro central do alvo (\"aro 1\") — fica em frente à cara do jogador. Vazio = auto por nome.")]
    public Transform AroCentral;

    [Tooltip("Câmara importada do FBX (desativada) — a direção dela para o aro 1 é a vista autoral " +
             "correta do alvo. Sem marcador, usa-se o forward do aro (menos fiável).")]
    public Transform MarcadorVista;

    [Tooltip("Distância (m) da cabeça ao alvo — postura de lançamento de dardos (oche ≈ 2.37 m).")]
    public float DistanciaLancamento = 2.37f;

    [Tooltip("Altura do centro do alvo relativa à cabeça (m). 0 = ao nível dos olhos.")]
    public float AlturaRelativaCabeca = 0f;

    [Tooltip("Só sem MarcadorVista: inverter a normal do alvo (se o forward do \"aro 1\" apontar para dentro).")]
    public bool InverterNormal = false;

    [Tooltip("FALLBACK sem alvo: ponto de spawn do jogador no mundo (marcador/câmara do FBX) — o mundo " +
             "é rodado até o forward do spawn coincidir com o dos óculos e transladado para o spawn " +
             "cair na cabeça. Vazio = usa o MarcadorVista.")]
    public Transform PontoSpawn;

    [Tooltip("Spawner colocado À MÃO na cena (componente PontoSpawnJogador) — quando existe tem " +
             "PRIORIDADE sobre o alvo. Vazio = auto-find na cena.")]
    public PontoSpawnJogador Spawner;

    [Tooltip("Tecla (no teclado do PC) para reposicionar o mundo à pose ATUAL dos óculos — " +
             "útil quando a cena arrancou com o headset pousado.")]
    public KeyCode TeclaReposicionar = KeyCode.F4;

    IEnumerator Start()
    {
        if (Spawner == null) Spawner = FindObjectOfType<PontoSpawnJogador>();
        if (AroCentral == null)
        {
            var aro1 = GameObject.Find("aro 1");
            if (aro1 != null) AroCentral = aro1.transform;
        }

        // Referência de ancoragem: spawner manual (prioridade), senão alvo,
        // senão spawn/marcador (fallback), senão nada a fazer.
        Transform referencia = Spawner != null ? Spawner.transform
                             : AroCentral != null ? AroCentral
                             : PontoSpawn != null ? PontoSpawn
                             : MarcadorVista;
        if (referencia == null)
        {
            Debug.LogWarning("[PosicionadorMundo] Sem spawner, alvo nem ponto de spawn — mundo fica como está " +
                             "(coloca um PontoSpawnJogador na cena ou atribui AroCentral no Inspector).");
            yield break;
        }
        if (RaizMundo == null)
        {
            RaizMundo = referencia;
            while (RaizMundo.parent != null) RaizMundo = RaizMundo.parent;
        }

        var xr = GestorXR.ObterOuCriar();

        // Espera pelo VR + primeira pose válida da cabeça (headset posto).
        float timeout = Time.unscaledTime + 15f;
        while (Time.unscaledTime < timeout &&
               (xr.Cabeca == null || xr.Cabeca.position.y < 0.3f))
            yield return null;
        if (xr.Cabeca == null || xr.Cabeca.position.y < 0.3f)
        {
            Debug.LogWarning("[PosicionadorMundo] Cabeça sem tracking válido — mundo fica como está (headset posto?).");
            yield break;
        }

        Posicionar();
    }

    void Update()
    {
        if (TeclaReposicionar != KeyCode.None && Input.GetKeyDown(TeclaReposicionar))
            Posicionar();
    }

    /// <summary>Roda+translada a raiz do mundo para a pose ATUAL dos óculos.</summary>
    public void Posicionar()
    {
        var xr = GestorXR.Instancia;
        if (xr == null || xr.Cabeca == null || RaizMundo == null) return;

        Vector3 cabeca = xr.Cabeca.position;
        Vector3 frente = xr.Cabeca.forward;
        frente.y = 0f;
        frente = frente.sqrMagnitude > 0.0001f ? frente.normalized : Vector3.forward;

        // SPAWNER manual na cena: prioridade sobre o alvo — o utilizador marcou
        // exatamente onde o jogador fica e para onde olha.
        if (Spawner != null)
        {
            PosicionarPorSpawn(Spawner.transform, Spawner.NoChao, cabeca, frente);
            return;
        }

        // FALLBACK sem alvo: alinhar o SPAWN POINT ao face-forward dos óculos.
        if (AroCentral == null)
        {
            Transform spawn = PontoSpawn != null ? PontoSpawn : MarcadorVista;
            if (spawn == null)
            {
                Debug.LogWarning("[PosicionadorMundo] Sem alvo nem ponto de spawn — mundo fica como está.");
                return;
            }
            PosicionarPorSpawn(spawn, noChao: false, cabeca, frente); // câmara do FBX = altura dos olhos
            return;
        }

        // Onde o aro 1 deve ficar: em frente à cara, à distância de lançamento.
        Vector3 alvo = cabeca + frente * DistanciaLancamento;
        alvo.y = cabeca.y + AlturaRelativaCabeca;

        // Direção autoral de vista do alvo: marcador (câmara do FBX) → aro 1.
        // Fallback sem marcador: a normal do aro a apontar ao jogador.
        Vector3 dirAutoral = MarcadorVista != null
            ? AroCentral.position - MarcadorVista.position
            : (InverterNormal ? -AroCentral.forward : AroCentral.forward) * -1f;
        dirAutoral.y = 0f;

        // 1) Roda a raiz (yaw puro) à volta do aro 1 até a vista autoral ficar
        //    paralela ao forward dos óculos.
        Vector3 pivot = AroCentral.position;
        if (dirAutoral.sqrMagnitude > 0.0001f)
        {
            Quaternion deltaRot = Quaternion.FromToRotation(dirAutoral.normalized, frente);
            RaizMundo.rotation = deltaRot * RaizMundo.rotation;
            RaizMundo.position = pivot + deltaRot * (RaizMundo.position - pivot);
        }

        // 2) Translada a raiz para o aro 1 cair no ponto alvo.
        RaizMundo.position += alvo - AroCentral.position;

        Debug.Log($"[PosicionadorMundo] Mundo \"{RaizMundo.name}\" alinhado com os óculos: " +
                  $"aro 1 em ({alvo.x:F2}, {alvo.y:F2}, {alvo.z:F2}), a {DistanciaLancamento:F2} m da cabeça " +
                  $"(vista autoral={(MarcadorVista != null ? MarcadorVista.name : "forward do aro")}; " +
                  $"{TeclaReposicionar} reposiciona).");
    }

    /// <summary>
    /// Alinha o mundo pelo ponto de spawn: roda (yaw) até o forward do spawn
    /// ficar paralelo ao face-forward dos óculos e translada para o spawn cair
    /// no jogador — na CABEÇA (noChao=false, spawn à altura dos olhos) ou nos
    /// PÉS (noChao=true, spawn no chão: x/z da cabeça, y do chão do tracking).
    /// </summary>
    void PosicionarPorSpawn(Transform spawn, bool noChao, Vector3 cabeca, Vector3 frente)
    {
        Vector3 frenteSpawn = spawn.forward;
        frenteSpawn.y = 0f;

        // 1) Roda a raiz (yaw puro) à volta do spawn até o forward dele
        //    coincidir com o face-forward dos óculos.
        Vector3 pivot = spawn.position;
        if (frenteSpawn.sqrMagnitude > 0.0001f)
        {
            Quaternion deltaRot = Quaternion.FromToRotation(frenteSpawn.normalized, frente);
            RaizMundo.rotation = deltaRot * RaizMundo.rotation;
            RaizMundo.position = pivot + deltaRot * (RaizMundo.position - pivot);
        }

        // 2) Translada a raiz para o spawn cair no jogador.
        Vector3 destino = cabeca;
        if (noChao)
        {
            var xr = GestorXR.Instancia;
            destino.y = xr != null && xr.Rig != null ? xr.Rig.transform.position.y : 0f;
        }
        RaizMundo.position += destino - spawn.position;

        Debug.Log($"[PosicionadorMundo] Mundo \"{RaizMundo.name}\" alinhado pelo SPAWN \"{spawn.name}\" " +
                  $"({(noChao ? "no chão, pés do jogador" : "altura dos olhos, cabeça")}): forward paralelo " +
                  $"aos óculos; {TeclaReposicionar} reposiciona.");
    }
}
