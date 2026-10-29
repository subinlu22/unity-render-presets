using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// "실사 카페" 프리셋에 필요한 것 만들기 (RenderTemplateBuilder.Build 안에서 불림)
// ① 고퀄 재질 세트: 모델 원본 재질을 복제해서 성질별로 조정 (원본은 그대로)
//    스테인리스·유리·석재 상판·나무 하부장·광택 도장·키오스크 화면(웹 스크린샷 발광)
// ② 룸 세트 프리팹: 나무 바닥 + 회벽 (이 프리셋에서만 보임)
// ③ 프리셋: HDRI 배경(실내) + 창가 햇빛 + 펜던트 + 실사용 포스트(AO·Bloom·DOF·그레인)
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

    public static GameObject BuildRoom(Bounds model)
    {
        var floorM = StdMat("Room_WoodFloor", "wood_floor/wood_floor_diff_1k.jpg", "wood_floor/wood_floor_nor_gl_1k.jpg", new Vector2(6, 6), 0.45f);
        var wallM = StdMat("Room_PlasterWall", "painted_plaster_wall/painted_plaster_wall_diff_1k.jpg", "painted_plaster_wall/painted_plaster_wall_nor_gl_1k.jpg", new Vector2(3, 1.5f), 0.1f);
        var root = new GameObject("Room_RealCafe");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor"; floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(model.center.x, model.min.y - 0.002f, model.center.z + 1.5f);
        floor.transform.localScale = new Vector3(1.2f, 1, 1.2f);       // 12m x 12m
        floor.GetComponent<Renderer>().sharedMaterial = floorM;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
        wall.name = "BackWall"; wall.transform.SetParent(root.transform);
        wall.transform.position = new Vector3(model.center.x, model.min.y + 1.6f, model.min.z - 0.03f);
        wall.transform.localScale = new Vector3(8f, 3.2f, 1f);
        wall.GetComponent<Renderer>().sharedMaterial = wallM;          // Quad 앞면이 -z를 보므로 카메라(+z) 쪽에서 보이게 회전
        wall.transform.rotation = Quaternion.Euler(0, 180, 0);
        foreach (var col in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        string path = Root + "/Presets/Room_RealCafe.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static void BuildPreset(Material sky, PostProcessProfile pp, GameObject room)
    {
        string path = Root + "/Resources/Looks/Look_05_RealCafe.asset";
        var l = Load<LookPreset>(path);
        l.displayName = "실사 카페"; l.order = 5;
        l.note = "실내 HDRI 배경·창가 햇빛·펜던트, 고퀄 재질(스테인리스·석재·나무·유리), 키오스크 화면 발광";
        l.skybox = sky; l.showSkybox = true; l.ambientIntensity = 0.9f; l.reflectionIntensity = 1f;
        l.originalLightScale = 0f; l.post = pp; l.roomPrefab = room; l.hqMaterials = true;
        l.reflectionProbe = true; l.probeSize = new Vector3(5, 3, 4); l.probeIntensity = 1f;
        l.fog = false;
        Vector3 win = new Vector3(2.2f, 1.8f, 1.6f);                   // 왼쪽 앞 위 창문 방향
        Vector3 pend = new Vector3(-0.1f, 1.3f, 0.5f);
        l.lights = new[]
        {
            new LookPreset.RigLight { name = "Window Sun (따뜻)", type = LightType.Directional, color = new Color(1f, 0.9f, 0.76f),
                intensity = 1.1f, position = win, euler = Quaternion.LookRotation(-win).eulerAngles, shadows = true },
            new LookPreset.RigLight { name = "Pendant", type = LightType.Spot, color = new Color(1f, 0.82f, 0.6f),
                intensity = 2.5f, position = pend, euler = Quaternion.LookRotation(-pend).eulerAngles, range = 4f, spotAngle = 70f, shadows = true },
        };
        EditorUtility.SetDirty(l);
    }

    public static PostProcessProfile BuildPost(string presetDir, float focusDist)
    {
        string path = presetDir + "/PP_RealCafe.asset";
        AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(p, path);
        var ao = p.AddSettings<AmbientOcclusion>(); ao.enabled.Override(true);
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance); ao.intensity.Override(0.9f);
        var cg = p.AddSettings<ColorGrading>(); cg.enabled.Override(true);
        cg.tonemapper.Override(Tonemapper.ACES); cg.postExposure.Override(0.15f);
        cg.contrast.Override(10f); cg.saturation.Override(4f); cg.temperature.Override(4f);
        var bl = p.AddSettings<Bloom>(); bl.enabled.Override(true);
        bl.threshold.Override(1.0f); bl.intensity.Override(1.2f); bl.softKnee.Override(0.5f);
        var dof = p.AddSettings<DepthOfField>(); dof.enabled.Override(true);
        dof.focusDistance.Override(focusDist); dof.aperture.Override(4f); dof.focalLength.Override(45f);
        var vg = p.AddSettings<Vignette>(); vg.enabled.Override(true); vg.intensity.Override(0.22f); vg.smoothness.Override(0.5f);
        var gr = p.AddSettings<Grain>(); gr.enabled.Override(true); gr.intensity.Override(0.12f); gr.size.Override(0.8f);
        var ca = p.AddSettings<ChromaticAberration>(); ca.enabled.Override(true); ca.intensity.Override(0.04f);
        foreach (var s in p.settings) { s.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector; AssetDatabase.AddObjectToAsset(s, p); }
        EditorUtility.SetDirty(p);
        return p;
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

    static Material StdMat(string name, string diff, string nor, Vector2 tiling, float smooth)
    {
        string path = Root + "/Presets/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_MainTex", T(diff)); m.mainTextureScale = tiling;
        var n = T(nor);
        if (n) { m.SetTexture("_BumpMap", n); m.SetTextureScale("_BumpMap", tiling); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
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
