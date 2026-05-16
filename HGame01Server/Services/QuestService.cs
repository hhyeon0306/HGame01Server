using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 서버 측 Quest 도메인 서비스. Active/Claim/RefreshDaily 흐름 + GdbQuestData 조회.
/// 진행 누적은 QuestProgressService 책임. 본 서비스는 인스턴스 발급/조회/수령.
public class QuestService
{
    private readonly IGameDB _gameDB;
    private readonly IClock _clock;
    private readonly IResetSchedule _dailyReset;
    private readonly GameDataManager _gameDataManager;
    private readonly CurrencyService _currencyService;
    private readonly MailService _mailService;

    public QuestService(IGameDB gameDB, IClock clock, IResetSchedule dailyReset, GameDataManager gameDataManager, CurrencyService currencyService, MailService mailService)
    {
        _gameDB = gameDB;
        _clock = clock;
        _dailyReset = dailyReset;
        _gameDataManager = gameDataManager;
        _currencyService = currencyService;
        _mailService = mailService;
    }

    // ===== 일일 cycle 정산 (Daily reset settlement) =====
    //
    // 도메인: "어제 reset window를 마무리하고 미수령 보상을 우편함으로 회수".
    // 호출자: GetActiveAsync / RefreshDailyAsync (자연 + cheat 통합).
    // 사용자 명시 종합 수령(ClaimDailyBundleAsync)은 CompensateExpiringCompletedAsync만 호출 (중복 지급 차단).
    //
    // force 의미:
    // - false: 자연 자정 통과 흐름. expiresAtUtc 도과 슬롯 + lastDailyBundleClaimedDateUtc 비교 기반 자격.
    // - true:  cheat resetdaily. 도과 검사 우회 + 종합 보상 자격 비교 우회 (자정 시뮬).

    /// 일일 cycle 정산 orchestrator — 호출자는 1줄.
    /// (1) 종합 보상 자격(Completed+Claimed) 판정 후 우편함 발송, (2) Quest 인스턴스 미수령 → 우편함 + Expired 마킹.
    /// 순서 중요 — Completed가 Expired로 마킹되기 전에 종합 보상 자격 판정해야 카운트 정확.
    private async Task SettleDailyResetAsync(long uid, string nowStr, bool force)
    {
        await CompensateUnclaimedBundleAsync(uid, nowStr, force);
        await CompensateExpiringCompletedAsync(uid, nowStr, force);
    }

    /// 미수령 Completed 슬롯의 보상을 quest별 개별 mail로 발송 + atomic Expired 마킹.
    /// 개별 발송 사유: 우편함 UI(InboxCellUI)가 cell당 단일 reward 표시이라 묶음 1통이면 첫 entry만 보임.
    /// reward_item 미설정/0 count quest는 mail 없이 expire만 (보상 자체가 없으므로 손실 X).
    private async Task CompensateExpiringCompletedAsync(long uid, string nowStr, bool force)
    {
        var expiring = await ResolveCompensableCompletedAsync(uid, nowStr, force);
        if (expiring.Count == 0)
        {
            return;
        }

        var quests = _gameDataManager.GetList<GdbQuestData>();
        var mails = new List<GameUserMail>();
        foreach (var inst in expiring)
        {
            var quest = quests?.FirstOrDefault(q => q.id == inst.questDataId);
            if (quest != null && !string.IsNullOrEmpty(quest.reward_item) && quest.reward_count > 0)
            {
                var singleReward = new List<MailRewardEntry>
                {
                    new() { rewardTag = quest.reward_item, count = quest.reward_count },
                };
                mails.Add(BuildExpiredQuestMail(uid, singleReward, nowStr));
            }
            inst.status = "Expired";
            inst.lastUpdatedUtc = nowStr;
        }
        await _gameDB.ExpireCompletedWithMailsAsync(expiring, mails);
    }

