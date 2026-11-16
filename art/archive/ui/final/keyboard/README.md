# 키보드 픽셀 아이콘

사용자가 제공한 원본 시트를 [source](source/)에 보존하고90종 키를4변형, 총360개16×16 PNG로 분리했다.

- [png](png/): `key-{key}-{variant}.png`
- [key-map.json](key-map.json): 키 이름·표시 문자·변형·PNG·원본셀과 Input System 바인딩 참고값

변형은 light·light-outline·dark·dark-outline다. 밝기와 테두리의 외형 선택지이며 눌림/비활성 동작을 뜻하지 않는다. Shift/Ctrl/Alt는 좌우 입력을 기록했고 모호한 문장부호의 자동 매칭은 비워 두었다. PageUp/PageDown과 방향 화살표는 별도다.

현재 게임은 필요한 Shift/F 안내를 연결했다. 나머지 키 그림은 동일 원본의 재사용 라이브러리로 유지한다.
