using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using P = ReferenceSetup.Part;
using S = ReferenceSetup.Surface;

// 메뉴 RenderTemplate > Reference Matcher
// 레퍼런스 사진을 넣고 → 사진 위에 벽·상판·하부장·기계 영역을 드래그로 지정 → "프리셋 만들기"
// 툴이 하는 일:
//  ① 영역마다 색을 뽑음 (중앙값, 반사 하이라이트에 덜 휘둘림)
//  ② 고른 재질 종류(대리석·원목·페인트…)에 그 색을 입혀 재질 생성 → 프리셋 전용 재질 세트
//  ③ 벽의 왼쪽/오른쪽 밝기 차이로 주 조명 방향을 정함
//  ④ 사진과 렌더의 밝기·대비·색온도·채도를 비교해 포스트 값을 자동 보정 (여러 번 반복)
// 모델은 그대로, 색·재질·조명·톤만 바꿈
public class ReferenceMatcher : EditorWindow
{
    const string Root = "Assets/RenderTemplate";
    ReferenceSetup _setup;
    int _active = -1;                            // 지금 영역을 그리는 슬롯
    Vector2 _dragStart; bool _dragging;
    Vector2 _scroll;
    static string _status = "";

    [MenuItem("RenderTemplate/Reference Matcher")]
    public static void Open() => GetWindow<ReferenceMatcher>("Reference Matcher");

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        _setup = (ReferenceSetup)EditorGUILayout.ObjectField("설정 파일", _setup, typeof(ReferenceSetup), false);
        if (!_setup)
        {
            if (GUILayout.Button("새 설정 만들기")) _setup = CreateSetup("RefSetup_New");
            EditorGUILayout.HelpBox("설정 파일을 고르거나 새로 만드세요. 레퍼런스 사진은 png/jpg로 Assets 안에 넣어야 합니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }
        var so = new SerializedObject(_setup);
        EditorGUILayout.PropertyField(so.FindProperty("reference"), new GUIContent("레퍼런스 사진"));
        EditorGUILayout.PropertyField(so.FindProperty("presetName"), new GUIContent("프리셋 이름"));
        EditorGUILayout.PropertyField(so.FindProperty("basePreset"), new GUIContent("시작 프리셋 (조명·카메라)"));
        EditorGUILayout.PropertyField(so.FindProperty("rules"), new GUIContent("프리셋 규칙"));
        so.ApplyModifiedProperties();

        // ---- 사진 + 영역 그리기
        var tex = _setup.reference;
        if (tex)
        {
            float w = position.width - 20, h = w * tex.height / tex.width;
            Rect r = GUILayoutUtility.GetRect(w, h);
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            for (int i = 0; i < _setup.slots.Length; i++)
            {
                var s = _setup.slots[i];
                if (!s.use || s.rect.width <= 0) continue;
                var rr = new Rect(r.x + s.rect.x * r.width, r.y + s.rect.y * r.height, s.rect.width * r.width, s.rect.height * r.height);
                Handles.DrawSolidRectangleWithOutline(rr, new Color(1, 1, 1, 0.08f), i == _active ? Color.yellow : Color.cyan);
                GUI.Label(new Rect(rr.x + 2, rr.y + 1, 120, 18), s.part.ToString(), EditorStyles.whiteBoldLabel);
            }
            HandleDrag(r);
            EditorGUILayout.HelpBox(_active >= 0 ? $"사진 위에서 드래그해서 [{_setup.slots[_active].part}] 영역을 지정하세요" : "아래에서 '영역 지정'을 누르고 사진 위를 드래그", MessageType.None);
        }

        // ---- 슬롯 목록
        EditorGUILayout.LabelField("영역과 재질 종류", EditorStyles.boldLabel);
        for (int i = 0; i < _setup.slots.Length; i++)
        {
            var s = _setup.slots[i];
            EditorGUILayout.BeginHorizontal();
            s.use = EditorGUILayout.ToggleLeft(s.part.ToString(), s.use, GUILayout.Width(90));
            using (new EditorGUI.DisabledScope(!s.use))
            {
                s.surface = (S)EditorGUILayout.EnumPopup(s.surface, GUILayout.Width(100));
                EditorGUILayout.ColorField(GUIContent.none, s.sampled, false, false, false, GUILayout.Width(40));
                EditorGUIUtility.labelWidth = 34;
                s.wear = EditorGUILayout.Slider("낡음", s.wear, 0, 1, GUILayout.Width(150));
                s.stickers = EditorGUILayout.IntField(s.part == P.벽 ? "포스터" : "스티커", s.stickers, GUILayout.Width(80));
                EditorGUIUtility.labelWidth = 0;
                var old = GUI.backgroundColor; if (i == _active) GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("영역 지정", GUILayout.Width(70))) _active = i == _active ? -1 : i;
                GUI.backgroundColor = old;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUI.changed) EditorUtility.SetDirty(_setup);

        if (GUILayout.Button("규칙대로 기본값 채우기 (시작 조명·부위별 더티)")) ApplyRuleDefaults(_setup);
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!tex || (!_setup.basePreset && !RulesOf(_setup).baseLighting)))
        {
            if (GUILayout.Button("프리셋 만들기 + 톤 자동 맞추기", GUILayout.Height(28))) { Generate(_setup); ToneMatch(_setup, 6); }
            using (new EditorGUI.DisabledScope(!_setup.output))
                if (GUILayout.Button("톤만 다시 맞추기")) ToneMatch(_setup, 4);
        }
        if (_setup.output) EditorGUILayout.ObjectField("만들어진 프리셋", _setup.output, typeof(LookPreset), false);
        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
        if (!string.IsNullOrEmpty(_setup.log)) EditorGUILayout.HelpBox(_setup.log, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    void HandleDrag(Rect img)
    {
        if (_active < 0) return;
        var e = Event.current;
        Vector2 n(Vector2 p) => new Vector2(Mathf.Clamp01((p.x - img.x) / img.width), Mathf.Clamp01((p.y - img.y) / img.height));
        if (e.type == EventType.MouseDown && img.Contains(e.mousePosition)) { _dragStart = n(e.mousePosition); _dragging = true; e.Use(); }
        if (_dragging && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
        {
            var a = _dragStart; var b = n(e.mousePosition);
            var s = _setup.slots[_active];
            s.rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            s.use = true;
            if (e.type == EventType.MouseUp) { _dragging = false; s.sampled = Sample(_setup.reference, s.rect); EditorUtility.SetDirty(_setup); }
            e.Use(); Repaint();
        }
    }

    // ---------------------------------------------------------------- 규칙
    static LookRules R;                                                   // 지금 생성에 쓰는 규칙
    const string DefaultRulesPath = Root + "/Rules/LookRules_Default.asset";
    public static LookRules DefaultRules()
    {
        var r = AssetDatabase.LoadAssetAtPath<LookRules>(DefaultRulesPath);
        if (r) return r;
        System.IO.Directory.CreateDirectory(Root + "/Rules"); AssetDatabase.Refresh();
        r = CreateInstance<LookRules>();
        r.baseLighting = AssetDatabase.LoadAssetAtPath<LookPreset>(Root + "/Resources/Looks/Look_30_WhiteLounge.asset");
        AssetDatabase.CreateAsset(r, DefaultRulesPath); AssetDatabase.SaveAssets();
        return r;
    }
    static LookRules RulesOf(ReferenceSetup s) => s.rules ? s.rules : DefaultRules();

    // 규칙대로 기본값 채우기: 시작 조명 + 부위별 더티 기본값 (영역·재질 종류는 그대로)
    public static void ApplyRuleDefaults(ReferenceSetup s)
    {
        var r = RulesOf(s);
        if (r.baseLighting) s.basePreset = r.baseLighting;
        foreach (var sl in s.slots) sl.wear = r.DefaultWear(sl.part);
        EditorUtility.SetDirty(s);
    }

    public static ReferenceSetup CreateSetup(string name)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/References/" + name + ".asset");
        var s = CreateInstance<ReferenceSetup>();
        s.basePreset = AssetDatabase.LoadAssetAtPath<LookPreset>(Root + "/Resources/Looks/Look_30_WhiteLounge.asset");
        s.rules = DefaultRules();
        ApplyRuleDefaults(s);
        AssetDatabase.CreateAsset(s, path);
        return s;
    }

    // ================================================================ ① 색 뽑기
    static Color[] Pixels(Texture2D src, out int w, out int h)
    {
        w = src.width; h = src.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var t = Read(rt);
        RenderTexture.ReleaseTemporary(rt);
        var px = t.GetPixels(); DestroyImmediate(t);
        return px;
    }
    static Texture2D Read(RenderTexture rt)
    {
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
        RenderTexture.active = prev;
        return t;
    }

    // rect는 왼쪽 위 기준 → 텍스처는 아래쪽이 y=0 이라 뒤집어서 읽음. 채널별 중앙값.
    public static Color Sample(Texture2D tex, Rect rect)
    {
        var px = Pixels(tex, out int w, out int h);
        var rs = new List<float>(); var gs = new List<float>(); var bs = new List<float>();
        int x0 = (int)(rect.xMin * w), x1 = Mathf.Max(x0 + 1, (int)(rect.xMax * w));
        int y0 = (int)((1 - rect.yMax) * h), y1 = Mathf.Max(y0 + 1, (int)((1 - rect.yMin) * h));
        for (int y = y0; y < y1; y += 2) for (int x = x0; x < x1; x += 2)
        { var c = px[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)]; rs.Add(c.r); gs.Add(c.g); bs.Add(c.b); }
        float Med(List<float> l) { l.Sort(); return l.Count == 0 ? 0.5f : l[l.Count / 2]; }
        return new Color(Med(rs), Med(gs), Med(bs));
    }

