using System.Collections;
using System.IO;
using UnityEngine;

// 4K 스크린샷. 화면 크기와 상관없이 카메라를 3840x2160 텍스처에 한 번 더 그려서 저장함
// (포스트 효과까지 그대로 나옴, 화면 글씨(OnGUI)는 안 찍힘).
// F12: 지금 분위기 한 장 / B: 같은 앵글로 "원본" + "지금 분위기" 두 장 (Before/After)
// 저장 위치: 프로젝트 폴더/Screenshots/
public class ScreenshotTool : MonoBehaviour
{
    public Camera cam;
    public LookSwitcher looks;
    public int width = 3840, height = 2160;
    public KeyCode shotKey = KeyCode.F12, beforeAfterKey = KeyCode.B;

    void Update()
    {
        if (Input.GetKeyDown(shotKey)) Save(looks ? looks.CurrentName : "shot");
        if (Input.GetKeyDown(beforeAfterKey) && looks) StartCoroutine(BeforeAfter());
    }

    IEnumerator BeforeAfter()
    {
        int now = looks.Index;
        string stamp = System.DateTime.Now.ToString("HHmmss");
        looks.Select(-1);
        yield return null; yield return null;        // 조명·반사 갱신 기다림
        Save("원본", stamp + "_A_before");
        looks.Select(now);
        yield return null; yield return null;
        Save(looks.CurrentName, stamp + "_B_after");
    }

    public string Save(string look, string prefix = null)
    {
        return Render(cam, width, height, Path.Combine(Application.dataPath, "..", "Screenshots"),
                      (prefix ?? System.DateTime.Now.ToString("HHmmss")) + "_" + look + ".png");
    }

    // 에디터 스크립트에서도 쓰려고 static
    public static string Render(Camera c, int w, int h, string folder, string file)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var old = c.targetTexture;
        c.targetTexture = rt;
        c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        c.targetTexture = old;
        RenderTexture.active = null;
        Directory.CreateDirectory(folder);
        string path = Path.GetFullPath(Path.Combine(folder, file));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        if (Application.isPlaying) { Destroy(rt); Destroy(tex); } else { DestroyImmediate(rt); DestroyImmediate(tex); }
        Debug.Log("[Screenshot] " + path);
        return path;
    }
}
