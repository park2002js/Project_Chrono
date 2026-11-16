# 대검 검기 편집 원본

| 공격 | 편집 원본 | 게임용 출력 |
| --- | --- | --- |
| 1타 | [연보라 원본](aseprite/greatsword-swing.aseprite) | [PNG·시트](png/) |
| 2타 | [연하늘 원본](aseprite/greatsword-swing-attack2.aseprite) | [PNG·시트](png-attack2/) |
| 3타 | [두 호 합성 원본](aseprite/greatsword-swing-attack3.aseprite) | [PNG·시트](png-attack3/) |

1타는 연보라 `#E5D2FF`, 2타는 연하늘 `#C3EAFF`다. 실제2타는 좌우 반전한다. 3타는 같은 피벗에 두 원본 호를 합성하고 왼쪽 겹침은 하늘, 오른쪽 겹침은 보라가 앞에 보이도록 처리한다. 각 그림 전체와 비겹침 부분을 유지한다.

원본은 70×66px·16PPU·중심 피벗(35,33)이며 큰 호→남은 호→작은 조각→잔광의4프레임이다. `VFX_EDIT` 또는 `SLASH_A_EDIT`·`SLASH_B_EDIT`를 수정하고, 원본 비교·가이드 레이어는 숨긴 상태로 유지한다.

저장 후 [1타 출력](Export-PNG.cmd)·[2타 출력](Export-PNG-Attack2.cmd)·[3타 출력](Export-PNG-Attack3.cmd)을 실행한다. 반복 출력용 [export.lua](export.lua)는 원본/가이드 제외와 시트·메타데이터 생성을 담당한다. Aseprite 저장만으로 PNG가 갱신되지는 않는다.

3타의 실제54포즈·색별 노출은 [최종 R14](../player-combo-preview-r14/README.md)를 따른다. 23·24번째는 보라1단계만,25번째부터 두 색,26번째는 보라2단계 선행,35번째까지 잔광,36번째부터 검기가 없다. 해당 결과는 Player에 연결했다.
