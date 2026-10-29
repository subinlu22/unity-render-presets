using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

// 분위기 프리셋 한 벌 (쉐이더팩처럼 파일 하나 = 분위기 하나). 원본 파일은 안 바꾸고 화면에만 덧씌움.
// Assets/RenderTemplate/Resources/Looks/ 폴더에 넣으면 LookSwitcher·패널 목록에 자동으로 추가됨.
// 만들기: Look Panel의 "지금 프리셋 복제" 버튼, 또는 Project 창 우클릭 > Create > RenderTemplate > Look Preset
[CreateAssetMenu(menuName = "RenderTemplate/Look Preset", fileName = "Look_New")]
public class LookPreset : ScriptableObject
{
    public string displayName = "새 프리셋";
    public int order = 10;                       // 목록 순서 (작은 것부터)
    [TextArea] public string note;               // 패널에 보일 한 줄 설명

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

    [Header("접지 그림자 (물건이 떠 보이지 않게, 0 = 끔)")]
    [Range(0, 1)] public float contactShadow = 0.6f;

    [Header("세트: 이 프리셋에서만 보일 배경 (벽·바닥 등 프리팹)")]
    public GameObject roomPrefab;

    [Header("재질: 켜면 재질 세트로 바꿈 (M 키로도 따로 토글)")]
    public bool hqMaterials = false;
    public MaterialSet materialSet;              // 비우면 기본 고퀄 세트 사용 (레퍼런스별 색·재질은 여기에)

    [Header("카메라 (켜면 이 앵글·화각으로, 끄면 원래 카메라)")]
    public bool overrideCamera = false;
    public Vector3 cameraOffset = new Vector3(0, 0.3f, 2f);   // 머신 중심 기준 카메라 위치
    public Vector3 lookOffset;                                 // 머신 중심 기준 바라볼 곳
    [Range(10, 90)] public float fov = 40f;

    [Header("웹 시뮬레이터 보정 (웹은 반사·환경광 계산이 달라서 룩마다 밝기를 맞춤, Live Link로 바로 확인)")]
    [Range(0, 4)] public float webEnvScale = 1f;     // HDRI 환경광·반사 배율 (어두운 HDRI면 올림)
    [Range(0, 3)] public float webLightScale = 1f;   // 추가 조명 배율
    [Range(-2, 2)] public float webExposure = 0f;    // 웹 노출 추가 (EV)

    [Header("레퍼런스 사진 (Look Panel에서 겹쳐 보기)")]
    public Texture2D reference;
}
