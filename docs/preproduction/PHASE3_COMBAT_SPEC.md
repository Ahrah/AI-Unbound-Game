# Phase 3 Combat Specification (AI Unbound)

## 1. VERTICAL_SLICE_SCOPE

| 구분 | 포함 대상 (Phase 3 필수 구현) |
| :--- | :--- |
| **플레이어 캐릭터** | 3종 (방어형 1, 공격형 1, 회복형 1) |
| **적 캐릭터** | 2종 (일반 근접 1, 일반 원거리 1) |
| **보스 캐릭터** | 1종 (광역 공격 패턴 보유) |
| **스테이지** | 단일 테스트 스테이지 1개 |
| **전투 시스템** | 고정 슬롯 배치, 자동 기본 공격, 보스전 한정 플레이어 액티브 스킬 조작 |
| **UI/UX** | 캐릭터 배치 UI, 스킬 발동 버튼, 체력(HP)/마나(MP) 게이지, 승리/패배 팝업 |

## 2. BATTLE_STATE_MACHINE

| 상태 (State) | 진입 조건 (Entry) | 종료 조건 (Exit / Next State) |
| :--- | :--- | :--- |
| **`Init`** | 스테이지 씬 로드 완료 | 맵/초기화 완료 → `Preparation` |
| **`Preparation`** | `Init` 완료 후 자동 진입 | 플레이어 '전투 시작' 버튼 클릭 → `BattleStart` |
| **`BattleStart`** | 캐릭터 배치 확정 | 등장 연출 및 스킬 쿨다운 초기화 완료 → `Fighting` |
| **`Fighting`** | `BattleStart` 종료 또는 스킬 사용 후 복귀 | 전투 종료 조건 달성 → `BattleEnd`<br>플레이어 스킬 버튼 클릭 → `PlayerSkillSelection` |
| **`PlayerSkillSelection`**| (보스전) 플레이어 스킬 버튼 클릭 | 스킬 타겟 지정 및 발동 완료 → `Fighting`<br>스킬 취소 시 → `Fighting` |
| **`BattleEnd`** | 승리, 패배, 시간 초과 조건 달성 | 결과 UI 출력 대기 (Phase 3 종료 지점) |

## 3. TARGETING_RULES

타겟은 매 공격 주기(Tick)마다 아래 우선순위로 갱신 및 확정된다.

**적군 공격 시 (공격형/방어형)**
1. **1순위**: 사거리(행렬 기준 최소 거리)가 가장 가까운 적
2. **2순위**: 현재 HP 비율(%)이 가장 낮은 적
3. **동점(Tie) 처리**: 인게임 씬에 생성된 `InstanceID`가 가장 작은 객체 (결정론적 보장)

**아군 지원 시 (회복형/지원형)**
1. **1순위**: 현재 HP 비율(%)이 가장 낮은 아군
2. **동점(Tie) 처리**: `InstanceID`가 가장 작은 객체

*특수 규칙*: 도발(Taunt) 상태인 적이 사거리 내에 존재할 경우, 모든 우선순위를 무시하고 해당 적을 타겟팅한다.

## 4. COMBAT_TICK

모바일 환경 및 결정론적 검증을 위해 `Update()`에서의 매 프레임 연산을 지양한다.

- **Tick 기반 업데이트**: 전투 로직은 `Time.deltaTime`을 누적하여 고정된 `TICK_RATE` (예: `0.1초`)마다 1회 연산된다.
- **실행 순서**:
  1. 버프/디버프 지속시간 차감
  2. 스킬 쿨다운 차감 / MP 회복
  3. 타겟 재탐색 (타겟이 죽었거나 범위를 벗어난 경우)
  4. 공격/스킬 실행 (조건 만족 시)
- **이동 애니메이션/UI 갱신**: 로직 Tick과 분리하여 Unity `Update()`에서 보간(Lerp) 처리만 담당한다.

## 5. DAMAGE_AND_HEALING

모든 계산은 Float 연산의 오차를 막기 위해 최종 단계에서 정수(Integer)로 반올림(`Mathf.RoundToInt`)한다.

- **기본 데미지 (Base Damage)**
  $$ Damage = \max(1, (Attack \times SkillMultiplier) - Defense) $$
- **치명타 데미지 (Critical Damage)**
  * 난수(RNG)가 치명타 확률(CritChance) 이하일 경우 발생
  $$ CritDamage = Damage \times 1.5 $$
- **회복량 (Healing)**
  $$ HealAmount = Attack \times SkillMultiplier $$
- **최종 적용**
  $$ TargetHP = \max(0, TargetHP - FinalDamage) $$
  $$ TargetHP = \min(MaxHP, TargetHP + HealAmount) $$

## 6. SKILL_RULES