    // ================================================================ ② 재질·프리셋 만들기
    public static void Generate(ReferenceSetup setup)
    {
        R = RulesOf(setup);
        if (!setup.basePreset) setup.basePreset = R.baseLighting;
        foreach (var s in setup.slots) if (s.use && s.rect.width > 0) s.sampled = Sample(setup.reference, s.rect);
        string id = "Ref_" + setup.name.Replace("RefSetup_", "");          // 설정 파일 이름 기준 → 다시 만들어도 같은 파일 덮어씀
        string dir = Root + "/Materials/" + id;
        System.IO.Directory.CreateDirectory(dir); AssetDatabase.Refresh();

        var basePreset = setup.basePreset;
        var baseSet = basePreset.materialSet;
        var pairs = baseSet.pairs.ToList();
        var src = AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/cafesim_a100.glb").OfType<Material>().ToArray();

        foreach (var s in setup.slots.Where(s => s.use && s.rect.width > 0))
            foreach (int i in Indices(s.part))
            {
                var from = src[i];
                int k = pairs.FindIndex(p => p.from == from);
                var baseMat = k >= 0 ? pairs[k].to : from;
                string path = $"{dir}/{id}_{s.part}_{i:00}.mat";
                AssetDatabase.DeleteAsset(path);
                var m = new Material(baseMat);
                Color c = R.ClampAlbedo(s.sampled);                         // 규칙: 실제 반사율 범위로
                if (System.Array.IndexOf(ReferenceLooks.Dividers, i) >= 0) c *= 0.7f;     // 하부장 칸막이는 조금 어둡게
                ApplySurface(m, s.surface, c, $"{id}_{s.part}");
                float wear = Mathf.Min(s.wear, R.wearMax);                   // 규칙: 더티 최대값
                if (wear > 0.01f) Weathering.Apply(m, $"{id}_{s.part}_{i:00}", wear, peel: s.part == P.하부장 && s.surface == S.무광페인트, seed: i);
                AssetDatabase.CreateAsset(m, path);
                var pair = new MaterialSet.Pair { from = from, to = m };
                if (k >= 0) pairs[k] = pair; else pairs.Add(pair);
            }

        // 디스펜서 본체가 어두우면 앞판 인쇄를 밝은 글씨로 반전 (다크 라운지와 같은 방식)
        foreach (var part in new[] { P.제빙기, P.컵디스펜서 })
        {
            var s = setup.slots.FirstOrDefault(x => x.part == part && x.use && x.rect.width > 0);
            if (s == null) continue;
            bool dark = s.sampled.grayscale < 0.35f;
            foreach (int i in part == P.제빙기 ? new[] { 2, 3, 22 } : new[] { 8, 9 })
            {
                int k = pairs.FindIndex(p => p.from == src[i]);
                var hq = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/HQ/HQ_{i:00}.mat");
                var m = new Material(hq ? hq : src[i]);
                var t = src[i].GetTexture("baseColorTexture") as Texture2D;
                if (t) m.SetTexture("baseColorTexture", dark
                    ? ReferenceLooks.Recolor(t, $"{id}_print_{i}", s.sampled, new Color(0.88f, 0.88f, 0.9f), invert: true, keepColor: true)
                    : ReferenceLooks.Recolor(t, $"{id}_print_{i}", s.sampled, new Color(0.15f, 0.15f, 0.16f), invert: true, keepColor: true));
                string path = $"{dir}/{id}_print_{i:00}.mat";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
                var pair = new MaterialSet.Pair { from = src[i], to = m };
                if (k >= 0) pairs[k] = pair; else pairs.Add(pair);
            }
        }

        string setPath = Root + "/Presets/MaterialSet_" + id + ".asset";
        var set = AssetDatabase.LoadAssetAtPath<MaterialSet>(setPath);
        if (!set) { set = CreateInstance<MaterialSet>(); AssetDatabase.CreateAsset(set, setPath); }
        set.pairs = pairs.ToArray(); EditorUtility.SetDirty(set);

        // 포스트 프로필: 시작 프리셋 것을 복제 (톤 맞추기로 이 복제본만 바뀜)
        string ppPath = Root + "/Presets/PP_" + id + ".asset";
        AssetDatabase.DeleteAsset(ppPath);
        var pp = CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(pp, ppPath);
        foreach (var st in basePreset.post.settings)
        {
            var c = Instantiate(st); c.name = st.name;
            c.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            pp.settings.Add(c); AssetDatabase.AddObjectToAsset(c, pp);
        }
        if (!pp.TryGetSettings(out ColorGrading cg0))
        {
            cg0 = pp.AddSettings<ColorGrading>(); cg0.tonemapper.Override(Tonemapper.ACES);
            cg0.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector; AssetDatabase.AddObjectToAsset(cg0, pp);
        }
        cg0.enabled.Override(true);
        // AO는 장면이 작아서(기계 높이 약 0.8m) 세게 걸면 벽에 검은 번짐이 생김 → 약하게 고정
        if (pp.TryGetSettings(out AmbientOcclusion ao0)) { ao0.intensity.Override(R.aoIntensity); ao0.thicknessModifier.Override(1f); }
        if (pp.TryGetSettings(out Vignette vg0)) vg0.intensity.Override(Mathf.Min(vg0.intensity.value, R.vignetteMax));
        EditorUtility.SetDirty(pp);

        // 프리셋: 시작 프리셋 복제 → 재질 세트·포스트·레퍼런스 교체
        string looksDir = Root + "/Resources/Looks/";
        string lookPath = setup.output ? AssetDatabase.GetAssetPath(setup.output) : AssetDatabase.GenerateUniqueAssetPath(looksDir + "Look_50_" + id + ".asset");
        var look = Instantiate(basePreset);
        AssetDatabase.DeleteAsset(lookPath); AssetDatabase.CreateAsset(look, lookPath);
        look.displayName = setup.presetName; look.order = 50; look.materialSet = set; look.post = pp; look.reference = setup.reference;
        look.hqMaterials = true; look.contactShadow = R.contactShadow;
        look.webEnvScale = R.webEnvScale; look.webLightScale = R.webLightScale;
        look.note = "Reference Matcher로 생성: 영역 색 추출 + 재질 종류 + 톤 자동 보정";

        // ③ 벽 영역의 왼쪽/오른쪽 밝기 차이 → 주 조명(첫 번째 Directional)을 밝은 쪽으로
        var wall = setup.slots.FirstOrDefault(x => x.part == P.벽 && x.use && x.rect.width > 0);
        if (wall != null && look.lights != null)
        {
            var r = wall.rect;
            float left = Sample(setup.reference, new Rect(r.x, r.y, r.width * 0.3f, r.height)).grayscale;
            float right = Sample(setup.reference, new Rect(r.xMax - r.width * 0.3f, r.y, r.width * 0.3f, r.height)).grayscale;
            for (int i = 0; i < look.lights.Length; i++)
                if (look.lights[i].type == LightType.Directional)
                {
                    var L = look.lights[i]; var p = L.position;
                    // 화면 왼쪽 = +x (카메라가 -z를 봄). 사진 왼쪽이 밝으면 +x 쪽에서 비춤
                    p.x = Mathf.Abs(p.x) * (left >= right ? 1 : -1);
                    L.position = p; L.euler = Quaternion.LookRotation(-p).eulerAngles;
                    look.lights[i] = L;
                    setup.log = $"조명 방향: 벽 왼쪽 밝기 {left:0.00} / 오른쪽 {right:0.00} → {(left >= right ? "왼쪽" : "오른쪽")}에서 비춤\n";
                    break;
                }
        }
        EditorUtility.SetDirty(look); EditorUtility.SetDirty(set); EditorUtility.SetDirty(pp);
        AssetDatabase.SaveAssets();                                         // 스티커 세트가 에셋을 다시 읽기 전에 먼저 저장 (안 하면 변경이 날아감)
        var dressing = Dressing.Build(setup, id);
        look.roomPrefab = dressing;
        EditorUtility.SetDirty(look);
        setup.output = AssetDatabase.LoadAssetAtPath<LookPreset>(lookPath);
        EditorUtility.SetDirty(setup);
        AssetDatabase.SaveAssets();
    }

