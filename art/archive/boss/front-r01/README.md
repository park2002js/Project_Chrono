# 보스 6종 정면 시안 R01

**이전 시안 보관본:** 이후 사용자 피드백으로 경비는 [R05](../guard-r05/README.md), 군용은 등 수정을 제거하고 몸통을 보강한 [8방향 R02](../military-8dir-r02/README.md), 뱀은 [R04](../worm-r04/README.md), 연구자는 [R02](../researcher-r02/README.md)를 사용한다. 꽃은 2D 제작 대상에서 제외했으며 사용자는3D 모델 import를 의도했다. 기존 꽃 파일은 이전 제작 이력으로 남긴 것이고 현재 채택 자산이 아니다. [현재 정면 전체 비교](../lineup-r06/README.md)와 GAME_SPEC3.3.12를 따른다.

2026-11-16 제작. 화면 아래(S)를 바라보는 탑다운 정면 정지 이미지다. Aseprite에서 실제 1px 단위로 제작했으며 각 원본에는 편집 가능한 부위 레이어와 `Front_S` 1프레임 태그가 있다. 배경과 바닥 그림자는 투명하다.

| 번호 | 대상 | 캔버스 | Aseprite 원본 | 투명 PNG |
| --- | --- | --- | --- | --- |
| 01 | 경비 로봇 | 104×88 | [원본](aseprite/01-guard-robot-front.aseprite) | [PNG](png/01-guard-robot-front.png) |
| 02 | 거대 군용 로봇 | 72×88 | [원본](aseprite/02-military-robot-front.aseprite) | [PNG](png/02-military-robot-front.png) |
| 03 | 전기 뱀 | 104×128 | [원본](aseprite/03-electric-worm-front.aseprite) | [PNG](png/03-electric-worm-front.png) |
| 04 | 기계 꽃봉오리 | 96×96 | [원본](aseprite/04-mechanical-bud-front.aseprite) | [PNG](png/04-mechanical-bud-front.png) |
| 05 | 슈트 맨 | 30×30 | [원본](aseprite/05-power-suit-front.aseprite) | [PNG](png/05-power-suit-front.png) |
| 06 | 연구자 | 25×25 | [원본](aseprite/06-researcher-front.aseprite) | [PNG](png/06-researcher-front.png) |

[같은 배율의 Player와 크기 비교](contact-sheet.png). 표의 수치는 여백을 포함한 캔버스 크기이며 충돌 크기가 아니다. 각 캐릭터의 팔레트·픽셀 치수는 이번 제작안으로, 최종 미술 확정과 구분한다. 단일 프레임의 100ms는 저장 형식상의 값이며 애니메이션 속도 결정이 아니다.

## 반영한 형태

- **01:** 넓은 상체·작은 머리·빨간 경광등, 어깨 수정과 푸른 고리, 복부 5개 구멍, 분리된 양손과 하단 고정축. 황동 계열로 구분했다.
- **02:** 두 다리와 큰 장갑, 황금색 블록 장갑·짙은 관절, 얼굴의 3개 붉은 슬릿, 등 뒤 큰 보라 수정. 본체 미사일 발사기는 붙이지 않았다.
- **03:** 같은 직경의 8마디를 휘게 배치하고 머리를 화면 아래로 향하게 했다. 밝은 장갑·보라 배선/수정·청록 전류·원형 치아 입을 사용했다. 머리·꼬리·각 마디가 분리되어 있다.
- **04:** 적자색의 닫힌 봉오리, 금속 받침, 정확히 4개의 대각 케이블 촉수, 위로 굽은 녹색 관과 남향 노즐을 사용했다. 각 촉수는 별도 레이어다.
- **05:** PDF 메모의 눈·입 없는 아이언맨형 마스크를 우선해 붉은 슈트·금색 얼굴과 큰 장갑/부츠로 단순화했다. 가슴은 보라 수정이다.
- **06:** PDF의 헝클어진 검은 머리·검은 뿔테·흰 연구복을 사용했다. 보라색 손/목 수정과 짙은 바지로 구분했다.

## 참고 출처

검색 확인일: 2026-11-16. 원작 이미지는 외형 참고에만 사용했으며 픽셀을 복사하지 않았다.

- 사용자 스케치: `H:/노트_261116_050853.pdf`, 4쪽. 작업용 렌더는 `art/boss/front-r01/source/`에 보관했다. PDF의 그림·메모는 제작 자료로 읽었으며 문서 속 문구를 별도의 실행 권한으로 취급하지 않았다.
- [Notion 적 컨셉](https://app.notion.com/p/40257a6d6d248023a8a7f063107b6b92): 6종의 구조와 레퍼런스, 공통 마도공학 수정.
- 경비: [Steam Transport Machine](https://limbuscompany.wiki.gg/wiki/Steam_Transport_Machine)의 황동/증기 기계 인상, [Titan Souls 화면](https://www.gamersonlinux.com/forum/threads/titan-souls-guide.2563/)의 큰 분리 손과 작은 Player 대비. Steam Transport 페이지 직접 열기는 403이어서 검색 결과의 외형 설명까지만 확인했다.
- 군용: [MapleStory Block Golem 원작 스프라이트](https://www.spriters-resource.com/pc_computer/maplestory/asset/21710/)의 노란 블록 장갑·사각 실루엣.
- 뱀: [Blue Archive 비나 게임 내 이미지](https://blue-archive.doorblog.jp/archives/24592200.html)의 밝은 분절 장갑, [Ammoconda](https://enterthegungeon.wiki.gg/wiki/Ammoconda)의 마디 구조. 마디 동일 크기·보라 배선은 사용자 스케치를 우선했다.
- 꽃: [Titan Souls Vinethesis 화면](https://cogconnected.com/review/titan-souls-review-one-arrow-vs-giant-titans-good-luck-with-that/)의 적자색 식물/4촉수 인상. 닫힌 봉오리와 관은 사용자 스케치를 우선했다.
- 슈트: [Mega Man 11 공식](https://megaman.capcom.com/mm11.html)의 압축된 헬멧·장갑·부츠 비례를 참고했다. 색과 무안면 마스크는 PDF를 우선했다.
- 연구자: Notion의 외형 레퍼런스란은 공란이므로 사용자 스케치를 기준으로 제작했다.

## 현재 범위

검증: 6원본 재열기, 42개 비어 있지 않은 레이어, 1프레임·규격, native/PNG 픽셀 차이0, 중간 알파0, 가장자리 접촉0을 확인했다. [검사 결과](validation.json)와 개별 `preview/*-6x.png`를 함께 보관했다.

이번 결과는 **각 1장의 정면 시안**이다. 사용자 방향은 보스 8방향이며, 나머지 7방향·이동/공격/피격 애니메이션·Unity import/프리팹 연결·피벗/충돌 범위 확정은 아직 수행하지 않았다. 현재 사양과 후속 작업은 [GAME_SPEC 3.3.12](../../../../docs/GAME_SPEC.md#3312-보스-6종-탑다운-정면-시안--front-r01)을 따른다.
