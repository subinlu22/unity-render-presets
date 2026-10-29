using UnityEngine;

// 프리셋 규칙 (룩 스타일 가이드): Reference Matcher가 새 프리셋을 만들 때 항상 따르는 기준값
// 레퍼런스 사진이 달라도 조명·색·재질·더티가 같은 "실사 기준" 안에 들어오게 해서 프리셋끼리 일관성을 지킴
// 파일: Assets/RenderTemplate/Rules/LookRules_Default.asset (인스펙터에서 고치면 이후 생성되는 프리셋에 적용)
[CreateAssetMenu(menuName = "RenderTemplate/Look Rules", fileName = "LookRules_New")]
public class LookRules : ScriptableObject
{
    [Header("조명: 시작점과 톤 보정 허용 범위")]
    public LookPreset baseLighting;                       // 조명·HDRI·카메라의 시작점 (비우면 화이트 라운지)
    [Range(0, 2)] public float exposureRange = 0.5f;      // 노출은 시작값 ± 이만큼만
    [Range(0, 30)] public float contrastRange = 8f;
    [Range(0, 30)] public float saturationRange = 12f;
    [Range(0, 30)] public float temperatureRange = 10f;
    [Range(0, 1.5f)] public float aoIntensity = 0.5f;     // 장면이 작아서 세면 벽에 검은 번짐
    [Range(0, 0.5f)] public float vignetteMax = 0.2f;
    [Range(0, 1)] public float contactShadow = 0.65f;

    [Header("색(알베도): 실제 물체 반사율 범위로 제한")]
    [Tooltip("사진 속 색에는 조명이 섞여 있음 → 그대로 쓰면 하얗게 뜨거나 까맣게 죽음. 실제 흰 페인트도 약 0.8")]
    [Range(0, 1)] public float albedoMax = 0.85f;
    [Range(0, 0.2f)] public float albedoMin = 0.04f;

    [Header("재질 종류별 표준값 (거칠기·금속성)")]
    public SurfaceRule paint = new SurfaceRule(0.75f, 0f);
    public SurfaceRule gloss = new SurfaceRule(0.25f, 0.05f);
    public SurfaceRule metal = new SurfaceRule(0.32f, 0.6f);
    public SurfaceRule marble = new SurfaceRule(0.12f, 0f);
    public SurfaceRule wood = new SurfaceRule(0.45f, 0f);
    public SurfaceRule oakFluted = new SurfaceRule(0.5f, 0f);
    public SurfaceRule plaster = new SurfaceRule(0.85f, 0f);
    public SurfaceRule terrazzo = new SurfaceRule(0.3f, 0f);

    [System.Serializable]
    public struct SurfaceRule
    {
        [Range(0, 1)] public float roughness;
        [Range(0, 1)] public float metalness;
        public SurfaceRule(float r, float m) { roughness = r; metalness = m; }
    }

    [Header("더티(낡음) 기본값: 부위별, 영역 지정 때 자동으로 채움")]
    [Range(0, 1)] public float wearWall = 0.15f;
    [Range(0, 1)] public float wearFloor = 0.2f;
    [Range(0, 1)] public float wearCounter = 0.1f;
    [Range(0, 1)] public float wearCabinet = 0.15f;
    [Range(0, 1)] public float wearMachine = 0.05f;
    [Range(0, 1)] public float wearMax = 0.9f;           // 이 이상은 못 올림

    [Header("웹 시뮬레이터 보정 기본값")]
    [Range(0, 4)] public float webEnvScale = 1f;
    [Range(0, 3)] public float webLightScale = 0.75f;

    public SurfaceRule Surface(ReferenceSetup.Surface s)
    {
        switch (s)
        {
            case ReferenceSetup.Surface.무광페인트: return paint;
            case ReferenceSetup.Surface.광택도장: return gloss;
            case ReferenceSetup.Surface.금속: return metal;
            case ReferenceSetup.Surface.흰대리석:
            case ReferenceSetup.Surface.어두운대리석: return marble;
            case ReferenceSetup.Surface.원목: return wood;
            case ReferenceSetup.Surface.오크세로홈: return oakFluted;
            case ReferenceSetup.Surface.회벽: return plaster;
            default: return terrazzo;
        }
    }

    public float DefaultWear(ReferenceSetup.Part p)
    {
        switch (p)
        {
            case ReferenceSetup.Part.벽: return wearWall;
            case ReferenceSetup.Part.바닥: return wearFloor;
            case ReferenceSetup.Part.상판: return wearCounter;
            case ReferenceSetup.Part.하부장: return wearCabinet;
            default: return wearMachine;
        }
    }

    // 사진에서 뽑은 색을 실제 반사율 범위로: 밝기(최대 채널)만 줄이거나 올리고 색감(비율)은 유지
    public Color ClampAlbedo(Color c)
    {
        float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (m > albedoMax) c = new Color(c.r * albedoMax / m, c.g * albedoMax / m, c.b * albedoMax / m, c.a);
        else if (m < albedoMin && m > 0.0001f) c = new Color(c.r * albedoMin / m, c.g * albedoMin / m, c.b * albedoMin / m, c.a);
        else if (m <= 0.0001f) c = new Color(albedoMin, albedoMin, albedoMin, c.a);
        return c;
    }
}
