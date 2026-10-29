# RenderTemplate: 커피머신 미니 렌더링 템플릿

cafeSim(웹 three.js 커피머신 시뮬레이터, 내 포크 subinlu22/cafesim)의 3D 장면을 유니티로 가져와서
라이팅·포스트 효과·분위기를 **프리셋 파일 하나**로 갈아끼우며 비교하는 툴. (Unity 6000.3.11f1, Built-in RP)

원본 파일(모델·재질·씬)은 바뀌지 않음. 프리셋은 화면에만 덧씌우고, 끄면 그대로 돌아옴.

## 패널
- **에디터 창**: 메뉴 `RenderTemplate > Look Panel`. 플레이 안 해도 버튼으로 프리셋을 바로 씌워 보고,
  "수정"으로 프리셋 값 고치기 → "다시 적용", "지금 프리셋 복제"로 내 프리셋 만들기, 고퀄 재질 토글, 효과 강도 슬라이더, 4K 스샷
- **게임 화면 패널**: 플레이하면 왼쪽 위에 뜸 (H로 숨김). 프리셋 버튼, 고퀄 재질, 효과 강도·주변광 슬라이더, 스샷 버튼

## 웹 시뮬레이터에 적용 (메뉴 RenderTemplate > Export Looks to Web)
- 프리셋마다 look.json + 텍스처를 웹 시뮬레이터(cafesim 포크, look-presets 브랜치)의 `packages/viewer/public/looks/`로 내보냄
- 웹의 LookLayer가 돌아가는 시뮬레이터 위에 같은 룩을 씌움 (오른쪽 아래 "룩" 선택창, "원본"이면 원래대로)
- 재질 연결: 유니티 렌더러 이름 Node-N = glb 노드 → glb 재질 번호, 웹은 GLTFExporter 순서로 같은 번호를 찾음
- 웹 포스트 효과는 three.js(GTAO·UnrealBloom·색 보정) 버전이라 유니티와 조금 다름, DOF 없음

### 실시간 연동 (메뉴 RenderTemplate > Live Link to Web, 체크로 켜고 끔)
- 켜 두면 프리셋·포스트·재질 등 RenderTemplate 에셋을 고칠 때마다 0.5초 뒤 자동으로 내보냄 (바뀐 것만)
- Look Panel에서 프리셋을 고르면 웹 시뮬레이터도 같은 프리셋으로 바뀜 (live.json)
- 웹은 개발 모드(`npm run dev`) 또는 주소에 `?live=1`일 때 1초마다 확인해서 1~2초 안에 반영 ("유니티에서 … 반영" 표시)
- 유니티와 웹 시뮬레이터를 같은 PC에서 둘 다 켜 두면 됨. 서버 추가 없이 파일로 주고받음
- 다 만든 뒤 배포용으로는 메뉴 Export Looks to Web(정리 후 전체 내보내기) 한 번

## 프리셋 규칙 (Rules/LookRules_Default, 룩 스타일 가이드)
Reference Matcher가 새 프리셋을 만들 때 항상 따르는 기준. 레퍼런스가 달라도 같은 "실사 기준" 안에서 만들어져 프리셋끼리 일관됨.
| 분류 | 기본값 |
|---|---|
| 조명 | 시작 조명 = 화이트 라운지, 톤 보정 범위: 노출 ±0.5 · 대비 ±8 · 채도 ±12 · 색온도 ±10, AO 0.5, 비네트 최대 0.2, 접지 그림자 0.65 |
| 색(알베도) | 사진에서 뽑은 색을 실제 반사율 범위(0.04~0.85)로 제한 → 하이라이트·그림자 색 때문에 하얗게 뜨거나 까맣게 죽지 않게 |
| 재질 거칠기/금속 | 페인트 0.75 · 광택도장 0.25 · 금속 0.32/0.6 · 대리석 0.12 · 원목 0.45 · 오크 세로 홈 0.5 · 회벽 0.85 · 테라조 0.3 |
| 더티 | 벽 0.15 · 바닥 0.2 · 상판 0.1 · 하부장 0.15 · 기계 0.05 (최대 0.9), 매처 창 "규칙대로 기본값 채우기"로 자동 입력 |
| 웹 보정 | 환경광 ×1.0, 조명 ×0.75 |
규칙 파일을 복제해서(예: 더 어둡고 거친 "인더스트리얼 규칙") 설정 파일의 "프리셋 규칙"에 넣으면 다른 기준으로 만들 수 있음.

