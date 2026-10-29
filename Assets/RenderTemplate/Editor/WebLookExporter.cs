using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// 메뉴 RenderTemplate > Export Looks to Web
// 유니티에서 만든 프리셋을 웹 시뮬레이터(cafesim)가 읽는 look.json + 텍스처로 내보냄
// → 웹의 LookLayer가 돌아가는 시뮬레이터 위에 같은 룩(조명·HDRI·재질·스티커·접지 그림자·톤)을 씌움
// 좌표: 유니티 = 웹에서 x만 뒤집힌 것 → 내보낼 때 x 부호를 다시 바꿈
// 재질 번호: 유니티 렌더러 이름 Node-N = glb 노드 N → 메시 → glb 재질 번호 (웹도 같은 번호로 찾음)
public static class WebLookExporter
{
    const string Root = "Assets/RenderTemplate";
    const string ModelPath = Root + "/Models/cafesim_a100.glb";
    const string PrefKey = "RenderTemplate.WebLooksDir";
    static string OutDir => EditorPrefs.GetString(PrefKey, "E:/cam_p/UnityMCP_CoffeeMachine/packages/viewer/public/looks");

    [MenuItem("RenderTemplate/Export Looks to Web")]
    public static void ExportMenu()
    {
        string dir = EditorUtility.OpenFolderPanel("웹 시뮬레이터의 public/looks 폴더", OutDir, "");
        if (string.IsNullOrEmpty(dir)) return;
        EditorPrefs.SetString(PrefKey, dir);
        Debug.Log("[WebLookExporter] " + Export(dir));
    }

    public static string Export(string outDir = null)
    {
        outDir = outDir ?? OutDir;
        var sw = Object.FindFirstObjectByType<LookSwitcher>();
        if (!sw) return "씬에 LookSwitcher 없음";
        int keep = sw.Index; sw.Select(-1); sw.SetHq(false); sw.ResetModelMaterials();

        var gltfIndex = MaterialIndexMap(sw.model);
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.GetFiles(outDir, "*.json")) File.Delete(old);             // 지난번 내보낸 것 정리 (지운 프리셋·안 쓰는 텍스처가 남지 않게)
        if (Directory.Exists(Path.Combine(outDir, "tex"))) Directory.Delete(Path.Combine(outDir, "tex"), true);
        Directory.CreateDirectory(Path.Combine(outDir, "tex"));
        Directory.CreateDirectory(Path.Combine(outDir, "env"));
        var index = new List<string>();
        Vector3 focus = sw.focus ? sw.focus.position : Vector3.zero;

