using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 레퍼런스 사진에 맞춘 프리셋 2개 (모델은 그대로, 색·재질·조명·카메라만)
//  - "화이트 라운지"  밝은 사무실 라운지, 흰 기계, 흰 대리석 상판, 밝은 오크 세로 홈 하부장, 베이지 벽
//  - "다크 라운지"   건메탈 커피머신 + 네이비 디스펜서, 진한 대리석, 월넛 세로 홈 + LED 띠, 회색 시멘트 벽, 벽등
// 흐름: 원본 재질 i번 → (고퀄 HQ_i 복제) → 레시피에 있는 번호만 색·재질 덮어쓰기 → 프리셋 전용 재질 세트
// 재질 번호는 Materials/HQ/_분류결과.txt 참고 (glb 안 재질 순서)
public static class ReferenceLooks
{
    const string Root = "Assets/RenderTemplate";
    const string Gen = Root + "/Generated";
    const string TP = Root + "/ThirdParty/";

    // 기계·부품별 재질 번호 (에디터에서 기계별로 쓰는 재질을 조회해서 정함)
    internal static readonly int[] BigBody = { 4, 23 }, IceBody = { 1 }, CupBody = { 7 };
    internal static readonly int[] PanelDecals = { 2, 3, 22, 8, 9 };          // 제빙기·컵 디스펜서 앞판 인쇄
    internal const int Glass = 0, DarkMetal = 21, Counter = 11, Cabinet = 12, Plinth = 16, Floor = 17, Wall = 18, Kiosk = 10;
    internal static readonly int[] Dividers = { 13, 14, 15 };

    static Vector3 Focus => new Vector3(0.15f, 0.32f, 0f);              // RenderTemplateBuilder.Target 과 같음
    static readonly Vector3 CamWorld = new Vector3(1.2f, 0.68f, 2.35f), LookWorld = new Vector3(0.0f, 0.24f, 0.0f);