## Reference Matcher: 레퍼런스 사진으로 프리셋 만들기 (메뉴 RenderTemplate > Reference Matcher)
1. "새 설정 만들기" → 레퍼런스 사진(png/jpg, Assets 안), 프리셋 이름, 시작 프리셋(조명·카메라) 지정
2. 부위(벽·바닥·상판·하부장·커피머신·제빙기·컵 디스펜서)마다 "영역 지정"을 누르고 사진 위를 드래그, 재질 종류 선택
   (무광페인트·광택도장·금속·흰대리석·어두운대리석·원목·오크세로홈·회벽·테라조)
3. "프리셋 만들기 + 톤 자동 맞추기"를 누르면 툴이 자동으로
   - 영역 색 추출(중앙값) → 재질 종류에 입혀 재질 생성 (원목·대리석은 결을 유지한 채 그 색으로 다시 칠함)
   - 디스펜서 본체가 어두우면 앞판 인쇄를 밝은 글씨로 반전
   - 벽 왼쪽/오른쪽 밝기 차이로 주 조명 방향 결정
   - 사진과 렌더의 밝기(중앙값)·대비(밝은쪽-어두운쪽)·채도·색온도를 비교해 포스트 값을 반복 보정, 오차를 창에 표시
4. 결과는 `Resources/Looks/Look_50_*`에 새 프리셋으로 생기고 L 키·패널 목록에 바로 들어감. 모델은 그대로

예시: `References/RefSetup_Vintage` (빈티지 그린, 톤 오차 0.879 → 0.166), `References/RefSetup_Retro` (레트로 분식, 0.801 → 0.030)

## 레퍼런스 사진에 맞추기 (수동 프리셋)
- 프리셋의 `reference`에 사진을 넣으면 Look Panel에 사진이 함께 보이고, 게임 화면 패널에서는 슬라이더로 반투명하게 겹쳐 볼 수 있음
- 프리셋마다 카메라 앵글·화각(`overrideCamera`)과 전용 재질 세트(`materialSet`)를 가질 수 있음. 모델은 그대로, 색·재질만 바꿈
- 레퍼런스 프리셋 값은 `Editor/ReferenceLooks.cs`에 정리 (재질 번호는 `Materials/HQ/_분류결과.txt`)

- Look Panel에서 고른 프리셋은 씬에 기억돼서 플레이를 눌러도 같은 룩으로 시작함 (LookSwitcher의 startPreset)
- 씬 뷰에서도 포스트 효과를 보려면 씬 뷰 위쪽 효과 메뉴(그림 아이콘)에서 Post Processing 켜기. 씬 뷰는 프리셋 카메라 앵글을 따르지 않음

## 단축키 (플레이 모드)
| 키 | 기능 |
|---|---|
| L | 분위기 전환: 원본 → 화이트 라운지 → 다크 라운지 → (Reference Matcher로 만든 프리셋) → 원본 |
| M | 고퀄 재질 세트 켜기/끄기 (원본 재질 ↔ 복제해서 조정한 재질) |
| H | 패널 숨기기/보이기 |
| F12 | 지금 화면 4K(3840x2160) 스크린샷 |
| B | 같은 앵글로 Before(원본) / After(지금 분위기) 두 장 |

스크린샷 저장 위치: `프로젝트 폴더/Screenshots/` (git에는 안 올라감)

