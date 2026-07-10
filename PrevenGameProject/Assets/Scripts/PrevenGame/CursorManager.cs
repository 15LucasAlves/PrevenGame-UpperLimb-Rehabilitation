using UnityEngine;

/// <summary>
/// CursorManager — Substitui o cursor do sistema pelo <c>UIAssets/mouse.png</c> do jogo.
///
/// Persistente (DontDestroyOnLoad) para o cursor se manter em todas as cenas. A textura tem de
/// estar importada como Cursor/Default com Read/Write ativo. O hotspot é o canto superior
/// esquerdo por omissão (ajustável se a ponta do cursor não estiver aí).
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instancia { get; private set; }

    [Tooltip("Textura do cursor (UIAssets/mouse.png).")]
    public Texture2D CursorTextura;

    [Tooltip("Ponto ativo do cursor em píxeis, a partir do canto superior esquerdo.")]
    public Vector2 Hotspot = Vector2.zero;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        DontDestroyOnLoad(gameObject);
        Aplicar();
    }

    public void Aplicar()
    {
        if (CursorTextura != null)
            Cursor.SetCursor(CursorTextura, Hotspot, CursorMode.Auto);
    }
}
