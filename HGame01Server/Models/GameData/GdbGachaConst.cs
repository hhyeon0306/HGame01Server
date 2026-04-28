namespace HGame01Server.Models.GameData;

// 이 파일은 수동 작성된 const 정의입니다. (Admin/UploadGameData 자동 생성 대상 아님)
// 클라 GachaConstantsData SO export로 GdbConst.Gacha가 자동 생성되지 않을 때 fallback 정의.
// 클라 ConstantsData가 List<GachaGradeWeight> 형태로 가중치를 보관하는데 export가 단일 키 시리얼라이즈만 지원해서
// WeightGrade1~4 키가 자동 생성되지 않음. 서버는 GameDataManager.GetConstInt fallback 값으로 동작.
public static class GdbGachaConst
{
    public const string Category = "gacha";

    // 단일/다중 뽑기 비용 (클라 GachaConstantsData.singleCostDiamond / multiCostDiamond)
    public const string SingleCostDiamond = "singleCostDiamond";
    public const string MultiCount = "multiCount";
    public const string MultiCostDiamond = "multiCostDiamond";

    // 등급별 가중치 (클라 List<GachaGradeWeight>는 export 미지원 — fallback 값 사용)
    public const string WeightGrade1 = "weightGrade1";
    public const string WeightGrade2 = "weightGrade2";
    public const string WeightGrade3 = "weightGrade3";
    public const string WeightGrade4 = "weightGrade4";
}
