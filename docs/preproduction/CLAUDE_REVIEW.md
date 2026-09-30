# 반드시 수정

### 1. [Critical] 재화와 해금 데이터를 클라이언트가 직접 저장하는 구조

* **문제:** 클라이언트가 재화, 캐릭터 해금, 진행도를 모두 계산합니다. 그 결과를 `save_data` JSON 문자열 하나로 통째로 덮어씁니다. Firestore Security Rules는 문자열 안의 값을 검증하지 못합니다.
* **왜 위험한지:** 메모리를 변조하면 `quantumCore`를 임의로 올린 채 저장할 수 있어 유료 재화가 무한이 됩니다. 출시 후에 이 구조를 바꾸면 모든 유저의 세이브를 마이그레이션해야 합니다.
* **정확한 수정안:** `Users/{uid}`의 필드를 두 영역으로 나눕니다.
  * **서버 전용:** `quantumCore`, 결제 원장(`transactionId` 목록), 유료 캐릭터 해금. Cloud Functions만 쓸 수 있게 하고, Security Rules로 클라이언트 쓰기를 막습니다.
  * **클라이언트 허용:** `dataCoin`, 레벨, 스테이지 진행. JSON 문자열 대신 map 필드로 저장하고 `saveVersion` 정수를 둡니다.
  * Core를 쓰는 행위(캐릭터 구매, DataCoin 구매, 에너지 충전)는 모두 Callable Function으로 처리합니다.
  * 소프트 재화 변조는 싱글플레이 게임이라 수용 가능한 위험으로 명시합니다.
* **수정 문서/항목:**
  * FIREBASE_SCHEMA.md 전체
  * DATA_SCHEMA.md `UserProfile`
  * IMPLEMENTATION_RISKS.md의 IAP 행에서 "Functions가 부담되면 암호화" 대안 삭제

### 2. [Critical] 저장 시점 공백으로 에너지 되돌리기와 진행 유실 발생

* **문제:** Firestore 쓰기는 스테이지 클리어와 결제 시점에만 일어납니다. 로컬 저장 정책은 정의돼 있지 않습니다. `lastEnergyUpdateTime`의 기준 시계도 정해져 있지 않습니다.
* **왜 위험한지:**
  * 스테이지에 입장한 뒤 패배하고 앱을 끄면 에너지 차감이 기록되지 않습니다. 재실행하면 에너지가 복구되어 무한 재도전이 가능합니다.
  * 레벨업 직후 앱이 종료되면 레벨과 DataCoin 사용 내역이 사라집니다.
  * 기기 시계 기준이면 시계를 조작해 에너지를 무한으로 얻을 수 있습니다.
* **정확한 수정안:**
  * 암호화된 로컬 파일을 1차 저장소로 두고, 값이 바뀔 때마다 즉시 기록합니다.
  * Firestore는 다음 체크포인트에서 동기화합니다: 스테이지 입장(에너지 차감 포함), 전투 결과, 업그레이드(묶어서 debounce), `OnApplicationPause`.
  * 에너지 회복은 서버 timestamp(`serverTimestamp`)를 기준으로 계산합니다.
* **수정 문서/항목:**
  * FIREBASE_SCHEMA.md 쓰기 정책
  * DATA_SCHEMA.md `lastEnergyUpdateTime`
  * ECONOMY.csv `Energy_Battery`
  * QA_MATRIX.md에 케이스 추가: 전투 중 강제 종료, 업그레이드 직후 강제 종료, 기기 시계 변경

### 3. [High] 게스트→Apple/Google 연동 흐름과 계정 충돌 처리 미정의

* **문제:** QA에는 "동일 계정 재로그인"만 있습니다. 다음이 모두 정의되지 않았습니다.
  * 첫 실행 시 인증 방식
  * 게스트에서 정식 계정으로 전환하는 방식
  * 이미 다른 세이브가 연결된 계정으로 로그인할 때의 처리
* **왜 위험한지:**
  * 로그인할 때 새 UID가 생기면 게스트 진행이 소실됩니다.
  * `credential-already-in-use` 상황에서 규칙이 없으면 유료 재화가 유실되거나 복제되어 고객지원 분쟁으로 이어집니다.
