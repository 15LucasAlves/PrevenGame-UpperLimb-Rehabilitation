using UnityEngine;

/// <summary>
/// HelperFala — Uma linha de diálogo: quem fala (Jane/Patrick), a emoção e o texto.
///
/// Editável no Inspector (dentro de um <see cref="DialogueSequence"/> ou de listas nos managers).
/// A emoção é escolhida por linha, pelo que cada balão pode ter uma emoção diferente.
/// </summary>
[System.Serializable]
public class HelperFala
{
    [Tooltip("Personagem que fala nesta linha.")]
    public HelperId Quem = HelperId.Jane;

    [Tooltip("Emoção do personagem nesta linha (troca o sprite).")]
    public HelperEmocao Emocao = HelperEmocao.Neutral;

    [TextArea(2, 5)]
    [Tooltip("Texto do balão. Aparece letra a letra (efeito de máquina de escrever).")]
    public string Texto = "";
}
