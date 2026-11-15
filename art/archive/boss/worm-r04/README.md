# 뱀 정면 R04 — 크기만 참고한 새 마디

첨부 이미지는 **27×27px 크기 기준만** 사용했다. R03에서 첨부의 윤곽·면 구성을 복제한 것은 AI의 잘못된 해석이며 사용자가 거절했다. R04는 볼록한 등판, 세로 장갑선, 두꺼운 앞쪽 테두리를 새로 그렸으며 첨부 픽셀·마스크·면 분할을 가져오지 않았다.

- [Aseprite 원본](aseprite/03-electric-worm-front.aseprite)
- [투명 PNG](png/03-electric-worm-front.png) · [6배 확대](preview-6x.png)
- 27×27px 단위 출력: [일반](png/modules/normal-27x27.png) · [수정](png/modules/crystal-27x27.png) · [머리](png/modules/head-27x27.png)
- [검증 결과](validation.json)

밝은 일반 마디 5개와 검보라 수정 마디 3개(머리 포함), 총 8개다. 가는 마력선과 내부의 침식·수정을 구분했고 몸 밖의 전기 잔가지는 없다. 세 종류 모두 캔버스와 불투명 외곽 크기가 27×27px다.

전체 캔버스 104×152px, 실제 외곽 89×144px, 9레이어·18색·정면 1프레임이다. Aseprite 원본 재열기와 PNG 전픽셀 일치, 중간 알파 0, 잘림 없음을 확인했다. 경비와 함께 보는 [수정 비교](../revisions-r04/README.md).

R03은 거절된 제작 이력으로 보존했다. 8방향·전투 애니메이션·Unity 연결은 이번 수정 범위에 포함하지 않았다. 현행 사양은 GAME_SPEC 3.3.12를 따른다.