* **정확한 수정안:**
  * 첫 실행 시 Firebase Anonymous Auth를 쓰고, 로그인 시 `linkWithCredential`로 같은 UID를 유지합니다.
  * 충돌이 나면 두 세이브의 요약(최고 스테이지, Core, 유료 캐릭터)을 보여주고 하나만 선택하게 합니다.
  * 재화 합산은 금지합니다.
  * 버려지는 쪽에 결제 원장이 있으면 선택 전에 경고합니다.
  * 게스트 상태로 결제하기 전에 "계정 미연동 시 재설치하면 복구 불가"를 고지합니다.
* **수정 문서/항목:**
  * UI_FLOW.md Title 화면(Auth State)
  * FIREBASE_SCHEMA.md
  * QA_MATRIX.md 저장/복원 항목

### 4. [Critical] IAP 서버 검증이 선택사항이고 중복 지급 방지가 없음

* **문제:** 서버 검증을 생략 가능한 옵션으로 두었습니다. Pending 복구 설명은 있지만 지급 멱등성(같은 결제를 두 번 지급하지 않는 장치)이 없습니다.
* **왜 위험한지:**
  * 위조 영수증으로 Core를 무료로 얻을 수 있습니다.
  * 강제 종료 후 복구할 때 `ProcessPurchase`가 다시 호출되면 이중 지급됩니다.
* **정확한 수정안:** v1.0부터 Cloud Functions 검증을 필수로 합니다(App Store Server API / Google Play Developer API).
  1. `ProcessPurchase`에서 `PurchaseProcessingResult.Pending`을 반환합니다.
  2. Function이 영수증을 검증합니다.
  3. 원장에 해당 `transactionId`(Apple) 또는 `purchaseToken`(Google)이 없을 때만 Firestore 트랜잭션으로 Core를 지급하고 원장에 기록합니다.
  4. 지급 성공 응답을 받은 뒤 `ConfirmPendingPurchase`를 호출합니다.
  * Google의 consume은 지급이 확정된 뒤에만 합니다.
  * `store_core_bonus_percent`는 서버에서 계산합니다.
* **수정 문서/항목:**
  * IMPLEMENTATION_RISKS.md IAP 행
  * QA_MATRIX.md 결제 항목(같은 영수증 재전송 시 미지급 케이스 추가)
  * REMOTE_CONFIG.md `store_core_bonus_percent`

### 5. [High] "구매 복원"의 근거가 스토어가 아니라 서버 원장뿐임

* **문제:** IAP 상품은 전부 소모성(Core)입니다. 유료 S캐릭터는 Core로 사는 가상 구매라 스토어에 기록이 남지 않습니다. 스토어의 Restore 기능은 이 둘을 복원하지 않습니다.
* **왜 위험한지:** 재설치하거나 기기를 바꿨을 때 유료 캐릭터를 복원할 근거가 Firestore밖에 없습니다. 그런데 현재 그 Firestore가 클라이언트가 쓰는 JSON이라 유실이나 분쟁 시 증빙이 불가능합니다.
* **정확한 수정안:**
  * 복원은 "로그인 후 서버 원장(1번의 서버 전용 필드)을 로드하는 것"으로 정의합니다.
  * 유료 캐릭터 해금은 Core 차감과 같은 Function 트랜잭션 안에서 기록합니다.
  * 비소모성 상품이 없으므로 스토어 Restore 버튼 구현에 의존하지 않습니다.
* **수정 문서/항목:**
  * QA_MATRIX.md 결제·저장 항목
  * FIREBASE_SCHEMA.md
  * UI_FLOW.md Shop

### 6. [High] 여러 기기 사용 시 마지막 쓰기가 이전 데이터를 덮어씀

* **문제:** 세이브를 통째로 덮어쓰는 방식이라 나중에 쓴 쪽이 무조건 이깁니다.
* **왜 위험한지:** 두 기기를 번갈아 쓰거나 오프라인 뒤 복귀하면, 오래된 세이브가 최신 진행을 덮습니다. 1번에서 필드를 나눠도 진행 필드는 여전히 같은 문제가 있습니다.
* **정확한 수정안:**
  * 쓸 때마다 `saveVersion`을 올립니다.
  * Firestore 트랜잭션에서 서버 버전이 로컬 세이브가 기반한 버전보다 높으면 쓰기를 거부하고 충돌 처리로 넘깁니다(3번의 선택 UI를 재사용).
  * 앱 시작 시 로컬과 서버의 버전을 비교하는 규칙을 명시합니다.
* **수정 문서/항목:**
  * FIREBASE_SCHEMA.md
  * DATA_SCHEMA.md `UserProfile`

### 7. [High] 진행도 스키마가 분기를 표현하지 못함

