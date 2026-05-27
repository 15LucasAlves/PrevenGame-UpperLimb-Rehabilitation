using UnityEngine;

/// <summary>
/// Garante que a câmara principal está posicionada corretamente ao iniciar.
/// Adicionado pelo BuildSceneJogo() à câmara da cena de jogo.
/// </summary>
public class OmmoCameraSetup : MonoBehaviour
{
    [Tooltip("Posição da câmara no espaço de tracking Ommo.")]
    public Vector3 Posicao = new Vector3(-10f, 18f, 2f);

    [Tooltip("Ponto para onde a câmara aponta.")]
    public Vector3 AlvoDeLook = new Vector3(0f, 13f, 4f);

    void Awake()
    {
        transform.position = Posicao;
        transform.LookAt(AlvoDeLook);
    }
}
