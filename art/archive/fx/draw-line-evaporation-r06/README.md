# Draw 증발 R06

사용자는 R05의 제자리 noise/UV 분해를 반려하고, 실제 빛 조각이 사방으로 퍼져 올라가며 사라지는 증발을 요청했다. 알파 왕복과 R04 발광 이미지·양끝 마감·표시 두께는 유지한다. 이 판은 구현·검증 결과이며 새 연출의 사용자 미술 승인은 아직 받지 않았다.

- `DrawEvaporationVfx`의 재사용 ParticleSystem이 원래 선 전체에서 한 번 방출한다. 월드 X 양옆·Y 위쪽으로 실제 이동하며 크기를 유지하고 알파로 소멸한다. 원래 선은 초기 25% 동안 입자로 넘겨진다. R05 noise·UV 지우기와 분해 API는 제거했다.
- 적용값: 전체 1.3초, 길이1당12조각(6→72·12→144, 최대768), 조각 수명은 전체의65~95%, X속도 ±0.5~1.3·Y속도0.9~1.8·위쪽 힘0.35. 실제 원본 PNG를 입자 전용 URP 재질에서도 사용한다.
- 씬 루트 객체를 한 번 준비해 재사용하고 Skill의 같은 게임 시간으로 수동 진행한다. 정지에서 위치·수명·색이 보존되며 비활성화는 렌더러만 숨긴다. 교체·수동 해제·사망·종료에서 입자를 정리한다. 입력 점 표시는 계속 만들지 않는다.

## 실제 녹화

[미리보기 GIF](draw-evaporation-preview.gif)는 Unity Game View 1093×515의 실제 30° 플레이 녹화다. 원본64캡처 중 동일한 빈 화면1장이 GIF에서 합쳐져63프레임, 총3.94초다. 관찰 게임 시간은 약3.466초이며 마지막0.5초는 반복 전 빈 화면을 보여 주기 위한 미리보기 여백이다. 별도 모사 애니메이션이 아니다.

검사 튜닝은 Line 수명2.4초·경고 비율0.9·왕복 주기0.4→0.08초·증발1.3초다. 게임 기본 경고 비율0.35·주기0.5→0.08초와 구분한다. `Encode-Preview.py`는 PNG를 실제 기록 시간으로 GIF에 인코딩한다. `frames.csv`, `particles.csv`, `preview-info.json`, `capture-validation.json`에 관찰값을 보관했다. 실제 PNG4장과 사용 셰이더2개도 보관했다.

72조각을 식별자로 추적한 결과 최대 크기 오차0, X변위 약−1.458~+1.526·최대 상승2.321월드 단위, 전부 알파 감소, 마지막 입자0개를 확인했다. GIF 표본의 평균 RGB 오차는 0~255 기준 약0.008~0.036이다.

## 검증 범위

동일 Unity6000.3.21f1 격리 사본에서 컴파일·Draw/편집/수명/Slash/Title23묶음 성공·프로세스 종료0을 확인했다. 실제 이동·고정3D크기·알파 감소·정지·표시 복구·재사용·교체·사망·Title 정리를 포함한다. R04 PNG SHA256은 `798FC9A404D216339A0F4C80439E17A0C5A2E8DDCD2E44AC25A3913CB98373D8`로 유지했다.

새 shader GUID 오기와 ParticleSystem 정지 전 seed 설정 경고를 수정했다. 새 소수 이동속도에서 드러난 기존 Fever 검사4개 수치의 정확 비교는 `Mathf.Approximately`로 바꿨으며 runtime 효과는 바꾸지 않았다. 이후23묶음이 모두 통과했다.

검증 사본은 병행 공격·Slash 코드의 필수 참조를 기존 `PlayerArtSetup.Apply`·`SlashPresentationSetup.Run`으로 준비한 뒤 `PlayerDrawSetup.RunChecks`를 실행했다. 검증 시점 C#78개를 기록했고 현재 Draw 소유11파일은 사본과 일치한다. 이후 병행 작업에서 AttackController·PlayerCharacter·PlayerAttackIntegrationChecks가 변경되어 전체 최신 C# 일치로 주장하지 않는다. source·scope·result와 실제 로그는 `project/Logs/DrawEvaporationVerification-*` 및 `.log`에 보관한다.

원본에는 검사된 Draw 재질2개와 새 컴포넌트·셰이더·준비 도구·검사, Player의 증발 재질 참조/1.3초 값만 해당 범위로 반영했다. 사본의 Player 전체·공격/Slash 자산은 복사하지 않았다. 원본 Editor 직접 Play/최종 import·제출 빌드는 미확인이다.