* **문제:** `highestStageCleared`가 정수 하나라 S21_A와 S21_B를 구분할 수 없습니다. `selectedBranch`도 문자열 하나뿐이라 분기를 다시 선택할 수 있는지가 스키마에 반영될 수 없습니다.
* **왜 위험한지:** 분기 정책(한 번 선택하면 영구 고정인지, 엔딩 후 재선택이 가능한지)에 따라 세이브 구조가 달라집니다. 출시 후에 바꾸면 세이브 마이그레이션이 필요합니다.
* **정확한 수정안:**
  * `highestStageCleared`를 `clearedStages: map<stageId, bool>`로 교체합니다.
  * 분기 재선택 정책을 지금 확정합니다. 허용한다면 `selectedBranch`를 분기별 진행 map으로 바꿉니다.
  * STAGE_PLAN.csv에 `prev_stage_id` 컬럼을 두어 해금 조건을 데이터로 정의합니다.
* **수정 문서/항목:**
  * DATA_SCHEMA.md
  * STAGE_PLAN.csv
  * STORY_BEATS.md 분기점
  * QA_MATRIX.md 분기 항목

### 8. [High] 스토리 분기로 제작량이 3배가 됨

* **문제:** 분기마다 적 유닛(Human_Soldier / Rogue_AI / Human_Elite), 보스, 엔딩이 모두 따로입니다. 길이도 다릅니다. A·B는 S25에서 끝나지만 C만 챕터 4의 S30까지 있습니다.
* **왜 위험한지:** Stage 21 이후 아트·밸런싱·QA 작업이 3배가 되는데, 한 유저는 그중 1/3만 봅니다. 특히 C 분기가 일정을 가장 크게 밀어냅니다.
* **정확한 수정안:**
  * 분기 간 적 유닛 풀을 공용으로 씁니다. 분기의 차이는 `special_rule`, 적 배치, 보스, 엔딩 텍스트로만 냅니다.
  * 세 분기 길이를 같게 맞춥니다(예: 각 S21~S25). C의 챕터 4는 삭제하거나 뒤로 미룹니다.
* **수정 문서/항목:**
  * STAGE_PLAN.csv
  * STORY_BEATS.md 분기별 엔딩
  * ASSET_LIST.csv 적/보스 행

### 9. [High] 전투 구현에 필요한 데이터 컬럼 부재

* **문제:**
  * 로스터에 스킬 쿨다운, 스킬 계수, 지속시간, 타겟 규칙이 없습니다.
  * `growth_stat` 컬럼은 수치가 아니라 "HP/DEF" 같은 라벨이라 `Base + Growth*Level` 공식을 구현할 수 없습니다.
  * 레벨 상한과 ECONOMY의 `BaseCost`가 정의돼 있지 않습니다.
* **왜 위험한지:** 데이터 테이블의 컬럼 구조가 바뀌면 파서, ScriptableObject, 밸런스 시트를 모두 다시 만들어야 합니다.
* **정확한 수정안:** 값은 임시로 두더라도 컬럼은 지금 확정합니다.
  * CHARACTER_ROSTER.csv: `cooldown_sec`, `skill_target_type`, `skill_value`, `skill_duration`, `growth_hp`, `growth_atk`, `growth_def`, `max_level`
  * ECONOMY.csv: `base_cost`
* **수정 문서/항목:**
  * CHARACTER_ROSTER.csv
  * ECONOMY.csv
  * DATA_SCHEMA.md `CharacterData`

### 10. [High] 보스 인터럽트 규칙의 공백

* **문제:** 다음 질문에 대한 답이 문서에 없습니다.
  * 자동전투 중 제어 스킬이 자동으로 발동되어 인터럽트용으로 남지 않을 수 있는지
  * 제어 스킬이 쿨다운 중이면 인터럽트를 할 수 있는지
  * 제어 캐릭터를 편성하지 않았을 때 어떻게 되는지
  * Aegis의 단일 대상 스턴이 보스를 조준하는지
  * `Boss_Wipeout`, `Boss_Summon`, `Boss_Shield`가 무엇인지
* **왜 위험한지:**
  * 제어 스킬이 이미 소진돼 있으면 보스전이 확정 패배가 되어 진행이 막힙니다.
  * 게임의 핵심 차별 메커니즘인데 구현자마다 다르게 해석하게 됩니다.
