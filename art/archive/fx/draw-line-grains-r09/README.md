# Draw 알갱이 Line 팔레트 R09

사용자는 알갱이 색을 Line VFX 팔레트 그대로 따르도록 요청했다. 입자 재질의 `_LinePaletteTex`를 실제 `DrawImageFX.mat.mainTexture`와 같은 R04 PNG에 연결하고, 임의로 지정했던 색 상수를 제거했다. 원형·화면1px 외곽·기존 중심 크기·이동·수명/알파 흐름은 유지한다.

셰이더는 같은 이미지의 흰 중심/청록 코로나/파랑 오라에서 RGB만 샘플한다. UV는 각각 `(0.5,0.5)`·`(0.5,0.578125)`·`(0.5,0.70703125)`이고 원본 bilinear RGB는0~255기준 약 `(253.5,253.25,253)`·`(81,234,249.75)`·`(37.25,166.75,252.75)`이다. 원본 샘플 좌표/색은 `palette-samples.json`에 기록했다. 텍스처의 긴 선 모양은 입자에 표시하지 않으며, alpha는 기존 원형 coverage와 입자 수명으로 제어한다.

`PlayerDrawSetup.PrepareEvaporationMaterial(imageMaterial)`이 같은 Texture 참조를 연결하므로 준비 도구 재실행에서도 이 관계가 유지된다. PNG SHA256 `798FC9A404D216339A0F4C80439E17A0C5A2E8DDCD2E44AC25A3913CB98373D8`와 기존 재질/셰이더 GUID를 보존했다.

## 실제 확인

Unity6000.3.21f1 격리 사본에서 C#/shader 오류0·Draw/편집/수명/Slash/Title23묶음 성공·프로세스 종료0을 확인했다. 추가 검사에서 입자 `_LinePaletteTex`와 실제 live Line의 mainTexture가 같은 참조인 것을 확인했다. 기존 native 센터·원형 직경·고정 크기·알파/정지/정리 검사도 통과했다. 실제 화면에서 흰 중심/청록/파란 외곽을 확인했다.

`draw-grains-preview.gif`는 실제30° Line의 Game View1093×515 녹화다.64캡처가 동일 화면 병합 후62프레임·3.89초로 저장됐다. 관찰 게임 시간3.478759초, 마지막0.5초는 반복 전 빈 화면 여백이다. 검사 튜닝은 Line 수명2.4초·경고 비율0.9·왕복0.4→0.08초·증발1.3초이며 게임 기본 경고값과 구분한다. PNG4장·시간/입자CSV·palette/검사JSON·셰이더·인코더를 보관했다.

72입자의 관찰 직경0.046440~0.084193·가로/세로 차이0·크기 변화0,72개 alpha 감소·마지막 입자0을 확인했다. 실제 입자가 있는 `frame-0049`의 GIF 평균 RGB 오차는0~255기준 약0.026이다.

## 검증 범위

동시 작업을 덮어쓰지 않도록 기존 R08 검증 사본에 R09 변경4파일(준비 도구·수명 검사·입자 셰이더·입자 재질)을 적용해 검사했다. 이4파일은 원본과 일치한다. 검증 C#81개를 기록했고 현재 런타임41개가 일치한다. 이후 동시 수정된16파일은 `project/Logs/DrawGrainPaletteVerification-scope.json`에 구분했으며 현재 최신 전체 게임 실행을 확인한 것으로 주장하지 않는다.

로그·source/result/scope JSON은 같은 `DrawGrainPaletteVerification` 이름으로 보관한다. 확인된 `DrawEvaporation.mat`만 원본으로 복사했고 Player 전체·다른 작업 자산은 복사하지 않았다. 원본 Editor 직접 통합 Play/최종 import·제출 빌드와 새 결과의 사용자 미술 승인은 미확인이다.
