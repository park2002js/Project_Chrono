# Player 적중 섬광 R01

2026-11-16. 세피리아 `SwordSlashFx`의 `Slash_Player0.png`~`Slash_Player6.png` 일곱 장을 원본 PNG 바이트 그대로 복사했다. 각 이미지는5×57·16PPU·중앙 피벗(2.5,28.5px)이며 자르기·색 변경·이동·확대를 하지 않았다. 원본과 아카이브·Unity 출력의 PNG가 바이트 단위로 같고 원본 디코딩 RGBA SHA-256을 [manifest.json](manifest.json)에 기록했다.7장 합계는2,920bytes다.

원본 파일은 `docs/reference/sephiria/assets/ExportedProject/Assets/Texture2D/Slash_PlayerN.png`, 연결 근거는 `Assets/Resources/fx/SwordSlashFx.prefab`의 SpriteFx→BodySprite/Animator2D_Basic이다. 원작의 기본 위치0·scale1·흰 tint와 화면 중심 피벗을 유지한다. 원작7장·20Hz의 명목0.35게임초는 scaled이며 AS와 독립이다. 원작 Timer는 프레임 경계를 넘긴 dt를 버려 실제 종료가 양자화될 수 있으므로 우리 런타임은 절대 게임 경과시간으로 명목0.35초를 유지한다. 이 차이는 의도한 시간 처리 변경이다.

실제 사용 PNG는 `project/Assets/Art/Player/HitFx`에 있다. Point·FullRect·중앙 pivot·16PPU·Uncompressed·mipmap 없음으로 준비했으며 새 meta GUID는 manifest에 저장했다. `PlayerHit.mat`은 현재 설치된 URP의 `Sprite-Unlit-Default`를 공유한다. 원작 Prefab에는 `_Intensity` 곡선이 있으나 연결 셰이더에 없는 속성이므로 이 곡선과 별도 밝기 셰이더는 복제하지 않았다. 우리 표시 정렬30·월드 접점/무작위 위치·각도·재사용과 종료는 런타임 PlayerFeedback의 책임이다.

`project/Assets/Editor/PlayerFeedbackSetup.cs`는 `ApplyToPrefab(GameObject root)`와 `Tools/Chrono/Connect Player Feedback` 메뉴의 `Apply()`를 제공한다. 기존 PlayerFeedback이 있으면 재사용하고 Volume·FootstepVolume·StepDistance를 덮지 않는다. 신규 컴포넌트는 클래스 기본값0.8·4·1.2로 시작한다. `FeedbackAudio` 자식 AudioSource를 별도로 확보해 Slash 소리와 분리하고 playOnAwake/loop=false·spatialBlend0·ignoreListenerPause=true·volume1로 설정한다. 추출한6개 PCM 클립·일곱 Sprite·공유 재질만 연결한다.

에셋 준비 단계에서는 기존 Player 프리팹을 직접 수정하거나 Unity를 실행하지 않았다. 이후 Unity6000.3.21f1 격리 Play23항목은 success=true·error 빈값·배치 종료0으로 통과했고 실제 import·접촉 배치·월드 고정·일곱20Hz 프레임/0.35게임초·걷기·비활성/Title 정리를 확인했다. 근거는 `project/Logs/PlayerFeedbackIntegrationFinal.log`와 `PlayerFeedbackIntegration-{result,metrics,verification,publication}.json`이다. 검증한 Player 프리팹·PlayerHit.mat·7PNG meta는 GUID를 보존해 원본에 반영했고 Player 프리팹 SHA-256은 `2089E506708EE5A51AD77F74C50D47432E74403AB81567CC8C1FAB155AE34E8B`다. 관련59대상 일치의 표적 검증이며 동시 변경된 맵·9개 런타임까지 포함한 전체 최신 원본 검증은 아니다. 주관적 청감·원본 Editor 직접 Play·제출 빌드는 미실행이다.
