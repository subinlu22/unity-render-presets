using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using P = ReferenceSetup.Part;

// 스티커·포스터 세트: 영역별 "스티커 수"(벽은 포스터 수)만큼 직접 그린 레트로 그림을 얇은 판으로 붙임
// 모델은 그대로 두고, 프리셋의 배경 세트(roomPrefab)로 붙었다 떨어짐
// 그림은 코드로 생성 (글자·도형·망점·바랜 종이) → 저작권 걱정 없음
public static class Dressing
{
    const string Root = "Assets/RenderTemplate";

    // 붙일 면: 위치(가운데), 크기, 바라보는 방향, 크기 범위 (모델 좌표, 에디터에서 조회한 값)
    struct Face { public Vector3 center; public Vector2 size; public Vector3 normal; public Vector2 item; public Rect avoid; }

    static Face[] Faces(P part)
    {
        switch (part)
        {
            case P.커피머신: return new[] {
                new Face { center = new Vector3(0.922f, 0.43f, -0.03f), size = new Vector2(0.5f, 0.7f), normal = Vector3.right, item = new Vector2(0.05f, 0.1f) } };   // 왼쪽 옆면
            case P.제빙기: return new[] { new Face { center = new Vector3(0f, 0.4f, 0.176f), size = new Vector2(0.26f, 0.66f), normal = Vector3.forward, item = new Vector2(0.04f, 0.07f), avoid = new Rect(-0.08f, 0.1f, 0.16f, 0.25f) } };
            case P.컵디스펜서: return new[] { new Face { center = new Vector3(-0.43f, 0.58f, 0.2f), size = new Vector2(0.36f, 0.44f), normal = Vector3.forward, item = new Vector2(0.045f, 0.08f) } };
            case P.하부장: return new[] { new Face { center = new Vector3(0f, -0.45f, 0.372f), size = new Vector2(2.4f, 0.7f), normal = Vector3.forward, item = new Vector2(0.08f, 0.22f) } };
            case P.벽: return new[] {
                new Face { center = new Vector3(0f, 1.3f, -0.328f), size = new Vector2(3.2f, 0.8f), normal = Vector3.forward, item = new Vector2(0.28f, 0.4f) },
                new Face { center = new Vector3(1.35f, 0.45f, -0.328f), size = new Vector2(0.7f, 0.7f), normal = Vector3.forward, item = new Vector2(0.22f, 0.32f) },
                new Face { center = new Vector3(-1.1f, 0.45f, -0.328f), size = new Vector2(0.8f, 0.7f), normal = Vector3.forward, item = new Vector2(0.22f, 0.32f) } };
        }
        return new Face[0];
    }