    /// 일일 완료(Completed+Claimed >= 임계) + 종합 보상 미수령 시 종합 보상 우편함 발송.
    /// 카운트 범위: force=false면 도과 Daily 슬롯만(어제 cycle), force=true면 활성 Daily 전체(cheat 시뮬).
    /// 자격 비교: force=false면 lastDailyBundleClaimedDateUtc 비교, force=true면 우회.
    /// 발송 후 컬럼 값: 자연 흐름은 어제 일자(오늘 새 cycle 수령 가능), cheat는 임의(직후 ClearDailyBundleClaimedDateAsync로 덮어쓰기).
    private async Task CompensateUnclaimedBundleAsync(long uid, string nowStr, bool force)
    {
        var currentDateUtc = _dailyReset.Current.ToString("yyyy-MM-dd");
        if (!force)
        {
            var lastClaimedDate = await _gameDB.GetLastDailyBundleClaimedDateAsync(uid);
            if (lastClaimedDate == currentDateUtc)
            {
                return;
            }
        }

        int requiredCount = _gameDataManager.GetConstInt(
            GdbConst.Quest.Category, GdbConst.Quest.RequiredCompletedCount, defaultValue: 4);
        var instances = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        int qualifiedCount = instances.Count(i =>
            i.containerStableId == QuestServerConstants.QuestContainerDailyStableId
            && (i.status == "Completed" || i.status == "Claimed")
            && (force || (!string.IsNullOrEmpty(i.expiresAtUtc) && string.Compare(i.expiresAtUtc, nowStr) < 0)));
        if (qualifiedCount < requiredCount)
        {
            return;
        }

        var rewardSum = AccumulateDailyBundleRewards(uid);
        if (rewardSum.Count == 0)
        {
            return;
        }

        // currency별 개별 mail 발송 — 우편함 UI cell당 단일 reward 표시 정합.
        var mails = new List<GameUserMail>();
        foreach (var kv in rewardSum)
        {
            string tag = CurrencyTypeIdToItemTag(kv.Key);
            if (string.IsNullOrEmpty(tag))
            {
                continue;
            }
            var singleReward = new List<MailRewardEntry>
            {
                new() { rewardTag = tag, count = (int)kv.Value },
            };
            mails.Add(BuildBundleMail(uid, singleReward, nowStr));
        }
        if (mails.Count == 0)
        {
            return;
        }

        string dateToSet = force
            ? currentDateUtc
            : _dailyReset.Current.AddDays(-1).ToString("yyyy-MM-dd");
        await _gameDB.SendBundleMailAtomicAsync(uid, dateToSet, mails);
    }

    /// 만료 대상 Completed 슬롯 조회. force 분기 캡슐화.
    private async Task<List<GameUserQuestInstance>> ResolveCompensableCompletedAsync(long uid, string nowStr, bool force)
    {
        if (force)
        {
            var all = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
            return all.Where(q => q.status == "Completed").ToList();
        }
        return await _gameDB.GetCompletedExpiringInstancesAsync(uid, nowStr);
    }

    /// 미수령 Quest 보상 1통 mail entity (quest당 1통).
    private GameUserMail BuildExpiredQuestMail(long uid, List<MailRewardEntry> rewards, string nowStr)
        => BuildCompensationMail(uid, rewards, nowStr, "Mail.Quest.DailyExpired", "QuestExpired");

    /// 종합 보상 묶음 1통 mail entity.
    private GameUserMail BuildBundleMail(long uid, List<MailRewardEntry> rewards, string nowStr)
        => BuildCompensationMail(uid, rewards, nowStr, "Mail.Quest.DailyBundleExpired", "QuestBundleExpired");

    /// Quest 도메인 보상 회수 mail factory. ShopService.BuyDiamondAsync와 동일 패턴 — iconAtlas/iconKey를 명시 set.
    /// rewards[0].rewardTag(GameplayTag) → GdbItemData 룩업 → icon_name. ItemAtlas sprite로 우편함 cell 아이콘 표시.
    /// 매칭 실패 시 빈 문자열 → 클라 ResolveIcon fallback 흐름. (Currency만 ItemData 매칭 — Equipment 등 미래 보상은 별도 atlas 정책 필요.)
    private GameUserMail BuildCompensationMail(long uid, List<MailRewardEntry> rewards, string nowStr, string titleKey, string mailKind)
    {
        string iconKey = ResolveItemIconName(rewards);
        return new GameUserMail
        {
            mailId = Guid.NewGuid().ToString("N"),
            uid = uid,
            titleKey = titleKey,
            mailKind = mailKind,
            rewardsJson = JsonSerializer.Serialize(rewards),
            iconAtlas = string.IsNullOrEmpty(iconKey) ? "" : "ItemAtlas",
            iconKey = iconKey,
            senderType = "Compensation",
            sentAt = nowStr,
            expireAt = FormatUtc(_clock.UtcNow.AddDays(30)),
            claimedAt = "",
        };
    }

