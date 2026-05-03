using HGame01Server.Models;
using HGame01Server.Models.GameData;

namespace HGame01Server.Services;

/// 보상 tag → PkRewardResult 셋업 유틸.
/// ItemData / EquipmentData 두 테이블을 순차 조회해서 tag 가 속한 위치로 RewardType 을 판별한다.
/// (BattleItem 은 스테이지 일회용이라 보상 대상 아님 — 룩업 체인에서 제외.)
/// 새 RewardType (Pet/Treasure 등) 추가 시 여기에 분기 한 줄 추가.
/// 실제 지급(Currency Add, Equipment 인벤토리 추가 등)은 호출자가 별도로 처리.
public static class RewardResolver
{
    /// 보상 tag → PkRewardResult. RewardType 미해상 시 빈 문자열로 반환 (호출자 책임 분기).
    public static PkRewardResult Resolve(GameDataManager gameDataManager, string rewardTag, int count)
    {
        var result = new PkRewardResult
        {
            RewardTag = rewardTag,
            Count = count,
        };

        if (string.IsNullOrEmpty(rewardTag))
        {
            return result;
        }

        var items = gameDataManager.GetList<GdbItemData>();
        var item = items?.FirstOrDefault(i => i.tag == rewardTag);
        if (item != null)
        {
            result.RewardType = "Item";
            result.ItemKind = item.kind;
            return result;
        }

        var equipments = gameDataManager.GetList<GdbEquipmentData>();
        var equipment = equipments?.FirstOrDefault(e => e.tag == rewardTag);
        if (equipment != null)
        {
            result.RewardType = "Equipment";
            return result;
        }

        return result;
    }

    /// rewardTag 가 Currency 보상일 때 GdbItemData.currency_type ("Gold"/"Diamond") 을 반환.
    /// Currency 가 아니거나 미해상 시 빈 문자열. tag 파싱이 아닌 데이터 lookup 으로 단일 진실 보장.
    public static string ResolveCurrencyType(GameDataManager gameDataManager, string rewardTag)
    {
        if (string.IsNullOrEmpty(rewardTag))
        {
            return string.Empty;
        }

        var items = gameDataManager.GetList<GdbItemData>();
        var item = items?.FirstOrDefault(i => i.tag == rewardTag);
        if (item == null || item.kind != "Currency")
        {
            return string.Empty;
        }

        return item.currency_type ?? string.Empty;
    }
}
