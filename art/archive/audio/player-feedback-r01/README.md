# Player 피드백 음원 R01

2026-11-16. 사용자가 선택한 세피리아 음원 추출·Unity 내장 오디오 연결용6개 WAV다. FMOD Unity 패키지는 설치하지 않았다. 게임 연결·발동 시점·Unity import/Play는 아래 후속 격리 검증으로 확인했으며 주관적 청감·최종 믹스 검토는 별도다.

| 출력 | 실제 원본 표본 · FSB subsong(1부터) | 원본 이벤트 | 길이 |
| --- | --- | --- | ---: |
| AttackSwingA.wav | attackSwish07 ·380 | event:/Scene/attackSwish07 | 0.318167초 |
| AttackSwingB.wav | attackSwish06 ·638 | event:/Scene/attackSwish06 | 0.181646초 |
| AttackHit.wav | hitSlash01 ·197 | event:/Scene/CrashSlice | 0.281250초 |
| FootstepTile01.wav | footstepRoad01 ·432 | event:/Scene/footstepRoad | 0.187500초 |
| FootstepTile02.wav | footstepRoad02 ·206 | 같은 이벤트 | 0.250000초 |
| FootstepTile03.wav | footstepRoad03 ·259 | 같은 이벤트 | 0.250000초 |

원작 기본 대검1·2타의 swingSoundEvent는 둘 다 `8b1b83c1-18c8-4d6b-820d-7ba509da9e18`이고, bank의 실제 이벤트 `attackSwish07`은 표본도 하나뿐이다. 사용자 요청의 서로 다른1·2타 소리를 위해 A는 원작07, B는 같은 swish군의 별도06을 선택했다. B가 원작 기본2타 전용 소리였다는 뜻은 아니다.3타는 별도 합성 파일 없이 A→B를 재생하며 연결 계약은 첫 검기 F23(22/60초),두 번째 검기 F25(24/60초)다. 실제 경과/AS 적용은 AttackController가 담당한다.

성공 적중 이벤트 `114d7ae3-8f7d-4012-8893-14975c761859`는 `CrashSlice`다. 원작 MeleeCollision은 성공한 피해에만 이 이벤트를 시작하고 대상 Material을 전달한다. 복원한 이벤트의11개 연결 표본 중 베기 적중 `hitSlash01` 하나를 선택했다. Material별 분기 전체를 도입하거나 원작 Player 피격음을 선택한 것이 아니다. 타일 발소리는 `footstepRoad`의 실제3개 표본을 현재 바닥용으로 선택했다. 원작의 타일→발소리 호출부 대응은 확인되지 않았으므로 원작 타일 매핑과 동일하다고 기록하지 않는다.

## 원본과 추출 근거

원본은 `docs/reference/sephiria/assets/ExportedProject/Assets/StreamingAssets`의 다음 두 파일이며 읽기 전용으로 사용했다.

- `Master.bank` ·100,506,144bytes ·SHA-256 `24f8cb016d55f9d434ef174b4239598a5fed48edc38a1a4f4f411c767d32ba9c`
- `Master.strings.bank` ·28,488bytes ·SHA-256 `b14a6518226947fa883836ab0d532c514d720d48e36502f212740425d4be3b9e`

Master.bank offset1,221,760의 FSB5 하나에720개 Custom Vorbis 표본이 있다. [공식 vgmstream r2168](https://github.com/vgmstream/vgmstream/releases/tag/r2168)의 Windows64 CLI로 필요한 표본만 전체 길이의48kHz·stereo·PCM16 WAV로 디코딩했다. 자르기·리샘플·정규화·피치/시간 변경은 없다. CLI ZIP SHA-256은 `692d45070852a35c1fa42726aa3c9de528e44eec925d37d99b1074256b99a229`다. 도구·전체 FSB·후보는 추적 대상이 아닌 `art/audio/sephiria-sfx` 작업 공간에만 둔다.

GUID→이벤트 경로는 strings.bank의24-bit RadixTree(953nodes/625GUID)를 실제로 복원했다. 이벤트→표본은 [FModBankParser의 고정 소스](https://github.com/Masusder/FModBankParser/tree/b7cfcfc60c93fcca7e4ffe666144e7ee3ed7ce77)와 그 소스가 지정한 [Fmod5Sharp submodule](https://github.com/Masusder/Fmod5Sharp/tree/b69f9afd44f36d8d290b529768b5ff061a8497fe)을 기존 .NET SDK로 실행해 교차 확인했다.600이벤트 중598개에 표본 연결이 복원됐고, 이 출력에 사용한4개 이벤트는 모두 연결됐다. 게임 이벤트를 실행한 결과가 아니라 정적 bank 그래프의 연결이다. 필요한 원문 결과는 [source-events.json](source-events.json)에 보존한다.

## 산출물·확인 범위

[manifest.json](manifest.json)에 실제 subsong·GUID·WAV/PCM 해시·표본 수·길이·피크/RMS·Unity GUID를 저장했다.6개 모두 디코더 헤더와 WAV 표본 수가 같고 유효한 비무음 PCM이며 full-scale 표본0이다. 추출 후 두 원본의 SHA가 그대로이고 `project/Assets/Audio/PlayerFeedback`의6개 WAV가 아카이브와 바이트 단위로 같다. Unity importer는 PCM·Decompress On Load·48kHz·stereo·Preload로 생성했으며 기존 meta가 있으면 GUID/설정을 보존한다.

Windows `winsound.PlaySound`로6개 파일의 동기 재생 API가 오류 없이 완료됐다. 스피커 출력의 주관적 청감·최종 믹스 검토는 미실행이다. Unity import·게임 재생은 별도 격리 검증의 결과로 아래에 구분한다. 특히 원본 Road의 peak는 약0.046~0.049·RMS는−43~−44dBFS로 스윙보다 작다. 원작 FMOD 이벤트의 gain·random pitch·3D·Material 라우팅은 이 dry WAV에 포함되지 않으며 게임 쪽에서 레벨을 정한다.

후속 Unity6000.3.21f1 격리 Play23항목은 success=true·error 빈값·배치 종료0으로 통과했다.6개 PCM의 실제 로드/GetData 비무음과 A/B/A→B·AudioSource 재생 시작/타이밍, 실제 접촉 적중·FX7장/0.35게임초·걷기·정리를 확인했다. 근거는 `project/Logs/PlayerFeedbackIntegrationFinal.log`와 `PlayerFeedbackIntegration-{result,metrics,verification,publication}.json`이다. 검증한 Player 프리팹은 GUID를 보존해 원본에 반영했고 SHA-256은 `2089E506708EE5A51AD77F74C50D47432E74403AB81567CC8C1FAB155AE34E8B`다. 관련59대상 일치의 표적 검증이며 동시 변경된 맵·9개 런타임까지 포함한 전체 최신 원본 검증은 아니다. 주관적 청감·원본 Editor 직접 Play·제출 빌드는 미실행이다.

## 다시 추출

저장소 루트에서 [extract_sfx.py](extract_sfx.py)를 사용한다. 위 공식 CLI ZIP을 작업 폴더에 준비한 뒤 다음 명령을 실행한다. 스크립트는 고정 원본 해시·표본 이름·선정 이벤트 연결을 검사하고6개 WAV와 manifest만 다시 출력하며 기존 Unity GUID를 재사용한다.

```powershell
python art/archive/audio/player-feedback-r01/extract_sfx.py --decoder art/audio/sephiria-sfx/tools/vgmstream-r2168/vgmstream-cli.exe --playback-check
```
