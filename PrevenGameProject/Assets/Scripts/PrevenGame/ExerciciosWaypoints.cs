using UnityEngine;

/// <summary>
/// ExerciciosWaypoints — Geradores de trajetória (waypoints) dos 4 exercícios gamificados.
///
/// Migrado do antigo PrevenGameManager (Clinical). Cada gerador devolve as 5 posições-mundo
/// que a palma deve percorrer para o exercício, a partir da calibração do paciente
/// (ombro, comprimento do braço, direção frontal e braço escolhido).
///
/// Proporções anatómicas (DIN 33402 / ISO 7250): braço superior 18.6/44, antebraço 14.6/44.
/// Reutilizado pelo GamificationManager (minijogo dos dardos) para qualquer exercício.
/// </summary>
public static class ExerciciosWaypoints
{
    /// <summary>Os 4 exercícios disponíveis. O índice inteiro é estável (serialização/seleção).</summary>
    public enum TipoExercicio
    {
        FlexaoBraco     = 0, // bicep curl frontal
        ElevacaoTotal   = 1, // elevação sagital 15°→180°
        AbducaoLateral  = 2, // abdução lateral + flexão do cotovelo
        FlexaoCotovelo  = 3, // bicep curl lateral
    }

    private const float FRACAO_BRACO_SUPERIOR = 18.6f / 44.0f;
    private const float FRACAO_ANTEBRACO      = 14.6f / 44.0f;

    /// <summary>Nome legível do exercício (subtítulo do card de seleção).</summary>
    public static string Nome(TipoExercicio tipo)
    {
        switch (tipo)
        {
            case TipoExercicio.FlexaoBraco:    return "flexão frontal";
            case TipoExercicio.ElevacaoTotal:  return "elevação total";
            case TipoExercicio.AbducaoLateral: return "abdução lateral";
            case TipoExercicio.FlexaoCotovelo: return "flexão do cotovelo";
            default:                           return "";
        }
    }

    /// <summary>Gera os waypoints do exercício pedido.</summary>
    public static Vector3[] Gerar(TipoExercicio tipo, Vector3 posOmbro, float comprimentoBraco,
                                  Vector3 dirFrente, bool bracoDireito)
    {
        float   L   = comprimentoBraco > 0.05f ? comprimentoBraco : 0.44f;
        Vector3 dir = dirFrente == Vector3.zero ? Vector3.forward : dirFrente;

        switch (tipo)
        {
            case TipoExercicio.ElevacaoTotal:  return ElevacaoTotal(posOmbro, L, dir);
            case TipoExercicio.AbducaoLateral: return AbducaoLateral(posOmbro, L, dir, bracoDireito);
            case TipoExercicio.FlexaoCotovelo: return FlexaoCotovelo(posOmbro, L, dir, bracoDireito);
            case TipoExercicio.FlexaoBraco:
            default:                           return FlexaoBraco(posOmbro, L, dir);
        }
    }

    /// <summary>Direção lateral ajustada ao braço (Direito → Cross(up, frente); Esquerdo → invertido).</summary>
    public static Vector3 ObterDirLateral(Vector3 dirFrente, bool bracoDireito)
    {
        Vector3 d = Vector3.Cross(Vector3.up, dirFrente).normalized;
        return bracoDireito ? d : -d;
    }

    // ── EX1 — Flexão do Braço (bicep curl frontal, cotovelo 0°→180°) ──────
    // Como na demo: braço paralelo ao chão, o cotovelo NÃO sai do sítio e o
    // antebraço roda até a mão encostar ao ombro (flexão completa = 180°).
    static Vector3[] FlexaoBraco(Vector3 posOmbro, float L, Vector3 dirFrente)
    {
        float Lu = L * FRACAO_BRACO_SUPERIOR;
        float Lf = L * FRACAO_ANTEBRACO;

        float[] angulos = { 0f, 45f, 90f, 135f, 180f };
        var pos = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            pos[i] = posOmbro
                + dirFrente  * (Lu + Lf * Mathf.Cos(rad))
                + Vector3.up * (Lf * Mathf.Sin(rad));
        }
        return pos;
    }

    // ── EX2 — Elevação Total (plano sagital 15°→180°) ────────────────────
    static Vector3[] ElevacaoTotal(Vector3 posOmbro, float L, Vector3 dirFrente)
    {
        float[] angulos = { 15f, 56f, 90f, 135f, 180f };
        var pos = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            pos[i] = posOmbro
                + dirFrente    * (Mathf.Sin(rad) * L)
                + Vector3.down * (Mathf.Cos(rad) * L);
        }
        return pos;
    }

    // ── EX3 — Abdução Lateral (0°→90°) + flexão do cotovelo ──────────────
    static Vector3[] AbducaoLateral(Vector3 posOmbro, float L, Vector3 dirFrente, bool bracoDireito)
    {
        Vector3 dirLateral = ObterDirLateral(dirFrente, bracoDireito);
        float   Lu = L * FRACAO_BRACO_SUPERIOR;
        float   Lf = L * FRACAO_ANTEBRACO;

        var pos = new Vector3[5];

        // Fase 1 — abdução 0°→90°
        float[] angulosOmbro = { 0f, 45f, 90f };
        for (int i = 0; i < 3; i++)
        {
            float rad = angulosOmbro[i] * Mathf.Deg2Rad;
            pos[i] = posOmbro
                + dirLateral   * (Mathf.Sin(rad) * L)
                + Vector3.down * (Mathf.Cos(rad) * L);
        }

        // Fase 2 — cotovelo flete para cima (bícep fixo horizontal lateral)
        float[] angulosCotovelo = { 72f, 108f };
        for (int i = 0; i < 2; i++)
        {
            float rad = angulosCotovelo[i] * Mathf.Deg2Rad;
            pos[3 + i] = posOmbro
                + dirLateral * (Lu + Lf * Mathf.Cos(rad))
                + Vector3.up * (Lf * Mathf.Sin(rad));
        }
        return pos;
    }

    // ── EX4 — Flexão do Cotovelo Lateral (bicep curl lateral, 0°→144°) ───
    static Vector3[] FlexaoCotovelo(Vector3 posOmbro, float L, Vector3 dirFrente, bool bracoDireito)
    {
        Vector3 dirLateral = ObterDirLateral(dirFrente, bracoDireito);
        float   Lu = L * FRACAO_BRACO_SUPERIOR;
        float   Lf = L * FRACAO_ANTEBRACO;

        float[] angulos = { 0f, 36f, 72f, 108f, 144f };
        var pos = new Vector3[angulos.Length];
        for (int i = 0; i < angulos.Length; i++)
        {
            float rad = angulos[i] * Mathf.Deg2Rad;
            pos[i] = posOmbro
                + dirLateral * (Lu + Lf * Mathf.Cos(rad))
                + dirFrente  * (Lf * Mathf.Sin(rad));
        }
        return pos;
    }
}
