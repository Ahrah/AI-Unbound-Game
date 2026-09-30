Firestore는 최소한의 쓰기(Write)를 위해 유저 단위로 통합된 하나의 문서만 유지.

⚬ Collection: Users
  ⚬ Document ID: Firebase Auth UID
    ⚬ uid: string
    ⚬ created_at: timestamp
    ⚬ last_login: timestamp
    ⚬ save_data: JSON string (UserProfile 전체를 직렬화하여 단일 필드에 저장. 스테이지 클리어/결제 시에만 덮어쓰기하여 Read/Write 비용 최소화)