    static int[] Indices(P part)
    {
        switch (part)
        {
            case P.벽: return new[] { ReferenceLooks.Wall };
            case P.바닥: return new[] { ReferenceLooks.Floor };
            case P.상판: return new[] { ReferenceLooks.Counter };
            case P.하부장: return new[] { ReferenceLooks.Cabinet }.Concat(ReferenceLooks.Dividers).ToArray();
            case P.커피머신: return ReferenceLooks.BigBody;
            case P.제빙기: return ReferenceLooks.IceBody;
            default: return ReferenceLooks.CupBody;
        }
    }

    // 재질 종류별: 뽑은 색을 그대로 쓰거나(단색), 텍스처 평균색이 그 색이 되도록 틴트
    static void ApplySurface(Material m, S surface, Color c, string texName)
    {
        const string TP = Root + "/ThirdParty/";
        void Plain(float metal, float rough)
        {
            ReferenceLooks.Pbr(m, c, metal, rough);
            m.SetTexture("baseColorTexture", null);                // 비우면 셰이더 기본(흰색) → 색만 남음
            if (m.HasProperty("normalTexture")) { m.SetTexture("normalTexture", null); m.DisableKeyword("_NORMALMAP"); }
        }
        void Tinted(Texture2D tex, float rough)
        {
            // 나뭇결(밝기 차이)은 그대로 두고 색만 뽑은 색 근처로 다시 칠함 (어두운 결 ~ 밝은 면)
            var t = ReferenceLooks.Recolor(tex, texName + "_" + tex.name, c * 0.55f, Color.Lerp(c, Color.white, 0.25f), invert: false);
            ReferenceLooks.Pbr(m, Color.white, 0, rough);
            ReferenceLooks.Tex(m, t, new Vector2(3, 1));
        }
        var marble = ReferenceLooks.T(TP + "AmbientCG/Marble012/Marble012_1K-JPG_Color.jpg");
        var marbleN = ReferenceLooks.T(TP + "AmbientCG/Marble012/Marble012_1K-JPG_NormalGL.jpg");
        switch (surface)
        {
            case S.무광페인트: Plain(R.paint.metalness, R.paint.roughness); break;
            case S.광택도장: Plain(R.gloss.metalness, R.gloss.roughness); break;
            case S.금속: Plain(R.metal.metalness, R.metal.roughness); break;
            case S.회벽:
                Plain(R.plaster.metalness, R.plaster.roughness);
                ReferenceLooks.Nrm(m, ReferenceLooks.T(TP + "PolyHaven/Textures/painted_plaster_wall/painted_plaster_wall_nor_gl_1k.jpg"), 0.6f, new Vector2(4, 2));
                break;
            case S.흰대리석:
            case S.어두운대리석:
            {
                bool dark = surface == S.어두운대리석;
                var vein = dark ? Color.Lerp(c, new Color(0.7f, 0.66f, 0.6f), 0.6f) : c * 0.6f;
                var tex = ReferenceLooks.Recolor(marble, texName + "_marble", c, vein, invert: true, curve: dark ? 1f : 1.3f);
                ReferenceLooks.Pbr(m, Color.white, R.marble.metalness, R.marble.roughness);
                ReferenceLooks.Tex(m, tex, new Vector2(3f, 0.9f)); ReferenceLooks.Nrm(m, marbleN, 0.4f, new Vector2(3f, 0.9f));
                break;
            }
            case S.원목:
                Tinted(ReferenceLooks.T(TP + "PolyHaven/Textures/wood_table_001/wood_table_001_diff_1k.jpg"), R.wood.roughness);
                ReferenceLooks.Nrm(m, ReferenceLooks.T(TP + "PolyHaven/Textures/wood_table_001/wood_table_001_nor_gl_1k.jpg"), 1f, new Vector2(3, 1));
                break;
            case S.테라조:
                ReferenceLooks.Pbr(m, Color.white, R.terrazzo.metalness, R.terrazzo.roughness);
                ReferenceLooks.Tex(m, Terrazzo(texName + "_terrazzo", c), new Vector2(4f, 1.2f));
                if (m.HasProperty("normalTexture")) { m.SetTexture("normalTexture", null); m.DisableKeyword("_NORMALMAP"); }
                break;
            case S.오크세로홈:
                Tinted(ReferenceLooks.T(TP + "PolyHaven/Textures/white_oak_veneer/white_oak_veneer_diff_1k.jpg"), R.oakFluted.roughness);
                ReferenceLooks.Nrm(m, ReferenceLooks.FluteNormal(), 1.4f, new Vector2(32, 1));
                break;
        }
    }

