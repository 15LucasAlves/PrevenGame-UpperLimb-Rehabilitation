using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DialogueSequence — Uma sequência de falas editável no Editor (ScriptableObject).
///
/// Cria com: botão direito no Project → Create → PrevenGame → Sequência de Diálogo.
/// Cada elemento da lista é uma <see cref="HelperFala"/> (personagem + emoção + texto).
/// Atribui a sequência no Inspector do GameFlowManager (tutorial, score, etc.) para escolher
/// facilmente onde e como cada helper fala, sem tocar no código.
/// </summary>
[CreateAssetMenu(fileName = "NovaSequenciaDialogo", menuName = "PrevenGame/Sequência de Diálogo", order = 0)]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Falas por ordem. Avançam ao clique; cada uma tem o seu personagem e emoção.")]
    public List<HelperFala> Falas = new List<HelperFala>();

    public bool TemFalas => Falas != null && Falas.Count > 0;
}