## 프리셋
| 파일 | 이름 | 내용 |
|---|---|---|
| (없음) | 원본 | 웹과 같은 조명(직사광+보조광+3색 주변광), ACES 톤매핑 |
| Look_30_WhiteLounge | 화이트 라운지 | `References/ref_install_01.png`에 맞춤. 밝은 라운지, 흰 대리석·밝은 오크 세로 홈, 부드러운 확산광, 3/4 앵글 |
| Look_40_DarkLounge | 다크 라운지 | 건메탈 커피머신·네이비 디스펜서(앞판 인쇄 색 반전), 진한 대리석, 시멘트 벽, 따뜻한 벽등 |
| Look_50_Ref_Vintage | 빈티지 그린 | Reference Matcher로 생성한 예시 (원목 상판·올리브 그린 하부장·크림색 디스펜서) |
| Look_50_Ref_Retro | 레트로 분식 | Reference Matcher로 생성한 예시 (테라조 상판·머스터드 제빙기·청록 디스펜서·청록 하부장) |

## 새 분위기 추가하기 (쉐이더팩처럼)
1. `Resources/Looks/` 안에서 기존 프리셋을 Ctrl+D로 복제 (또는 우클릭 > Create > RenderTemplate > Look Preset)
2. 이름·순서(order)·조명·HDRI·포스트 프로필(`Presets/PP_*` 복제해서 수정)을 바꾸기
3. 플레이 → L 키 목록에 자동으로 들어감

## 구조
- `Scripts/LookPreset.cs`: 프리셋 데이터(환경·조명·프로브·포스트)
- `Scripts/LookSwitcher.cs`: 켤 때 원래 상태 기억 → 적용, 끌 때 되돌림 (추가한 조명·프로브는 저장 안 되는 임시 오브젝트)
- `Scripts/ScreenshotTool.cs`: 카메라를 4K 텍스처에 다시 그려 저장 (포스트 효과 포함)
- `Editor/RenderTemplateBuilder.cs`: 메뉴 **RenderTemplate > Build Demo Scene**: 프리셋·씬을 처음부터 다시 만들기
- 씬의 `RenderTemplate` 오브젝트를 끄면 원본과 똑같음. 라이트맵 베이크 없음 (실시간 조명 + 실시간 반사 프로브)

## 모델 가져오기 (cafesim → 유니티)
웹 시뮬레이터를 로컬에서 띄우고(`npm run dev`), 브라우저 콘솔에서 three.js `GLTFExporter`로
장면(조명 제외)을 `Models/cafesim_a100.glb`로 내보냄. 키오스크 화면은 HTML이라 모델에 포함되지 않음.
시뮬레이터 동작(결제·추출)과의 실시간 연동은 아직 없음 (다음 단계).

## 외부 에셋 출처 (모두 CC0)
| 에셋 | 출처 | 라이선스 |
|---|---|---|
| cayley_interior (HDRI 2k) | https://polyhaven.com/a/cayley_interior | CC0 |
| painted_plaster_wall (텍스처 1k) | https://polyhaven.com/a/painted_plaster_wall | CC0 |
| wood_table_001 (텍스처 1k) | https://polyhaven.com/a/wood_table_001 | CC0 |
| photo_studio_loft_hall (HDRI 2k) | https://polyhaven.com/a/photo_studio_loft_hall | CC0 |
| white_oak_veneer (텍스처 1k) | https://polyhaven.com/a/white_oak_veneer | CC0 |
| Marble012 (텍스처 1K) | https://ambientcg.com/view?id=Marble012 | CC0 |

키오스크 화면 이미지(`Models/kiosk_screen.png`)는 내 포크 cafesim 뷰어(`?view=screen`)를 캡처해서 화면 부분만 자른 것.

HDRI·텍스처 파일은 git에 안 올림. 받기: `python Tools/fetch_polyhaven.py`

## Unity MCP (MCP for Unity 10.3.0)
`Packages/com.coplaydev.unity-mcp/`에 내장 패키지로 넣어 둠 (94MB라 git 제외).
이 드라이브에서는 git 주소로 설치가 실패해서 복사 방식을 씀. 다른 PC에서는 Package Manager >
Add package from git URL: `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0`
에디터를 열면 서버가 http://127.0.0.1:8080 에서 자동으로 켜짐.
