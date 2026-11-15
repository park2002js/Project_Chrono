# Keyboard Pixel Icon

사용자가 제공한 `kb_dark_all.png`의90종 키 그림을 각4변형, 총360개 PNG로 분리했다. 원본은 `source/`에 보존하고, 각 PNG는 원본16×16셀과 투명 여백을 그대로 유지한다.

- `png/key-{key}-{variant}.png`: 각 아이콘. 예: `key-f-light.png`, `key-shift-light-outline.png`
- `key-map.json`: 키 이름·표시 문자·변형·PNG 상대경로·원본 좌상단 rect와 입력 매칭 참고값
- `validation.json`: 전체 원본 픽셀 포함·겹침 없음·모든 PNG 저장 후 원본 crop과 픽셀 일치 검사

변형은 `light`, `light-outline`, `dark`, `dark-outline`다. 원본의 밝기·테두리 차이를 구분한 이름이며 누름/비활성 등 동작 의미를 새로 부여하지 않는다. 키와 스킬의 연결은 아직 구현하지 않았다.

`inputControls`는 Unity Input System의 바인딩 참고값이다. Shift/Ctrl/Alt에는 좌우 키를 모두 기록했다. 문장부호는 키보드 배열에 따라 달라지므로 자동 매칭이 불명확한 표시는 빈 배열로 남겼다. `LCK`는 일반 잠금 표시, `degree-hyphen`은 그림에 보이는 기호를 묘사한 이름이다. 텍스트 `Up`/`Dwn`은 Insert/Delete/Home/End와 같은 그룹에 있어 PageUp/PageDown으로 분류했으며, 방향 화살표4종은 별도다. 두 Enter 그림은 동일 Enter 키의 외형 선택지다.

360개 PNG 모두 Aseprite에서 분리·재열기했다. 원본에서 알파가 있는 모든 픽셀을 빠짐없이 포함하고 중복 crop 없이 각 픽셀을 보존했으며 원본 시트의 SHA256도 일치한다. Unity import는 상위 UI 결과의 검증 기록을 따른다.
