namespace HGame01Server.Models.GameData;

/// 클라 EAbilityGrade 미러. SO는 enum 이름("Common" 등)을 JSON 문자열로 직렬화하므로
/// 서버는 Enum.Parse로 받아 정수 비교/저장에 사용한다.
/// Constants의 weightGrade1~4 키는 Common(1)~Legendary(4)와 정렬됨.
public enum EAbilityGrade
{
    Invalid = 0,
    Common = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4,
}

/// 클라 EEquipmentSlot 미러. DB GameUserEquipment.slot은 int 컬럼이므로
/// JSON의 enum 이름을 받아 정수로 변환해 저장한다.
public enum EEquipmentSlot
{
    Bow = 0,
    Sword = 1,
}
