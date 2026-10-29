using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 RenderTemplate > Look Panel
// 플레이 안 해도 버튼 하나로 프리셋을 씌웠다 벗겼다 하며 Scene/Game 뷰에서 바로 비교하는 창.
// 새 프리셋: "지금 프리셋 복제" → 인스펙터에서 값 수정 → "다시 적용"으로 확인
public class LookPanelWindow : EditorWindow
{
    Vector2 _scroll;

    [MenuItem("RenderTemplate/Look Panel")]
    public static void Open() => GetWindow<LookPanelWindow>("Look Panel");

    static LookSwitcher Find() => FindFirstObjectByType<LookSwitcher>();

    void OnGUI()
    {
        var sw = Find();
        if (!sw)
        {
            EditorGUILayout.HelpBox("씬에 RenderTemplate(LookSwitcher)가 없음. RenderTemplate > Build Demo Scene 으로 만들기", MessageType.Info);
            return;
        }
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("분위기 프리셋", EditorStyles.boldLabel);
        if (Btn("원본 (프리셋 끄기)", sw.Index < 0)) Select(sw, -1);
        for (int i = 0; i < sw.presets.Count; i++)
        {
            var p = sw.presets[i];
            EditorGUILayout.BeginHorizontal();
            if (Btn(p.displayName, sw.Index == i)) Select(sw, i);
            if (GUILayout.Button("수정", GUILayout.Width(44))) Selection.activeObject = p;
            EditorGUILayout.EndHorizontal();
        }
        if (sw.Current && !string.IsNullOrEmpty(sw.Current.note)) EditorGUILayout.HelpBox(sw.Current.note, MessageType.None);
        if (sw.Current && sw.Current.reference)
        {
            // 레퍼런스 사진을 창 폭에 맞춰 보여줌 → Game 뷰와 나란히 비교
            EditorGUILayout.LabelField("레퍼런스 사진", EditorStyles.miniBoldLabel);
            var tex = sw.Current.reference;
            float w = position.width - 12, h = w * tex.height / tex.width;
            GUILayout.Label(GUIContent.none, GUILayout.Width(w), GUILayout.Height(h));
            GUI.DrawTexture(GUILayoutUtility.GetLastRect(), tex, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("목록 새로고침")) { sw.LoadPresets(); Repaint(); }
        if (GUILayout.Button("다시 적용")) { sw.Reapply(); RepaintViews(); }
        EditorGUILayout.EndHorizontal();
        using (new EditorGUI.DisabledScope(!sw.Current))
            if (GUILayout.Button("지금 프리셋 복제 → 새 프리셋")) Duplicate(sw);

        EditorGUILayout.Space();
        if (Btn(sw.HqOn ? "고퀄 재질: 켜짐" : "고퀄 재질: 꺼짐", sw.HqOn)) { sw.ToggleHqByUser(); RepaintViews(); }
        if (sw.PostVolume)
        {
            float w = EditorGUILayout.Slider("포스트 효과 강도", sw.PostVolume.weight, 0, 1);
            if (!Mathf.Approximately(w, sw.PostVolume.weight)) { sw.PostVolume.weight = w; RepaintViews(); }
        }

        EditorGUILayout.Space();
        var shot = sw.GetComponent<ScreenshotTool>();
        if (shot && GUILayout.Button("4K 스크린샷 (지금 화면)"))
            EditorUtility.RevealInFinder(shot.Save(sw.CurrentName));
        EditorGUILayout.HelpBox("원본 파일(모델·재질·씬)은 바뀌지 않음. 씬 저장할 때는 자동으로 원본 상태로 저장됨.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    static void Select(LookSwitcher sw, int i) { sw.Select(i); RepaintViews(); }
    static void RepaintViews() { SceneView.RepaintAll(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); }

    static bool Btn(string text, bool on)
    {
        var old = GUI.backgroundColor;
        if (on) GUI.backgroundColor = new Color(1f, 0.75f, 0.35f);
        bool r = GUILayout.Button((on ? "▶ " : "") + text, GUILayout.Height(24));
        GUI.backgroundColor = old;
        return r;
    }

    static void Duplicate(LookSwitcher sw)
    {
        string src = AssetDatabase.GetAssetPath(sw.Current);
        string dst = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(src).Replace('\\', '/') + "/Look_90_Custom.asset");
        AssetDatabase.CopyAsset(src, dst);
        var p = AssetDatabase.LoadAssetAtPath<LookPreset>(dst);
        p.displayName = sw.Current.displayName + " (내 버전)"; p.order = 90;
        EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
        sw.LoadPresets(); sw.Select(sw.presets.IndexOf(p));
        Selection.activeObject = p;
    }

    void OnInspectorUpdate() => Repaint();
}

// 씬 저장 직전에 프리셋을 잠깐 끄고, 저장 후 다시 켬 → 씬 파일에는 항상 원본 상태만 저장
[InitializeOnLoad]
static class LookSaveGuard
{
    static int _was = -1; static bool _hq;
    static LookSaveGuard()
    {
        EditorSceneManager.sceneSaving += (s, p) =>
        {
            var sw = Object.FindFirstObjectByType<LookSwitcher>();
            if (!sw) return;
            _was = sw.Index; _hq = sw.HqOn;
            sw.Select(-1); sw.SetHq(false);
        };
        EditorSceneManager.sceneSaved += s =>
        {
            var sw = Object.FindFirstObjectByType<LookSwitcher>();
            if (!sw || (_was < 0 && !_hq)) return;
            sw.Select(_was); if (_hq && !sw.HqOn) sw.ToggleHqByUser();
            _was = -1; _hq = false;
        };
    }
}
