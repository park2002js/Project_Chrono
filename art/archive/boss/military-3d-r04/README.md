# 전투 기계 로봇 3D R04

[OBJ·MTL 묶음](military-robot-obj-package.zip) · [OBJ](military-robot.obj) · [MTL](military-robot.mtl)

[정면·사선](preview-final.png) · [측면·후면](side-back-preview.png) · [발사구 상세](launcher-details.png)

사용자의 정정에 따라 등 런처를 사각 박스형으로 제작했다. R03의 원통형 해석은 현행 디자인이 아니다. 박스 폭2.20은 몸통 너비와 같으며, 상판의 실제 원형 발사구9개를3×3으로 배치했다. 등 위쪽 발사 방향은 이전 제작 해석을 유지했다.

양손 각2구 발사기, 작은 손 본체, 팔 축 정렬과 손등 수정, 머리·몸통·다리는 R03 그대로다. 변경 범위는 등 런처에 해당하는 `Back_` 부품이다.

## 파일·확인

- OBJ:95개 부품,13,510정점·26,676삼각형
- MTL:18개 색상 재질. OBJ와 같은 폴더에 둔다
- `model-info.json`: 부품 목록·규격
- `validation.json`: 실제 OBJ의 부품별 닫힘·면 방향·인덱스·노멀·재질·대칭 검사
- `layout-validation.json`: 등 박스 너비·3×3 배열 검사
- `preservation-validation.json`: R03 원본과 비등 부품의 보존 대조

Y축이 위, +Z가 정면이며 발바닥은 Y=0이다. 전체 크기는 약3.968×3.420×3.382의 임의 모델 단위다. 내보낸 OBJ/MTL을 다시 읽어4방향과 등/손 상세를 렌더링해 확인했다. 부품별 닫힌 메시로 조립했으며 단일 불리언 모델은 아니다.

Blender/Unity 실제 import, 리깅·UV·애니메이션·공격 구현과 자기 교차/부품 교차의 전수 검사는 미실행이다. 파일 제작·검사와 사용자 최종 미술 승인을 구분한다.
