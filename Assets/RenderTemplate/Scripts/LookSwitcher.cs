using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

// 분위기 전환: 원본 → 프리셋1 → 프리셋2 … → 원본 (L 키, 패널 버튼)
// 흐름: 켤 때 지금 상태(하늘·주변광·조명 밝기·배경)를 기억 → 프리셋 적용(조명·프로브·포스트·배경 세트 추가)
//       원본으로 돌아갈 때 기억한 값을 되돌리고 추가한 것은 지움. 원본 파일은 바뀌지 않음.
[ExecuteAlways]
public class LookSwitcher : MonoBehaviour
{
    public Camera cam;
    public Transform focus;                      // 커피머신 중심 (조명·프로브 위치 기준)
    public Transform model;                      // 재질을 바꿀 모델
    public MaterialSet hqSet;                    // 고퀄 재질 세트
    public KeyCode key = KeyCode.L, materialKey = KeyCode.M;

    public List<LookPreset> presets = new List<LookPreset>();
    public int Index { get; private set; } = -1; // -1 = 원본
    public LookPreset Current => Index < 0 ? null : presets[Index];
    public string CurrentName => Index < 0 ? "원본" : presets[Index].displayName;
    public PostProcessVolume PostVolume { get; private set; }   // 패널의 "효과 강도" 슬라이더용
    public bool HqOn => _swap.IsOn;

    // 되돌리기용 원래 값
    Material _sky; AmbientMode _ambMode; float _ambInt, _reflInt;
    bool _fog; Color _fogColor; float _fogDensity; FogMode _fogMode;
    CameraClearFlags _clear; Color _bg;
    Vector3 _camPos; Quaternion _camRot; float _camFov;
    readonly Dictionary<Light, float> _orig = new Dictionary<Light, float>();
    readonly MaterialSwapper _swap = new MaterialSwapper();
    bool _hqByUser;                              // M 키로 직접 켰는지
    GameObject _rig;                             // 프리셋이 추가한 것들 (저장 안 됨)

    [Tooltip("시작할 때 씌울 프리셋 (Look Panel에서 고르면 자동으로 저장됨). 비우면 원본")]
    public LookPreset startPreset;

    // 켜질 때(플레이 시작, 씬 열기, 스크립트 다시 불러오기) 마지막으로 고른 프리셋을 다시 씌움
    // → 에디터에서 본 룩과 플레이 화면이 같아짐
    // 모델 재질을 glb 원본으로 되돌림 (스크립트 다시 불러오기 등으로 "원래 재질 기억"이 사라졌을 때 대비)
    // glb 파일 안의 같은 이름(Node-N) 렌더러 재질을 그대로 가져와 씀
    public string modelAssetPath = "Assets/RenderTemplate/Models/cafesim_a100.glb";
    public int ResetModelMaterials()
    {
        int n = 0;
#if UNITY_EDITOR
        if (Application.isPlaying || !model) return -1;
        var src = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
        if (!src) return -2;
        var orig = new Dictionary<string, Material[]>();
        foreach (var r in src.GetComponentsInChildren<Renderer>(true)) orig[r.name] = r.sharedMaterials;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            if (orig.TryGetValue(r.name, out var m)) { r.sharedMaterials = m; n++; }
        _swap.Forget();
#endif
        return n;
    }

    void OnEnable()
    {
        if (!HqOn) ResetModelMaterials();
        LoadPresets();
        if (startPreset && Index < 0) Select(presets.IndexOf(startPreset));
    }
    void OnDisable() { Select(-1); SetHq(false); }

    // Resources/Looks 안의 프리셋을 order 순으로 읽음 → 파일만 넣으면 추가됨
    public void LoadPresets()
    {
        var cur = Current;
        presets = Resources.LoadAll<LookPreset>("Looks").OrderBy(p => p.order).ThenBy(p => p.name).ToList();
        if (cur) Index = presets.IndexOf(cur);
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (Input.GetKeyDown(key)) Next();
        if (Input.GetKeyDown(materialKey)) { _hqByUser = !HqOn; SetHq(_hqByUser); }
    }

    public void Next() { Select(Index + 1 < presets.Count ? Index + 1 : -1); }

    public void Select(int i)
    {
        if (Index >= 0) Restore();
        Index = (i >= 0 && i < presets.Count) ? i : -1;
        if (Index >= 0) Apply(presets[Index]);
        SetHq(_hqByUser || (Current && Current.hqMaterials));
    }

    public void Reapply() { Select(Index); }     // 프리셋 값을 고친 뒤 다시 적용

    public MaterialSet ActiveSet => Current && Current.materialSet ? Current.materialSet : hqSet;

    public void SetHq(bool on)
    {
        if (HqOn) _swap.Restore();               // 프리셋마다 세트가 다를 수 있어서 항상 새로 적용
        if (on) _swap.Apply(model, ActiveSet);
    }
    public void ToggleHqByUser() { _hqByUser = !HqOn; SetHq(_hqByUser); }

