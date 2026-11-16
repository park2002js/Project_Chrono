# SaveSlot·설정창 공용 픽셀 프레임 R01

2026-11-16 요청의 검정·파랑과 짙은 보라 포인트로 제작한 **빈 슬롯 한 개**다. 후속 가로 배열·Unity 반영 요청으로 실제 Title에 상단`saveslot`·가로3카드를 연결했다. 픽셀 원본은 그대로 사용하며 상세 실행 범위는 GAME_SPEC3.3.15를 따른다.

| 파일 | 용도 |
| --- | --- |
| [layout-r02/horizontal-slots.png](layout-r02/horizontal-slots.png) | **현재 Unity 화면**, 뒤로 제거·카드144×206/하단310 |
| [layout-r02/verification.json](layout-r02/verification.json) | R02 컴파일 오류0·실제2검사·종료0과 소스 대조 |
| [window-frame.aseprite](window-frame.aseprite) | 편집 원본, 256×64px·RGBA·정지1프레임·4레이어 |
| [window-frame.png](window-frame.png) | 실제 적용용 원본 크기 PNG, 바깥 투명·안쪽 불투명 |
| [preview-slot-4x.png](preview-slot-4x.png) | 단일 슬롯4배 확대 검토 |
| [preview-layout-1920x1080.png](preview-layout-1920x1080.png) | 상단 `saveslot`·아래 세로3슬롯 사용 예시 |
| [preview-horizontal-unity-1920x1080.png](preview-horizontal-unity-1920x1080.png) | 이전 Unity 가로144×144/뒤로 버튼 배치 이력 |
| [preview-confirmation-unity-1920x1080.png](preview-confirmation-unity-1920x1080.png) | 실제 완료 기록 새 게임/취소 확인창 |
| `preview-horizontal-1280x720.png`, `preview-horizontal-1920x1080.png`, `preview-horizontal-2560x1440.png` | Aseprite에서 출력한 가로 배열 크기 예시, 글씨는 예시 |
| [preview-settings-1920x1080.png](preview-settings-1920x1080.png) | 동일 PNG를320×224 원본 크기의 큰 창으로 확장한 예시 |
| `preview-layout-1280x720.png`, `preview-layout-2560x1440.png` | 동일 구도의 다른 해상도 예시 |
| `Export-PNG.cmd`, `export.lua` | 원본 편집·저장 후 PNG·미리보기 재출력, Aseprite 원본에는 쓰지 않음 |
| `validation.json`, `independent-validation.json` | 실제 파일·확대·슬라이스 검사 결과 |
| [generation-prompt.txt](generation-prompt.txt) | 내장 이미지 생성 도구에 전달한 시안 프롬프트 |

## 디자인과 편집

11색(투명 포함), 안쪽 #070A12·검정 외곽·남색/파랑 단차·#301D44/#442B58 계열의 어두운 보라 인레이를 사용한다. 보라 장식은 네 모서리 고정 영역에 두고 중간 변과 내부는 균일하게 만들어 큰 설정창에도 이어 쓸 수 있다.

레이어는 `01_Outer_Shadow`, `02_Blue_Bevel`, `03_Near_Black_Fill`, `04_Dark_Violet_Inlays`다. `saveslot`, 저장 정보와 설정 항목은 이미지에 포함하지 않았다. 미리보기의 글씨·3슬롯 배치는 사용 예시이고 실제 저장 데이터나 확정 UI 폰트·배치가 아니다.

내장 `image_gen`으로 디자인 시안을 생성한 뒤 Aseprite1.3.18.6에서1px 격자·고정 팔레트·레이어·슬라이스를 갖춘 원본으로 다시 제작했다. 생성 시안은 작업 폴더 `/art/ui/window-frame-r01/design-reference.png`, 최종 사용 파일은 이 폴더의 Aseprite/PNG다. 재출력은 원본 저장 후 `Export-PNG.cmd`를 실행한다.

## 크기와 Unity 적용

