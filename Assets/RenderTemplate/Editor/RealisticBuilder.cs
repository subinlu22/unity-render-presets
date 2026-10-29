using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 고퀄 재질 세트 만들기 (RenderTemplateBuilder.Build 안에서 불림)
// 모델 원본 재질을 복제해서 성질별로 조정 (원본은 그대로)
// 스테인리스·유리·석재 상판·나무 하부장·광택 도장·키오스크 화면(웹 스크린샷)
// 모든 프리셋과 Reference Matcher가 이 세트를 바탕으로 색·재질을 덮어씀
public static class RealisticBuilder
{
    const string Root = "Assets/RenderTemplate";
    const string MatDir = Root + "/Materials/HQ";
    const string Tex = Root + "/ThirdParty/PolyHaven/Textures/";

    public static MaterialSet BuildMaterialSet(string modelPath)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        AssetDatabase.Refresh();
        NormalMaps();
        var woodD = T("wood_table_001/wood_table_001_diff_1k.jpg"); var woodN = T("wood_table_001/wood_table_001_nor_gl_1k.jpg");
        var kiosk = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Models/kiosk_screen.png");

        var src = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray();
        var set = Load<MaterialSet>(Root + "/Presets/MaterialSet_HQ.asset");
        var pairs = new System.Collections.Generic.List<MaterialSet.Pair>();
        var report = new System.Text.StringBuilder("고퀄 재질 분류 결과 (Build Demo Scene 때마다 새로 씀)\n");
        for (int i = 0; i < src.Length; i++)
        {
            var o = src[i];
            if (!o.HasProperty("baseColorFactor")) continue;
            Color c = o.GetColor("baseColorFactor");
            bool unlit = !o.HasProperty("metallicFactor");
            float m = unlit ? 0 : o.GetFloat("metallicFactor"), r = unlit ? 1 : o.GetFloat("roughnessFactor");
            bool tex = o.GetTexture("baseColorTexture");
            bool emis = o.HasProperty("emissiveFactor") && o.GetColor("emissiveFactor").maxColorComponent > 0.01f;
            string path = $"{MatDir}/HQ_{i:00}.mat";
            AssetDatabase.DeleteAsset(path);
            var h = new Material(o);
            string kind;
            if (unlit && c.a < 0.05f)
            {
                // 웹에서 키오스크 HTML이 보이던 "투명 구멍" → 키오스크 스크린샷을 화면에 붙임
                if (!kiosk) continue;
                kind = "키오스크 화면";
                h.SetColor("baseColorFactor", Color.white * 1.15f); h.SetTexture("baseColorTexture", kiosk);
            }
            else if (c.a < 0.05f || unlit) continue;               // 안 보이는 재질, 인쇄용 unlit은 그대로
            else if (emis) { kind = "발광"; h.SetColor("emissiveFactor", o.GetColor("emissiveFactor") * 2.5f); }
            else if (c.a < 0.95f) { kind = "유리"; Set(h, c, 0.2f, 0.03f); }
            else if (m >= 0.7f) { kind = "스테인리스"; Set(h, new Color(0.8f, 0.8f, 0.82f), 1f, 0.2f); }
            else if (m > 0.2f) { kind = "금속(어두운)"; Set(h, c, 0.85f, 0.32f); }
            else if (!tex && c.maxColorComponent < 0.05f && r < 0.5f) { kind = "검은 유광(화면 테두리)"; Set(h, c, 0.1f, 0.08f); }
            else if (!tex && Near(c, new Color(0.93f, 0.91f, 0.87f)) && Mathf.Abs(r - 0.55f) < 0.03f)
            {
                kind = "석재 상판(쿼츠)"; Set(h, new Color(0.93f, 0.92f, 0.9f), 0f, 0.16f);
            }
            else if (tex && Mathf.Abs(r - 0.7f) < 0.05f && c.a > 0.99f && o.renderQueue < 3000)
            {
                kind = "나무 하부장"; Set(h, Color.white, 0f, 0.45f);
                h.SetTexture("baseColorTexture", woodD); Normal(h, woodN, 1f);
            }
            else if (!tex && c.r > 0.85f && c.g > 0.85f) { kind = "광택 도장(흰)"; Set(h, c, 0f, 0.25f); }
            else if (tex) { kind = "인쇄 스티커"; Set(h, c, m, 0.3f); }
            else { kind = "기타"; Set(h, c, m, Mathf.Clamp01(r * 0.7f)); }
            AssetDatabase.CreateAsset(h, path);
            pairs.Add(new MaterialSet.Pair { from = o, to = h });
            report.AppendLine($"HQ_{i:00} {kind,-12} 원본 color=({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00}) metal={m:0.00} rough={r:0.00} tex={tex} unlit={unlit} queue={o.renderQueue}");
        }
        set.pairs = pairs.ToArray();
        System.IO.File.WriteAllText(MatDir + "/_분류결과.txt", report.ToString(), new System.Text.UTF8Encoding(false));
        EditorUtility.SetDirty(set);
        Debug.Log("[RenderTemplate] 고퀄 재질 " + pairs.Count + "개: " + string.Join(", ", pairs.Select(p => p.to.name)));
        return set;
    }

    // ---------------------------------------------------------------- 도우미
    static Texture2D T(string rel) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + rel);
    static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.06f;
    static void Set(Material h, Color c, float metal, float rough)
    {
        h.SetColor("baseColorFactor", c); h.SetFloat("metallicFactor", metal); h.SetFloat("roughnessFactor", rough);
    }
    static void Normal(Material h, Texture2D n, float scale)
    {
        if (!n) return;
        h.SetTexture("normalTexture", n); h.SetFloat("normalTexture_scale", scale); h.EnableKeyword("_NORMALMAP");
    }

    static void NormalMaps()
    {
        foreach (var g in AssetDatabase.FindAssets("_nor_gl_ t:Texture2D", new[] { Tex.TrimEnd('/') }))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g));
            if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        }
    }

    static TAsset Load<TAsset>(string path) where TAsset : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<TAsset>(path);
        if (!a) { a = ScriptableObject.CreateInstance<TAsset>(); AssetDatabase.CreateAsset(a, path); }
        return a;
    }
}