* **정확한 수정안:**
  * 보스전에서는 Stun/Silence 스킬을 자동 발동에서 제외하고 수동 전용으로 둡니다.
  * 인터럽트는 텔레그래프 중에 해당 스킬 쿨다운이 준비된 경우에만 가능하고, 사용하면 쿨다운을 소모합니다.
  * 인터럽트 대상은 항상 보스로 고정합니다.
  * 보스는 Bribe와 일반 CC에 면역이고, 텔레그래프 중의 Stun/Silence만 유효합니다.
  * `Boss_*` 특수 규칙의 효과를 수치로 정의합니다. `Wipeout`이 즉사라면 Apex Override(무적)로 막을 수 있는지도 명시합니다.
  * 입력 판정은 터치 타임스탬프를 로직 틱으로 환산해 처리합니다.
* **수정 문서/항목:**
  * COMBAT_RULES.md 보스 스킬
  * STAGE_PLAN.csv `special_rule` 정의표
  * CHARACTER_ROSTER.csv C01/C04

### 11. [High] 타겟팅과 제한시간 규칙의 모순

* **문제:**
  * "가까운 열"이 1순위, "도발"이 2순위라서 도발이 사실상 작동하지 않습니다.
  * 같은 열에 3명이 있을 때 누구를 먼저 칠지 정해져 있지 않습니다.
  * 문서는 3x3 그리드라고 하는데, IMPLEMENTATION_RISKS는 연속 좌표 계산인 `Vector2.Distance`를 권장합니다.
  * 적의 배치 방식이 정의돼 있지 않습니다.
  * 제한시간은 90초인데 스테이지 길이는 60~120초로 되어 있습니다.
* **왜 위험한지:**
  * 동률일 때 무작위로 고르면 같은 편성이라도 결과가 매번 달라 밸런싱과 버그 재현이 불가능합니다.
  * 그리드 방식이냐 좌표 방식이냐에 따라 전투 코어 전체가 달라집니다.
* **정확한 수정안:**
  * 셀 기반 거리로 확정합니다.
  * 우선순위는 도발 > 가장 가까운 열 > 같은 열 안에서는 행 인덱스 순서로 고정합니다.
  * 적도 3x3 그리드(웨이브별 슬롯)로 정의합니다.
  * STAGE_PLAN.csv에 `time_limit` 컬럼을 두고 기본값을 90초로 합니다.
* **수정 문서/항목:**
  * COMBAT_RULES.md 타겟 선정
  * IMPLEMENTATION_RISKS.md Medium 행
  * GAME_SPEC.md 승리/패배 조건, 세션 길이
  * STAGE_PLAN.csv

### 12. [High] 경제 구조의 진행 막힘

* **문제:**
  * MemoryChip의 획득처는 "Boss_Stage"로만 적혀 있는데, STAGE_PLAN.csv의 보스 스테이지 보상 어디에도 MemoryChip이 없습니다. 실제 획득 경로가 0입니다.
  * GAME_SPEC.md의 "오프라인 방치 보상"이 ECONOMY.csv에 없습니다.
  * 에너지 최대치가 정의돼 있지 않습니다.
* **왜 위험한지:**
  * 스킬 강화가 원천적으로 불가능해집니다(곡선도 1.5^Tier로 가파름).
  * 막힌 구간을 풀 수단이 사실상 과금뿐이 됩니다.
  * 에너지가 무한히 쌓이거나, 반대로 상한 처리 버그가 생깁니다.
* **정확한 수정안:**
  * 보스 스테이지의 `first_clear_reward`와 `repeat_reward`에 MemoryChip을 넣습니다.
  * Energy_Battery에 자연 회복 상한(`max_natural`)을 명시합니다.
  * 방치 보상은 v1 범위라면 ECONOMY.csv에 행을 추가하고, 범위 밖이면 GAME_SPEC.md에서 문구를 삭제합니다.
* **수정 문서/항목:**
  * STAGE_PLAN.csv
  * ECONOMY.csv
  * GAME_SPEC.md 세션 길이 항목

### 13. [High] 유료 S캐릭터가 핵심 메커니즘을 무력화함 (Pay-to-win)

* **문제:**
  * Apex의 Override(전체 무적 3초)를 텔레그래프에 맞춰 쓰면 인터럽트가 필요 없어집니다.
  * Midas의 Bribe가 보스에 통하면 보스 자체를 무력화할 수 있습니다.
  * Oracle의 Deep Learning 스택에 상한이 없습니다.
