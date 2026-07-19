using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CamaraApoioExercicio — câmara auxiliar que filma o exercício DE LADO (do
/// lado do braço ativo) e entrega a imagem numa RenderTexture, mostrada pelo
/// <see cref="HudVR"/> no canto superior esquerdo da vista VR. O jogador vê o
/// perfil do arco guia e a própria mão/dardo, o que ajuda a compreender o
/// movimento que está a fazer.
///
/// Ortográfica e auto-enquadrada: a cada frame centra-se nos pontos do
/// percurso e ajusta o tamanho para os apanhar todos com folga. Renderiza uma
/// única vez por frame (sem custo por-olho do VR).
/// </summary>
public class CamaraApoioExercicio : MonoBehaviour
{
    [Tooltip("Resolução (px) da textura do feed.")]
    public int Resolucao = 512;

    [Tooltip("Afastamento lateral (m) da câmara ao centro do exercício.")]
    public float Distancia = 1.6f;

    [Tooltip("Folga (m) à volta dos pontos no enquadramento.")]
    public float Margem = 0.20f;

    /// <summary>Textura com o feed (ligar a um RawImage — o HudVR trata disso).</summary>
    public RenderTexture Textura { get; private set; }

    private Camera _cam;

    public static CamaraApoioExercicio ObterOuCriar()
    {
        var existente = FindObjectOfType<CamaraApoioExercicio>();
        if (existente != null) return existente;
        return new GameObject("CamaraApoioExercicio").AddComponent<CamaraApoioExercicio>();
    }

    void Awake()
    {
        Textura = new RenderTexture(Resolucao, Resolucao, 16);

        _cam = gameObject.AddComponent<Camera>();
        _cam.targetTexture   = Textura;
        _cam.stereoTargetEye = StereoTargetEyeMask.None; // feed mono, nunca vai ao HMD
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.09f, 0.12f, 0.10f, 1f); // tom dos painéis do jogo
        _cam.orthographic    = true;
        _cam.nearClipPlane   = 0.05f;
        _cam.farClipPlane    = 10f;
        _cam.enabled         = false; // só liga durante a rep (Enquadrar)
    }

    void OnDestroy()
    {
        if (Textura != null) Textura.Release();
    }

    public void Mostrar(bool ativo)
    {
        if (_cam != null) _cam.enabled = ativo;
    }

    /// <summary>
    /// Posiciona a câmara ao LADO do exercício (lado do braço ativo), virada
    /// para o percurso, e ajusta o zoom ortográfico para enquadrar os primeiros
    /// <paramref name="n"/> pontos (o arco da ida) com folga.
    /// </summary>
    public void Enquadrar(IReadOnlyList<Vector3> pontos, int n, Vector3 frente, bool bracoDireito)
    {
        if (_cam == null || pontos == null) return;
        n = Mathf.Min(n, pontos.Count);
        if (n <= 0) return;

        Vector3 centro = Vector3.zero;
        for (int i = 0; i < n; i++) centro += pontos[i];
        centro /= n;

        Vector3 lado = ExerciciosWaypoints.ObterDirLateral(frente, bracoDireito);
        transform.SetPositionAndRotation(centro + lado * Distancia,
                                         Quaternion.LookRotation(-lado, Vector3.up));

        // Meio-extento máximo dos pontos no plano da câmara → zoom certo.
        float ext = 0.25f;
        for (int i = 0; i < n; i++)
        {
            Vector3 local = transform.InverseTransformPoint(pontos[i]);
            ext = Mathf.Max(ext, Mathf.Abs(local.x), Mathf.Abs(local.y));
        }
        _cam.orthographicSize = ext + Margem;

        if (!_cam.enabled) _cam.enabled = true;
    }
}
