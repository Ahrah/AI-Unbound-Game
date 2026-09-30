Risk Level	컴포넌트	이슈 설명 및 완화 방안 (Mitigation)
Critical	IAP 영수증 검증	클라이언트 단독 결제 처리는 변조에 취약. Firebase Cloud Functions를 통한 서버사이드 영수증 검증 필수. v1.0에서 Functions 구축이 부담되면 유저 데이터를 최소한으로 암호화하여 저장.
High	보스 스킬 Interrupt(타이밍) UX	화면의 프레임 드랍이나 기기 성능에 따라 터치 딜레이 발생 가능. Interrupt 입력 허용 판정 시간(Window)을 기획(3초)보다 조금 더 관대하게(서버/논리 틱 보정) 잡아야 함.
Medium	Auto-battle Targeting	물리 엔진(Rigidbody) 기반이 아닌 거리 기반 수학적 타겟팅 구현 필요. Unity 물리 엔진을 쓰면 연산 낭비 및 버그 발생 높음. Vector2.Distance로 순수 논리적 연산 권장.
Low	AI 에셋 해상도 불일치	AI로 생성된 캐릭터 일러스트 간 화풍 및 크기가 다를 수 있음. 프로젝트 세팅 시 픽셀 퍼펙트 또는 고정 캔버스 비율(예: 512x512)을 반드시 가이드로 설정.