| 항목 | 일반 전투 (자동) | 보스전 핵심 스킬 (수동) |
| :--- | :--- | :--- |
| **사용 가능 조건** | 쿨다운 0 & 사거리 내 타겟 존재 | 쿨다운 0 & MP Max & 보스 웨이브 도달 |
| **버튼 상태** | UI 비활성화 (보이지 않음) | 쿨다운 중: 비활성화 (회색)<br>사용 가능: 활성화 (발광 이펙트) |
| **게임 속도** | 1x 배속 유지 | 스킬 버튼 터치 시 `Time.timeScale = 0.1` (슬로우 모션) |
| **타겟 선정** | TARGETING_RULES 자동 적용 | 화면 터치/드래그로 타겟 슬롯 직접 지정 |
| **쿨다운 초기화** | 스킬 시전 애니메이션 종료 시점 | 스킬 시전 애니메이션 종료 시점 |

## 7. POSITION_SYSTEM

자유 이동 없이 정해진 Grid 내에서만 전투가 수행된다.

- **Grid 구조**: 세로형 화면 기준, 화면 하단은 아군 진영(3x3), 화면 상단은 적군 진영(3x3).
- **슬롯 데이터**: 각 슬롯은 `Vector2` 로컬 좌표, `IsOccupied`(점유 여부), `Occupant`(현재 캐릭터 참조) 데이터를 가진다.
- **배치 제약**: 한 슬롯에는 하나의 캐릭터만 배치 가능하며, 전투 중 캐릭터의 위치 이동(Knockback 제외)은 구현하지 않는다.

## 8. END_CONDITIONS

조건은 매 전투 Tick의 마지막 단계에서 평가된다.

1. **Victory (승리)**: 적군 진영의 모든 캐릭터 HP가 0 이하가 됨.
2. **Defeat (패배)**: 아군 진영의 모든 캐릭터 HP가 0 이하가 됨.
3. **Timeout (시간 초과)**: 전투 시작 후 지정된 제한 시간(예: 90초) 경과 시 Defeat 처리.

## 9. UNITY_COMPONENT_MAP

| 컴포넌트 클래스 | 책임 (Responsibility) | 주요 보유 데이터 | 참조 객체 |
| :--- | :--- | :--- | :--- |
| **`BattleManager`** | 상태 머신 제어, Tick 루프 관리, 승패 판정 | `CurrentState`, `TimeElapsed` | `GridManager`, `CharacterEntity` 리스트 |
| **`GridManager`** | 슬롯 생성 및 좌표 관리, 배치 유효성 검사 | `List<Slot>` | `CharacterEntity` |
| **`CharacterEntity`** | 개별 캐릭터 스탯, HP/MP 상태, 쿨다운 관리 | `CurrentHP`, `CurrentMP`, `BaseStats` | `SkillController`, 현재 `Target` |
| **`SkillController`** | 스킬/공격 공식 적용, 투사체/이펙트 트리거 | `ActiveSkillSO`, `PassiveSkillSO` | `CharacterEntity` (소유자 및 타겟) |
| **`BattleUIController`**| HP바 갱신, 스킬 버튼 입력 처리, 결과 팝업 | UI 캔버스 요소들 | `BattleManager`, 플레이어 `CharacterEntity` |

## 10. PHASE3_ACCEPTANCE_TESTS

1. **정상 배치 테스트**: 3x3 아군 그리드에 3명의 캐릭터가 겹치지 않고 정상 배치되는가?
2. **결정론적 전투 테스트**: 동일한 캐릭터 배치로 전투 시작 시, 난수(시드 고정) 상황에서 항상 동일한 타이밍에 동일한 적이 사망하는가?
3. **타겟 갱신 테스트**: 타겟팅 중인 적이 사망했을 때, 남은 적 중 규칙에 맞는 다음 타겟으로 즉각 변경되는가?
4. **승리 조건 테스트**: 모든 적의 HP가 0이 되는 즉시 `Fighting` 상태가 종료되고 승리 UI가 호출되는가?
5. **패배 조건 테스트**: 아군 전멸 시 즉각 패배 UI가 호출되는가?
6. **최소 데미지 보장 테스트**: 방어력이 공격력보다 높아도 데미지가 최소 1 이상 들어가는가?
7. **수동 스킬 타임라인 테스트**: 보스전에서 스킬 버튼 클릭 시 게임 속도가 느려지고, 타겟 지정 후 원래 속도로 복귀하는가?
8. **쿨다운 검증 테스트**: 스킬 사용 직후 쿨다운이 갱신되며, 쿨다운 수치가 0이 되기 전까지 버튼이 비활성화되는가?

## 11. DO_NOT_BUILD_YET

다음 항목은 Phase 3에서 절대 구현하거나 코드를 추가하지 않는다.
- Firebase Analytics, Crashlytics, Authentication 등 일체의 백엔드 통신
- IAP (인앱 결제) 모듈
- 데이터 저장/불러오기 (Save/Load 시스템 - Phase 3는 메모리 상에서만 실행)
- Vertical Slice 범위를 벗어난 추가 스테이지 및 몬스터 로직
- 복잡한 길찾기 (A* 등) 및 캐릭터 간 충돌/밀어내기 물리 연산
- 튜토리얼 씬 및 스토리 대화 분기 시스템
- 오브젝트 풀링 고도화 및 메모리 최적화 작업 (단순 `Instantiate`/`Destroy` 또는 최소한의 풀링만 허용)
- 최종 퀄리티의 파티클 VFX 및 FMOD/오디오 믹서 연동
