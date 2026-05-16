using HGame01Server.Models;
using HGame01Server.Models.GameData;
using HGame01Server.Repository;

namespace HGame01Server.Services;

/// 뽑기 비즈니스 로직.
public class GachaService
{
    private readonly GameDbContext _context;
    private readonly IGameDB _gameDB;
    private readonly CurrencyService _currencyService;
    private readonly EquipmentStorageService _storageService;
    private readonly GameDataManager _gameDataManager;
    private readonly ILogger<GachaService> _logger;

    public GachaService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, EquipmentStorageService storageService, GameDataManager gameDataManager, ILogger<GachaService> logger)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _storageService = storageService;
        _gameDataManager = gameDataManager;
        _logger = logger;
    }

    /// 뽑기 실행. pullCount만큼 장비를 뽑아 완성된 응답 DTO를 반환.
    public async Task<PkGachaPullResponse> PullAsync(long uid, int pullCount)
    {
        if (pullCount != 1 && pullCount != 10)
        {
            return new PkGachaPullResponse { Result = ErrorCode.GachaInvalidPullCount };
        }

        // 장비 보관함 풀 상태 체크 — 트랜잭션/재화 차감 전에 차단 (확장 유도 팝업 흐름).
        // "받을 땐 통과, 다음 시도부터 차단" 정책: 현재 count >= capacity 면 진입 거부.
        if (await _storageService.IsFullAsync(uid))
        {
            return new PkGachaPullResponse { Result = ErrorCode.EquipmentStorageFull };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. 비용 계산 — 1뽑/10뽑 각각 클라 GachaConstantsData와 동일한 키 사용
            long totalCost = pullCount == 1
                ? _gameDataManager.GetConstInt(GdbGachaConst.Category, GdbGachaConst.SingleCostDiamond, 300)
                : _gameDataManager.GetConstInt(GdbGachaConst.Category, GdbGachaConst.MultiCostDiamond, 2700);

            // 2. 재화 차감 (조건부 원자 UPDATE)
            var diamondError = await _currencyService.DeductAsync(uid, CurrencyType.Diamond, totalCost);
            if (diamondError != ErrorCode.None)
            {
                return new PkGachaPullResponse { Result = ErrorCode.GachaInsufficientCurrency };
            }

            // 3. 등급별 가중치 로드 + 합계 방어
            var gradeWeights = LoadGradeWeights();
            int totalWeight = gradeWeights.Values.Sum();
            if (totalWeight <= 0)
            {
                _logger.LogError("[GachaService] 등급 가중치 합이 0 이하입니다. GameData 업로드 상태를 확인하세요.");
                await transaction.RollbackAsync();
                return new PkGachaPullResponse { Result = ErrorCode.GachaPullFailed };
            }

            // 4. 장비 풀 로드 — GameData 미업로드 시 더미 풀로 fallback (로컬 테스트 전용)
            var equipments = _gameDataManager.GetList<GdbEquipmentData>();
            if (equipments == null || equipments.Count == 0)
            {
                equipments = BuildDummyEquipmentPool();
                _logger.LogWarning("[GachaService] EquipmentData가 비어 있어 더미 풀({Count}개)로 진행합니다. Admin/UploadGameData 이후 더미 fallback이 비활성화됩니다.", equipments.Count);
            }

            // 5. 뽑기 실행 — 배치 삽입으로 N+1 쓰기 방지.
            //
            // ⚠ 분기 통합 검토 시점:
            //   - Equipment 인스턴스 발급(AddEquipmentBatchAsync) 은 Shop/Mail 의 Currency 지급과
            //     같은 보상 도메인이지만 grant 분기가 가챠에 박혀 있다.
            //   - 새 RewardType (Pet/Treasure) 추가나 가챠로 Equipment 외 보상이 풀리는 시점에는
            //     아래 인스턴스 조립을 RewardGrantService.GrantAsync(uid, PkRewardResult) 로 추출.
            //   - PkRewardResult 인터페이스는 이미 결정되어 있어 통합 비용은 분기 이전만큼.
            var results = new List<PkGachaResultItem>();
            var newEquipments = new List<GameUserEquipment>();
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            for (int i = 0; i < pullCount; i++)
            {
                int grade = RollGrade(gradeWeights, totalWeight);
                string gradeName = ((EAbilityGrade)grade).ToString();

                var pool = equipments.Where(e => e.grade == gradeName).ToList();
                if (pool.Count == 0)
                {
                    pool = equipments;
                }

                var selected = pool[Random.Shared.Next(pool.Count)];

                int selectedSlot = Enum.TryParse<EEquipmentSlot>(selected.slot, out var s) ? (int)s : 0;
                int selectedGrade = Enum.TryParse<EAbilityGrade>(selected.grade, out var g) ? (int)g : 0;

                newEquipments.Add(new GameUserEquipment
                {
                    uid = uid,
                    equipmentTag = selected.tag,
                    slot = selectedSlot,
                    isEquipped = false,
                    equippedCharacterTag = "",
                    acquiredAt = now
                });

                results.Add(new PkGachaResultItem
                {
                    Reward = new PkRewardResult
                    {
                        RewardType = "Equipment",
                        RewardTag = selected.tag,
                        Count = 1,
                    },
                    Grade = selectedGrade,
                });
            }

            // 한 번의 SaveChanges로 배치 삽입 — EF Core가 newEquipments[i].id를 자동 채움
            await _gameDB.AddEquipmentBatchAsync(newEquipments);

            await transaction.CommitAsync();

            _logger.LogInformation("[GachaService] Uid:{Uid} PullCount:{PullCount} Cost:{Cost} Grades:[{Grades}]",
                uid, pullCount, totalCost, string.Join(",", results.Select(r => r.Grade)));

            // 응답 DTO 조립 — 신규 장비를 클라이언트가 바로 Store에 반영할 수 있도록 패킷 매핑
            var response = new PkGachaPullResponse
            {
                Result = ErrorCode.None,
                Items = results,
                Equipments = EquipmentService.MapToPacket(newEquipments),
            };

            // 재화는 PopulateCurrenciesAsync 단일 경로로 채움 (currency-contract)
            await _currencyService.PopulateCurrenciesAsync(response, uid);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GachaService] 가챠 처리 중 예외 발생. Uid:{Uid}", uid);
            await transaction.RollbackAsync();
            return new PkGachaPullResponse { Result = ErrorCode.GachaPullFailed };
        }
    }

    /// Constants에서 등급별 가중치 로드. 기본값: 1등급 5%, 2등급 15%, 3등급 40%, 4등급 40%
    private Dictionary<int, int> LoadGradeWeights()
    {
        return new Dictionary<int, int>
        {
            { 1, _gameDataManager.GetConstInt(GdbGachaConst.Category, GdbGachaConst.WeightGrade1, 5) },
            { 2, _gameDataManager.GetConstInt(GdbGachaConst.Category, GdbGachaConst.WeightGrade2, 15) },
            { 3, _gameDataManager.GetConstInt(GdbGachaConst.Category, GdbGachaConst.WeightGrade3, 40) },
            { 4, _gameDataManager.GetConstInt(GdbGachaConst.Category, GdbGachaConst.WeightGrade4, 40) },
        };
    }

    /// 가중치 기반 등급 결정. totalWeight 사전 계산값을 받아 매 호출 Sum 비용 제거.
    private static int RollGrade(Dictionary<int, int> weights, int totalWeight)
    {
        int roll = Random.Shared.Next(totalWeight);
        int cumulative = 0;
        foreach (var (grade, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return grade;
            }
        }
        return weights.Keys.Max();
    }

    /// EquipmentData SO가 업로드되지 않은 환경에서 가챠 흐름을 끝까지 검증할 수 있도록 제공하는 더미 풀.
    /// 4 등급 × 2 슬롯 = 8종. 실제 데이터 업로드 후에는 호출되지 않는다.
    private static List<GdbEquipmentData> BuildDummyEquipmentPool()
    {
        var pool = new List<GdbEquipmentData>();
        int id = 90001;
        for (int gradeValue = 1; gradeValue <= 4; gradeValue++)
        {
            string gradeName = ((EAbilityGrade)gradeValue).ToString();
            foreach (EEquipmentSlot slot in Enum.GetValues<EEquipmentSlot>())
            {
                pool.Add(new GdbEquipmentData
                {
                    id = id++,
                    tag = $"Tag.Equipment.Dummy.{gradeName}.{slot}",
                    grade = gradeName,
                    slot = slot.ToString(),
                });
            }
        }
        return pool;
    }
}