    /// rewards 첫 entry의 rewardTag(GameplayTag string) → GdbItemData.icon_name 룩업.
    /// 매칭 실패 시 빈 문자열.
    private string ResolveItemIconName(List<MailRewardEntry> rewards)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return "";
        }
        string rewardTag = rewards[0].rewardTag;
        if (string.IsNullOrEmpty(rewardTag))
        {
            return "";
        }
        var items = _gameDataManager.GetList<GdbItemData>();
        var item = items?.FirstOrDefault(i => i.tag == rewardTag);
        return item?.icon_name ?? "";
    }

    /// 활성(InProgress/Completed/Claimed) 인스턴스 조회 — 정산 + 만료 lazy 정리 + DTO 변환.
    /// Expired는 응답에서 제외.
    public async Task<List<PkQuestInstanceDto>> GetActiveAsync(long uid)
    {
        var nowStr = FormatUtc(_clock.UtcNow);
        await SettleDailyResetAsync(uid, nowStr, force: false);
        await _gameDB.ExpireQuestInstancesAsync(uid, nowStr);
        var instances = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        return instances.Select(ToDto).ToList();
    }

    /// 일일 슬롯 발급 — 자정 통과 시 호출. 기존 InProgress 만료 + 신규 InProgress 발급을 단일 트랜잭션으로 atomic 보장.
    /// 미수령 Completed 보상 + 종합 보상은 SettleDailyResetAsync가 우편함으로 회수.
    /// 신규 인스턴스의 subProgressJson은 GdbQuestData required count로 채움 — 서버 권위 progress 판정의 토대.
    /// idempotency 가드 — 만료 시각 전 fresh InProgress가 존재하면 skip 후 기존 슬롯 반환.
    public async Task<List<PkQuestInstanceDto>> RefreshDailyAsync(long uid, int dailyContainerStableId, int slotCount, IReadOnlyList<int> dailyQuestDataIds, bool force = false)
    {
        var nowStr = FormatUtc(_clock.UtcNow);
        await SettleDailyResetAsync(uid, nowStr, force);

        var existing = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        var dailyInProgress = existing
            .Where(q => q.containerStableId == dailyContainerStableId
                && q.status == "InProgress")
            .ToList();

        // idempotency — 같은 reset window 내 재호출은 신규 발급 skip.
        // string.Compare는 yyyy-MM-dd HH:mm:ss 형식이라 lexical = chronological 일치.
        // 응답은 신규 발급 여부와 무관하게 자기 컨테이너의 모든 활성(InProgress/Completed/Claimed) 인스턴스 — 클라 Hydrate가 dedup 정렬.
        // Claimed 인스턴스 누락 시 클라가 destroy → UX 사고 차단.
        // force=true면 idempotency 가드 우회 — cheat resetdaily 강제 재발급 흐름.
        bool anyFresh = !force && dailyInProgress.Any(q =>
            !string.IsNullOrEmpty(q.expiresAtUtc) && string.Compare(q.expiresAtUtc, nowStr) > 0);
        if (anyFresh)
        {
            return existing
                .Where(q => q.containerStableId == dailyContainerStableId)
                .Select(ToDto)
                .ToList();
        }

        // force=true(cheat resetdaily) — 새 cycle 시뮬. SettleDailyResetAsync가 set한 컬럼을 빈 문자열로 클리어.
        // 자연 자정은 IResetSchedule.Current가 새 일자라 컬럼 값과 미일치로 자동 풀림 → 클리어 불필요.
        if (force)
        {
            await _gameDB.ClearDailyBundleClaimedDateAsync(uid);
        }

        // 만료 진행 — 남은 InProgress + Claimed Expired로. Completed는 이미 SettleDailyResetAsync가 우편함 회수 + Expired 마킹.
        var dailyExisting = existing
            .Where(q => q.containerStableId == dailyContainerStableId
                && (q.status == "InProgress" || q.status == "Claimed"))
            .ToList();
        foreach (var q in dailyExisting)
        {
            q.status = "Expired";
            q.lastUpdatedUtc = nowStr;
        }

        // 신규 발급 — pool에서 slotCount만큼.
        var newInstances = new List<GameUserQuestInstance>();
        if (dailyQuestDataIds != null && dailyQuestDataIds.Count > 0)
        {
            var nextResetStr = FormatUtc(_dailyReset.Next);
            int limit = System.Math.Min(slotCount, dailyQuestDataIds.Count);
            for (int i = 0; i < limit; i++)
            {
                int qdId = dailyQuestDataIds[i];
                newInstances.Add(new GameUserQuestInstance
                {
                    instanceId = System.Guid.NewGuid().ToString(),
                    uid = uid,
                    questDataId = qdId,
                    containerStableId = dailyContainerStableId,
                    subProgressJson = BuildInitialSubProgressJson(qdId),
                    status = "InProgress",
                    issuedAtUtc = nowStr,
                    expiresAtUtc = nextResetStr,
                    lastUpdatedUtc = nowStr,
                });
            }
        }

        await _gameDB.RefreshDailyQuestsTransactionAsync(dailyExisting, newInstances);

        // 응답 — 신규 InProgress + 기존 Completed/Claimed (Expired 제외).
        // 신규 발급 흐름에서도 기존 Claimed가 응답에 살아있어야 클라가 destroy 사고 X.
        var refreshed = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        return refreshed
            .Where(q => q.containerStableId == dailyContainerStableId)
            .Select(ToDto)
            .ToList();
    }

    /// 신규 인스턴스의 초기 subProgress JSON. GdbQuestData.required_count로 채움 — ApplyDelta가 Required 정합 판정 가능.
    /// 본 라운드는 단일 condition 가정. Composite는 Phase 9 도입 시 condition 다형 직렬화로 확장.
    /// quest 미발견 또는 required_count<=0이면 데이터 결함 — fail-fast throw로 즉시 노출 (Active 응답 자체가 실패).
    private string BuildInitialSubProgressJson(int questDataId)
    {
        var quests = _gameDataManager.GetList<GdbQuestData>();
        var quest = quests?.FirstOrDefault(q => q.id == questDataId);
        if (quest == null)
        {
            throw new InvalidOperationException($"[Quest] BuildInitialSubProgressJson: GdbQuestData id={questDataId} 미발견 — 데이터 시드 결함.");
        }
        if (quest.required_count <= 0)
        {
            throw new InvalidOperationException($"[Quest] BuildInitialSubProgressJson: GdbQuestData id={questDataId} (tag={quest.quest_tag}) required_count={quest.required_count} 무효 — 디자이너 데이터 결함.");
        }
        var entries = new List<PkSubProgressEntry>
        {
            new() { ChildIndex = 0, Progress = 0, Required = quest.required_count }
        };
        return JsonSerializer.Serialize(entries);
    }

    /// 보상 수령 — Completed에서만 허용. RewardResolver로 보상 결정 + Currency 즉시 누적.
    /// Equipment/Item 등 비-Currency 보상은 응답에 정보만 담고 실제 지급은 Phase 9 RewardGrantService 통합에서 처리.
    public async Task<PkQuestClaimResponse> ClaimAsync(long uid, string instanceId)
    {
        var inst = await _gameDB.GetQuestInstanceAsync(uid, instanceId);
        if (inst == null)
        {
            return new PkQuestClaimResponse { Result = ErrorCode.QuestInstanceNotFound, InstanceId = instanceId };
        }
        if (inst.status != "Completed")
        {
            return new PkQuestClaimResponse { Result = ErrorCode.QuestNotClaimable, InstanceId = instanceId };
        }

        // GdbQuestData 조회 — id 기반. reward_item / reward_count 자동 동기화 필드 사용.
        var quests = _gameDataManager.GetList<GdbQuestData>();
        var quest = quests?.FirstOrDefault(q => q.id == inst.questDataId);
        if (quest == null)
        {
            return new PkQuestClaimResponse { Result = ErrorCode.QuestDataNotFound, InstanceId = instanceId };
        }

        // 보상 결정 — Shop과 동일 패턴. reward_item 미설정이면 reward null (status 전환만).
        // Currency 보상은 즉시 누적. 비-Currency(Equipment/Item)는 응답에 정보만 — Phase 9 RewardGrantService 통합 시 실제 지급.
        // currencyTypeId=-1은 통화 없음 sentinel — Diamond=0이 valid 값이라 0을 sentinel로 절대 사용 금지.
        PkRewardResult? reward = null;
        int currencyTypeId = -1;
        long currencyDelta = 0;
        if (!string.IsNullOrEmpty(quest.reward_item) && quest.reward_count > 0)
        {
            reward = RewardResolver.Resolve(_gameDataManager, quest.reward_item, quest.reward_count);
            string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, reward.RewardTag);
            if (!string.IsNullOrEmpty(currencyName))
            {
                currencyTypeId = ParseCurrencyType(currencyName);
                currencyDelta = quest.reward_count;
            }
        }

        // 인스턴스 Claimed 전환 + Currency 누적을 단일 트랜잭션으로 묶음 — 부분 실패 시 중복 지급 사고 차단.
        inst.status = "Claimed";
        inst.lastUpdatedUtc = FormatUtc(_clock.UtcNow);
        await _gameDB.ClaimQuestTransactionAsync(inst, uid, currencyTypeId, currencyDelta);

        var response = new PkQuestClaimResponse
        {
            Result = ErrorCode.None,
            InstanceId = instanceId,
            Reward = reward,
        };
        await _currencyService.PopulateCurrenciesAsync(response, uid);
        return response;
    }

    /// 현 reset window 안에서 일일 종합 보상을 이미 수령했는가.
    /// PkQuestActiveResponse.DailyBundleClaimedToday 채움 — 클라 popup이 부팅 시점 받기 버튼 잠금 결정용.
    public async Task<bool> IsDailyBundleClaimedTodayAsync(long uid)
    {
        var currentDateUtc = _dailyReset.Current.ToString("yyyy-MM-dd");
        var lastClaimedDate = await _gameDB.GetLastDailyBundleClaimedDateAsync(uid);
        return lastClaimedDate == currentDateUtc;
    }

    /// 일일 종합 보상 수령 — 활성 Daily 슬롯 중 Claimed 카운트가 임계 이상이면 Currency 누적.
    /// 일일 1회 제한: users.lastDailyBundleClaimedDateUtc 컬럼이 현 reset window 일자와 일치하면 AlreadyClaimed.
    /// users 컬럼 갱신 + Currency 누적은 ClaimDailyBundleTransactionAsync로 atomic 보장.
    /// 임계 + 보상은 GdbConstants(Quest 카테고리)에서 조회 — 디자이너가 SO만 수정/재업로드하면 코드 변경 없이 반영.
    public async Task<PkQuestClaimDailyBundleResponse> ClaimDailyBundleAsync(long uid)
    {
        // ① 일일 1회 제한 — IResetSchedule.Current 기준 일자(yyyy-MM-dd) 비교.
        // 자정(또는 dailyResetHourUtc) 통과 시 reset window가 새 일자로 회전 → 컬럼 값과 달라지므로 자동으로 다시 수령 가능.
        var currentDateUtc = _dailyReset.Current.ToString("yyyy-MM-dd");
        var lastClaimedDate = await _gameDB.GetLastDailyBundleClaimedDateAsync(uid);
        if (lastClaimedDate == currentDateUtc)
        {
            return new PkQuestClaimDailyBundleResponse { Result = ErrorCode.QuestDailyBundleAlreadyClaimed };
        }

        // ② Claimed 카운트 검증 — 활성 Daily 인스턴스 중 Claimed가 임계 이상이어야 함.
        // 임계는 GdbConst.Quest.RequiredCompletedCount 조회 (QuestConstantsData.requiredCompletedCount SO 필드).
        int requiredCompletedCount = _gameDataManager.GetConstInt(
            GdbConst.Quest.Category,
            GdbConst.Quest.RequiredCompletedCount,
            defaultValue: 4);

        // 사용자 명시 수령 흐름 — Quest 인스턴스 stale만 정리. CompensateUnclaimedBundle은 호출 X (중복 지급 차단).
        // 종합 보상 자동 우편함 발송은 GetActiveAsync/RefreshDailyAsync(SettleDailyResetAsync)가 책임.
        var nowStr = FormatUtc(_clock.UtcNow);
        await CompensateExpiringCompletedAsync(uid, nowStr, force: false);
        await _gameDB.ExpireQuestInstancesAsync(uid, nowStr);
        var instances = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        int claimedDailyCount = instances.Count(i =>
            i.containerStableId == QuestServerConstants.QuestContainerDailyStableId
            && i.status == "Claimed");

        if (claimedDailyCount < requiredCompletedCount)
        {
            return new PkQuestClaimDailyBundleResponse { Result = ErrorCode.QuestDailyBundleNotReady };
        }

        // ③ 보상 sum — GdbConst.Quest.DailyMissionRewards JSON 배열 파싱 → currency별 누적.
        // 클라 QuestConstantsData.dailyMissionRewards (List<QuestRewardLine>)이 GameDataUploader로 JSON 배열 문자열로 직렬화됨.
        // 각 line.item(GameplayTag.name) → GdbItemData lookup → currency_type → 누적.
        var sumByCurrency = AccumulateDailyBundleRewards(uid);
        if (sumByCurrency.Count == 0)
        {
            // 데이터 결함 — 빈 SO 또는 직렬화 실패. fail-fast로 노출.
            throw new InvalidOperationException($"[Quest] ClaimDailyBundleAsync: GdbConst.Quest.DailyMissionRewards 비어있음 또는 currency 매칭 실패. Constants.json 점검 필요.");
        }

        // ④ Currency 누적 + 컬럼 갱신을 atomic 트랜잭션으로 묶음.
        var currencyDeltas = sumByCurrency.Select(kv => (kv.Key, kv.Value)).ToList();
        await _gameDB.ClaimDailyBundleTransactionAsync(uid, currentDateUtc, currencyDeltas);

        // primary reward — 합산 amount가 가장 큰 currency를 UI 연출용으로 응답.
        // 나머지 currency는 응답 Currencies(전체 절대 스냅샷) 갱신으로 클라 UI 자연 반영.
        // PkRewardResult 단일 반환은 RewardSequence(CoinFlyStep)가 1종 sprite/flyTarget으로 분기하는 기존 제약 정합 — 다중 currency 동시 연출은 응답 List 확장이 필요한 별도 작업.
        var topCurrency = sumByCurrency.OrderByDescending(kv => kv.Value).First();
        var reward = new PkRewardResult
        {
            RewardTag = CurrencyTypeIdToItemTag(topCurrency.Key),
            Count = (int)topCurrency.Value,
            RewardType = "Item",
            ItemKind = "Currency",
        };

        var response = new PkQuestClaimDailyBundleResponse
        {
            Result = ErrorCode.None,
            Reward = reward,
        };
        await _currencyService.PopulateCurrenciesAsync(response, uid);
        return response;
    }

    /// GdbConst.Quest.DailyMissionRewards JSON 배열 파싱 → currency_type별 sum.
    /// 빈 문자열/파싱 실패/currency 매칭 실패 시 빈 Dictionary 반환 — 호출자가 fail-fast 분기.
    private Dictionary<int, long> AccumulateDailyBundleRewards(long uid)
    {
        var result = new Dictionary<int, long>();
        string rewardsJson = _gameDataManager.GetConstString(
            GdbConst.Quest.Category,
            GdbConst.Quest.DailyMissionRewards,
            defaultValue: "");
        if (string.IsNullOrEmpty(rewardsJson))
        {
            return result;
        }

        List<DailyBundleRewardLineDto>? lines;
        try
        {
            lines = JsonSerializer.Deserialize<List<DailyBundleRewardLineDto>>(rewardsJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"[Quest] dailyMissionRewards JSON 파싱 실패 — Constants.json 직렬화 결함: {ex.Message}");
        }
        if (lines == null)
        {
            return result;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line == null || line.amount <= 0 || string.IsNullOrEmpty(line.item))
            {
                continue;
            }
            string currencyName = RewardResolver.ResolveCurrencyType(_gameDataManager, line.item);
            if (string.IsNullOrEmpty(currencyName))
            {
                continue;
            }
            int currencyTypeId = ParseCurrencyType(currencyName);
            if (currencyTypeId < 0)
            {
                continue;
            }
            if (result.TryGetValue(currencyTypeId, out var prev))
            {
                result[currencyTypeId] = prev + line.amount;
            }
            else
            {
                result[currencyTypeId] = line.amount;
            }
        }
        return result;
    }

    /// CurrencyType.Diamond/Gold → Item tag로 역매핑. UI 연출(CoinFlyStep)이 RewardTag로 sprite/flyTarget 분기.
    private static string CurrencyTypeIdToItemTag(int currencyTypeId)
    {
        return currencyTypeId switch
        {
            CurrencyType.Diamond => "Tag.Item.Currency_Diamond",
            CurrencyType.Gold => "Tag.Item.Currency_Gold",
            _ => "",
        };
    }

    /// 클라 QuestRewardLine 직렬화 형태 — GameDataUploader.FlattenObject가 snake_case 변환 X (이미 lowercase 필드).
    private class DailyBundleRewardLineDto
    {
        public string item { get; set; } = "";
        public int amount { get; set; }
    }

    /// DB row → DTO 변환.
    public static PkQuestInstanceDto ToDto(GameUserQuestInstance inst)
    {
        return new PkQuestInstanceDto
        {
            InstanceId = inst.instanceId,
            QuestDataId = inst.questDataId,
            ContainerStableId = inst.containerStableId,
            SubProgress = ParseSubProgress(inst.subProgressJson),
            Status = inst.status,
            IssuedAtUtc = inst.issuedAtUtc,
            ExpiresAtUtc = inst.expiresAtUtc,
        };
    }

    private static List<PkSubProgressEntry> ParseSubProgress(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<PkSubProgressEntry>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<PkSubProgressEntry>>(json) ?? new List<PkSubProgressEntry>();
        }
        catch
        {
            return new List<PkSubProgressEntry>();
        }
    }

    private static int ParseCurrencyType(string s)
    {
        return s?.ToLowerInvariant() switch
        {
            "diamond" => CurrencyType.Diamond,
            "gold" => CurrencyType.Gold,
            _ => -1,
        };
    }

    /// cheat 전용 — 활성 InProgress Daily 슬롯의 progress 가득 채워 Completed 전환.
    /// slotIndex: 1~N = issuedAtUtc 정렬 기준 N번째 / -1 = 전체.
    /// 발견 못한 인덱스(out-of-range)는 빈 응답 — 호출자가 무시 가능.
    public async Task<List<PkQuestInstanceDto>> CheatCompleteDailyAsync(long uid, int slotIndex)
    {
        var instances = await _gameDB.GetActiveQuestInstancesByUidAsync(uid);
        var dailyInProgress = instances
            .Where(i => i.containerStableId == QuestServerConstants.QuestContainerDailyStableId
                && i.status == "InProgress")
            .OrderBy(i => i.issuedAtUtc)
            .ToList();

        List<GameUserQuestInstance> targets;
        if (slotIndex == -1)
        {
            targets = dailyInProgress;
        }
        else
        {
            int zeroIdx = slotIndex - 1;
            if (zeroIdx < 0 || zeroIdx >= dailyInProgress.Count)
            {
                return new List<PkQuestInstanceDto>();
            }
            targets = new List<GameUserQuestInstance> { dailyInProgress[zeroIdx] };
        }
        if (targets.Count == 0)
        {
            return new List<PkQuestInstanceDto>();
        }

        var quests = _gameDataManager.GetList<GdbQuestData>();
        var nowStr = FormatUtc(_clock.UtcNow);
        foreach (var inst in targets)
        {
            var quest = quests?.FirstOrDefault(q => q.id == inst.questDataId);
            int required = quest != null && quest.required_count > 0 ? quest.required_count : 1;
            inst.subProgressJson = JsonSerializer.Serialize(new List<PkSubProgressEntry>
            {
                new() { ChildIndex = 0, Progress = required, Required = required },
            });
            inst.status = "Completed";
            inst.lastUpdatedUtc = nowStr;
        }
        await _gameDB.UpdateQuestInstancesRangeAsync(targets);
        return targets.Select(ToDto).ToList();
    }

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyy-MM-dd HH:mm:ss");
}
