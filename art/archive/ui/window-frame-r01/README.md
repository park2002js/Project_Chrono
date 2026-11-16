# 저장 슬롯 공용 픽셀 프레임

[Aseprite 원본](window-frame.aseprite) · [PNG](window-frame.png) · [최종 정보 화면](info-r03/README.md)

가로3카드와 완료 기록 확인창에 같은 프레임을 사용한다. 모서리12px를9-slice로 보존하고 카드144×206의 중앙 정보는 Bold·항목 사이 빈 줄로 표시한다.

슬롯 화면이 열려 있을 때 Esc는 확인창을 함께 닫고 Title 메뉴로 돌아간다. 확인창 취소 버튼은 슬롯 목록으로 돌아가며 저장 데이터와 Current를 변경하지 않는다. 준비·월드 로딩 중에는 입력을 차단한다.

현재 슬롯 화면·Esc 수명·저장 보존을5검사로 확인했다. 설정창 기능은 구현 범위에 포함되지 않으며 현재 규칙은 [GAME_SPEC](../../../../docs/GAME_SPEC.md)의 F-U01을 따른다.

원본 편집 뒤 PNG를 다시 출력하고 기존 프레임 크기·12px 경계·meta를 유지한다. [Export-PNG.cmd](Export-PNG.cmd)와 [export.lua](export.lua)는 반복 출력용이다.