    public static void Build(string modelPath, float focusDist)
    {
        Directory.CreateDirectory(Gen);
        CopyReference("ref_install_01.png");
        CopyReference("ref_premium_01.png");
        AssetDatabase.Refresh();

        var flute = FluteNormal();
        var marble = T(TP + "AmbientCG/Marble012/Marble012_1K-JPG_Color.jpg");
        var marbleN = T(TP + "AmbientCG/Marble012/Marble012_1K-JPG_NormalGL.jpg");
        var oak = T(TP + "PolyHaven/Textures/white_oak_veneer/white_oak_veneer_diff_1k.jpg");
        var plasterN = T(TP + "PolyHaven/Textures/painted_plaster_wall/painted_plaster_wall_nor_gl_1k.jpg");
        var kiosk = T(Root + "/Models/kiosk_screen.png");
        var darkMarble = Recolor(marble, "DarkMarble", new Color(0.13f, 0.1f, 0.09f), new Color(0.62f, 0.56f, 0.5f), invert: true);

        // ---------------- 1) 실제 설치 (밝은 라운지) - 프리미엄과 같은 방식(질감 있는 벽·광택 대리석·방향 있는 빛)을 밝은 색으로
        var marbleWhite = Recolor(marble, "WhiteMarble", new Color(0.86f, 0.85f, 0.83f), new Color(0.5f, 0.5f, 0.52f), invert: true, curve: 1.3f);
        var setA = MakeSet("RefInstall", modelPath, new Dictionary<int, Action<Material>>
        {
            [Counter] = m => { Pbr(m, Color.white, 0, 0.1f); Tex(m, marbleWhite, new Vector2(3f, 0.9f)); Nrm(m, marbleN, 0.4f, new Vector2(3f, 0.9f)); },
            [Cabinet] = m => { Pbr(m, new Color(0.98f, 0.9f, 0.8f), 0, 0.5f); Tex(m, oak, new Vector2(3, 1)); Nrm(m, flute, 1.4f, new Vector2(32, 1)); },
            [Plinth] = m => Glow(m, new Color(1f, 0.85f, 0.65f) * 0.8f),   // 은은한 LED 띠 (밝은 공간이라 약하게)
            [Floor] = m => Pbr(m, new Color(0.7f, 0.69f, 0.67f), 0, 0.35f),
            [Wall] = m => { Pbr(m, new Color(0.8f, 0.74f, 0.66f), 0, 0.85f); Nrm(m, plasterN, 0.6f, new Vector2(4, 2)); },
            [DarkMetal] = m => Pbr(m, new Color(0.55f, 0.56f, 0.58f), 0.9f, 0.25f),
            [Kiosk] = m => Screen(m, kiosk, 0.35f),                    // 살짝 켜진 유리 화면
        }, Each(BigBody.Concat(IceBody).Concat(CupBody), m => Pbr(m, new Color(0.8f, 0.8f, 0.79f), 0.05f, 0.3f)),
           Each(Dividers, m => Pbr(m, new Color(0.62f, 0.5f, 0.38f), 0, 0.6f)));

        var ppA = Profile("PP_RefInstall", p =>
        {
            var ao = p.AddSettings<AmbientOcclusion>(); ao.enabled.Override(true); ao.intensity.Override(0.5f); ao.thicknessModifier.Override(1f);
            ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
            var cg = p.AddSettings<ColorGrading>(); cg.enabled.Override(true); cg.tonemapper.Override(Tonemapper.ACES);
            cg.postExposure.Override(0f); cg.contrast.Override(4f); cg.temperature.Override(2f); cg.saturation.Override(-4f);
            var bl = p.AddSettings<Bloom>(); bl.enabled.Override(true); bl.threshold.Override(1.4f); bl.intensity.Override(0.4f); bl.softKnee.Override(0.5f);
            var vg = p.AddSettings<Vignette>(); vg.enabled.Override(true); vg.intensity.Override(0.15f); vg.smoothness.Override(0.5f);
            var dof = p.AddSettings<DepthOfField>(); dof.enabled.Override(true); dof.focusDistance.Override(Vector3.Distance(CamWorld, LookWorld));
            dof.aperture.Override(5.6f); dof.focalLength.Override(40f);
        });
        var warm = new Color(1f, 0.86f, 0.7f);
        Preset("Look_30_WhiteLounge", "화이트 라운지", 30, "밝은 라운지·흰 기계·결 있는 흰 대리석·오크 세로 홈+은은한 LED, 질감 있는 베이지 벽, 왼쪽 창빛 + 벽 위 따뜻한 빛 번짐",
               Sky("Sky_LoftHall", TP + "PolyHaven/photo_studio_loft_hall_2k.hdr"), 0.7f, ppA, setA, new[]
               {
                   Dir("창빛 (왼쪽)", new Color(1f, 0.97f, 0.92f), 0.6f, new Vector3(2.2f, 1.9f, 1.3f), true),
                   Point("벽 빛 번짐 왼쪽", warm, 1.0f, new Vector3(1.05f, 1.2f, -0.25f), 1.9f),
                   Point("벽 빛 번짐 오른쪽", warm, 0.8f, new Vector3(-1.3f, 1.2f, -0.25f), 1.9f),
                   Dir("오른쪽 반사광", new Color(0.92f, 0.95f, 1f), 0.25f, new Vector3(-2f, 0.8f, 1.5f), false),
               }, "ref_install_01");

        // ---------------- 2) 프리미엄 (어두운 라운지)
        var setB = MakeSet("RefPremium", modelPath, new Dictionary<int, Action<Material>>
        {
            [Counter] = m => { Pbr(m, Color.white, 0, 0.12f); Tex(m, darkMarble, new Vector2(3f, 0.9f)); Nrm(m, marbleN, 0.4f, new Vector2(3f, 0.9f)); },
            [Cabinet] = m => { Pbr(m, new Color(0.62f, 0.45f, 0.31f), 0, 0.5f); Tex(m, oak, new Vector2(3, 1)); Nrm(m, flute, 1.4f, new Vector2(32, 1)); },
            [Plinth] = m => Glow(m, new Color(1f, 0.72f, 0.42f) * 2.2f),   // 상판 아래 LED 띠
            [Floor] = m => Pbr(m, new Color(0.3f, 0.26f, 0.23f), 0, 0.4f),
            [Wall] = m => { Pbr(m, new Color(0.55f, 0.54f, 0.52f), 0, 0.85f); Nrm(m, plasterN, 0.6f, new Vector2(4, 2)); },
            [DarkMetal] = m => Pbr(m, new Color(0.16f, 0.16f, 0.17f), 0.9f, 0.3f),
            [Kiosk] = m => Screen(m, kiosk, 1f),
        }, Each(BigBody, m => Pbr(m, new Color(0.5f, 0.5f, 0.51f), 0.5f, 0.32f)),
           Each(IceBody.Concat(CupBody), m => Pbr(m, new Color(0.09f, 0.12f, 0.18f), 0.3f, 0.4f)),
           Each(Dividers, m => Pbr(m, new Color(0.2f, 0.14f, 0.1f), 0, 0.6f)),
           Each(PanelDecals, m => { var t = m.GetTexture("baseColorTexture") as Texture2D; if (t) m.SetTexture("baseColorTexture", Recolor(t, "Navy_" + t.name + "_" + m.name, new Color(0.06f, 0.085f, 0.13f), new Color(0.85f, 0.87f, 0.9f), invert: true, keepColor: true)); }));

        var ppB = Profile("PP_RefPremium", p =>
        {
            var ao = p.AddSettings<AmbientOcclusion>(); ao.enabled.Override(true); ao.intensity.Override(0.55f); ao.thicknessModifier.Override(1f);
            ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
            var cg = p.AddSettings<ColorGrading>(); cg.enabled.Override(true); cg.tonemapper.Override(Tonemapper.ACES);
            cg.postExposure.Override(0.75f); cg.contrast.Override(12f); cg.temperature.Override(8f); cg.saturation.Override(-4f);
            var bl = p.AddSettings<Bloom>(); bl.enabled.Override(true); bl.threshold.Override(1f); bl.intensity.Override(2f); bl.softKnee.Override(0.6f);
            var vg = p.AddSettings<Vignette>(); vg.enabled.Override(true); vg.intensity.Override(0.28f); vg.smoothness.Override(0.5f);
            var dof = p.AddSettings<DepthOfField>(); dof.enabled.Override(true); dof.focusDistance.Override(Vector3.Distance(CamWorld, LookWorld));
            dof.aperture.Override(5.6f); dof.focalLength.Override(40f);
        });
        var sconce = new Color(1f, 0.72f, 0.42f);
        Preset("Look_40_DarkLounge", "다크 라운지", 40, "건메탈 커피머신·네이비 디스펜서·진한 대리석·월넛 세로 홈+LED 띠, 시멘트 벽, 따뜻한 벽등",
               Sky("Sky_RealCafe", TP + "PolyHaven/cayley_interior_2k.hdr"), 0.9f, ppB, setB, new[]
               {
                   Point("벽등 왼쪽", sconce, 3.5f, new Vector3(1.05f, 1.15f, -0.25f), 1.8f),
                   Point("벽등 오른쪽", sconce, 3f, new Vector3(-1.3f, 1.15f, -0.25f), 1.8f),
                   Dir("창 햇빛 (오른쪽)", new Color(0.92f, 0.94f, 1f), 0.9f, new Vector3(-2.2f, 1.6f, 1.4f), true),
               }, "ref_premium_01");
        AssetDatabase.SaveAssets();
    }

