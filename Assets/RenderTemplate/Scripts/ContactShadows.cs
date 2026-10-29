using UnityEngine;

// 접지 그림자: 물건이 바닥·벽에 닿은 곳을 부드럽게 어둡게 해서 "떠 보이는" 느낌을 없앰
// (Built-in RP 실시간 조명엔 접촉 그림자가 없어서, 가장자리가 흐린 반투명 검은 판을 깔아 흉내냄)
// 모델 아래 부품 중 "기계"(높고 폭이 좁은 것)와 "하부장"(넓고 높은 것)을 크기로 찾아서 자동 배치
public static class ContactShadows
{
    static Texture2D _soft;
    static Material _mat;

    public static void Build(Transform model, Transform parent, float strength)
    {
        if (!model) return;
        var root = model.childCount == 1 ? model.GetChild(0) : model;
        foreach (Transform t in root)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var s = b.size;
            if (s.y > 0.3f && s.x < 1.2f && s.x > 0.1f && s.z > 0.1f)
            {
                // 기계: 상판 위 발밑 그림자 + 뒤 벽에 닿는 어둠
                Quad(parent, "접지_" + t.name, new Vector3(b.center.x, b.min.y + 0.002f, b.center.z),
                     Quaternion.Euler(90, 0, 0), new Vector3(s.x * 1.25f, s.z * 1.3f, 1), strength);
                Quad(parent, "벽닿음_" + t.name, new Vector3(b.center.x, b.min.y + s.y * 0.3f, -0.329f),
                     Quaternion.Euler(0, 180, 0), new Vector3(s.x * 1.15f, s.y * 0.75f, 1), strength * 0.3f);
            }
            else if (s.x > 2f && s.y > 0.5f)
            {
                // 하부장: 바닥 그림자
                Quad(parent, "접지_" + t.name, new Vector3(b.center.x, b.min.y - 0.007f, b.center.z + 0.05f),
                     Quaternion.Euler(90, 0, 0), new Vector3(s.x * 1.08f, s.z * 1.6f, 1), strength * 0.8f);
            }
        }
    }

    static void Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, float alpha)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name; go.hideFlags = HideFlags.DontSave;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        var m = new Material(Mat()) { hideFlags = HideFlags.DontSave };
        m.color = new Color(0, 0, 0, Mathf.Clamp01(alpha));
        r.sharedMaterial = m;
    }

    static Material Mat()
    {
        if (_mat) return _mat;
        _mat = new Material(Shader.Find("Unlit/Transparent")) { hideFlags = HideFlags.DontSave, mainTexture = Soft() };
        return _mat;
    }

    // 가운데 진하고 가장자리로 갈수록 흐려지는 둥근 사각형
    static Texture2D Soft()
    {
        if (_soft) return _soft;
        const int N = 64;
        _soft = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = Mathf.Abs(x / (N - 1f) * 2 - 1), v = Mathf.Abs(y / (N - 1f) * 2 - 1);
            float d = Mathf.Pow(Mathf.Pow(u, 4) + Mathf.Pow(v, 4), 0.25f);          // 둥근 사각형 거리
            float a = Mathf.Clamp01(1 - d); a = a * a * (3 - 2 * a);
            _soft.SetPixel(x, y, new Color(0, 0, 0, a));
        }
        _soft.Apply();
        return _soft;
    }
}