    // 테라조: 바탕색 위에 회색·검정·빨강·초록 돌 조각을 흩뿌린 텍스처 (매번 같은 무늬)
    static Texture2D Terrazzo(string name, Color bg)
    {
        const int N = 512;
        var t = new Texture2D(N, N, TextureFormat.RGB24, false);
        var px = new Color[N * N];
        var rnd = new System.Random(7);
        for (int i = 0; i < px.Length; i++) { float n = (float)rnd.NextDouble() * 0.04f - 0.02f; px[i] = new Color(bg.r + n, bg.g + n, bg.b + n); }
        Color[] chips = { new Color(0.55f, 0.55f, 0.55f), new Color(0.25f, 0.25f, 0.26f), new Color(0.72f, 0.7f, 0.66f),
                          new Color(0.6f, 0.32f, 0.25f), new Color(0.42f, 0.5f, 0.4f), new Color(0.82f, 0.78f, 0.7f) };
        for (int k = 0; k < 900; k++)
        {
            int cx = rnd.Next(N), cy = rnd.Next(N); float r = 1.5f + (float)rnd.NextDouble() * (k % 9 == 0 ? 9f : 4f);
            var col = chips[rnd.Next(chips.Length)];
            for (int y = -(int)r - 1; y <= r + 1; y++) for (int x = -(int)r - 1; x <= r + 1; x++)
                if (x * x * 0.8f + y * y * 1.2f < r * r) px[((cy + y + N) % N) * N + (cx + x + N) % N] = col;   // 살짝 찌그러진 조각, 이음매 없이 반복
        }
        t.SetPixels(px); t.Apply();
        string path = Root + "/Generated/" + string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars())) + ".png";
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG()); DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ================================================================ ④ 톤 자동 맞추기
    struct Stats { public float lum, contrast, sat, warm; }

    // 밝기 = 중앙값(창문 같은 아주 밝은 곳에 덜 휘둘림), 대비 = 밝은 쪽(90%)과 어두운 쪽(10%) 차이
    // 채도 = 픽셀 평균 (최대-최소 채널), 따뜻함 = 중간 밝기 픽셀의 (빨강-파랑) 평균
    static Stats Measure(Color[] px)
    {
        var ys = new float[px.Length];
        double s = 0, wsum = 0; int wn = 0;
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            float y = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            ys[i] = y;
            s += Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            if (y > 0.2f && y < 0.85f) { wsum += c.r - c.b; wn++; }
        }
        System.Array.Sort(ys);
        float P(float q) => ys[Mathf.Clamp((int)(q * ys.Length), 0, ys.Length - 1)];
        return new Stats { lum = P(0.5f), contrast = P(0.9f) - P(0.1f), sat = (float)(s / px.Length), warm = wn > 0 ? (float)(wsum / wn) : 0 };
    }

    static string Fmt(Stats s) => $"밝기 {s.lum:0.000} 대비 {s.contrast:0.000} 채도 {s.sat:0.000} 따뜻함 {s.warm:+0.000;-0.000}";
    static float Err(Stats a, Stats b) => Mathf.Abs(a.lum - b.lum) + Mathf.Abs(a.contrast - b.contrast) + Mathf.Abs(a.sat - b.sat) + Mathf.Abs(a.warm - b.warm);

    // 렌더 → 측정 → 포스트 값 보정 을 iterations번 반복. 프리셋 적용 뒤 주변광이 갱신되도록 에디터 프레임을 기다리며 진행
    public static void ToneMatch(ReferenceSetup setup, int iterations)
    {
        R = RulesOf(setup);
        var sw = FindFirstObjectByType<LookSwitcher>();
        if (!sw || !setup.output) { _status = "씬에 LookSwitcher가 없거나 프리셋이 아직 없음"; return; }
        sw.LoadPresets();
        string outPath = AssetDatabase.GetAssetPath(setup.output);
        int idx = sw.presets.FindIndex(p => p && AssetDatabase.GetAssetPath(p) == outPath);   // 같은 순서 번호가 여럿이어도 파일로 정확히 찾음
        if (idx < 0) { _status = "프리셋이 Resources/Looks 안에 없음"; return; }
        // 안전장치: 이 설정이 만든 전용 포스트 파일(PP_Ref_*)만 고침. 다른 프리셋(화이트 라운지 등) 값은 절대 안 건드림
        if (!setup.output.post || !setup.output.post.name.StartsWith("PP_Ref_")) { _status = "전용 포스트 파일이 아님 → 톤 맞추기 중단 (프리셋 만들기를 먼저)"; Debug.LogWarning("[ReferenceMatcher] " + _status); return; }
        var refPx = Pixels(setup.reference, out _, out _);
        var target = Measure(refPx);
        var pp = setup.output.post;
        pp.TryGetSettings(out ColorGrading cg);
        foreach (var p in new ParameterOverride[] { cg.postExposure, cg.temperature, cg.contrast, cg.saturation }) p.overrideState = true;
        float exp0 = cg.postExposure.value, con0 = cg.contrast.value, sat0 = cg.saturation.value, tmp0 = cg.temperature.value;     // 시작 프리셋 값 (이 근처에서만 움직임)

        string log = (setup.log ?? "").Split('\n')[0] + "\n레퍼런스: " + Fmt(target) + "\n";
        int iter = 0, wait = 0; float firstErr = -1;
        EditorApplication.CallbackFunction tick = null;
        sw.Select(idx); DynamicGI.UpdateEnvironment();
        tick = () =>
        {
            if (sw.Index != idx) { sw.Select(idx); wait = 0; return; }          // 다른 프리셋이 떠 있으면 다시 고르고 기다림
            if (++wait < 25) return;
            wait = 0;
            var now = Measure(RenderSmall(sw.cam));
            float e = Err(now, target);
            if (firstErr < 0) firstErr = e;
            log += $"{iter}회: {Fmt(now)}  (오차 {e:0.000})\n";
            if (iter >= iterations)
            {
                log += $"→ 오차 {firstErr:0.000} → {e:0.000}\n최종 포스트: 노출 {cg.postExposure.value:+0.00;-0.00} 색온도 {cg.temperature.value:+0;-0} 대비 {cg.contrast.value:+0;-0} 채도 {cg.saturation.value:+0;-0}";
                setup.log = log; _status = "톤 맞추기 완료";
                EditorUtility.SetDirty(pp); EditorUtility.SetDirty(setup); AssetDatabase.SaveAssets();
                EditorApplication.update -= tick;
                Debug.Log("[ReferenceMatcher] " + log.Replace("\n", " | "));
                foreach (var w in Resources.FindObjectsOfTypeAll<ReferenceMatcher>()) w.Repaint();
                return;
            }
            // 화면 밝기는 감마 공간이라 노출(EV)로 바꿀 때 약 2.2배, 한 번에 너무 크게 안 움직이게 줄여서 반영
            // 밝기·대비는 조금만 (시작 프리셋의 조명 톤을 지켜야 실사처럼 그림자가 남음), 색온도·채도는 사진 쪽으로
            cg.postExposure.value = Mathf.Clamp(cg.postExposure.value + Mathf.Clamp(Mathf.Log(target.lum / Mathf.Max(now.lum, 0.01f), 2) * 0.8f, -0.25f, 0.25f), exp0 - R.exposureRange, exp0 + R.exposureRange);
            cg.temperature.value = Mathf.Clamp(cg.temperature.value + Mathf.Clamp((target.warm - now.warm) * 80f, -5f, 5f), tmp0 - R.temperatureRange, tmp0 + R.temperatureRange);
            cg.contrast.value = Mathf.Clamp(cg.contrast.value + Mathf.Clamp((target.contrast / Mathf.Max(now.contrast, 0.01f) - 1f) * 30f, -4f, 4f), con0 - R.contrastRange, con0 + R.contrastRange);
            cg.saturation.value = Mathf.Clamp(cg.saturation.value + Mathf.Clamp((target.sat / Mathf.Max(now.sat, 0.01f) - 1f) * 20f, -5f, 5f), sat0 - R.saturationRange, sat0 + R.saturationRange);
            EditorUtility.SetDirty(pp);
            iter++;
            _status = $"톤 맞추는 중… {iter}/{iterations}";
        };
        EditorApplication.update += tick;
        _status = "톤 맞추는 중…";
    }

    static Color[] RenderSmall(Camera cam)
    {
        var rt = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var old = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
        var t = Read(rt); var px = t.GetPixels();
        DestroyImmediate(t); rt.Release(); DestroyImmediate(rt);
        return px;
    }
}