    // ================================================================ 프리셋·재질 세트
    static void Preset(string file, string display, int order, string note, Material sky, float ambient,
                       PostProcessProfile pp, MaterialSet set, LookPreset.RigLight[] lights, string refName)
    {
        string path = Root + "/Resources/Looks/" + file + ".asset";
        var l = AssetDatabase.LoadAssetAtPath<LookPreset>(path);
        if (!l) { l = ScriptableObject.CreateInstance<LookPreset>(); AssetDatabase.CreateAsset(l, path); }
        l.displayName = display; l.order = order; l.note = note;
        l.skybox = sky; l.showSkybox = true; l.ambientIntensity = ambient; l.reflectionIntensity = 1f;
        l.originalLightScale = 0; l.lights = lights; l.post = pp; l.roomPrefab = null;
        l.reflectionProbe = true; l.probeSize = new Vector3(4, 2.5f, 3); l.probeIntensity = 1f;
        l.hqMaterials = true; l.materialSet = set; l.contactShadow = 0.6f;
        l.overrideCamera = true; l.cameraOffset = CamWorld - Focus; l.lookOffset = LookWorld - Focus; l.fov = 38f;
        l.reference = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/References/" + refName + ".png");
        // 웹 보정: 같은 앵글에서 유니티 게임 화면과 밝기를 재서 맞춘 값 (다크는 HDRI가 어두워 금속 반사가 죽음 → 환경광 올림)
        bool dark = file.Contains("Dark");
        l.webEnvScale = dark ? 2.2f : 1f; l.webLightScale = dark ? 1.35f : 0.75f; l.webExposure = 0f;
        EditorUtility.SetDirty(l);
    }

