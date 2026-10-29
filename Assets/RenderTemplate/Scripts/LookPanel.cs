using UnityEngine;

// 게임 화면 왼쪽 위 패널 (플레이 중). H 키로 숨기기/보이기 (Alt+Tab 창 전환에 반응하지 않게 Tab 대신 H).
// 프리셋 버튼을 누르면 바로 바뀌고, 슬라이더로 효과 강도·주변광을 조절하며 비교할 수 있음.
public class LookPanel : MonoBehaviour
{
    public LookSwitcher looks;
    public ScreenshotTool shots;
    public KeyCode hideKey = KeyCode.H;
    bool _show = true;
    Rect _win = new Rect(12, 12, 270, 10);
    string _msg = "";

    void Update() { if (Input.GetKeyDown(hideKey) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt)) _show = !_show; }

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
        // 크기 고정: 프리셋마다 내용이 달라도 창 크기가 변하지 않게 (높이는 프리셋 수로만 정함)
        _win.width = 300; _win.height = 330 + (looks.presets.Count + 1) * 24;
        _win = GUI.Window(7341, _win, Draw, "Render Presets  (H 숨기기)");
    }

    GUIStyle _wrap;

    void Draw(int id)
    {
        if (_wrap == null) _wrap = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = 11 };
        GUILayout.Space(4);
        GUILayout.Label("분위기 (L 키로도 넘김)");
        if (Button("원본", looks.Index < 0)) looks.Select(-1);
        for (int i = 0; i < looks.presets.Count; i++)
            if (Button(looks.presets[i].displayName, looks.Index == i)) looks.Select(i);
        // 설명은 항상 같은 높이 칸에 (없으면 빈 칸)
        GUILayout.Label(looks.Current ? looks.Current.note : "웹 시뮬레이터와 같은 원래 모습", _wrap, GUILayout.Height(46));

        if (Button(looks.HqOn ? "고퀄 재질: 켜짐 (M)" : "고퀄 재질: 꺼짐 (M)", looks.HqOn)) looks.ToggleHqByUser();

        // 슬라이더는 항상 그리고, 쓸 수 없을 때는 흐리게
        var vol = looks.PostVolume;
        GUI.enabled = vol;
        GUILayout.Label($"포스트 효과 강도 {(vol ? vol.weight : 0):0.00}");
        float w = GUILayout.HorizontalSlider(vol ? vol.weight : 0, 0, 1);
        if (vol) vol.weight = w;

        GUI.enabled = looks.Index >= 0;
        GUILayout.Label($"주변광 {RenderSettings.ambientIntensity:0.00}");
        float a = GUILayout.HorizontalSlider(RenderSettings.ambientIntensity, 0, 2);
        if (GUI.enabled && !Mathf.Approximately(a, RenderSettings.ambientIntensity)) { RenderSettings.ambientIntensity = a; DynamicGI.UpdateEnvironment(); }

        GUI.enabled = looks.Current && looks.Current.reference;
        GUILayout.Label($"레퍼런스 사진 겹쳐 보기 {_refAlpha:0.00}");
        _refAlpha = GUILayout.HorizontalSlider(_refAlpha, 0, 1);
        if (!GUI.enabled) _refAlpha = 0;
        GUI.enabled = true;

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (shots && GUILayout.Button("4K 스샷 (F12)")) _msg = "저장: " + System.IO.Path.GetFileName(shots.Save(looks.CurrentName));
        if (shots && GUILayout.Button("Before/After (B)")) { shots.StartBeforeAfter(); _msg = "Before/After 저장 중"; }
        GUILayout.EndHorizontal();
        GUILayout.Label(_msg, GUILayout.Height(20));
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
