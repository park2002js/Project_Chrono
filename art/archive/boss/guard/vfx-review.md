# Guard 패턴 VFX 검수

기준은 이 폴더의 [패턴 문서](pattern.md), [Guard 편집 원본](guard-amethyst.aseprite), [PNG 클립별 재생 정보](png/animations.json)다. 번호는 패턴 문서의 순서다. 투사체, 바닥 공격 예고 A·B·C, SFX는 이번 제작 범위에서 제외했다.

[패턴별 VFX 발생 시점](#패턴별-vfx-발생-시점)에서 각 효과를 언제 켜고 끄는지 확인할 수 있다.

## 구현 목록

| 패턴 | 준비된 부분 | 만들어 적용하거나 조합할 부분 |
| --- | --- | --- |
| 1 · 튜토리얼 단발 | 좌우 발사 섬광, 공용 Draw·Slash | 추가 보스 텍스처 없음. 탄환 감속·정지, 탄환을 남긴 화면 암전, 입력 안내는 시간 제어·화면 합성·UI 작업이다. |
| 2 · 양쪽 단발 | 좌우 총구에 각각 6장, 280ms의 발사 섬광 | 추가 텍스처 없음. 두 총구의 섬광 시작을 같은 발사 시점에 맞춘다. |
| 3 · 교대 내려찍기 | 좌우 주먹 파츠, 테두리를 따라 빛이 도는 82장 | **좌우 하강 스미어와 착지 고리**. 각 30ms의 두 그림을 거쳐 손이 닿으면 해당 손의 바닥 접점에서 흰색·금색 고리가 한 번 퍼진다. |
| 4 · 양손 중앙 내려찍기 | 좌우 주먹, [합친 주먹 원본](<combine fist.aseprite>), 공용 하강 잔상 | **두 손 접점의 1.5초 충전**, **7번과 공용인 착지 폭발·바닥 균열·방사 왜곡**. 합친 주먹이 바닥에 닿는 순간 세 효과를 함께 시작한다. |
| 5 · 눈 레이저 | 눈 충전 6장·1.20초, 눈 발광 4장·0.30초 반복. Unity 경비 프리팹에는 빔과 착탄 렌더러도 연결되어 있다. | **양눈과 이동 접점 연결, 접점의 가속·불티**. 아래 Gol-Set 분석에서 픽셀 그림과 매 프레임 계산할 부분을 구분했다. |
| 6 · 좌우 교대 부채꼴 | 발사 시점의 좌우 총구 섬광 | **양 총구가 동시에 진행하는 1.5초 준비 효과**. 0.6초 뒤부터 기존 섬광을 좌·우·좌·우 순으로 재생한다. |
| 7 · 한 손 지면 충격파 | 주먹 파츠·테두리, 공용 하강 스미어 | **한 손의 3.5초 충전**, **착지 순간의 방사 왜곡·바깥으로 튀는 흙먼지·폭발**, **중앙을 향해 부채꼴로 전파되는 지면 타격**. '흑먼지'는 사용자가 확인한 흙먼지로 해석했다. |

기존 `fist-slam-left/right`는 한 손당 82개의 테두리 픽셀 위로 색이 순환하는 클립이다. 손 형상의 하강이나 착지 폭발은 들어 있지 않다. `eye-laser-fire`는 눈 두 곳의 각각 3×3px만 밝힌다. `muzzle-left/right`는 발사 후 고리가 퍼지고 마지막 프레임이 투명해진다. 각 클립이 실제로 덮는 부위를 기준으로 제작 목록을 정했다.

## 패턴별 VFX 발생 시점

**1·2번**은 [패턴 문서](pattern.md)의 이벤트를 기준으로 한다. 두 패턴의 발사 시각은 예고 A가 시작되는 순간을 0초로 잡았다. **3~7번**의 숫자는 각 미리보기 첫 프레임부터 잰 초 단위 시각이다. 재생을 붙일 때는 표의 ‘시작 조건’을 사용한다.

3~7번 Aseprite의 원본 격자는 320×180이다. 몸체와 손 아래 그림자는 대기부터 계속 표시된다. 3·4·7번에서 손이 들리면 손 그림자는 바닥의 투영 위치를 따라가고, 4번에서 양손이 모이면 한 그림자로 이어진다.

### 1. 튜토리얼 단발

| VFX | 시작 조건과 위치 | 지속·종료 |
| --- | --- | --- |
| 공용 Draw 선 | 첫 예고 전에 플레이어가 안내된 두 점으로 선을 확정하면 해당 위치에 표시한다. | 튜토리얼에서는 탄환이 이 선에 멈추고, 플레이어가 Slash를 발동하면 선이 소모된다. |
| 좌우 총구 섬광 | 예고 A 0.70초와 B 0.60초를 마친 **1.30초**, 양쪽 총구에서 동시에 시작한다. | `muzzle-left/right`를 각 1회, 0.28초 재생한다. |
| 화면 암전 | 발사된 탄환이 지정 지점에서 감속을 시작하면 탄환을 제외한 화면이 점점 어두워진다. | 탄환이 선에 완전히 멈추면 Slash 입력을 기다린다. 암전 해제 시점과 전환 길이는 패턴 문서에 지정되어 있지 않다. |
| 공용 Slash 연출 | 정지한 탄환 앞에서 플레이어가 Slash를 입력하는 순간 시작한다. | 발도·납도 후 소모한 선을 따라 공간참이 발생한다. 연출이 끝나면 튜토리얼 설명으로 넘어간다. |

편집 원본은 [왼쪽 총구 섬광](vfx/muzzle-left.aseprite)과 [오른쪽 총구 섬광](vfx/muzzle-right.aseprite)이며 2·6번 발사에도 같은 클립을 쓴다.

### 2. 양쪽 총구 단발

| VFX | 시작 조건과 위치 | 지속·종료 |
| --- | --- | --- |
| 좌우 총구 섬광 | 추적 0.70초 뒤 표적 위치를 고정하고 0.60초 뒤 양쪽 총구에서 발사한다. 발사와 동시에 `muzzle-left/right`를 켠다. | 양쪽 모두 **1.30~1.58초**에 한 번씩 재생한다. 각 6장 중 마지막 0.06초는 투명 프레임이다. |

### 3. 교대 내려찍기

| VFX | 시작 조건과 위치 | 미리보기 시각 |
| --- | --- | --- |
| 하강 스미어 | 공격하는 손이 1.00초 추적하고 0.60초간 위치를 고정한 뒤 내려올 때, 해당 손에 두 그림을 각 30ms 표시한다. | 왼손 **1.60~1.66초**, 오른손 **3.80~3.86초**. 착지 프레임에서 끝난다. |
| 흰색·금색 착지 고리 | 각 손이 바닥에 닿는 프레임에 그 접점에서 `Golem_Stmap_FX0…6`을 20fps로 한 번 재생한다. 손이 복귀해도 고리는 같은 자리에 남는다. | 왼손 **1.66~2.01초**, 오른손 **3.86~4.21초**. 각 0.35초. |

[MP4](pattern-03/P03-fist-drop.mp4) · [GIF](pattern-03/P03-fist-drop.gif) · [Aseprite](pattern-03/P03-fist-drop.aseprite) · [스미어 원본](vfx/fist-drop-smear.aseprite)

### 4. 양손 중앙 내려찍기

| VFX | 시작 조건과 위치 | 미리보기 시각 |
| --- | --- | --- |
| 양손 접점 충전 | 두 손이 맞닿는 순간 접점에 아스카드의 앞쪽 섬광과 뒤쪽 전류를 함께 시작한다. 뒤쪽 전류는 첫 0.15초가 비어 있다. | **0.50~2.00초**, 1.50초간 충전. 뒤쪽 전류는 0.65초부터 보인다. |
| 합친 주먹의 하강 스미어 | 충전이 끝나고 0.60초 뒤, 합친 주먹이 하강할 때 손목 위에 잔상 두 장을 각 30ms 표시한다. | **2.60~2.66초** |
| 바닥 균열 | 합친 주먹이 중앙 바닥에 닿으면 접점 아래에 `Askard_Phase1_Wave_Burst_Ground00…04`를 재생한다. | **2.66~2.91초**, 0.25초 |
| 수직 폭발 | 균열과 동시에 같은 접점에서 `Askard_Phase1_Wave_Burst00…06`이 손 앞쪽으로 솟는다. | **2.66~3.01초**, 0.35초 |
| 방사 왜곡 | 같은 착지 프레임에 접점을 중심으로 주변 바닥과 물체의 화면 픽셀을 밀어낸다. | **2.66초부터 약 0.83초**, 약 3.49초에 종료 |

마지막 세 효과는 7번과 원본·크기·재생 속도를 공유한다. [MP4](pattern-04/P04-double-slam.mp4) · [GIF](pattern-04/P04-double-slam.gif) · [Aseprite](pattern-04/P04-double-slam.aseprite)

### 5. 양눈 레이저

| VFX | 시작 조건과 위치 | 미리보기 시각 |
| --- | --- | --- |
| 눈 충전 고리 | 발사 준비가 시작되면 양눈에 `eye-laser-tell` 6장을 순서대로 표시한다. | **0.24~1.44초**, 1.20초 |
| 발사 중 눈 발광 | 충전이 끝나는 프레임에 양눈을 `eye-laser-fire`로 바꾸고 0.30초짜리 4장 루프를 반복한다. | **1.44~4.44초**, 빔 종료와 함께 꺼진다. |
| 연속 빔 두 줄 | 눈 발광과 동시에 각 눈에서 이동하는 지면 접점까지 광선을 그린다. 접점이 후반으로 갈수록 빨라진다. | **1.44~4.44초**, 3.00초 |
| 접점 불티 | 빔을 켠 프레임부터 양쪽 지면 접점에서 0.05초마다 입자를 방출한다. 개별 입자 수명은 0.20초다. | **1.44초** 첫 방출, **4.39초** 마지막 방출. 남은 입자는 **4.59초**에 사라진다. |

[MP4](pattern-05/P05-eye-laser.mp4) · [GIF](pattern-05/P05-eye-laser.gif) · [Aseprite](pattern-05/P05-eye-laser.aseprite) · [눈 충전 원본](vfx/eye-laser-tell.aseprite) · [눈 발광 원본](vfx/eye-laser-fire.aseprite)

### 6. 좌우 교대 부채꼴

영상 앞부분은 곡사포, 뒷부분은 바바 상인의 레일건을 같은 패턴에 적용한 비교다. 각 구간에서 양 총구는 같은 충전 후보를 쓴다.

| VFX | 시작 조건과 위치 | 미리보기 시각 |
| --- | --- | --- |
| 양 총구 충전 · 곡사포 후보 | 준비 시작과 동시에 양쪽 총구에 `Mushroom_Howitzer_AttackFX_Ready_00…07`을 재생한다. | **0.22~1.72초**, 1.50초 |
| 양 총구 충전 · 레일건 후보 | 두 번째 비교 구간에서 같은 자리에 `BabaMerchant_RailGun_Ready_FX00…15`를 재생한다. | **4.80~6.30초**, 1.50초 |
| 발사 섬광 | 충전 종료 0.60초 뒤부터 발사하는 총구에만 `muzzle-left/right`를 1회씩 재생한다. 순서는 좌·우·좌·우, 간격은 0.60초다. | 곡사포 구간 **2.32 / 2.92 / 3.52 / 4.12초**. 레일건 구간 **6.90 / 7.50 / 8.10 / 8.70초**. 각 클립 0.28초 중 마지막 0.06초는 투명하다. |

[MP4](pattern-06/P06-muzzle-charge.mp4) · [GIF](pattern-06/P06-muzzle-charge.gif) · [Aseprite](pattern-06/P06-muzzle-charge.aseprite)

### 7. 한 손 지면 충격파

영상은 왼손이 왼쪽 끝에서 중앙을 향해 공격하는 경우다.

| VFX | 시작 조건과 위치 | 미리보기 시각 |
| --- | --- | --- |
| 한 손 충전 | 주먹이 가장자리의 지정 위치에 도착하면 손 중심에 4번과 같은 앞쪽 섬광·뒤쪽 전류를 켠다. 충전 후반에는 끝 그림을 반복한다. | **0.50~4.00초**, 3.50초간 충전. 뒤쪽 전류는 0.65초부터 보인다. |
| 하강 스미어 | 충전 종료 0.60초 뒤 내려치는 손에 두 그림을 각 30ms 표시한다. | **4.60~4.66초** |
| 바닥 균열 · 4번 공용 | 주먹이 바닥에 닿으면 손 바로 아래에서 시작한다. | **4.66~4.91초**, 0.25초 |
| 수직 폭발 · 4번 공용 | 같은 착지 접점에서 손 앞쪽으로 솟는다. | **4.66~5.01초**, 0.35초 |
| 방사 왜곡 · 4번 공용 | 같은 프레임에 접점을 중심으로 화면이 일그러진다. | **4.66초부터 약 0.83초**, 약 5.49초에 종료 |
| 황토색 흙먼지 | 착지와 동시에 발밑에서 `OinkTotemDustFX_00…08`이 바깥쪽으로 퍼진다. | **4.66~5.11초**, 0.45초 |
| 부채꼴 지면 타격 | 착지 즉시 가까운 지점부터 중앙 방향 8곳에 `QliphothRabbit_Fly_DownFX00…07`을 순차 재생한다. 먼 쪽일수록 폭이 넓어진다. | **4.66초부터 약 5.64초까지**. 착지 후 각 지점의 시작은 **0 / 155 / 180 / 315 / 350 / 510 / 545 / 575ms**, 각 0.40초다. |

[MP4](pattern-07/P07-ground-wave.mp4) · [GIF](pattern-07/P07-ground-wave.gif) · [Aseprite](pattern-07/P07-ground-wave.aseprite)

## 세피리아에서 확인한 생성 순서

프리팹의 효과 필드와 타임라인의 스프라이트 GUID를 대조한 뒤 [Texture2D 원본 폴더][textures]의 PNG를 열었다. 대표 프레임을 원래 크기로 확인하려면 [에르마의 착지 링][stampframe], [아스카드의 충전 뒷면][backframe], [토템의 흙먼지][dustframe], [곡사포 충전][howframe], [레일건 충전][babaframe]을 열면 된다.

| 쓰이는 순간 | 원작의 실제 호출과 스프라이트 | Guard에서의 적용 |
| --- | --- | --- |
| 손 추적과 하강 | 에르마 손은 `StompUp → StompWait → StompDown`을 거친다. 대기·공중·낙하 상태가 같은 `BigGolem_Hand0.png`를 참조하며, 높이가 0에 도달해야 근접 판정을 생성한다. 좌우의 독립 바닥 그림자는 `Golem_Hand_Shadow0/1.png`다. [손 상태 코드][hand] · [손 스프라이트 상태][handclip] · [왼손 프리팹][lefthand] | 3·4·7번에서 그림자를 바닥에 남기고 Guard의 손이 먼저 내려온다. 새 스미어의 두 그림과 착지한 손을 서로 다른 시점에 표시한다. |
| 두 손 접점 또는 한 손의 충전 | 아스카드의 내려찍기 분신은 낙하 1초 전에 앞·뒤 두 효과를 만들고 낙하 직전에 회수한다. 뒤쪽은 `Askard_Phase1_Wave_Back00…07`(169×155), 앞쪽은 `Front00…08`(66×5)이다. 둘 다 20fps이며 뒤쪽의 첫 3틱은 빈 키다. [생성·회수 코드][ask] · [앞쪽 타임라인][front] · [뒤쪽 타임라인][back] | 손 가운데에 뒤쪽 전류와 앞쪽 섬광을 겹친다. 4번은 1.5초, 7번은 3.5초 동안 유지하고 예고 구간 진입 시 끈다. 7번은 긴 충전을 위해 원본 끝의 변화를 반복한다. 뒤쪽 전류가 손 너비를 크게 넘어서는 것이 원배율의 인상이다. |
| 교대 주먹의 착지 고리 | 에르마가 바닥에 닿으면 `ArmadilloGolemHandStomping`이 생기고 근접 판정의 `swingFxPrefab`이 `GolemHandStampFx`를 생성한다. `Golem_Stmap_FX0…6.png`(원본 철자), 76×39, 중앙 피벗, 20fps, 0.35초. [근접 프리팹][melee] · [효과 생성][meleecode] · [7장 타임라인][stamp] | 3번의 왼손과 오른손이 각각 지면에 닿을 때 한 번 재생한다. 처음에는 희고 뒤에서 금색으로 얇아진다. |
| 양 총구의 준비와 발사 | 곡사포는 공격 프레임 0에 준비 효과를 만들고 17에 발사한다. `Mushroom_Howitzer_AttackFX_Ready_00…07`은 41×33, 10fps다. 바바 상인은 조준 프레임 5에 레일건 효과를 켜고 17에 추적을 멈추며 21에 효과를 끄고 발사한다. 별도 효과 `BabaMerchant_RailGun_Ready_FX00…15`는 27×27, 20fps다. 에르마 자체의 발사 섬광 `HeadShootFX00…03`은 35×41, 20fps다. [곡사포 호출][howcode] · [곡사포 준비][how] · [바바 공격 이벤트][babaactor] · [레일건 준비][baba] · [에르마의 발사][golemfire] | 두 후보 모두 원래 0.80초다. 6번에서는 순서를 유지해 1.50초에 펼쳤다. 발사는 Guard의 기존 좌우 섬광으로 전환한다. 1·2번도 같은 섬광을 공유한다. |
| 발밑 흙먼지 | 오잉크 토템의 애니메이션 이벤트 `CreateFx`가 발밑에 `OinkTotemDustFx`를 만든다. `OinkTotemDustFX_00…08`은 49×24, 20fps, 0.45초이고 기준점은 좌상단에서 (24.5,16)이다. 에르마의 다른 후보 `DashDust`는 **하강 시작**에 흰 먼지를 4~7방향으로 만들고, 캐릭터 낙하의 `Demon_Stmap_FX0…5`는 착지 때 흰색에서 회보라로 옅어진다. [토템 이벤트][oinkactor] · [생성 코드][oinkcode] · [먼지 9장][oinkdust] · [낙하 착지 호출][falldust] | 7번에는 손이 바닥에 닿는 프레임에 토템의 황토색 먼지를 한 번 배치했다. 폭발과 시작은 같고 먼지가 0.10초 더 남는다. |
| 4·7번 공용 착지 충격 | 아스카드는 착지 확인 후 지면과 수직 폭발을 겹치고 카메라에 `RadialDistortion.Type.Strong`을 호출한다. `Askard_Phase1_Wave_Burst00…06`(98×142, 기준점 약 47.5,122)과 `Burst_Ground00…04`(112×51, 중앙)는 각 20fps다. 왜곡 강도와 오프셋은 약 0.83초간 변한다. [착지 코드][ask] · [폭발][burst] · [지면 균열][burstground] · [왜곡 곡선][distort] | 접점 한 곳에서 폭발 0.35초·바닥 균열 0.25초·방사 왜곡 약 0.83초를 같은 시각에 시작한다. 7번에만 황토색 흙먼지와 부채꼴 지면 타격이 더해진다. |
| 부채꼴로 번지는 지면 타격 | 에르마의 6갈래 충격파와 도마뱀 머리 찍기의 117° 확산은 원작에서 투사체를 발사한다. 비투사체 쪽에는 QBoss 착지 시 한 지점에서 재생되는 `QliphothRabbit_Fly_DownFX00…07`(174×143, 바닥 피벗 87,122, 20fps), Library Guard가 고정 지면 판정을 차례로 여는 사례가 있다. [에르마 발사][hand] · [도마뱀 확산][lizard] · [QBoss 착지][qbossclip] · [지면 판정 순차 생성][library] | 7번의 부채꼴은 Guard 전용 배치다. QBoss의 단일 타격을 44×36으로 축소해 가까운 한 지점에서 시작하고 먼 쪽으로 폭이 넓어지는 8지점에 시차를 둔다. 마지막 타격은 착지 후 0.975초에 끝난다. 실제 피해 범위는 Unity에서 별도로 연결해야 한다. |

## Gol-Set의 눈과 지면 접점

[bridge2 맵][bridge]의 `boss_colossus2`에서 [생성 분기][factory]로 들어가 해당 보스의 업데이트와 렌더 함수를 추적했다. 아래 함수명과 주소는 로컬 실행 파일에서 복원한 의사코드 기준이다.

| 부위 | 원작 구현 | 텍스처와 코드의 역할 |
| --- | --- | --- |
| 눈·빔 시작점 | 몸의 높이와 위치로부터 좌우 시작점을 각각 계산한다. 좌우 눈 간격은 중심을 기준으로 ±16.5, 끝점은 움직이는 지면 중심에서 ±4이고 끝점의 높이는 0이다. [두 광선 좌표][endpoints] | 눈의 충전·발광은 Guard의 기존 스프라이트를 쓴다. 매 프레임 양눈 좌표와 끝점을 따로 넘겨야 이동·방향 변경을 따라간다. |
| 연속 빔 | 양 끝의 단면을 잇는 삼각형 띠를 만들고 정점색으로 안쪽과 바깥쪽을 그린다. UV가 고정되어 있다. [광선 그리기][strip] | 정지한 한 장면의 광선은 그림으로 남길 수 있다. 길이·기울기·접점이 바뀌는 실제 동작은 메시 또는 선 렌더러가 담당한다. |
| 접점 이동·판정 | 가속형은 진행값이 0을 지난 뒤 업데이트마다 속도를 누적하고 진행값이 1을 넘으면 두 광선을 끈다. 직선 등속으로 진행값을 증가시키는 별도 변형도 있다. 원작은 지면 중심과 플레이어의 거리를 검사한다. [가속형][accel] · [등속형][linear] · [발사와 판정][endpoints] | Guard 문서의 무작위 시작점·플레이어를 지나는 벡터·3/4 통과점·속도·피해 판정은 코드가 맡는다. 검수 영상은 동일 경로를 반복해서 확인할 수 있게 시작 (107,106), 플레이어 발밑 (191,139), 끝 (219,150)을 고정하고 후반을 가속했다. |
| 지면의 불티 | 좌우 접점에 입자를 방출한다. 256×256 `particles.png`에서 16×16 셀을 뽑는다. 확인한 셀 번호는 0·1·224·225이고 좌상단 좌표는 (0,0), (16,0), (0,224), (16,224)다. [입자 선택][emitter] · [입자 초기화][particleinit] · [실제 아틀라스][particles] | 입자 **낱개**는 편집할 수 있는 그림이다. 생성 위치·수·속도·수명·투명도는 코드가 맡는다. 영상은 이 셀과 원작 발색 배율을 이용하고 Guard 눈 간격에 맞춰 축소했다. 방출 간격 50ms, 잔류 200ms는 검수용 값이다. |

세피리아의 에르마 골렘은 손에서 레이저 투사체를 만들며, 머리와 몸통 시트가 시작 00~03, 반복 04~09, 종료 10~11로 나뉜다. 바바 상인 레일건에는 끝에 붙는 `BabaMerchant_RailGun_Laser_Hit00…08`도 있다. 두 사례 중 Gol-Set의 움직이는 지면 불티에 해당하는 것은 별도의 입자 방출이어서, 5번 영상에는 Gol-Set 방식으로 그린 빔과 접점 입자를 넣었다. [에르마 손 레이저][hand] · [바바 상인 레이저 끝][babaend]

Guard 프리팹의 눈 충전 1.20초와 발사 3.00초를 영상의 시간 기준으로 썼다. 원작 Gol-Set의 업데이트 횟수를 초로 환산한 값은 아니다. 현재 Unity 경비는 0.4초 전 플레이어 위치를 추적하고, 이 영상은 새 패턴 문서의 한 방향 가속 경로를 보여준다. 검수 범위는 원작 코드·직렬화 자산 대조, Aseprite 재열기, 합성 영상 재생 프레임이다. Unity 플레이 모드에서의 피해·중단·복귀 검증은 후속 연결 작업이다. [경비 프리팹][guardprefab] · [현재 경비 레이저][guardlaser]

[hand]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/Unit_ArmadilloGolem_Hand.cs>
[handclip]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/ArmadilloGolem_Hand.asset
[lefthand]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/Resources/unit/boss/ArmadilloGolem_LeftHand.prefab
[ask]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/AskardGlitchObject_Stomp.cs>
[front]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/AskardStompReadyFx.asset
[back]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/AskardStompReadyBackFx.asset
[melee]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/Resources/meleecollision/ArmadilloGolemHandStomping.prefab
[meleecode]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/MeleeCollision.cs>
[stamp]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/GolemHandStampFx.asset
[howcode]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/Unit_Howitzer.cs>
[how]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/HowitzerDemonReadyAttackFx.asset
[babaactor]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/BabaMerchant_New.asset
[baba]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/BabaMerchant_UnlitRailgunFx.asset
[golemfire]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/GolemFireFx.asset
[oinkactor]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/Oink_Totem.asset
[oinkcode]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/Unit_OinkTotem.cs>
[oinkdust]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/OinkTotemDustFx.asset
[falldust]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/AvatarSpawn_Apperance_Fall.cs>
[burst]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/AskardStompBustFx.asset
[burstground]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/AskardStompBustGroundFx.asset
[distort]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/AnimationClip/Distortion_Strong.anim
[lizard]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/Unit_LizardDemon.cs>
[qbossclip]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/_QBoss_StompAttack.asset
[library]: <../../../../docs/reference/sephiria/code/ingame code/Assembly-CSharp/Unit_LibraryGuard.cs>
[bridge]: <../../../../docs/reference/titan souls/assets/MAPS/FLOOR2/bridge2.tmx>
[factory]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/00556520.c>
[endpoints]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/0043cfdc.c>
[strip]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/00452c60.c>
[accel]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/0043e3c4.c>
[linear]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/0043e5dc.c>
[emitter]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/00451354.c>
[particleinit]: <../../../../docs/reference/titan souls/code/ingame code/TITAN/pseudocode/functions/00450ebc.c>
[particles]: <../../../../docs/reference/titan souls/assets/IMG/particles.png>
[babaend]: ../../../../docs/reference/sephiria/project/ExportedProject/Assets/MonoBehaviour/BabaRailgunEnd.asset
[guardprefab]: ../../../../project/Assets/Prefabs/Startup/GuardRobot.prefab
[guardlaser]: ../../../../project/Assets/Scripts/Enemy/GuardEyeLaser.cs
[textures]: ../../../../docs/reference/sephiria/assets/Assets/Texture2D/
[stampframe]: ../../../../docs/reference/sephiria/assets/Assets/Texture2D/Golem_Stmap_FX1.png
[backframe]: ../../../../docs/reference/sephiria/assets/Assets/Texture2D/Askard_Phase1_Wave_Back04.png
[dustframe]: ../../../../docs/reference/sephiria/assets/Assets/Texture2D/OinkTotemDustFX_01.png
[howframe]: ../../../../docs/reference/sephiria/assets/Assets/Texture2D/Mushroom_Howitzer_AttackFX_Ready_03.png
[babaframe]: ../../../../docs/reference/sephiria/assets/Assets/Texture2D/BabaMerchant_RailGun_Ready_FX09.png