    public static GameObject Build(ReferenceSetup setup, string id)
    {
        string prefabPath = Root + "/Presets/Dressing_" + id + ".prefab";
        int total = 0; foreach (var s in setup.slots) if (s.use) total += s.stickers;
        if (total == 0) { AssetDatabase.DeleteAsset(prefabPath); return null; }

        string dir = Root + "/Generated/" + id + "_dressing";
        AssetDatabase.DeleteAsset(dir);                                     // 예전 스티커 그림·재질 지우고 새로 (덮어쓰기 실패로 재질이 빠지는 것 방지)
        AssetDatabase.CreateFolder(Root + "/Generated", id + "_dressing");
        var rnd = new System.Random(id.GetHashCode() & 0xffff);
        var root = new GameObject("Dressing_" + id);
        int n = 0;
        foreach (var s in setup.slots)
        {
            if (!s.use || s.stickers <= 0) continue;
            var faces = Faces(s.part);
            if (faces.Length == 0) continue;
            bool poster = s.part == P.벽 || s.part == P.하부장;
            var placed = new List<Rect>();
            for (int k = 0, tries = 0; k < s.stickers && tries < 300; tries++)
            {
                var f = faces[rnd.Next(faces.Length)];
                float h = Mathf.Lerp(f.item.x, f.item.y, (float)rnd.NextDouble());
                float w = poster && rnd.NextDouble() < 0.8 ? h * 0.72f : h * (0.8f + (float)rnd.NextDouble() * 0.5f);
                float lx = ((float)rnd.NextDouble() - 0.5f) * Mathf.Max(0, f.size.x - w), ly = ((float)rnd.NextDouble() - 0.5f) * Mathf.Max(0, f.size.y - h);
                var r = new Rect(lx - w / 2, ly - h / 2, w, h);
                var abs = new Rect(r.x + (f.normal == Vector3.right ? f.center.z : f.center.x), r.y + f.center.y, w, h);
                bool hit = f.avoid.width > 0 && f.avoid.Overlaps(abs);
                foreach (var p in placed) if (p.Overlaps(abs)) { hit = true; break; }
                if (hit) continue;
                placed.Add(abs);

                int seed = rnd.Next();
                var tex = poster ? RetroArt.Poster(seed) : RetroArt.Sticker(seed);
                string texPath = $"{dir}/{(poster ? "poster" : "sticker")}_{n:00}.png";
                System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();

                var mat = new Material(Shader.Find("Standard"));
                mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                mat.SetFloat("_Glossiness", poster ? 0.15f : 0.45f);
                // 컷아웃 (스티커 모양대로 자름)
                mat.SetFloat("_Mode", 1); mat.SetFloat("_Cutoff", 0.5f); mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = 2450;
                AssetDatabase.CreateAsset(mat, $"{dir}/{(poster ? "poster" : "sticker")}_{n:00}.mat");

                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = (poster ? "포스터 " : "스티커 ") + s.part + " " + k;
                q.transform.SetParent(root.transform);
                // 판은 기본으로 -z를 봄 → 면 방향에 맞게 돌리고, 면에서 살짝 띄움 (겹침마다 조금씩 더)
                q.transform.rotation = Quaternion.LookRotation(-f.normal) * Quaternion.Euler(0, 0, ((float)rnd.NextDouble() - 0.5f) * (poster ? 6 : 24));
                var side = f.normal == Vector3.right ? Vector3.forward : Vector3.right;
                q.transform.position = f.center + side * (lx * (f.normal == Vector3.right ? -1 : 1)) + Vector3.up * ly + f.normal * (0.0012f + n * 0.0002f);
                q.transform.localScale = new Vector3(w, h, 1);
                var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                k++; n++;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        // 메모리에 남은 예전 프리팹(지운 재질을 가리킴)을 버리고 파일에서 다시 읽음 → 안 하면 스티커가 분홍(재질 없음)으로 나옴
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }
}

// 레트로 그림 생성: 스티커(테두리 흰 칼선 + 도형 + 글자) / 포스터(바랜 종이 + 큰 도형 + 망점 + 제목)
public static class RetroArt
{
    static readonly Color[] Pal = {
        new Color(0.89f, 0.25f, 0.22f), new Color(0.96f, 0.78f, 0.25f), new Color(0.13f, 0.55f, 0.52f),
        new Color(0.12f, 0.2f, 0.36f), new Color(0.95f, 0.55f, 0.25f), new Color(0.4f, 0.65f, 0.35f), new Color(0.93f, 0.4f, 0.55f) };
    static readonly string[] Words = { "COFFEE", "HOT", "ICE", "1988", "RETRO", "SODA", "DONUT", "OPEN", "CAFE", "TOAST", "MILK", "SALE", "NO.1", "COLA", "1979", "YUM" };

    public static Texture2D Sticker(int seed)
    {
        var r = new System.Random(seed); const int N = 256;
        var t = Blank(N, N);
        Color a = Pal[r.Next(Pal.Length)], b = Pal[r.Next(Pal.Length)];
        int shape = r.Next(3);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float u = x / (N - 1f) * 2 - 1, v = y / (N - 1f) * 2 - 1;
            float d = shape == 0 ? Mathf.Sqrt(u * u + v * v)                                    // 원
                    : shape == 1 ? Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 6) + Mathf.Pow(Mathf.Abs(v), 6), 1 / 6f)   // 둥근 사각형
                    : Star(u, v);                                                              // 별 터짐
            if (d > 0.98f) continue;
            Color c = d > 0.86f ? new Color(0.97f, 0.96f, 0.92f) : (d > 0.8f ? b : a);         // 흰 칼선 + 띠 + 바탕
            if (d < 0.8f && ((x / 12 + y / 12) % 2 == 0) && shape == 1) c = Color.Lerp(c, b, 0.15f);
            t.SetPixel(x, y, c);
        }
        string w = Words[r.Next(Words.Length)];
        Text(t, w, N / 2, N / 2, Mathf.Max(3, 26 / Mathf.Max(3, w.Length)), Contrast(a));
        Age(t, r, 0.25f);
        t.Apply();
        return t;
    }

    public static Texture2D Poster(int seed)
    {
        var r = new System.Random(seed); const int W = 360, H = 500;
        var t = Blank(W, H);
        Color paper = new Color(0.93f, 0.88f, 0.74f), a = Pal[r.Next(Pal.Length)], b = Pal[r.Next(Pal.Length)];
        int layout = r.Next(3);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            Color c = paper;
            float u = x / (float)W, v = y / (float)H;
            if (layout == 0 && (u - 0.5f) * (u - 0.5f) + (v - 0.55f) * (v - 0.55f) * 0.6f < 0.09f) c = a;      // 큰 해
            if (layout == 1 && v > 0.3f && v < 0.75f) c = ((int)(u * 8) % 2 == 0) ? a : b;                         // 줄무늬
            if (layout == 2 && v > 0.25f && v < 0.8f && u > 0.12f && u < 0.88f) c = a;                              // 큰 상자
            float dot = ((x % 10) - 5) * ((x % 10) - 5) + ((y % 10) - 5) * ((y % 10) - 5);
            if (v < 0.25f && dot < 6 * (1 - v * 4) * 4) c = Color.Lerp(c, b, 0.8f);                                // 아래 망점
            if (u < 0.03f || u > 0.97f || v < 0.02f || v > 0.98f) c = b;                                            // 테두리
            t.SetPixel(x, y, c);
        }
        Text(t, Words[r.Next(Words.Length)], W / 2, (int)(H * 0.86f), 7, b);
        Text(t, Words[r.Next(Words.Length)], W / 2, (int)(H * 0.13f), 4, Contrast(paper));
        Age(t, r, 0.6f);
        t.Apply();
        return t;
    }

    static float Star(float u, float v)
    {
        float ang = Mathf.Atan2(v, u), rad = Mathf.Sqrt(u * u + v * v);
        return rad / (0.82f + 0.12f * Mathf.Cos(ang * 12));
    }
    static Color Contrast(Color c) => c.grayscale > 0.55f ? new Color(0.12f, 0.1f, 0.1f) : new Color(0.98f, 0.96f, 0.9f);
    static Texture2D Blank(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var clear = new Color[w * h]; t.SetPixels(clear);
        return t;
    }

    // 바램·얼룩·긁힘
    static void Age(Texture2D t, System.Random r, float amt)
    {
        float ox = r.Next(1000), oy = r.Next(1000);
        var px = t.GetPixels(); int w = t.width;
        for (int i = 0; i < px.Length; i++)
        {
            if (px[i].a < 0.5f) continue;
            float u = (i % w) / (float)w, v = (i / w) / (float)t.height;
            float n = Mathf.PerlinNoise(u * 5 + ox, v * 5 + oy);
            var c = Color.Lerp(px[i], new Color(0.85f, 0.78f, 0.6f), amt * 0.35f);           // 누렇게 바램
            c = Color.Lerp(c, c * 0.7f, Mathf.Clamp01((n - 0.6f) * 3) * amt);                   // 얼룩
            if (Mathf.PerlinNoise(u * 120 + oy, v * 4 + ox) > 1 - 0.05f * amt) c = Color.Lerp(c, Color.white, 0.5f);   // 긁힘
            c.a = 1; px[i] = c;
        }
        t.SetPixels(px);
    }

    // 5x7 픽셀 글자 (대문자·숫자·점)
    static readonly Dictionary<char, string> Font = new Dictionary<char, string> {
        {'A',"01110100011000111111100011000110001"},{'C',"01110100011000010000100001000101110"},{'D',"11110100011000110001100011000111110"},
        {'E',"11111100001000011110100001000011111"},{'F',"11111100001000011110100001000010000"},{'H',"10001100011000111111100011000110001"},
        {'I',"01110001000010000100001000010001110"},{'K',"10001100101010011000101001001010001"},{'L',"10000100001000010000100001000011111"},
        {'M',"10001110111010110101100011000110001"},{'N',"10001110011010110011100011000110001"},{'O',"01110100011000110001100011000101110"},
        {'P',"11110100011000111110100001000010000"},{'R',"11110100011000111110101001001010001"},{'S',"01111100001000001110000010000111110"},
        {'T',"11111001000010000100001000010000100"},{'U',"10001100011000110001100011000101110"},{'Y',"10001100010101000100001000010000100"},
        {'0',"01110100011001110101110011000101110"},{'1',"00100011000010000100001000010001110"},{'7',"11111000010001000100010000100001000"},
        {'8',"01110100011000101110100011000101110"},{'9',"01110100011000101111000010001001100"},{'.',"00000000000000000000000000110001100"},
    };

    static void Text(Texture2D t, string s, int cx, int cy, int scale, Color col)
    {
        int cw = 6 * scale, x0 = cx - s.Length * cw / 2 + scale / 2, y0 = cy + 7 * scale / 2;
        for (int k = 0; k < s.Length; k++)
        {
            if (!Font.TryGetValue(s[k], out var g)) continue;
            for (int row = 0; row < 7; row++) for (int colI = 0; colI < 5; colI++)
            {
                if (g[row * 5 + colI] != '1') continue;
                for (int dy = 0; dy < scale; dy++) for (int dx = 0; dx < scale; dx++)
                {
                    int x = x0 + k * cw + colI * scale + dx, y = y0 - row * scale - dy;
                    if (x < 0 || y < 0 || x >= t.width || y >= t.height || t.GetPixel(x, y).a < 0.5f) continue;
                    t.SetPixel(x, y, col);
                }
            }
        }
    }
}
