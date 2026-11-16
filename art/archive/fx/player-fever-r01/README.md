# Player 각성 황금 오라 R01

2026-11-16. 탑다운 Player의 몸에서 위로 피어오르는 황금 오라의 초기 제작안이다. 작은 픽셀 불길과 불씨를 Unity 기본 ParticleSystem 두 개로 표시한다. 색·크기·밀도는 실제 화면을 확인해 재조정한 AI 초기값이며 내부 시각 검토는 통과했다. 사용자 최종 미술 승인은 아직 확인하지 않았다.

편집 원본은 [FeverAura.shader](FeverAura.shader)·[FeverAuraSetup.cs](FeverAuraSetup.cs)와 두 `.mat` 초기 프리셋이다. 실제 사용 파일은 `project/Assets/Art/Player/FeverAura`와 `project/Assets/Editor/FeverAuraSetup.cs`에 있다. 별도 bitmap 텍스처를 그리거나 기존 Draw 셰이더를 수정하지 않았다.3×8 정수 셀의 비대칭 가는 불길과3×3 십자 불씨를 Unlit 셰이더가 직접 그린다. 큰 불투명 면 대신 작은 반투명 입자·연한 금빛 중심·노랑/금색에서 주황으로 식는 색과 알파 소멸을 사용한다.

| 시스템 | 발생 영역·상승 | 크기·수명·밀도 | 정렬 |
| --- | --- | --- | --- |
| FeverAura/Flames | 발부근 localY0.1·폭0.8·높이0.06, +Y1.15~1.7world/게임초 | 폭0.18~0.28·높이0.5~0.75,0.7~1.0초·초당20개·최대32개 | 몸 뒤,bodyOrder−1(기본19) |
| FeverAura/Embers | 발부근 localY0.1·폭0.65·높이0.06, +Y1.05~1.5world/게임초 | 0.04~0.07,0.65~1.0초·초당10개·최대24개 | 몸 앞,bodyOrder+2(기본22) |

현재 재조정값의 입자 중심은 설정상 명목 약0.7~1.7world 위로 올라가며, 가는 불길만 로컬 Noise(X0.045/Y0.015)로 흔들린다. 최대 알파는 불길0.85·불씨1.0,수명60% 알파는0.7·0.8이며 수명70%까지 크기0.8을 유지한다. 얼굴·검을 큰 면으로 덮지 않도록 불길은 몸 뒤에 두고 앞에는 작은 불씨만 둔다. 두 시스템은 Local simulation으로 Player를 따라가며 scaled time·looping·playOnAwake=false를 사용한다. 수명 비율에 따라 크기와 알파도 줄어든다. 재생·각성 유지·종료 Clear의 책임은 별도 `FeverAuraFx`에 있고 Setup은 Emitters 두 참조만 연결한다.

`FeverAuraSetup.ApplyToPrefab(GameObject root)`는 기존 FeverAura 자식·두 ParticleSystem·FeverAuraFx를 재사용해 중복을 만들지 않는다. 몸체 레이어·정렬은 적용 시 한 번 읽으며 다른 Player의 공격·오디오·Buff 수치를 수정하지 않는다. 실제 저장은 PlayerArtSetup의 연결 또는 `Tools/Chrono/Connect Fever Aura` 메뉴의 `Apply()`가 담당한다. 아트 작업자가 원본 Player 프리팹을 직접 수정하거나 Unity를 실행한 것은 아니다.

참고한 원작은 세피리아 `CharacterBuff.Start/OnUpdate_Client/OnDestroy`의 지속 FX 소유·대상 위치 추종·종료와 `ElementalParticle.SetMatrix/Stop`의 재생/정리 역할이다. 원작 네트워크 Buff·화면 밖 주차·범용 범위 FX·원본 불꽃 이미지는 복제하지 않았다. 프로젝트의 DrawEvaporationVfx/셰이더에서는 현재 설치된 URP Unlit·vertex color·파티클 정지/재사용 패턴만 참고했다.