        foreach (var p in Resources.LoadAll<LookPreset>("Looks").OrderBy(x => x.order).ThenBy(x => x.name))
        {
            string id = p.name;
            var j = new Json();
            j.Str("id", id).Str("name", p.displayName).Str("note", p.note ?? "");
            // 환경
            string hdr = p.skybox ? AssetDatabase.GetAssetPath(p.skybox.GetTexture("_MainTex")) : null;
            j.Raw("env", new Json().Str("hdr", Copy(hdr, outDir, "env")).Num("intensity", p.ambientIntensity)
                .Bool("background", p.showSkybox).Arr("bgColor", p.backgroundColor).Num("reflection", p.reflectionIntensity).ToString());
            if (p.fog) j.Raw("fog", new Json().Arr("color", p.fogColor).Num("density", p.fogDensity).ToString());
            j.Num("originalLightScale", p.originalLightScale);
            // 조명
            var lights = new List<string>();
            foreach (var l in p.lights ?? new LookPreset.RigLight[0])
            {
                var dir = Quaternion.Euler(l.euler) * Vector3.forward;
                var pos = focus + l.position;
                lights.Add(new Json().Str("type", l.type.ToString().ToLower()).Arr("color", l.color).Num("intensity", l.intensity)
                    .Vec("position", W(pos)).Vec("direction", W(dir)).Vec("target", W(focus)).Num("range", l.range)
                    .Num("angle", l.spotAngle).Bool("shadows", l.shadows).ToString());
            }
            j.Raw("lights", "[" + string.Join(",", lights) + "]");
            // 카메라
            if (p.overrideCamera)
                j.Raw("camera", new Json().Vec("position", W(focus + p.cameraOffset)).Vec("target", W(focus + p.lookOffset)).Num("fov", p.fov).ToString());
            // 포스트
            j.Raw("post", Post(p.post));
            // 재질
            var set = p.hqMaterials ? (p.materialSet ? p.materialSet : sw.hqSet) : null;
            var mats = new List<string>();
            if (set != null)
                foreach (var pair in set.pairs)
                {
                    if (!pair.from || !pair.to || !gltfIndex.TryGetValue(pair.from, out int gi)) continue;
                    if (!pair.from.HasProperty("metallicFactor")) continue;          // 키오스크 구멍(unlit) 등은 그대로 둠 → 웹은 진짜 키오스크 화면
                    var m = pair.to;
                    if (!m.HasProperty("baseColorFactor")) continue;
                    var mj = new Json().Num("index", gi).Arr("color", m.GetColor("baseColorFactor"))
                        .Num("metalness", m.GetFloat("metallicFactor")).Num("roughness", m.GetFloat("roughnessFactor"));
                    TexField(mj, "map", m, "baseColorTexture", outDir);
                    if (m.IsKeywordEnabled("_NORMALMAP")) { TexField(mj, "normalMap", m, "normalTexture", outDir); mj.Num("normalScale", m.GetFloat("normalTexture_scale")); }
                    if (m.HasProperty("emissiveFactor")) mj.Arr("emissive", m.GetColor("emissiveFactor"));
                    mats.Add(mj.ToString());
                }
            j.Raw("materials", "[" + string.Join(",", mats) + "]");
            // 스티커·포스터 (배경 세트)
            var stickers = new List<string>();
            if (p.roomPrefab)
                foreach (var mr in p.roomPrefab.GetComponentsInChildren<MeshRenderer>())
                {
                    var t = mr.transform; var mat = mr.sharedMaterial;
                    if (!mat || !mat.mainTexture) continue;
                    stickers.Add(new Json().Str("map", Copy(AssetDatabase.GetAssetPath(mat.mainTexture), outDir, "tex", id + "_"))
                        .Vec("position", W(t.position)).Vec("normal", W(-t.forward)).Vec("up", W(t.up))
                        .Arr2("size", t.lossyScale.x, t.lossyScale.y).Num("roughness", 1 - mat.GetFloat("_Glossiness")).ToString());
                }
            j.Raw("stickers", "[" + string.Join(",", stickers) + "]");
            // 접지 그림자 (유니티와 같은 계산으로 판을 만든 뒤 위치만 기록)
            var shadows = new List<string>();
            if (p.contactShadow > 0)
            {
                var tmp = new GameObject("_tmpShadows");
                ContactShadows.Build(sw.model, tmp.transform, p.contactShadow);
                foreach (var mr in tmp.GetComponentsInChildren<MeshRenderer>())
                {
                    var t = mr.transform;
                    shadows.Add(new Json().Vec("position", W(t.position)).Vec("normal", W(-t.forward)).Vec("up", W(t.up))
                        .Arr2("size", t.lossyScale.x, t.lossyScale.y).Num("alpha", mr.sharedMaterial.color.a).ToString());
                }
                Object.DestroyImmediate(tmp);
            }
            j.Raw("shadows", "[" + string.Join(",", shadows) + "]");

            File.WriteAllText(Path.Combine(outDir, id + ".json"), j.ToString(), new UTF8Encoding(false));
            index.Add(new Json().Str("id", id).Str("name", p.displayName).Str("file", id + ".json").ToString());
        }
        File.WriteAllText(Path.Combine(outDir, "index.json"), "[" + string.Join(",", index) + "]", new UTF8Encoding(false));
        sw.Select(keep);
        return $"{index.Count}개 룩을 내보냄 → {outDir}";
    }

    // 유니티 재질 → glb 재질 번호
    [System.Serializable] class GNode { public int mesh = -1; }
    [System.Serializable] class GPrim { public int material = -1; }
    [System.Serializable] class GMesh { public GPrim[] primitives; }
    [System.Serializable] class GRoot { public GNode[] nodes; public GMesh[] meshes; }

    static Dictionary<Material, int> MaterialIndexMap(Transform model)
    {
        var bytes = File.ReadAllBytes(ModelPath);
        int len = System.BitConverter.ToInt32(bytes, 12);
        var g = JsonUtility.FromJson<GRoot>(Encoding.UTF8.GetString(bytes, 20, len));
        var map = new Dictionary<Material, int>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!r.name.StartsWith("Node-") || !int.TryParse(r.name.Substring(5), out int n)) continue;
            int mi = g.nodes[n].mesh; if (mi < 0) continue;
            var prim = g.meshes[mi].primitives;
            for (int k = 0; k < r.sharedMaterials.Length && k < prim.Length; k++)
                if (r.sharedMaterials[k] && prim[k].material >= 0) map[r.sharedMaterials[k]] = prim[k].material;
        }
        return map;
    }

    static string Post(PostProcessProfile pp)
    {
        var j = new Json();
        if (!pp) return j.ToString();
        if (pp.TryGetSettings(out ColorGrading cg) && cg.enabled.value)
            j.Num("exposure", cg.postExposure.value).Num("contrast", cg.contrast.value).Num("saturation", cg.saturation.value).Num("temperature", cg.temperature.value);
        if (pp.TryGetSettings(out AmbientOcclusion ao) && ao.enabled.value) j.Num("ao", ao.intensity.value);
        if (pp.TryGetSettings(out Bloom bl) && bl.enabled.value) j.Num("bloomThreshold", bl.threshold.value).Num("bloom", bl.intensity.value);
        if (pp.TryGetSettings(out Vignette vg) && vg.enabled.value) j.Num("vignette", vg.intensity.value).Num("vignetteSmooth", vg.smoothness.value);
        return j.ToString();
    }

    // 파일로 된 텍스처만 복사 (glb 안에 들어있는 원래 텍스처는 웹이 이미 가지고 있으니 건드리지 않음)
    static void TexField(Json j, string key, Material m, string prop, string outDir)
    {
        var t = m.GetTexture(prop);
        if (!t) return;
        string path = AssetDatabase.GetAssetPath(t);
        if (string.IsNullOrEmpty(path) || path.EndsWith(".glb")) return;
        j.Str(key, Copy(path, outDir, "tex"));
        var s = m.GetTextureScale(prop); j.Arr2(key + "Repeat", s.x, s.y);
    }

    static string Copy(string assetPath, string outDir, string sub, string prefix = "")
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        string name = prefix + Path.GetFileName(assetPath);
        string dst = Path.Combine(outDir, sub, name);
        string src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst)) File.Copy(src, dst, true);
        return sub + "/" + name;
    }

    static Vector3 W(Vector3 v) => new Vector3(-v.x, v.y, v.z);           // 유니티 → 웹(three.js) 좌표

    // 아주 작은 JSON 작성기 (Unity JsonUtility는 딕셔너리·가변 구조를 못 써서)
    class Json
    {
        readonly List<string> f = new List<string>();
        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
        static string N(float v) => v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
        public Json Str(string k, string v) { f.Add(Q(k) + ":" + (v == null ? "null" : Q(v))); return this; }
        public Json Num(string k, float v) { f.Add(Q(k) + ":" + N(v)); return this; }
        public Json Bool(string k, bool v) { f.Add(Q(k) + ":" + (v ? "true" : "false")); return this; }
        public Json Vec(string k, Vector3 v) { f.Add(Q(k) + ":[" + N(v.x) + "," + N(v.y) + "," + N(v.z) + "]"); return this; }
        public Json Arr(string k, Color c) { f.Add(Q(k) + ":[" + N(c.r) + "," + N(c.g) + "," + N(c.b) + "," + N(c.a) + "]"); return this; }
        public Json Arr2(string k, float a, float b) { f.Add(Q(k) + ":[" + N(a) + "," + N(b) + "]"); return this; }
        public Json Raw(string k, string json) { f.Add(Q(k) + ":" + json); return this; }
        public override string ToString() => "{" + string.Join(",", f) + "}";
    }
}
