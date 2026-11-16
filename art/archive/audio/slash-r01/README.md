# Slash 합성 효과음

| 출력 | 길이 | 연결 |
| --- | --- | --- |
| [sheath-clack-r01.wav](sheath-clack-r01.wav) | 0.14초 | 납도 완료 프레임 |
| [space-slash-r01.wav](space-slash-r01.wav) | 0.42초 | 지연 종료 후 공간참 시작 |

편집 원본은 [generate.py](generate.py)다. 고정 시드 잡음과 수식으로 이 프로젝트에서 합성했으며 외부 음원 표본을 사용하지 않았다. 같은 원본으로 같은 WAV를 다시 생성할 수 있다.

48kHz·mono·PCM16이며 `project/Assets/Audio/Slash/`에 연결했다. 연출 사건에서 AudioSource.PlayOneShot으로 재생하며 음량은 SlashPresentationFx.Volume으로 조정한다. 실제 청음과 최종 음색 승인은 별도다.