[manifest.json](manifest.json)에 초기 모듈 수치·GUID·셰이더/Setup 원본 해시를 저장했다. 아카이브의 셰이더·Setup은 준비 시점의 게임 소스와 바이트 단위로 같고, `.mat`은 초기 편집 프리셋이다. Unity의 재직렬화 뒤 Material YAML 형식은 달라질 수 있다. 초기 오라 표시 검사는 기존 Buff/오라 상태 검사를 통과한 뒤 `goldenPixels=0`과 `Particle Velocity curves must all be in the same mode` 오류로 표시 확인에 실패했다. VelocityOverLifetime의 X/Y는 TwoConstants인데 Z만 Constant0였으므로 root가 Z도 `MinMaxCurve(0f,0f)`로 맞췄고 이 아카이브 Setup과 해시를 같은 소스로 갱신했다. 곡선 모드 수정 뒤 기능11항목은 통과했고 velocity 경고는0이었으나, 실제 금색 표시는 발근처92픽셀에 그쳐 root와 검증 담당이 위로 피어오르는 몸 오라 요구를 충족하지 못했다고 판단했다. 이에 발생 폭·밀도·수명·상승·크기·알파·Noise를 위 표의 값으로 국소 재조정하고 편집 원본/Setup 해시를 현재 코드와 맞췄다. 기능 통과와 시각 충족을 구분한 뒤, 재조정한 최종 결과를 아래와 같이 확인했다.

## 최종 확인·미리보기

Unity6000.3.21f1 격리 실행은 컴파일 종료·11항목 success=true·error 빈값·배치 종료0이며 velocity 경고0·셰이더 오류0이다. 실제 C 팔레트 바닥에서 금색775픽셀과 정지/이동을 확인했고, 각성 진입·재획득/수명·정지/Draw·Draw 정렬 복구·재활성·사망/부활·Title 정리를 검사했다. root가 실제 idle-015/moving-020을 확인해 몸 양옆의 금빛과 머리 위 끝 표현·얼굴 가독성을 충족하는 초기 표현으로 판단했다. 이는 사용자 최종 미술 승인이 아니다.

[정지 GIF·30장/2초](fever-idle.gif) · [이동 GIF·45장/3초](fever-moving.gif) · [정지 프레임표](fever-idle-contact-sheet.png) · [이동 프레임표](fever-moving-contact-sheet.png)

원본 PNG75장을 팔레트 양자화한 결과와 GIF 디코딩 프레임의 왕복 일치를 확인했다. 두 GIF는 같은 Player를 실제로 이동/정지시킨 기록이며 정지 이동량0·이동 경로 길이9.4778227이다. 촬영 때만 preview zoom3.2를 적용해 실제 시간으로 워밍업하고 촬영 뒤 복구했으며 OnGUI는 제외했다. 일부 이동 프레임(moving-027 등)은 촬영 구도상 발 하단이 잘린다. 원본 PNG75장의 시간/해시와 GIF·프레임표 해시는 manifest에 보존했다.

근거는 `project/Logs/FeverAuraFinal.log`와 `FeverAura-{result,metrics,verification,publication}.json`이다. 현재 런타임54개·관련 C#7개·출력9개는 검증본과 일치한다. 전체 스냅샷1516파일의 차이3개는 PlayerSlashChecks·GuardRobot·Map이며 동시 Editor/Guard 출판과 격리 Guard QA fixture 차이로 구분했다. 전체 최신 원본 검증으로 확대하지 않는다. Guard QA fixture는 원본에 출판하지 않았고, 검증한 Player 프리팹만 GUID를 보존해 원본에 반영했다(SHA-256 `8C9A6EBBB076E32EBAE4CD5578923F96BF2813D5D17E975EE177E9C26E01363F`). 기존 Editor 검색 초기화 예외는 관찰됐으며 오라 검사 완료와 구분한다. 원본 Editor 직접 Play·제출 빌드는 미실행이다.
