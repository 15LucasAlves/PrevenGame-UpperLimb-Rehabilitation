using UnityEngine;

/// <summary>
/// RastreadorCorpoJogador — Estima a posição dos ombros a partir da câmara VR.
///
/// Regras (decididas com o utilizador):
///   • O ombro segue a POSIÇÃO da câmara (se o jogador andar/deslocar-se, o ombro
///     acompanha — não se assume que está sentado).
///   • O yaw de corpo segue o yaw da cabeça DIRETAMENTE, sem deadband nem
///     suavização — o exercício acompanha o jogador rigidamente.
///
/// O offset cabeça→ombro por braço vem da calibração (headset posto), guardado em
/// <see cref="SessionManager.DadosBraco.OffsetOmbroLocalCabeca"/> no referencial
/// yaw-local. Sem VR (fallback), devolve as posições mundo calibradas.
/// </summary>
public class RastreadorCorpoJogador : MonoBehaviour
{
    [Tooltip("Esferas de debug nos ombros estimados (editor/desenvolvimento).")]
    public bool MostrarDebug = false;

    [Tooltip("Ajuste fino ao ombro estimado, no referencial yaw-local da cabeça " +
             "[x=lateral, y=vertical, z=frente]. Ex.: y=-0.05 se o exercício aparecer alto demais.")]
    public Vector3 AjusteOmbroLocal = Vector3.zero;

    /// <summary>Yaw estimado do corpo (rotação só em Y).</summary>
    public Quaternion YawCorpo => Quaternion.Euler(0f, _yawCorpo, 0f);

    /// <summary>True quando há câmara VR ativa para derivar os ombros.</summary>
    public bool UsaVr => GestorXR.Instancia != null && GestorXR.Instancia.VrAtivo &&
                         GestorXR.Instancia.Cabeca != null;

    private float _yawCorpo;
    private GameObject _debugEsq, _debugDir;

    void Update()
    {
        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (cabeca == null) return;

        // Rígido: o yaw de corpo É o yaw da cabeça (sem deadband nem lerp).
        _yawCorpo = cabeca.eulerAngles.y;

        AtualizarDebug();
    }

    /// <summary>Alinha imediatamente o yaw de corpo com a cabeça (ex.: no início de um bloco).</summary>
    public void ReporYawCorpo()
    {
        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (cabeca != null) _yawCorpo = cabeca.eulerAngles.y;
    }

    /// <summary>
    /// Posição mundo estimada do ombro do braço pedido: posição da câmara + offset
    /// calibrado rodado pelo yaw de corpo. Se ESTE braço não tiver dados de cabeça
    /// mas o OUTRO tiver, usa o offset do outro espelhado lateralmente — ambos os
    /// braços seguem SEMPRE o jogador (nunca um segue e o outro fica fixo).
    /// Último recurso: posição mundo da calibração.
    /// </summary>
    public Vector3 ObterOmbroAtual(bool direito)
    {
        var sm = SessionManager.Instancia;
        if (sm == null) return Vector3.zero;
        var dados = sm.ObterBraco(direito);
        if (!dados.Valido) return Vector3.zero;

        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (cabeca != null)
        {
            if (dados.TemDadosCabeca)
                return cabeca.position + YawCorpo * (dados.OffsetOmbroLocalCabeca + AjusteOmbroLocal);

            // Espelho do outro braço (offset lateral invertido em yaw-local).
            var outro = sm.ObterBraco(!direito);
            if (outro.Valido && outro.TemDadosCabeca)
            {
                AvisarEspelho(direito);
                var off = outro.OffsetOmbroLocalCabeca;
                off.x = -off.x;
                return cabeca.position + YawCorpo * (off + AjusteOmbroLocal);
            }
        }

        return dados.PosOmbro;
    }

    /// <summary>
    /// Direção frente atual do braço pedido. Com VR é SEMPRE o forward horizontal
    /// dos óculos — o exercício (waypoints, arco guia, marcador) fica paralelo à
    /// direção da cabeça, não à direção que calhou ficar gravada na calibração
    /// (essa saía torta se a cabeça estivesse rodada ao calibrar).
    /// </summary>
    public Vector3 ObterDirecaoFrenteAtual(bool direito)
    {
        var cabeca = UsaVr ? GestorXR.Instancia.Cabeca : null;
        if (cabeca != null)
            return YawCorpo * Vector3.forward;

        var sm = SessionManager.Instancia;
        if (sm == null) return Vector3.forward;
        var dados = sm.ObterBraco(direito);
        return dados.Valido ? dados.DirecaoFrente : Vector3.forward;
    }

    private bool _avisoEspelhoDado;
    void AvisarEspelho(bool direito)
    {
        if (_avisoEspelhoDado) return;
        _avisoEspelhoDado = true;
        Debug.LogWarning($"[RastreadorCorpo] Braço {(direito ? "direito" : "esquerdo")} foi calibrado SEM dados de " +
                         "cabeça — a usar o espelho do outro braço para seguir o jogador. Recalibrar com o headset " +
                         "bem posto resolve de vez.");
    }

    // ── Debug ─────────────────────────────────────────────────────────
    void AtualizarDebug()
    {
        if (!MostrarDebug)
        {
            if (_debugEsq != null) { Destroy(_debugEsq); Destroy(_debugDir); _debugEsq = _debugDir = null; }
            return;
        }

        if (_debugEsq == null)
        {
            _debugEsq = CriarEsferaDebug("DebugOmbroEsq", Color.cyan);
            _debugDir = CriarEsferaDebug("DebugOmbroDir", Color.yellow);
        }
        _debugEsq.transform.position = ObterOmbroAtual(false);
        _debugDir.transform.position = ObterOmbroAtual(true);
    }

    GameObject CriarEsferaDebug(string nome, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nome;
        go.transform.localScale = Vector3.one * 0.05f;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material.color = cor;
        return go;
    }
}
