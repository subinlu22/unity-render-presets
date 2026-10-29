using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

// 분위기 프리셋 한 벌 (쉐이더팩처럼 파일 하나 = 분위기 하나).
// Assets/RenderTemplate/Resources/Looks/ 폴더에 넣으면 LookSwitcher가 자동으로 목록에 추가함.
// 만들기: Project 창 우클릭 > Create > RenderTemplate > Look Preset
[CreateAssetMenu(menuName = "RenderTemplate/Look Preset", fileName = "Look_New")]
public class LookPreset : ScriptableObject
{
    public string displayName = "새 프리셋";
    public int order = 10;                       // L 키로 넘길 때 순서 (작은 것부터)

    [Header("환경: HDRI 주변광·반사")]
    public Material skybox;                      // Skybox/Panoramic + HDRI
    [Range(0, 3)] public float ambientIntensity = 1f;
    [Range(0, 2)] public float reflectionIntensity = 1f;
    public bool showSkybox = false;              // false면 배경은 단색
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f);

    [Header("안개")]
    public bool fog = false;
    public Color fogColor = Color.gray;
    public float fogDensity = 0.05f;

    [Header("원래 씬 조명 밝기 배율 (0 = 끔)")]
    [Range(0, 2)] public float originalLightScale = 0f;

    [Header("추가 조명 (키·필·림 등)")]
    public RigLight[] lights;

    [System.Serializable]
    public struct RigLight
    {
        public string name;
        public LightType type;                   // Directional / Spot / Point
        public Color color;
        public float intensity;
        public Vector3 position;                 // 머신 중심 기준 (Spot/Point)
        public Vector3 euler;                    // 비추는 방향
        public float range;
        [Range(1, 179)] public float spotAngle;
        public bool shadows;
    }

    [Header("리플렉션 프로브 (실시간, 베이크 안 함)")]
    public bool reflectionProbe = true;
    public Vector3 probeSize = new Vector3(3, 2, 2);
    [Range(0, 2)] public float probeIntensity = 1f;

    [Header("포스트 프로세싱 (AO·비네트·Bloom·DOF·톤매핑)")]
    public PostProcessProfile post;
}
