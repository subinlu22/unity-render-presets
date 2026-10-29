using System.Collections.Generic;
using UnityEngine;

// 재질 세트: "원본 재질 → 고퀄 재질" 짝 목록 (원본은 안 건드리고 복제본을 씀)
[CreateAssetMenu(menuName = "RenderTemplate/Material Set", fileName = "MaterialSet_New")]
public class MaterialSet : ScriptableObject
{
    [System.Serializable]
    public struct Pair { public Material from; public Material to; }
    public Pair[] pairs;

    public Material Find(Material m)
    {
        if (pairs != null) foreach (var p in pairs) if (p.from == m) return p.to;
        return null;
    }
}

// 모델 아래 렌더러의 재질을 세트대로 바꾸고, 끄면 원래 재질로 되돌림
public class MaterialSwapper
{
    readonly Dictionary<Renderer, Material[]> _orig = new Dictionary<Renderer, Material[]>();
    public bool IsOn => _orig.Count > 0;

    public void Apply(Transform root, MaterialSet set)
    {
        if (IsOn || !root || !set) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var to = set.Find(mats[i]);
                if (to) { if (!changed) _orig[r] = (Material[])mats.Clone(); mats[i] = to; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    public void Restore()
    {
        foreach (var kv in _orig) if (kv.Key) kv.Key.sharedMaterials = kv.Value;
        _orig.Clear();
    }
}
