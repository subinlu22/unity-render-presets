using UnityEngine;

// 레퍼런스 매칭 설정 한 벌: 레퍼런스 사진 + 사진 위에 지정한 영역(벽·상판·하부장·기계 본체…) + 재질 종류
// Reference Matcher 창이 이걸 읽어서 → 영역 색 추출 → 재질 생성 → 새 프리셋 → 톤 자동 맞추기
[CreateAssetMenu(menuName = "RenderTemplate/Reference Setup", fileName = "RefSetup_New")]
public class ReferenceSetup : ScriptableObject
{
    public Texture2D reference;
    public string presetName = "새 레퍼런스 룩";
    public LookPreset basePreset;                // 조명·HDRI·카메라는 여기서 시작
    public Slot[] slots = DefaultSlots();
    public LookPreset output;                    // 만들어진 프리셋

    public enum Surface { 무광페인트, 광택도장, 금속, 흰대리석, 어두운대리석, 원목, 오크세로홈, 회벽, 테라조 }   // 새 종류는 맨 뒤에 추가 (저장된 값 유지)
    public enum Part { 벽, 바닥, 상판, 하부장, 커피머신, 제빙기, 컵디스펜서 }

    [System.Serializable]
    public class Slot
    {
        public Part part;
        public bool use = true;
        public Surface surface;
        public Rect rect;                        // 사진 기준 0~1 (왼쪽 위가 0,0)
        public Color sampled = Color.gray;       // 영역에서 뽑은 색 (자동)
        [Range(0, 1)] public float wear;          // 낡음: 때·얼룩·흘러내린 자국 (하부장 페인트면 벗겨짐)
        public int stickers;                      // 붙일 스티커 수 (벽은 포스터 수)
        public Slot(Part p, Surface s) { part = p; surface = s; use = false; }
    }

    public static Slot[] DefaultSlots() => new[]
    {
        new Slot(Part.벽, Surface.회벽), new Slot(Part.바닥, Surface.무광페인트), new Slot(Part.상판, Surface.흰대리석),
        new Slot(Part.하부장, Surface.오크세로홈), new Slot(Part.커피머신, Surface.광택도장),
        new Slot(Part.제빙기, Surface.광택도장), new Slot(Part.컵디스펜서, Surface.광택도장),
    };

    [Header("톤 맞추기 결과 (자동)")]
    [TextArea(3, 8)] public string log;
}