    void Apply(LookPreset p)
    {
        // 1) 원래 값 기억
        _sky = RenderSettings.skybox; _ambMode = RenderSettings.ambientMode;
        _ambInt = RenderSettings.ambientIntensity; _reflInt = RenderSettings.reflectionIntensity;
        _fog = RenderSettings.fog; _fogColor = RenderSettings.fogColor;
        _fogDensity = RenderSettings.fogDensity; _fogMode = RenderSettings.fogMode;
        if (cam)
        {
            _clear = cam.clearFlags; _bg = cam.backgroundColor;
            _camPos = cam.transform.position; _camRot = cam.transform.rotation; _camFov = cam.fieldOfView;
            if (p.overrideCamera)
            {
                Vector3 f = focus ? focus.position : Vector3.zero;
                cam.transform.position = f + p.cameraOffset;
                cam.transform.rotation = Quaternion.LookRotation(f + p.lookOffset - cam.transform.position);
                cam.fieldOfView = p.fov;
            }
        }
        _orig.Clear();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) _orig[l] = l.intensity;

        // 2) 환경 (HDRI를 주변광으로)
        if (p.skybox)
        {
            RenderSettings.skybox = p.skybox;
            RenderSettings.ambientMode = AmbientMode.Skybox;
        }
        RenderSettings.ambientIntensity = p.ambientIntensity;
        RenderSettings.reflectionIntensity = p.reflectionIntensity;
        RenderSettings.fog = p.fog; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = p.fogColor; RenderSettings.fogDensity = p.fogDensity;
        DynamicGI.UpdateEnvironment();
        if (cam)
        {
            cam.clearFlags = p.showSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = p.backgroundColor;
        }

        // 3) 원래 조명 줄이기
        foreach (var kv in _orig) if (kv.Key) kv.Key.intensity = kv.Value * p.originalLightScale;

        // 4) 추가 조명·세트·프로브·포스트는 _rig 아래에 (돌아갈 때 통째로 지움)
        _rig = new GameObject("_LookRig (" + p.displayName + ")") { hideFlags = HideFlags.DontSave };
        Vector3 c = focus ? focus.position : Vector3.zero;
        if (p.roomPrefab)
        {
            var room = Instantiate(p.roomPrefab, _rig.transform);
            room.name = p.roomPrefab.name;
            foreach (var t in room.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
        }
        if (p.contactShadow > 0) ContactShadows.Build(model, _rig.transform, p.contactShadow);
        if (p.lights != null)
            foreach (var r in p.lights)
            {
                var go = new GameObject(r.name) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(_rig.transform);
                go.transform.position = c + r.position;
                go.transform.rotation = Quaternion.Euler(r.euler);
                var l = go.AddComponent<Light>();
                l.type = r.type; l.color = r.color; l.intensity = r.intensity;
                l.range = r.range > 0 ? r.range : 5; l.spotAngle = r.spotAngle > 0 ? r.spotAngle : 40;
                l.shadows = r.shadows ? LightShadows.Soft : LightShadows.None;
            }
        if (p.reflectionProbe)
        {
            var go = new GameObject("ReflectionProbe") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_rig.transform);
            go.transform.position = c;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            rp.size = p.probeSize; rp.intensity = p.probeIntensity; rp.importance = 10;
            rp.boxProjection = true; rp.resolution = 256;
            rp.RenderProbe();
        }
        PostVolume = null;
        if (p.post)
        {
            // 원본용 기본 볼륨보다 우선순위 높게 → 이 프리셋 값이 덮어씀
            var go = new GameObject("PostVolume") { hideFlags = HideFlags.DontSave, layer = 0 };
            go.transform.SetParent(_rig.transform);
            PostVolume = go.AddComponent<PostProcessVolume>();
            PostVolume.isGlobal = true; PostVolume.priority = 10; PostVolume.sharedProfile = p.post;
        }
    }

    void Restore()
    {
        RenderSettings.skybox = _sky; RenderSettings.ambientMode = _ambMode;
        RenderSettings.ambientIntensity = _ambInt; RenderSettings.reflectionIntensity = _reflInt;
        RenderSettings.fog = _fog; RenderSettings.fogColor = _fogColor;
        RenderSettings.fogDensity = _fogDensity; RenderSettings.fogMode = _fogMode;
        DynamicGI.UpdateEnvironment();
        if (cam)
        {
            cam.clearFlags = _clear; cam.backgroundColor = _bg;
            cam.transform.SetPositionAndRotation(_camPos, _camRot); cam.fieldOfView = _camFov;
        }
        foreach (var kv in _orig) if (kv.Key) kv.Key.intensity = kv.Value;
        _orig.Clear();
        if (_rig) { if (Application.isPlaying) Destroy(_rig); else DestroyImmediate(_rig); }
        _rig = null; PostVolume = null;
    }
}