    static MaterialSet MakeSet(string name, string modelPath, Dictionary<int, Action<Material>> recipe, params Dictionary<int, Action<Material>>[] more)
    {
        foreach (var d in more) foreach (var kv in d) recipe[kv.Key] = kv.Value;
        string dir = Root + "/Materials/" + name;
        Directory.CreateDirectory(dir); AssetDatabase.Refresh();
        var src = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray();
        var pairs = new List<MaterialSet.Pair>();
        for (int i = 0; i < src.Length; i++)
        {
            var hq = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/HQ/HQ_{i:00}.mat");
            if (!recipe.ContainsKey(i)) { if (hq) pairs.Add(new MaterialSet.Pair { from = src[i], to = hq }); continue; }
            string path = $"{dir}/{name}_{i:00}.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(hq ? hq : src[i]);
            recipe[i](m);
            AssetDatabase.CreateAsset(m, path);
            pairs.Add(new MaterialSet.Pair { from = src[i], to = m });
        }
        string setPath = Root + "/Presets/MaterialSet_" + name + ".asset";
        var set = AssetDatabase.LoadAssetAtPath<MaterialSet>(setPath);
        if (!set) { set = ScriptableObject.CreateInstance<MaterialSet>(); AssetDatabase.CreateAsset(set, setPath); }
        set.pairs = pairs.ToArray();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<MaterialSet>(setPath);
    }

    static Dictionary<int, Action<Material>> Each(IEnumerable<int> ids, Action<Material> a) => ids.ToDictionary(i => i, i => a);

