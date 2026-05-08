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
    [HttpPost("Active")]
    public async Task<PkQuestActiveResponse> Active([FromHeader] HeaderDTO header)
    {
        var response = new PkQuestActiveResponse();
        MdbUserData userInfo = (MdbUserData)HttpContext.Items[nameof(MdbUserData)]!;
        long uid = userInfo.UId;
        _logger.ZLogInformation($"[Quest/Active] Uid:{uid}");

        response.Instances = await _questService.GetActiveAsync(uid);
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
