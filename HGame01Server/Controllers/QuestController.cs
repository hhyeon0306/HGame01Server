using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;
using HGame01Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZLogger;

namespace HGame01Server.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QuestController : ControllerBase
{
    private readonly ILogger<QuestController> _logger;
    private readonly QuestService _questService;
    private readonly QuestProgressService _questProgress;
    private readonly GameDataManager _gameDataManager;

    public QuestController(ILogger<QuestController> logger, QuestService questService, QuestProgressService questProgress, GameDataManager gameDataManager)
    {
        _logger = logger;
        _questService = questService;
        _questProgress = questProgress;
        _gameDataManager = gameDataManager;
    }

    /// 활성 quest 인스턴스 조회 — Hydrate / Reconcile.
    /// 신규 유저 또는 모든 Daily 슬롯이 Expired인 경우 자동 RefreshDaily 트리거 — 서버 권위 진입점 단일화.
    /// 활성 InProgress/Completed Daily 슬롯이 하나라도 있으면 자동 발급 안 함 (기존 진행 보존).
    /// 자동 RefreshDaily 후에는 GetActiveAsync 재호출로 응답을 권위 상태로 재구성 — stale row 섞임 차단.
    [HttpPost("Active")]
    public async Task<PkQuestActiveResponse> Active([FromHeader] HeaderDTO header)
    {
        var response = new PkQuestActiveResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/Active] Uid:{uid}");

        var instances = await _questService.GetActiveAsync(uid);

        bool hasActiveDaily = instances.Any(i =>
            i.ContainerStableId == QuestServerConstants.QuestContainerDailyStableId
            && (i.Status == "InProgress" || i.Status == "Completed"));
        if (!hasActiveDaily)
        {
            var dailyIds = CollectDailyQuestDataIds();
            if (dailyIds.Count > 0)
            {
                _logger.ZLogInformation($"[Quest/Active] Uid:{uid} 활성 Daily 슬롯 0건 — 자동 RefreshDaily 트리거 (slotCount:{QuestServerConstants.DailySlotCount}).");
                await _questService.RefreshDailyAsync(
                    uid,
                    QuestServerConstants.QuestContainerDailyStableId,
                    QuestServerConstants.DailySlotCount,
                    dailyIds);
                // 재호출로 권위 응답 재구성 — RefreshDaily가 만든 신규 InProgress + 기존 Completed(미수령) 모두 포함.
                instances = await _questService.GetActiveAsync(uid);
            }
            else
            {
                _logger.ZLogWarning($"[Quest/Active] Uid:{uid} Daily 풀 비어있음 — GdbQuestData 중 quest_tag prefix 'Tag.Quest.Daily.' 0건. 자동 발급 skip.");
            }
        }

        response.Instances = instances;
        response.Result = ErrorCode.None;
        return response;
    }

    /// 클라 이벤트 배치 적용 — 멱등 dedup + progress 누적.
    [HttpPost("EventsBatch")]
    public async Task<PkQuestEventBatchResponse> EventsBatch([FromHeader] HeaderDTO header, [FromBody] PkQuestEventBatchRequest request)
    {
        var response = new PkQuestEventBatchResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/EventsBatch] Uid:{uid} Events:{request.Events?.Count ?? 0}");

        var (applied, duplicate, updated) = await _questProgress.ApplyBatchAsync(uid, request.Events ?? new());
        response.Applied = applied;
        response.Duplicate = duplicate;
        response.UpdatedInstances = updated.Select(QuestService.ToDto).ToList();
        response.Result = ErrorCode.None;
        return response;
    }

    /// 보상 수령 — Completed → Claimed + currencies 응답.
    [HttpPost("Claim")]
    public async Task<PkQuestClaimResponse> Claim([FromHeader] HeaderDTO header, [FromBody] PkQuestClaimRequest request)
    {
        var response = new PkQuestClaimResponse { InstanceId = request.InstanceId };
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/Claim] Uid:{uid} InstanceId:{request.InstanceId}");

        var (error, reward, currencies) = await _questService.ClaimAsync(uid, request.InstanceId);
        response.Result = error;
        response.Reward = reward;
        response.Currencies = currencies;
        return response;
    }

    /// 일일 종합 보상 수령 — 4 일일 퀘스트 모두 수령 후 일괄 보너스(Diamond 450).
    /// 라운드 D 1차: 일일 1회 제한 미구현 (클라 session memory만 가드). 라운드 D 2차에 schema 추가.
    [HttpPost("ClaimDailyBundle")]
    public async Task<PkQuestClaimDailyBundleResponse> ClaimDailyBundle([FromHeader] HeaderDTO header, [FromBody] PkQuestClaimDailyBundleRequest request)
    {
        var response = new PkQuestClaimDailyBundleResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/ClaimDailyBundle] Uid:{uid}");

        var (error, reward, currencies) = await _questService.ClaimDailyBundleAsync(uid);
        response.Result = error;
        response.Reward = reward;
        response.Currencies = currencies;
        return response;
    }

    /// 일일 슬롯 강제 재발급 — 자정 통과 시 클라가 호출.
    /// 풀: GdbQuestData에서 quest_tag prefix "Tag.Quest.Daily." 자동 필터 (디자이너 별도 풀 등록 의무 없음 — Quest tag 카테고리만 맞추면 자동 편입).
    /// 슬롯 수: DAILY_SLOT_COUNT 상수 (4). 향후 GdbConst.Quest.DailySlotCount로 외부화.
    [HttpPost("RefreshDaily")]
    public async Task<PkQuestRefreshResponse> RefreshDaily([FromHeader] HeaderDTO header, [FromBody] PkQuestRefreshRequest request)
    {
        var response = new PkQuestRefreshResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/RefreshDaily] Uid:{uid} ContainerStableId:{request.ContainerStableId}");

        var dailyIds = CollectDailyQuestDataIds();
        if (dailyIds.Count == 0)
        {
            _logger.ZLogWarning($"[Quest/RefreshDaily] Daily 풀 비어있음 — GdbQuestData 중 quest_tag prefix 'Tag.Quest.Daily.' 0건. 빈 응답 반환.");
            response.Instances = new();
            response.Result = ErrorCode.None;
            return response;
        }

        var instances = await _questService.RefreshDailyAsync(uid, request.ContainerStableId, DAILY_SLOT_COUNT, dailyIds);
        response.Instances = instances;
        response.Result = ErrorCode.None;
        return response;
    }

    private const string DAILY_TAG_PREFIX = "Tag.Quest.Daily.";
    private const int DAILY_SLOT_COUNT = 4;

    /// GdbQuestData 풀에서 Daily prefix 매칭 questDataId 수집.
    /// 디자이너 별도 풀 등록 의무 없음 — Quest tag 카테고리만 맞추면 자동 편입 (mock 분기와 동일 패턴).
    private List<int> CollectDailyQuestDataIds()
    {
        var quests = _gameDataManager.GetList<GdbQuestData>();
        if (quests == null || quests.Count == 0)
        {
            return new List<int>();
        }
        var ids = new List<int>();
        for (int i = 0; i < quests.Count; i++)
        {
            var q = quests[i];
            if (q == null || string.IsNullOrEmpty(q.quest_tag))
            {
                continue;
            }
            if (q.quest_tag.StartsWith(DAILY_TAG_PREFIX))
            {
                ids.Add(q.id);
            }
        }
        return ids;
    }
}
