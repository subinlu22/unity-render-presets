using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

// 메뉴 RenderTemplate > Build Demo Scene
// 하는 일: ① HDRI 스카이 재질·포스트 프로필·프리셋(스튜디오, 실사 카페, 화이트·다크 라운지) 만들기
//         ② 데모 씬 만들기: 커피머신 모델(glb) + 웹과 같은 원래 조명·카메라 + 템플릿 오브젝트
//         ③ 원본/프리셋별 미리보기 이미지 저장 (Screenshots/preview_*.png)
// 좌표: 웹(three.js)에서 가져온 값은 x 부호만 바꿈 (glTF 불러올 때 같은 방식으로 뒤집힘)
public static class RenderTemplateBuilder
{
    const string Root = "Assets/RenderTemplate";
    const string PresetDir = Root + "/Presets";
    const string LookDir = Root + "/Resources/Looks";
    const string ScenePath = Root + "/Scenes/CafeSimRender.unity";
    const string ModelPath = Root + "/Models/cafesim_a100.glb";
    const string HdriDir = Root + "/ThirdParty/PolyHaven";

    static Vector3 Web(float x, float y, float z) => new Vector3(-x, y, z);
    static readonly Vector3 Target = Web(-0.15f, 0.32f, 0f);      // 웹 overview 시점이 보는 곳
    static readonly Vector3 CamPos = Web(-0.15f, 0.62f, 2.05f);

    [MenuItem("RenderTemplate/Build Demo Scene")]
    public static void Build()
    {
        foreach (var d in new[] { PresetDir, LookDir, Root + "/Scenes" }) Directory.CreateDirectory(d);
        AssetDatabase.Refresh();

        var studioSky = Sky("Sky_Studio", HdriDir + "/studio_small_09_1k.hdr");
        float focusDist = Vector3.Distance(CamPos, Target);

        // ---------- 원본용 포스트: 웹과 같은 ACES 톤매핑만
        var basePP = Profile("PP_Original", p =>
        {
            var cg = p.AddSettings<ColorGrading>(); cg.enabled.Override(true);
            cg.tonemapper.Override(Tonemapper.ACES); cg.postExposure.Override(0.07f);
        });

        // ---------- 스튜디오
        var studioPP = Profile("PP_Studio", p =>
        {
            var ao = p.AddSettings<AmbientOcclusion>(); ao.enabled.Override(true);
            ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance); ao.intensity.Override(0.8f);
            var cg = p.AddSettings<ColorGrading>(); cg.enabled.Override(true);
            cg.tonemapper.Override(Tonemapper.ACES); cg.postExposure.Override(0.25f); cg.contrast.Override(8f);
            var vg = p.AddSettings<Vignette>(); vg.enabled.Override(true);
            vg.intensity.Override(0.22f); vg.smoothness.Override(0.5f);
        });
        Look("Look_10_Studio", "스튜디오", 10, studioSky, studioPP, l =>
        {
            l.ambientIntensity = 0.55f; l.reflectionIntensity = 1f;
            l.backgroundColor = new Color(0.15f, 0.15f, 0.16f);
            l.lights = new[]
            {
                Dir("Key (따뜻)", new Color(1f, 0.93f, 0.84f), 1.5f, new Vector3(1.5f, 2.0f, 2.0f), true),
                Dir("Fill (약하게)", new Color(0.85f, 0.9f, 1f), 0.35f, new Vector3(-2.0f, 0.8f, 1.5f), false),
                Dir("Rim", Color.white, 1.6f, new Vector3(0.6f, 1.8f, -2.5f), false),
            };
        });

        // ---------- 실사 카페: 고퀄 재질 세트 + 룸 세트 + 실내 HDRI
        var hq = RealisticBuilder.BuildMaterialSet(ModelPath);
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
        var b = new Bounds(tmp.transform.position, Vector3.zero);
        foreach (var r in tmp.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Object.DestroyImmediate(tmp);
        var room = RealisticBuilder.BuildRoom(b);
        RealisticBuilder.BuildPreset(Sky("Sky_RealCafe", HdriDir + "/cayley_interior_2k.hdr"),
                                     RealisticBuilder.BuildPost(PresetDir, focusDist), room);
        AssetDatabase.SaveAssets();

        // ---------- 레퍼런스 사진에 맞춘 프리셋 (실제 설치 / 프리미엄)
        ReferenceLooks.Build(ModelPath, focusDist);

        BuildScene(basePP, hq);
        Debug.Log("[RenderTemplate] 데모 씬 완료: " + ScenePath);
    }

    // ================================================================ 씬
    static void BuildScene(PostProcessProfile basePP, MaterialSet hq)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 원래 환경 (웹: 반구광 0.7 + 방 환경맵 0.55 → 비슷하게 3색 주변광)
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(1f, 0.97f, 0.93f) * 0.5f;
        RenderSettings.ambientEquatorColor = new Color(0.75f, 0.72f, 0.66f) * 0.42f;
        RenderSettings.ambientGroundColor = new Color(0.54f, 0.49f, 0.42f) * 0.35f;
        RenderSettings.skybox = null;
        Lightmapping.lightingSettings = new LightingSettings { bakedGI = false, realtimeGI = false };

        // 커피머신 모델
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        model.name = "CafeSim_A100 (웹에서 내보낸 모델)";
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.On;

