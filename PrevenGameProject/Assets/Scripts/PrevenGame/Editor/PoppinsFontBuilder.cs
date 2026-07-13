using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Gera os assets TMP SDF do Poppins esperados pelo OmmoSceneBuilder:
///   Assets/Fonts/Poppins-ExtraBold SDF.asset
///   Assets/Fonts/Poppins-Medium SDF.asset
/// a partir dos TTF em Assets/Fonts (Google Fonts, licença OFL — ver OFL.txt).
///
/// Usa atlas em modo dinâmico: os glifos são rasterizados on-demand a partir do TTF,
/// pelo que não é preciso passar pelo Font Asset Creator nem escolher character sets.
/// Correr uma vez via Ommo → PrevenGame → Criar Fontes Poppins (SDF) e depois
/// re-correr o Build Cenas para os textos passarem a usar o Poppins.
/// </summary>
public static class PoppinsFontBuilder
{
    const string PastaFontes = "Assets/Fonts";

    [MenuItem("Ommo/PrevenGame/Criar Fontes Poppins (SDF)")]
    public static void CriarFontes()
    {
        CriarSDF("Poppins-ExtraBold");
        CriarSDF("Poppins-Medium");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void CriarSDF(string nomeTTF)
    {
        string caminhoTTF = $"{PastaFontes}/{nomeTTF}.ttf";
        string caminhoSDF = $"{PastaFontes}/{nomeTTF} SDF.asset";

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(caminhoTTF);
        if (ttf == null)
        {
            Debug.LogError($"[PoppinsFontBuilder] TTF não encontrado: {caminhoTTF}");
            return;
        }

        // Não recriar por cima de um asset existente — as cenas já construídas
        // guardam referências a ele e perdê-las-iam.
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminhoSDF) != null)
        {
            Debug.Log($"[PoppinsFontBuilder] Já existe, não recriado: {caminhoSDF}");
            return;
        }

        var sdf = TMP_FontAsset.CreateFontAsset(
            ttf,
            samplingPointSize: 90,
            atlasPadding: 9,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 1024,
            atlasHeight: 1024,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);
        sdf.name = $"{nomeTTF} SDF";

        AssetDatabase.CreateAsset(sdf, caminhoSDF);

        // O material e o atlas nascem só em memória — têm de ser gravados como
        // sub-assets para o TMP_FontAsset sobreviver a um reload do projeto.
        sdf.material.name  = $"{nomeTTF} SDF Material";
        sdf.material.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(sdf.material, sdf);

        if (sdf.atlasTextures != null && sdf.atlasTextures.Length > 0 && sdf.atlasTextures[0] != null)
        {
            sdf.atlasTextures[0].name = $"{nomeTTF} SDF Atlas";
            sdf.atlasTextures[0].hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(sdf.atlasTextures[0], sdf);
        }

        EditorUtility.SetDirty(sdf);
        Debug.Log($"[PoppinsFontBuilder] Criado: {caminhoSDF}");
    }
}
