namespace HGame01Server.Models.GameData;

// 이 파일은 Admin/UploadGameData API에 의해 자동 생성되었습니다.
// 직접 수정하지 마세요. Unity에서 데이터를 다시 업로드하면 덮어씌워집니다.

public static class GdbConst
{
    public static class Character
    {
        public const string Category = "character";
        public const string DefaultCharacterTag = "defaultCharacterTag";
    }

    public static class Combat
    {
        public const string Category = "combat";
        public const string CriticalMultiplier = "criticalMultiplier";
        public const string MaxComboCount = "maxComboCount";
        public const string ComboResetTime = "comboResetTime";
        public const string GuardDamageReduction = "guardDamageReduction";
        public const string ParryWindowDuration = "parryWindowDuration";
        public const string MaxBattleItemPerType = "maxBattleItemPerType";
    }

    public static class Dialog
    {
        public const string Category = "dialog";
        public const string TypingSpeed = "typingSpeed";
    }

    public static class EquipmentStorage
    {
        public const string Category = "equipmentStorage";
        public const string ExpandCostDiamond = "expandCostDiamond";
        public const string ExpandSlotsPerPurchase = "expandSlotsPerPurchase";
        public const string MaxCapacity = "maxCapacity";
    }

    public static class Gacha
    {
        public const string Category = "gacha";
        public const string SingleCostDiamond = "singleCostDiamond";
        public const string MultiCount = "multiCount";
        public const string MultiCostDiamond = "multiCostDiamond";
        public const string WeightGrade1 = "weightGrade1";
        public const string WeightGrade2 = "weightGrade2";
        public const string WeightGrade3 = "weightGrade3";
        public const string WeightGrade4 = "weightGrade4";
    }

    public static class GameMode
    {
        public const string Category = "gameMode";
        public const string StageManagerAddress = "stageManagerAddress";
        public const string DevManagerAddress = "devManagerAddress";
    }

    public static class Quest
    {
        public const string Category = "quest";
        public const string RequiredCompletedCount = "requiredCompletedCount";
        public const string DailyMissionRewards = "dailyMissionRewards";
        public const string DailyMissionRewardSequence = "dailyMissionRewardSequence";
    }

    public static class SeasonPass
    {
        public const string Category = "seasonPass";
        public const string ExpPerLevel = "expPerLevel";
        public const string PremiumPassPrice = "premiumPassPrice";
        public const string StageVictoryExp = "stageVictoryExp";
        public const string StageDefeatExp = "stageDefeatExp";
        public const string ClaimRewardSequence = "claimRewardSequence";
    }

    public static class Shop
    {
        public const string Category = "shop";
        public const string DailyResetHourUtc = "dailyResetHourUtc";
        public const string GachaProducts = "gachaProducts";
    }

}