        // 원래 조명 (웹 Stage.js와 같은 위치·세기)
        var orig = new GameObject("Original Lights");
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(orig.transform);
        sun.type = LightType.Directional; sun.intensity = 1.0f; sun.shadows = LightShadows.Soft;
        sun.transform.position = Web(0.8f, 2.4f, 1.6f);
        sun.transform.rotation = Quaternion.LookRotation(-sun.transform.position);
        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.transform.SetParent(orig.transform);
        fill.type = LightType.Point; fill.color = new Color(1f, 0.95f, 0.88f); fill.intensity = 0.6f; fill.range = 4f;
        fill.transform.position = Web(-1.2f, 1.2f, 1.2f);
        var probe = new GameObject("Original Reflection").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(orig.transform);
        probe.transform.position = Target;
        probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(4, 3, 3);

        // 카메라 (웹 overview 시점, 세로 시야각 38)
        var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        cam.transform.position = CamPos;
        cam.transform.rotation = Quaternion.LookRotation(Target - CamPos);
        cam.fieldOfView = 38f; cam.nearClipPlane = 0.01f; cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0xd9, 0xd3, 0xc9, 255);
        cam.allowHDR = true;
        cam.gameObject.AddComponent<AudioListener>();
        var layer = cam.gameObject.AddComponent<PostProcessLayer>();
        layer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset"));
        layer.volumeTrigger = cam.transform; layer.volumeLayer = 1;   // Default 레이어
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        var baseVol = new GameObject("Original Post (ACES)").AddComponent<PostProcessVolume>();
        baseVol.transform.SetParent(orig.transform);
        baseVol.isGlobal = true; baseVol.priority = 0; baseVol.sharedProfile = basePP;

        // 템플릿 (이 오브젝트를 끄면 원본과 같음)
        var tpl = new GameObject("RenderTemplate");
        var focus = new GameObject("Focus (머신 중심)").transform;
        focus.SetParent(tpl.transform); focus.position = Target;
        var sw = tpl.AddComponent<LookSwitcher>();
        sw.cam = cam; sw.focus = focus; sw.model = model.transform;
        sw.hqSet = AssetDatabase.LoadAssetAtPath<MaterialSet>(Root + "/Presets/MaterialSet_HQ.asset");
        Debug.Log("[RenderTemplate] hqSet " + (sw.hqSet ? "연결됨" : "없음") + " / hq 인자 " + (hq ? "있음" : "null"));
        sw.LoadPresets();
        var shot = tpl.AddComponent<ScreenshotTool>();
        shot.cam = cam; shot.looks = sw;
        var panel = tpl.AddComponent<LookPanel>();
        panel.looks = sw; panel.shots = shot;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        // 미리보기 (원본 + 프리셋마다 한 장)
        string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
        probe.RenderProbe();
        if (Application.isBatchMode)
        {
            for (int i = -1; i < sw.presets.Count; i++)
            {
                sw.Select(i);
                ScreenshotTool.Render(cam, 1280, 720, dir, $"preview_{i + 1}_{sw.CurrentName}.png");
            }
            sw.Select(-1);
        }
        else Previews(sw, cam, dir);
    }

    // 에디터에서는 프리셋을 바꾼 뒤 주변광(HDRI)·반사가 갱신될 때까지 잠깐 기다렸다가 찍음
    // (바로 찍으면 앞 프리셋의 주변광이 남아서 밝기가 틀어짐)
    static void Previews(LookSwitcher sw, Camera cam, string dir)
    {
        int i = -1, wait = 0;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            if (!sw) { EditorApplication.update -= tick; return; }
            if (wait == 0)
            {
                sw.Select(i);
                DynamicGI.UpdateEnvironment();
                foreach (var rp in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) rp.RenderProbe();
            }
            if (++wait < 30) return;
            ScreenshotTool.Render(cam, 1280, 720, dir, $"preview_{i + 1}_{sw.CurrentName}.png");
            wait = 0;
            if (++i >= sw.presets.Count) { sw.Select(-1); EditorApplication.update -= tick; Debug.Log("[RenderTemplate] 미리보기 완료"); }
        };
        EditorApplication.update += tick;
    }

    // ================================================================ 도우미
    static Vector3 Aim(Vector3 from) => Quaternion.LookRotation(-from).eulerAngles;

    static LookPreset.RigLight Dir(string n, Color c, float i, Vector3 from, bool shadows) =>
        new LookPreset.RigLight { name = n, type = LightType.Directional, color = c, intensity = i, position = from, euler = Aim(from), shadows = shadows };

    static Material Sky(string name, string hdrPath)
    {
        string path = PresetDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(hdrPath));
        m.SetFloat("_Mapping", 1); m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        EditorUtility.SetDirty(m);
        return m;
    }

    static PostProcessProfile Profile(string name, System.Action<PostProcessProfile> fill)
    {
        string path = PresetDir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);                       // 다시 만들 때 깨끗하게
        var p = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(p, path);
        fill(p);
        foreach (var s in p.settings) { s.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector; AssetDatabase.AddObjectToAsset(s, p); }
        EditorUtility.SetDirty(p);
        return p;
    }

    static void Look(string file, string display, int order, Material sky, PostProcessProfile pp, System.Action<LookPreset> fill)
    {
        string path = LookDir + "/" + file + ".asset";
        var l = AssetDatabase.LoadAssetAtPath<LookPreset>(path);
        if (!l) { l = ScriptableObject.CreateInstance<LookPreset>(); AssetDatabase.CreateAsset(l, path); }
        l.displayName = display; l.order = order; l.skybox = sky; l.post = pp;
        fill(l);
        EditorUtility.SetDirty(l);
    }
}
