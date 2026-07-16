using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MinijogoTesteWaypoints — Implementação MÍNIMA de <see cref="MinijogoBase"/>
/// para testar o loop completo (GestorMinijogo + HUD + tabela + SessionManager)
/// ANTES do rework dos dardos.
///
/// Cada repetição: 5 esferas nos waypoints, tocadas por ordem com a mão
/// (sensor Ommo). A esfera ativa é maior/da cor do braço; tocar numa esfera
/// avança para a seguinte. A % da rep = esferas tocadas dentro do tempo por
/// waypoint (sem limite de tempo global — mas cada toque a menos de
/// <see cref="RaioToque"/> conta 100 % nesse waypoint).
/// </summary>
public class MinijogoTesteWaypoints : MinijogoBase
{
    [Tooltip("Raio (m) a que a mão conta como 'tocou' no waypoint.")]
    public float RaioToque = 0.10f;

    [Tooltip("Escala visual das esferas dos waypoints (m).")]
    public float EscalaEsfera = 0.08f;

    private readonly List<GameObject> _esferas = new List<GameObject>();
    private ConfigRep _cfg;
    private int  _alvoAtual;
    private int  _tocados;
    private bool _emRep;

    public override void PrepararRep(ConfigRep cfg)
    {
        LimparEsferas();
        _cfg       = cfg;
        _alvoAtual = 0;
        _tocados   = 0;

        if (cfg.Waypoints == null || cfg.Waypoints.Length == 0)
        {
            Debug.LogWarning("[MinijogoTeste] Rep sem waypoints — a concluir com 0 %.");
            EmitirRepConcluida(0f);
            return;
        }

        Color cor = cfg.BracoDireito ? new Color(1f, 0.75f, 0.2f) : new Color(0.2f, 0.7f, 1f);
        for (int i = 0; i < cfg.Waypoints.Length; i++)
            _esferas.Add(CriarEsfera($"Waypoint_{i + 1}", cfg.Waypoints[i], cor));

        AtualizarVisual();
        _emRep = true;
    }

    public override void Terminar()
    {
        _emRep = false;
        LimparEsferas();
    }

    void Update()
    {
        if (!_emRep || _cfg.Mao == null || !_cfg.Mao.Ativa) return;

        Vector3 mao = _cfg.Mao.Posicao;
        if (Vector3.Distance(mao, _cfg.Waypoints[_alvoAtual]) <= RaioToque)
        {
            _tocados++;
            _alvoAtual++;

            if (_alvoAtual >= _cfg.Waypoints.Length)
            {
                _emRep = false;
                float pct = 100f * _tocados / _cfg.Waypoints.Length;
                LimparEsferas();
                EmitirRepConcluida(pct);
                return;
            }
            AtualizarVisual();
        }
    }

    // ── Visual ────────────────────────────────────────────────────────
    void AtualizarVisual()
    {
        for (int i = 0; i < _esferas.Count; i++)
        {
            bool ativo   = i == _alvoAtual;
            bool passado = i <  _alvoAtual;
            var t = _esferas[i].transform;
            t.localScale = Vector3.one * (ativo ? EscalaEsfera * 1.6f : EscalaEsfera);
            var r = _esferas[i].GetComponent<Renderer>();
            var c = r.material.color;
            c.a = passado ? 0.15f : (ativo ? 1f : 0.45f);
            r.material.color = c;
        }
    }

    GameObject CriarEsfera(string nome, Vector3 pos, Color cor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nome;
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * EscalaEsfera;
        Destroy(go.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Standard")) { color = cor };
        // Transparência para distinguir esferas já tocadas.
        mat.SetFloat("_Mode", 3f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    void LimparEsferas()
    {
        foreach (var e in _esferas) if (e != null) Destroy(e);
        _esferas.Clear();
    }
}