* **왜 위험한지:** 유료 캐릭터 하나로 보스전의 핵심 설계가 우회되면, 무과금 유저와의 격차가 "편의"가 아니라 "클리어 가능 여부"가 됩니다.
* **정확한 수정안:**
  * Override가 보스 텔레그래프 스킬 피해에도 적용되는지 명시합니다. 적용한다면 쿨다운을 텔레그래프 주기보다 길게 설정합니다.
  * Bribe는 보스 면역으로 합니다(10번과 동일).
  * Deep Learning에 스택 상한을 둡니다.
* **수정 문서/항목:**
  * CHARACTER_ROSTER.csv C06/C07/C08
  * COMBAT_RULES.md 보스 스킬

### 14. [High] iOS/Android 동시 출시 필수 요건

* **문제:** 로그인과 계정 관련 스토어 요건이 반영돼 있지 않습니다.
* **왜 위험한지:** 아래 요건을 지키지 않으면 심사에서 반려됩니다.
  * iOS에서 Google 로그인을 제공하면 Sign in with Apple을 반드시 함께 제공해야 합니다(App Review 4.8).
  * 계정을 만드는 앱은 앱 안에서 계정을 삭제할 수 있어야 합니다(Apple 5.1.1(v)). Google Play도 데이터 삭제 요청 경로를 요구합니다.
  * Android에서 Apple 로그인을 제공하려면 Firebase OAuth 웹 플로우가 필요해 구현 방식이 다릅니다.
* **정확한 수정안:**
  * 플랫폼별 로그인 매트릭스를 정합니다. 예: iOS는 Apple+Google, Android는 Google(Apple 로그인은 선택).
  * 계정 삭제 기능을 둡니다. Auth 계정과 Firestore 문서를 함께 삭제하는 Function으로 처리합니다.
  * 상품 ID를 양 스토어에서 동일하게 매핑하고, 가격은 각 스토어의 가격 티어 기준으로 표기합니다.
* **수정 문서/항목:**
  * UI_FLOW.md(Title, 계정 삭제 진입점)
  * QA_MATRIX.md
  * ECONOMY.csv `iap_candidate`의 상품 ID

---

# 그대로 사용 가능

* **코어 루프와 패배 무페널티:** 모바일 세션 구조에 잘 맞습니다.
* **물리 엔진을 쓰지 않는 수학적 타겟팅:** 방향이 맞습니다. 11번처럼 그리드 기준으로만 확정하면 됩니다.
* **유저당 단일 Firestore 문서 원칙:** read/write 비용 면에서 적절합니다. 1번과 6번처럼 필드를 나누면 그대로 유지할 수 있습니다.
* **Remote Config의 적 HP/ATK·보상·에너지 배율:** 패치 없이 밸런싱하는 수단으로 충분합니다. 결제 보너스만 서버에서 적용하면 됩니다.
* **텔레그래프 + 수동 인터럽트 메커니즘 자체:** 차별점으로 유효합니다.
* **초반 무료 로스터:** 탱커·힐러·딜러·제어가 모두 무료로 확보되고, 제어 캐릭터 Aegis가 기본 지급입니다. 보스전 막힘을 막는 좋은 구조입니다.
* **소프트/하드 재화 분리와 가챠 없는 직접 판매:** 확률형 아이템 규제 부담이 없습니다.
* **분석 이벤트 6개 최소 세트**
* **에셋 고정 캔버스 가이드와 네트워크 필수 진입 정책:** 네트워크 필수 진입은 저장 일관성에도 유리합니다.

# 개발 중 결정해도 됨

* 스킬 계수, 쿨다운, 성장치, BaseCost, 레벨 상한의 실제 값(컬럼만 먼저 확정)
* ASPD의 틱 단위 반올림 방식(누산기 방식 권장)
* 데미지 편차 난수의 시드 처리
* 소탕 기능의 비용과 해금 조건
* Stage 30~50 확장분과 웨이브 구성
* 텔레그래프 연출, 경고 텍스트, UI 세부 레이아웃
* 캐릭터·적·효과음 에셋의 실제 소스 선택
* 환불된 결제의 재화 회수 정책(서버 원장만 있으면 출시 후 추가 가능)
* Unity IAP 버전 선택(v4 / v5 API). 착수 시점에 한 번만 정하면 됩니다.
* 분석 이벤트 파라미터 확장(예: `iap_purchase`에 통화 코드 추가)
* 엔딩 연출과 스토리 텍스트 분량