- **9-slice:** Left/Right/Top/Bottom 모두 **12px**, center `(12,12,232,40)`. Aseprite 원본에도 `window-frame` slice로 저장했다. [Aseprite의 slice.center 공식 설명](https://aseprite.org/api/slice#slicecenter)을 확인했다.
- **확장:** 모서리를 고정하고 중앙·변만 늘린다. 통째로 가로/세로를 다르게 확대하지 않는다. 프레임이 차지하는 최소 기하 크기는25×25px지만, 글자 공간과 가독성을 고려한 슬롯 예시는256×64px다.
- **픽셀:** Point/Nearest, Clamp, Mipmap 끔, 압축 없음, NPOT 크기 보존. 선명한 픽셀 크기를 원하면 UI 전체를 정수 배율로 표시한다. 640×360 논리 화면의2/3/4배를720p/1080p/1440p 예시에 사용했으며 기존 프로젝트의 기준 해상도를 변경한 것은 아니다. 다른 화면비는 UI 앵커·여백으로 대응하고 프레임 비율은9-slice로 조정한다.
- **uGUI:** PNG를 Sprite Single·Full Rect로 import하고 Border12, Image Type `Sliced`로 사용한다. 픽셀 확대 배율은 Canvas/표시 배율과 함께 맞춘다. PPU 값만으로 모든 해상도가 자동 대응되는 것은 아니다.
- **현재 프로젝트의 IMGUI:** `UI_Title`이 Texture2D와 `GUIStyle.border`를 사용하는 방식으로 별도 연결해야 한다. Sprite Border만 저장하면 현재 슬롯 버튼이 바뀌지 않는다.

첫 요청은 이미지와 적용 규격 준비였으며 이후 후속 요청으로 Unity에 반영했다. 실제 Sprite는 `project/Assets/Art/UI/Windows/window-frame.png`(GUID`662a92fe29ad429ba68a2bf6889f3c19`)이며`Title.unity`의`UI_Title.SlotFrame`에 연결했다. `UIArtSetup.Apply`로 다시 연결할 때도12px 경계를 유지한다. 현재 슬롯은640×360 논리 화면의 `(80+168×slot,104,144,206)`에 배치하고 기존1920×1080 표시 안에서3배로 그린다. R02 후속 요청으로 뒤로 버튼을 제거하고 이전 버튼 하단310까지 카드를 늘렸다. 프레임 바깥 치수와 모서리가 함께 표시 배율을 따른다. 같은 이미지를 완료 기록 확인창에도 사용한다. 설정창 기능 자체와 제출 빌드는 수행하지 않았다.

## 확인 결과

현재 R02는11:12:31 KST에 같은 Unity6000.3.21f1 격리 일반 Editor의2검사/컴파일 오류0,11:12:33 종료0을 확인했다. 실제1080p의 카드 하단310·파란 하단 프레임·이전 뒤로 글자 영역의 패널 채움·완료 확인/취소를 확인했다.112대상 예상 밖 차이0·53/53런타임 일치이며 프레임 원본/GUID는 유지했다. `layout-r02/`에 현재 화면·확인창과 result/process/verification을 보존한다. 다른 해상도는 Rect 투영, 월드 재진입·제출 빌드는 미실행이다. 아래 검사는 이전 배치와 원본 제작 이력이다.

Aseprite에서 저장한 native를 별도 프로세스로 재열기·PNG 출력했고, RGBA32bit·4레이어·1프레임·slice 중심·11색·alpha0/255·중앙 단색을 확인했다.160×48·256×64·416×64·320×224의9-slice 결과에서 네 모서리12×12픽셀이 원본과 동일했다. 별도 PNG/ASE 파서로 보라92px의 모서리 내부 배치, 변의 균일성,720p/1080p/1440p의 정확한2/3/4배 최근접 확대를 확인했다. 실제 단일 슬롯·3슬롯·설정창 예시를 시각 검토했다.

후속 Unity 확인의 원본은`docs/GAME_SPEC.md`3.3.15다. 실제1080p 가로 카드/완료 확인창·프레임 색/12px 모서리·취소 시 저장 보존·실제 슬롯0 월드 진입/Title복귀를 확인했다.720p/1440p·가로/세로 화면비는 실제 카드 Rect의 좌표 투영 검사이며 해당 해상도의 별도 Unity 캡처와 구분한다. R01 Title과 슬롯의7검사 결과는`unity-result-r01.json`이다. 동시 Title 연출 변경과 최신 소스 대조 범위는 별도 integration 검증 기록을 따른다.

최신 검사는 Unity6000.3.21f1 일반 Editor에서 컴파일 오류0·슬롯/import/완료 확인2묶음·종료0을11:00:07 KST에 확인했다.53/53 런타임 C#·112대상 중 예상 밖 차이0, 검사 드라이버만 격리 사본의2검사로 다르다. 근거는`unity-integration-validation.json`·`unity-current-result.json`·`unity-current-process.json`이다. 같은 시점 다른 Boss 프리팹의 손 연결이 작업 중이어서 최신 월드 진입은 제외했고, 이는 새 런타임 오류를 관찰했다는 뜻이 아니다. 안정 R01 사본의 실제 월드 진입/Title복귀 결과와 구분한다. 원본 직접 수동 Play·제출 빌드는 미실행이며 현재 Notion 기록은 완료했다.

## 정보·입력 R03

[현재 굵은 글자·정보 간격](info-r03/slots-bold.png)과 [Title 메뉴](info-r03/menu-bold.png), 실제 Esc·글자 비교5검사의 [결과](info-r03/verification.json)를 저장했다. 프레임 원본과 R02 카드 하단 확장은 보존하며 현재 입력·표시 사양과 미실행 범위는 GAME_SPEC3.3.15 R03을 따른다.
