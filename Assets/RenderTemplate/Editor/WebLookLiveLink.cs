using UnityEditor;
using UnityEngine;

// 메뉴 RenderTemplate > Live Link to Web (켜기/끄기, 체크 표시)
// 켜 두면:
//  ① 프리셋·포스트·재질 세트·재질 등 RenderTemplate 안의 에셋을 고치면 0.5초 뒤 자동으로 웹에 내보냄 (바뀐 것만 덮어씀)
//  ② Look Panel에서 프리셋을 고르면 live.json에 기록 → 웹 시뮬레이터가 같은 프리셋으로 따라 바뀜
// 웹 쪽(LookLayer)은 개발 모드에서 1초마다 live.json을 확인해서 바뀌면 다시 씌움
[InitializeOnLoad]
static class WebLookLiveLink
{
    const string Key = "RenderTemplate.LiveLink", Menu_ = "RenderTemplate/Live Link to Web";
    static bool On { get => EditorPrefs.GetBool(Key, false); set => EditorPrefs.SetBool(Key, value); }
    static double _due = -1;
    static int _lastIndex = -2;
    static LookSwitcher _sw;

    static WebLookLiveLink()
    {
        ObjectChangeEvents.changesPublished += OnChanges;
        EditorApplication.update += Tick;
    }

    [MenuItem(Menu_)]
    static void Toggle()
    {
        On = !On;
        if (On) _due = EditorApplication.timeSinceStartup;                    // 켜자마자 한 번 내보냄
        Debug.Log("[LiveLink] " + (On ? "켜짐 → " + WebLookExporter.OutDir : "꺼짐"));
    }
    [MenuItem(Menu_, true)]
    static bool ToggleCheck() { Menu.SetChecked(Menu_, On); return true; }

    // 에셋이 바뀌면 (인스펙터 수정, Reference Matcher 생성 등) 잠깐 모았다가 한 번에 내보냄
    static void OnChanges(ref ObjectChangeEventStream stream)
    {
        if (!On) return;
        for (int i = 0; i < stream.length; i++)
        {
            string path = null;
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.ChangeAssetObjectProperties:
                    stream.GetChangeAssetObjectPropertiesEvent(i, out var a); path = AssetDatabase.GUIDToAssetPath(a.guid); break;
                case ObjectChangeKind.CreateAssetObject:
                    stream.GetCreateAssetObjectEvent(i, out var c); path = AssetDatabase.GUIDToAssetPath(c.guid); break;
            }
            if (path != null && path.StartsWith("Assets/RenderTemplate/")) _due = EditorApplication.timeSinceStartup + 0.5;
        }
    }

    // 새 프리셋 만들기·복제·삭제처럼 "파일이 생기거나 없어지는" 변화도 잡음 (인스펙터 수정 감지로는 안 잡히는 경우)
    class Watcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!On) return;
            foreach (var list in new[] { imported, deleted, moved })
                foreach (var p in list)
                    if (p.StartsWith("Assets/RenderTemplate/") && !p.Contains("/Generated/")) { Schedule(); return; }
        }
    }
    static void Schedule() => _due = EditorApplication.timeSinceStartup + 0.5;

    static void Tick()
    {
        if (!On) return;
        if (!_sw) _sw = Object.FindFirstObjectByType<LookSwitcher>();
        if (_sw && _sw.Index != _lastIndex)                                       // 유니티에서 고른 프리셋이 바뀜 → 웹도 따라가게
        {
            _lastIndex = _sw.Index;
            try
            {
                System.IO.Directory.CreateDirectory(WebLookExporter.OutDir);
                // 아직 내보낸 적 없는 프리셋(방금 만든 것)이면 먼저 내보냄 → 웹이 바로 찾을 수 있게
                if (_sw.Current && !System.IO.File.Exists(System.IO.Path.Combine(WebLookExporter.OutDir, _sw.Current.name + ".json")))
                    WebLookExporter.Export(null, clean: false);
                else WebLookExporter.WriteLive(WebLookExporter.OutDir, _sw);
            }
            catch (System.Exception e) { Debug.LogWarning("[LiveLink] " + e.Message); }
        }
        if (_due > 0 && EditorApplication.timeSinceStartup >= _due)
        {
            _due = -1;
            try { Debug.Log("[LiveLink] " + WebLookExporter.Export(null, clean: false)); }
            catch (System.Exception e) { Debug.LogWarning("[LiveLink] 내보내기 실패: " + e.Message); }
        }
    }
}
