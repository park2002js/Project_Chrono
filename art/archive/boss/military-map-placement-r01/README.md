# 군용 로봇 Map 정적 배치

[실제 Play 화면](placement-play.png) · [현재8방향 원본](../military-8dir-r04/README.md)

사용자 요청으로 Map 두 번째 위쪽 방의(14.5,64,0)에 정면S Sprite를 배치했다.144×144px·16PPU·아래 기준 지면 피벗(72,24)·Point·무압축·Mipmap 없음이며 LaboratoryUnlit 재질과 표시 순서20을 사용한다.

실제 위치는 `project/Assets/Prefabs/Startup/Map.prefab`의 MilitaryRobot 자식,재사용 프리팹은 MilitaryRobot.prefab이다. 현재8방향 PNG와 정면 표시의 import·프리팹 재로드·실제 카메라 배치를 확인했다.

현재는 Transform·SpriteRenderer의 시각 배치다. 자동8방향·이동·공격·충돌·피격·조우/처치 등록은 구현하지 않았다.
