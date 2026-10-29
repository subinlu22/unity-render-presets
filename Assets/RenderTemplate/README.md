# RenderTemplate: 커피머신 미니 렌더링 템플릿

cafeSim(웹 three.js 커피머신 시뮬레이터, 내 포크 subinlu22/cafesim)의 3D 장면을 유니티로 가져와서
라이팅·포스트 효과·분위기를 **프리셋 파일 하나**로 갈아끼우며 비교하는 툴. (Unity 6000.3.11f1, Built-in RP)

원본 파일(모델·재질·씬)은 바뀌지 않음. 프리셋은 화면에만 덧씌우고, 끄면 그대로 돌아옴.

## 패널
- **에디터 창**: 메뉴 `RenderTemplate > Look Panel`. 플레이 안 해도 버튼으로 프리셋을 바로 씌워 보고,
  "수정"으로 프리셋 값 고치기 → "다시 적용", "지금 프리셋 복제"로 내 프리셋 만들기, 고퀄 재질 토글, 효과 강도 슬라이더, 4K 스샷
- **게임 화면 패널**: 플레이하면 왼쪽 위에 뜸 (Tab으로 숨김). 프리셋 버튼, 고퀄 재질, 효과 강도·주변광 슬라이더, 스샷 버튼

## 레퍼런스 사진에 맞추기
- 프리셋의 `reference`에 사진을 넣으면 Look Panel에 사진이 함께 보이고, 게임 화면 패널에서는 슬라이더로 반투명하게 겹쳐 볼 수 있음
- 프리셋마다 카메라 앵글·화각(`overrideCamera`)과 전용 재질 세트(`materialSet`)를 가질 수 있음. 모델은 그대로, 색·재질만 바꿈
- 레퍼런스 프리셋 값은 `Editor/ReferenceLooks.cs`에 정리 (재질 번호는 `Materials/HQ/_분류결과.txt`)

## 단축키 (플레이 모드)
| 키 | 기능 |
|---|---|
| L | 분위기 전환: 원본 → 실사 카페 → 스튜디오 → 화이트 라운지 → 다크 라운지 → 원본 |
| M | 고퀄 재질 세트 켜기/끄기 (원본 재질 ↔ 복제해서 조정한 재질) |
| Tab | 패널 숨기기/보이기 |
| F12 | 지금 화면 4K(3840x2160) 스크린샷 |
| B | 같은 앵글로 Before(원본) / After(지금 분위기) 두 장 |

스크린샷 저장 위치: `프로젝트 폴더/Screenshots/` (git에는 안 올라감)

## 프리셋
| 파일 | 이름 | 내용 |
|---|---|---|
| (없음) | 원본 | 웹과 같은 조명(직사광+보조광+3색 주변광), ACES 톤매핑 |
| Look_30_WhiteLounge | 화이트 라운지 | `References/ref_install_01.png`에 맞춤. 밝은 라운지, 흰 대리석·밝은 오크 세로 홈, 부드러운 확산광, 3/4 앵글 |
| Look_40_DarkLounge | 다크 라운지 | 건메탈 커피머신·네이비 디스펜서(앞판 인쇄 색 반전), 진한 대리석, 시멘트 벽, 따뜻한 벽등 |
| Look_05_RealCafe | 실사 카페 | 실내 HDRI 배경, 나무 바닥·회벽 세트, 창가 햇빛·펜던트, 고퀄 재질(스테인리스·석재·나무·유리), 키오스크 화면, AO·Bloom·DOF·그레인 |
| Look_10_Studio | 스튜디오 | 키(따뜻)·필(약)·림 조명, HDRI 주변광, 리플렉션 프로브, AO·ACES·약한 비네트 |

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
| studio_small_09 (HDRI 1k) | https://polyhaven.com/a/studio_small_09 | CC0 |
| cayley_interior (HDRI 2k) | https://polyhaven.com/a/cayley_interior | CC0 |
| wood_floor (텍스처 1k) | https://polyhaven.com/a/wood_floor | CC0 |
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