    // ================================================================ 재질 도우미 (glTF 셰이더 속성)
    internal static void Pbr(Material m, Color c, float metal, float rough)
    {
        if (m.HasProperty("metallicFactor")) { m.SetFloat("metallicFactor", metal); m.SetFloat("roughnessFactor", rough); }
        m.SetColor("baseColorFactor", c);
    }
    internal static void Tex(Material m, Texture t, Vector2 tiling)
    {
        if (!t) return;
        m.SetTexture("baseColorTexture", t); m.SetTextureScale("baseColorTexture", tiling); m.EnableKeyword("_TEXTURE_TRANSFORM");
    }
    internal static void Nrm(Material m, Texture t, float scale, Vector2 tiling)
    {
        if (!t || !m.HasProperty("normalTexture")) return;
        m.SetTexture("normalTexture", t); m.SetFloat("normalTexture_scale", scale);
        m.SetTextureScale("normalTexture", tiling); m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_TEXTURE_TRANSFORM");
    }
    internal static void Glow(Material m, Color e)
    {
        Pbr(m, Color.black, 0, 0.5f);
        m.SetColor("emissiveFactor", e); m.EnableKeyword("_EMISSION");
    }
    // 키오스크 화면: unlit 투명 구멍 → 반사되는 유리 화면(Standard) + 화면 발광
    static void Screen(Material m, Texture kiosk, float bright)
    {
        m.shader = Shader.Find("Standard");
        m.SetColor("_Color", new Color(0.02f, 0.02f, 0.025f)); m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.93f);
        m.SetTexture("_EmissionMap", kiosk); m.SetColor("_EmissionColor", Color.white * bright); m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetFloat("_Mode", 0); m.renderQueue = -1;
    }

    static LookPreset.RigLight Dir(string n, Color c, float i, Vector3 from, bool shadows) =>
        new LookPreset.RigLight { name = n, type = LightType.Directional, color = c, intensity = i, position = from, euler = Quaternion.LookRotation(-from).eulerAngles, shadows = shadows };
    static LookPreset.RigLight Point(string n, Color c, float i, Vector3 pos, float range) =>
        new LookPreset.RigLight { name = n, type = LightType.Point, color = c, intensity = i, position = pos, range = range };

    static Material Sky(string name, string hdr)
    {
        string path = Root + "/Presets/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(hdr));
        m.SetFloat("_Mapping", 1); m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        EditorUtility.SetDirty(m);
        return m;
    }

    static PostProcessProfile Profile(string name, Action<PostProcessProfile> fill)
    {
        string path = Root + "/Presets/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(p, path);
        fill(p);
        foreach (var s in p.settings) { s.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector; AssetDatabase.AddObjectToAsset(s, p); }
        EditorUtility.SetDirty(p);
        return p;
    }

    // ================================================================ 텍스처 만들기
    internal static Texture2D T(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp && path.Contains("Normal") || imp && path.Contains("_nor_"))
            if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void CopyReference(string file)
    {
        string src = Path.Combine(Application.dataPath, "..", "References", file);
        string dst = Path.Combine(Application.dataPath, "RenderTemplate", "References", file);
        if (!File.Exists(src) || File.Exists(dst)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(src, dst);
    }

    // 세로 홈(플루팅) 노멀맵: 한 장에 홈 8개, 가로로 반복해서 씀
    internal static Texture2D FluteNormal()
    {
        string path = Gen + "/FluteNormal.png";
        if (!File.Exists(path))
        {
            const int W = 256, H = 16, N = 8;
            var t = new Texture2D(W, H, TextureFormat.RGB24, false, true);
            for (int x = 0; x < W; x++)
            {
                float u = (x * N / (float)W) % 1f;                 // 홈 하나 안에서 0~1
                float s = Mathf.Clamp((u - 0.5f) * 2f, -0.98f, 0.98f);
                float slope = -s / Mathf.Sqrt(1 - s * s) * 0.35f;  // 둥근 볼록 면의 기울기
                var n = new Vector3(slope, 0, 1).normalized;
                var c = new Color(n.x * 0.5f + 0.5f, 0.5f, n.z * 0.5f + 0.5f);
                for (int y = 0; y < H; y++) t.SetPixel(x, y, c);
            }
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.NormalMap; imp.wrapMode = TextureWrapMode.Repeat; imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 텍스처 색 바꾸기: 밝기 기준으로 bg↔fg 사이를 칠함 (invert면 어두운 곳이 fg)
    // keepColor: 빨강·파랑 아이콘처럼 채도 높은 픽셀은 원래 색 유지
    internal static Texture2D Recolor(Texture2D src, string name, Color bg, Color fg, bool invert, bool keepColor = false, float curve = 1f)
    {
        if (!src) return null;
        string safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');
        string path = Gen + "/" + safe + ".png";
        AssetDatabase.DeleteAsset(path);
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); t.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = t.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            Color.RGBToHSV(c, out _, out float sat, out float val);
            if (keepColor && sat > 0.35f && val > 0.25f) continue;
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            float k = Mathf.Pow(Mathf.Clamp01(invert ? 1 - lum : lum), curve);   // curve>1 이면 결이 더 연해짐
            var o = Color.Lerp(bg, fg, k); o.a = c.a;
            px[i] = o;
        }
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
