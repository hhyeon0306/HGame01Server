using HGame01Server.Models;
using HGame01Server.Models.GameData;

namespace HGame01Server.Services;

/// 보상 tag → PkRewardResult 셋업 유틸.
/// ItemData(Currency 전용) / EquipmentData / BattleItemData 세 테이블을 순차 조회하여
/// tag가 속한 위치로 kind를 판별한다. 실제 지급(Currency Add, Equipment 인벤토리 추가 등)은 호출자가 별도로 처리.
public static class RewardResolver
{
    public static PkRewardResult Resolve(GameDataManager gameDataManager, string itemTag, int count)
    {
        var result = new PkRewardResult
        {
            ItemTag = itemTag,
            Count = count,
        };

        if (string.IsNullOrEmpty(itemTag))
        {
            return result;
        }

        var items = gameDataManager.GetList<GdbItemData>();
        var item = items?.FirstOrDefault(i => i.tag == itemTag);
        if (item != null)
        {
            result.ItemKind = item.kind;
            if (item.kind == "Currency")
            {
                result.CurrencyType = item.currency_type;
            }
            return result;
        }

        var equipments = gameDataManager.GetList<GdbEquipmentData>();
        var equipment = equipments?.FirstOrDefault(e => e.tag == itemTag);
        if (equipment != null)
        {
            result.ItemKind = "Equipment";
            result.EquipmentRef = equipment.tag;
            return result;
        }

        var battleItems = gameDataManager.GetList<GdbBattleItemData>();
        var battleItem = battleItems?.FirstOrDefault(b => b.tag == itemTag);
        if (battleItem != null)
        {
            result.ItemKind = "BattleItem";
            result.BattleItemRef = battleItem.tag;
            return result;
        }

        return result;
    }
}
