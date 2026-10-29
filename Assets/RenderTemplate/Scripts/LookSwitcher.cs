using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

// L 키로 분위기 전환: 원본 → 프리셋1 → 프리셋2 … → 원본
// 흐름: 켤 때 지금 상태(하늘·주변광·조명 밝기·배경)를 기억 → 프리셋 적용(조명·프로브·포스트 추가)
//       원본으로 돌아갈 때 기억한 값을 되돌리고 추가한 것은 지움. 그래서 끄면 원래 씬과 같음.
[ExecuteAlways]
public class LookSwitcher : MonoBehaviour
{
    public Camera cam;
    public Transform focus;                      // 커피머신 중심 (조명·프로브 위치 기준)
    public KeyCode key = KeyCode.L;
    public bool showLabel = true;

    public List<LookPreset> presets = new List<LookPreset>();
    public int Index { get; private set; } = -1; // -1 = 원본
    public string CurrentName => Index < 0 ? "원본" : presets[Index].displayName;

    // 되돌리기용 원래 값
    Material _sky; AmbientMode _ambMode; float _ambInt, _reflInt;
    bool _fog; Color _fogColor; float _fogDensity; FogMode _fogMode;
    CameraClearFlags _clear; Color _bg;
    readonly Dictionary<Light, float> _orig = new Dictionary<Light, float>();
    GameObject _rig;                             // 프리셋이 추가한 것들 (저장 안 됨)

    void OnEnable() { LoadPresets(); }
    void OnDisable() { Select(-1); }

    // Resources/Looks 안의 프리셋을 order 순으로 읽음 → 파일만 넣으면 추가됨
    public void LoadPresets()
    {
        presets = Resources.LoadAll<LookPreset>("Looks").OrderBy(p => p.order).ThenBy(p => p.name).ToList();
    }

    void Update()
    {
        if (Application.isPlaying && Input.GetKeyDown(key)) Next();
    }

    public void Next() { Select(Index + 1 < presets.Count ? Index + 1 : -1); }

    public void Select(int i)
    {
        if (Index >= 0) Restore();
        Index = (i >= 0 && i < presets.Count) ? i : -1;
        if (Index >= 0) Apply(presets[Index]);
    }

    void Apply(LookPreset p)
    {
        // 1) 원래 값 기억
        _sky = RenderSettings.skybox; _ambMode = RenderSettings.ambientMode;
        _ambInt = RenderSettings.ambientIntensity; _reflInt = RenderSettings.reflectionIntensity;
        _fog = RenderSettings.fog; _fogColor = RenderSettings.fogColor;
        _fogDensity = RenderSettings.fogDensity; _fogMode = RenderSettings.fogMode;
        if (cam) { _clear = cam.clearFlags; _bg = cam.backgroundColor; }
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

        // 4) 추가 조명·프로브·포스트는 _rig 아래에 (돌아갈 때 통째로 지움)
        _rig = new GameObject("_LookRig (" + p.displayName + ")") { hideFlags = HideFlags.DontSave };
        Vector3 c = focus ? focus.position : Vector3.zero;
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
        if (p.post)
        {
            // 원본용 기본 볼륨보다 우선순위 높게 → 이 프리셋 값이 덮어씀
            var go = new GameObject("PostVolume") { hideFlags = HideFlags.DontSave, layer = 0 };
            go.transform.SetParent(_rig.transform);
            var v = go.AddComponent<PostProcessVolume>();
            v.isGlobal = true; v.priority = 10; v.sharedProfile = p.post;
        }
    }

    void Restore()
    {
        RenderSettings.skybox = _sky; RenderSettings.ambientMode = _ambMode;
        RenderSettings.ambientIntensity = _ambInt; RenderSettings.reflectionIntensity = _reflInt;
        RenderSettings.fog = _fog; RenderSettings.fogColor = _fogColor;
        RenderSettings.fogDensity = _fogDensity; RenderSettings.fogMode = _fogMode;
        DynamicGI.UpdateEnvironment();
        if (cam) { cam.clearFlags = _clear; cam.backgroundColor = _bg; }
        foreach (var kv in _orig) if (kv.Key) kv.Key.intensity = kv.Value;
        _orig.Clear();
        if (_rig) { if (Application.isPlaying) Destroy(_rig); else DestroyImmediate(_rig); }
        _rig = null;
    }

    void OnGUI()
    {
        if (!showLabel || !Application.isPlaying) return;
        GUI.Label(new Rect(12, 10, 600, 24), $"[L] 분위기: {CurrentName}   [M] 재질   [F12] 4K 스샷   [B] Before/After");
    }
}
