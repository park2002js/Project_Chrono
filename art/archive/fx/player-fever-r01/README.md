# Player 각성 황금 오라

[Shader](FeverAura.shader) · [불길 Material](FeverFlames.mat) · [불씨 Material](FeverEmbers.mat) · [정지 GIF](fever-idle.gif) · [이동 GIF](fever-moving.gif)

Player 몸 양옆과 머리 위로 피어오르는 작은 불길·불씨를 두 ParticleSystem으로 표현한다. Local simulation·scaled time·looping·playOnAwake=false이며 별도 bitmap 텍스처 없이 Shader가 픽셀 불길과 십자 불씨를 그린다.

| 표현 | 현재 범위 |
| --- | --- |
| 불길 | 폭0.18~0.28·높이0.5~0.75,수명0.7~1.0초·초당20개·최대32개,몸 뒤(bodyOrder−1) |
| 불씨 | 크기0.04~0.07,수명0.65~1.0초·초당10개·최대24개,몸 앞(bodyOrder+2) |

발 부근에서 위로 올라가며 수명에 따라 크기와 alpha가 줄어든다. FeverAuraFx가 각성 상태·재사용·정지·종료 Clear를 담당하며 실제 자산은 `project/Assets/Art/Player/FeverAura/`에 연결했다.

각성 진입·수명 갱신·Draw·재활성·사망/부활·정리를11검사와 실제 정지/이동 화면으로 확인했다. 사용자 최종 미술 승인은 별도이며 이동 GIF 일부 프레임의 발 하단은 촬영 구도로 잘린다.
