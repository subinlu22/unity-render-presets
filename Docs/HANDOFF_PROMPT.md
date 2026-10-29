# 핸드오프 프롬프트 (다른 Claude 세션에 그대로 붙여 넣기)

> 아래 내용을 새 대화의 첫 메시지로 붙여 넣으면, 이 프로젝트의 현재 상태와 규칙을 이어받아 작업할 수 있습니다.

---

너는 내(수빈) Unity 툴 프로젝트를 이어서 도와줄 거야. 답은 한국어로 짧게, 확인 못 한 건 추측하지 말고 "확인 못 함"이라고 해줘. 기능 하나 끝날 때마다 바뀐 파일 목록을 보여주고 커밋해. 새 스크립트는 쉬운 말로 핵심 흐름을 주석으로 설명해.

## 프로젝트 한 줄 요약
웹 커피머신 학습 시뮬레이터(cafeSim, three.js)를 포크해서, **모델·시뮬레이션 기능은 그대로 두고 "보이는 룩(조명·재질·색감·더티)"만 갈아끼우는 Unity 룩 프리셋 툴**을 만들었다. 레퍼런스 사진 한 장으로 프리셋을 반자동 생성하고(Reference Matcher), 만든 룩을 **돌아가는 웹 시뮬레이터에 실시간으로 적용**한다(Live Link).

## 저장소 / 경로
| 무엇 | 위치 |
|---|---|
| Unity 툴 (메인) | `E:\cam_p\UnityRenderPresets` → github.com/subinlu22/unity-render-presets (`main`) |
| 웹 시뮬레이터 포크 | `E:\cam_p\UnityMCP_CoffeeMachine` → github.com/subinlu22/cafesim (`look-presets` 브랜치) |
| 원본 레포 | samcho93/cafesim — **절대 건드리거나 push 하지 말 것** |
| 로봇팔 프로젝트 | `E:\cam_p\CameraDigitalTwin` — **건드리지 말 것** (LookPreset 아이디어의 출처) |

- Unity 6000.3.11f1, Built-in RP, Post Processing v2 (3.5.4), glTFast 6.0.1, MCP for Unity 10.3.0(내장 패키지, git 제외)
- 웹: three.js 0.170, Vite 6. **E: 드라이브(exFAT)는 심볼릭 링크가 안 돼서 `npm install` 불가** → 포크를 C: 임시 폴더에 복사해서 `npm run dev` (그 복사본의 `public/looks`만 E: 저장소로 정션 연결)
- E: 드라이브에서 git은 `git -c safe.directory=<경로>` 필요, 커밋 뒤 repack 경고는 무시해도 됨

## 핵심 구성 (Unity: `Assets/RenderTemplate/`)
- `Scripts/LookPreset.cs` 프리셋 데이터(환경 HDRI·조명·반사 프로브·포스트·재질 세트·카메라·접지 그림자·웹 보정·레퍼런스 사진)
- `Scripts/LookSwitcher.cs` 원래 상태 기억 → 프리셋 적용/복원, `Resources/Looks`의 프리셋 자동 로드, 시작 프리셋 기억, 모델 재질 glb 원본 복구
- `Scripts/MaterialSet.cs` 원본 재질 ↔ 복제 재질 교체(원본 수정 안 함)
- `Scripts/ContactShadows.cs` 접지 그림자(기계 밑·벽 닿는 곳·하부장 밑)
- `Scripts/LookPanel.cs` 게임 화면 패널(H로 숨김) / `Editor/LookPanelWindow.cs` 에디터 창(RenderTemplate > Look Panel)
- `Scripts/ScreenshotTool.cs` 4K 스샷(F12), 같은 앵글 Before/After(B)
- `Scripts/LookRules.cs` 프리셋 규칙(조명 범위·알베도 0.04~0.85·재질별 거칠기·부위별 더티 기본값·웹 보정) → `Rules/LookRules_Default.asset`
- `Scripts/ReferenceSetup.cs` + `Editor/ReferenceMatcher.cs` 레퍼런스 사진 → 영역 지정 → 색 추출 → 재질 생성 → 톤 자동 보정
- `Editor/Weathering.cs` 더티 텍스처 굽기(얼룩·흘러내림·하부장 페인트 벗겨짐) / `Editor/Dressing.cs` 스티커·포스터 절차 생성
- `Editor/WebLookExporter.cs` 프리셋 → `look.json`+텍스처 (RenderTemplate > Export Looks to Web)
- `Editor/WebLookLiveLink.cs` 실시간 연동 (RenderTemplate > Live Link to Web)
- `Editor/RenderTemplateBuilder.cs` 데모 씬·고퀄 재질 세트·라운지 프리셋 재생성 (RenderTemplate > Build Demo Scene)
- 웹: `packages/viewer/src/look/LookLayer.js` (+ `main.js` 2줄), 룩 파일 `packages/viewer/public/looks/`

## 현재 프리셋
원본 / 화이트 라운지 / 다크 라운지 / 레트로 분식(매처 생성, 스티커·포스터·더티) / 빈티지 그린(매처 생성)

## 지켜야 할 규칙
- 모델(지오메트리)은 절대 수정 안 함. 색·재질·조명·톤·배경 소품(판)만
- 외부 에셋은 CC0만(Poly Haven, ambientCG), 큰 원본은 gitignore + `Tools/fetch_polyhaven.py`로 받기
- 웹 시뮬레이터 본체(Stage.js)는 수정 안 함(LookLayer가 렌더 호출만 가로챔)
- 라이트맵 베이크 안 함(실시간 조명 + 실시간 반사 프로브)

## 알려진 이슈 / 다음 할 일
- Unity가 플레이 모드인 채로 남아 있었음 → 플레이 끈 뒤 씬 모델 재질을 glb 원본으로 정리·저장 필요(`LookSwitcher.ResetModelMaterials`)
- 웹 포스트는 three.js 버전이라 Unity와 100% 같지 않음(DOF 없음, 금속 반사 차이) → 룩별 "웹 시뮬레이터 보정"으로 맞춤
- Reference Matcher 창의 마우스 영역 드래그 UI는 직접 써본 검증이 아직 없음(좌표를 코드로 넣어 테스트함)
- 데모릴: 데모 모드(턴테이블·프리셋 자동 순환·Before/After 와이프) + Unity Recorder 설정은 아직 안 만듦
