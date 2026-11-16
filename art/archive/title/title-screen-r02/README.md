# Title R02 — 검 흰 테두리와 틈새 푸른 광

2026-11-16 사용자 요청. 검의 손잡이·검집을 알아보기 쉽도록 원본 Aseprite에 흰색1px 테두리를 추가하고, 원본 검흔 선을 따라 약3px 범위의 푸른 광을 별도 VFX로 준비했다.

- **현재 원본:** `../logo_17_natural_cut_r06/title-logo-17-natural-cut.aseprite`. 이번 요청에 따라 직접 수정했다. 같은 이름의 원본 PNG와 `project/Assets/Art/UI/Title/title-logo.png`에도 동일 출력을 반영했으며 기존 Unity `.meta`는 수정하지 않았다.
- **보관:** `before-title-logo-17-natural-cut.aseprite`·PNG는 수정 직전 바이트 보존본이다. 이 폴더의 접두사 없는 Aseprite·PNG는 이번 R02 결과 사본이다. 기존 R01의 원본 보존 검사는 당시 버전에 대한 이력이다.
- **흰 테두리:** 원래 손잡이6번·검집9번 레이어의 불투명240px에서 바깥4방향으로 정확히1px를 더했다. 가드·노출 은날은 제외했다. 새 `11 White grip and saya outline 1px` 레이어96px만 흰색으로 추가했으며 원래 검정 채움과 글자·&·검흔·빛을 포함한 기존10개 레이어의 픽셀·메타데이터를 보존했다. 원래 십자 하이라이트는 새 테두리 위에 남는다. 원본200×80·1프레임·100ms도 유지한다.
- **VFX 출력:** `title-fissure-glow-0.png`·`-1.png`·`-2.png`,200×80 RGBA. 기존 로고에 굽지 않는다. 원본 검흔의 픽셀 중심(3.5,48.5)→(196.5,22.5)을 따라 수직거리±3px 안쪽에서만 빛을 두고, 양끝7px에서 투명하게 마감했다. 실제 alpha가 있는 최대 거리는2.891px다. 전체 선의 약한 푸른 빛을 유지하며 검집 중심(130.5,31.5)에서 양쪽으로 번지는 강한 구간의 위상만 세 장에서 달라진다.
- **게임 연결:** Runtime은 Root 작업에서 세 장을 인접 위상끼리 crossfade하고 밝기를 실제 시간으로 변조한다. 기존 십자 VFX와 별도로 겹친다. ±3px는 원본 로고 픽셀 기준이며1920×1080의 기존6배 로고에서는 중심선 한쪽 약18화면px다. 폭·색·이동 위상은 이 요청의 첫 제작 적용값이며 사용자 최종 미술 판단과 구분한다.

`outline-before-after-6x.png`는 수정 전/후, `fissure-glow-phases-6x.png`는 새 원본 위에 세 위상을 각각85%로 겹친 정적 검토판이다. 실제 Unity의 시간 변화 검증·미리보기는 Root의 F-U01 검사 결과를 따른다.

재생성·검사:

1. 저장소 루트에서 `py -3.11 art/archive/title/title-screen-r02/generate-assets.py`를 실행한다. 보존한 이전 원본을 읽어 테두리 PNG와 푸른 광3장을 생성하며 현재 원본을 덮어쓰지 않는다.
2. Aseprite1.3.18.6에서 `apply-outline.lua`에 `--script-param output=<이 폴더의 절대 경로>`를 전달해 실행하면 이전 원본 위에 독립 테두리 레이어를 얹은 새 native·PNG를 이 폴더에 저장한다. 이후 사용자 편집이 생기면 보존본에서 다시 만드는 대신 현재 native의 해당 레이어를 편집한다.
3. `py -3.11 art/archive/title/title-screen-r02/validate-assets.py`로 기존10층 RGBA/메타·테두리1px·변경96px·기존 십자 위층·PNG/native 합성·원본/Unity PNG 일치·푸른 광 폭/색/세 위상 차이를 검사한다. 결과는 `asset-validation.json`이며 생성 수치는 `glow-generation-validation.json`, 수정 전 해시는 `source-before.json`이다.

검사 결과는 파일·원본 보존 검증이다. 실제 런타임·사용자 미술 승인·제출 빌드 완료를 뜻하지 않는다.

## Unity 연결·실제 화면 검증

같은 Unity6000.3.21f1 격리 일반 Editor에서 최신 C# 컴파일과 TitleVisualOnly7묶음·종료0을 확인했다. 실제 정지중 두 푸른 광 화면에서 지속 발광·흐름/밝기 변화·소스 alpha마스크 밖 보존, 기존십자와 가로슬롯 프레임·완료확인취소를 확인했다. 신규3개 meta는 테스트한 import값을GUID동일 검사 후 반영했고 기존 meta는 보존했다. 소유18파일과 검사 시점 런타임은 원본/검사본이 같다.

`title-fissure-preview.gif`는1920×1080 실제60캡처를960×540로 줄인6.99초 미리보기다. `fissure-a.png`/`fissure-b.png`는 푸른 광만 있는 두 실제 위상, `peak.png`는 기존 십자도 표시되는 상태, `original-only.png`는 파일 원본 대조를 위해 VFX를 잠시 숨긴 검사 화면이다. 마지막 이미지는 정상 재생에서 빛이 꺼지는 상태를 뜻하지 않는다. `runtime-preview-validation.json`에 실제 변화범위·메뉴 동일 여부를 기록했다.

초기 snapshot은 동시 보스 클래스 생성 전을 가져와 컴파일이 중단됐으나 최신 클래스까지 복사한 최종 실행은 통과했다. 이전 보스로 대체하지 않았다. 경비prefab 연결이 다른 작업에서 진행 중이므로 명시적 시각검사 옵션으로 게임 진입·Title복귀는 실행하지 않았다. 원본 Editor 직접Play·제출 빌드·사용자 미술 승인은 별도다. 상세 로그/한계는 GAME_SPEC3.3.14 R02와 `project/Logs/TitleFissureR02-verification.json`을 따른다.
