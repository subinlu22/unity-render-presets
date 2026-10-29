using UnityEngine;

// 게임 화면 왼쪽 위 패널 (플레이 중). Tab 키로 숨기기/보이기.
// 프리셋 버튼을 누르면 바로 바뀌고, 슬라이더로 효과 강도·주변광을 조절하며 비교할 수 있음.
public class LookPanel : MonoBehaviour
{
    public LookSwitcher looks;
    public ScreenshotTool shots;
    public KeyCode hideKey = KeyCode.Tab;
    bool _show = true;
    Rect _win = new Rect(12, 12, 270, 10);
    string _msg = "";

    void Update() { if (Input.GetKeyDown(hideKey)) _show = !_show; }

    float _refAlpha = 0f;                        // 레퍼런스 사진 겹쳐 보기 (0 = 안 보임)

    void OnGUI()
    {
        if (!looks) return;
        var refTex = looks.Current ? looks.Current.reference : null;
        if (refTex && _refAlpha > 0.01f)
        {
            var old = GUI.color; GUI.color = new Color(1, 1, 1, _refAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), refTex, ScaleMode.ScaleAndCrop);
            GUI.color = old;
        }
        if (!_show) return;
        _win = GUILayout.Window(7341, _win, Draw, "Render Presets  (Tab 숨기기)");
    }

    void Draw(int id)
    {
        GUILayout.Label("분위기 (L 키로도 넘김)");
        if (Button("원본", looks.Index < 0)) looks.Select(-1);
        for (int i = 0; i < looks.presets.Count; i++)
            if (Button(looks.presets[i].displayName, looks.Index == i)) looks.Select(i);
        if (looks.Current && !string.IsNullOrEmpty(looks.Current.note))
            GUILayout.Label(looks.Current.note, GUI.skin.box);

        GUILayout.Space(6);
        if (Button(looks.HqOn ? "고퀄 재질: 켜짐 (M)" : "고퀄 재질: 꺼짐 (M)", looks.HqOn)) looks.ToggleHqByUser();

        if (looks.PostVolume)
        {
            GUILayout.Label($"포스트 효과 강도 {looks.PostVolume.weight:0.00}");
            looks.PostVolume.weight = GUILayout.HorizontalSlider(looks.PostVolume.weight, 0, 1);
        }
        if (looks.Index >= 0)
        {
            GUILayout.Label($"주변광 {RenderSettings.ambientIntensity:0.00}");
            float a = GUILayout.HorizontalSlider(RenderSettings.ambientIntensity, 0, 2);
            if (!Mathf.Approximately(a, RenderSettings.ambientIntensity)) { RenderSettings.ambientIntensity = a; DynamicGI.UpdateEnvironment(); }
        }

        if (looks.Current && looks.Current.reference)
        {
            GUILayout.Label($"레퍼런스 사진 겹쳐 보기 {_refAlpha:0.00}");
            _refAlpha = GUILayout.HorizontalSlider(_refAlpha, 0, 1);
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (shots && GUILayout.Button("4K 스샷 (F12)")) _msg = "저장: " + System.IO.Path.GetFileName(shots.Save(looks.CurrentName));
        if (shots && GUILayout.Button("Before/After (B)")) { shots.StartBeforeAfter(); _msg = "Before/After 저장 중"; }
        GUILayout.EndHorizontal();
        if (_msg != "") GUILayout.Label(_msg);
        GUI.DragWindow();
    }

    static bool Button(string text, bool on)
    {
        var old = GUI.color;
        if (on) GUI.color = new Color(1f, 0.8f, 0.4f);
        bool r = GUILayout.Button((on ? "▶ " : "") + text);
        GUI.color = old;
        return r;
    }
}
