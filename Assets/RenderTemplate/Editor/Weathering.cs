using UnityEditor;
using UnityEngine;

// 낡음 굽기: 재질의 지금 색(텍스처×색)을 한 장으로 구우면서 때·얼룩·흘러내린 자국을 섞음
// peel=true 면 페인트가 군데군데 벗겨져 밑의 어두운 나무가 드러남 (레트로 하부장)
public static class Weathering
{
    const string Gen = "Assets/RenderTemplate/Generated/";

    public static void Apply(Material m, string name, float wear, bool peel, int seed)
    {
        const int W = 1024, H = 512;
        var baseTex = m.GetTexture("baseColorTexture") as Texture2D;
        var tiling = baseTex ? m.GetTextureScale("baseColorTexture") : Vector2.one;
        Color tint = m.GetColor("baseColorFactor");
        Color[] src = null; int sw = 0, sh = 0;
        if (baseTex) src = Read(baseTex, out sw, out sh);
        var wood = Read(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RenderTemplate/ThirdParty/PolyHaven/Textures/wood_table_001/wood_table_001_diff_1k.jpg"), out int ww, out int wh);

        float ox = seed * 13.7f, oy = seed * 7.3f;
        var dirt = new Color(0.32f, 0.27f, 0.2f);
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                Color c = tint;
                if (src != null)
                {
                    int sx = (int)(Frac(u * tiling.x) * sw), sy = (int)(Frac(v * tiling.y) * sh);
                    c *= src[Mathf.Clamp(sy, 0, sh - 1) * sw + Mathf.Clamp(sx, 0, sw - 1)];
                }
                // 얼룩(큰 덩어리) + 흘러내린 자국(세로로 긴 무늬) + 아래쪽이 더 더러움
                float blot = Fbm(u * 6 + ox, v * 3 + oy);
                float drip = Mathf.PerlinNoise(u * 60 + ox, v * 2.5f + oy) * Mathf.PerlinNoise(u * 9 + oy, v * 1.2f + ox);
                float bottom = Mathf.Clamp01(1 - v * 3f) * 0.6f;
                float g = Mathf.Clamp01((blot - 0.45f) * 2.2f) * 0.6f + Mathf.Clamp01((drip - 0.25f) * 3f) * 0.5f + bottom;
                c = Color.Lerp(c, c * dirt * 1.6f, Mathf.Clamp01(g * wear * 0.75f));
                // 작은 긁힘·점
                float speck = Mathf.PerlinNoise(u * 300 + ox, v * 150 + oy);
                if (speck > 1 - 0.06f * wear) c *= 0.75f;
                if (peel)
                {
                    float pm = Fbm(u * 22 + oy, v * 11 + ox) * 0.8f + Mathf.PerlinNoise(u * 70, v * 35) * 0.25f + bottom * 0.25f;   // 잘게, 아래쪽이 더 벗겨짐
                    float edge = 1 - wear * 0.3f;
                    if (pm > edge)
                    {
                        var wc = wood[(int)(Frac(v * 2) * (wh - 1)) * ww + (int)(Frac(u * 3) * (ww - 1))] * new Color(0.55f, 0.4f, 0.3f);
                        c = pm > edge + 0.03f ? wc : Color.Lerp(c, new Color(0.9f, 0.88f, 0.8f), 0.5f);   // 벗겨진 가장자리는 밝은 페인트 테두리
                    }
                }
                c.a = 1;
                px[y * W + x] = c;
            }
        var t = new Texture2D(W, H, TextureFormat.RGB24, true);
        t.SetPixels(px); t.Apply();
        string path = Gen + Safe(name) + "_wear.png";
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        m.SetTexture("baseColorTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        m.SetTextureScale("baseColorTexture", Vector2.one);
        m.SetColor("baseColorFactor", Color.white);
        if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", Mathf.Lerp(m.GetFloat("roughnessFactor"), 0.85f, wear * 0.6f));
    }

    static float Frac(float f) => f - Mathf.Floor(f);
    static float Fbm(float x, float y) => Mathf.PerlinNoise(x, y) * 0.55f + Mathf.PerlinNoise(x * 2.1f, y * 2.1f) * 0.3f + Mathf.PerlinNoise(x * 4.3f, y * 4.3f) * 0.15f;
    static string Safe(string s) => string.Join("_", s.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(' ', '_');

    public static Color[] Read(Texture2D tex, out int w, out int h)
    {
        w = tex.width; h = tex.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var px = t.GetPixels(); Object.DestroyImmediate(t);
        return px;
    }
}
