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
    private readonly GameDataManager _gameDataManager;
    private readonly ILogger<GachaService> _logger;

    public GachaService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, GameDataManager gameDataManager, ILogger<GachaService> logger)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _gameDataManager = gameDataManager;
        _logger = logger;
    }

    /// 뽑기 실행. pullCount만큼 장비를 뽑아서 반환.
    public async Task<(ErrorCode error, List<PkGachaResultItem> items, List<PkUserEquipment> equipments)> PullAsync(long uid, int pullCount)
    {
        if (pullCount != 1 && pullCount != 10)
        {
            return (ErrorCode.GachaInvalidPullCount, new(), new());
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
                return (ErrorCode.GachaInsufficientCurrency, new(), new());
            }

            // 3. 등급별 가중치 로드 + 합계 방어
            var gradeWeights = LoadGradeWeights();
            int totalWeight = gradeWeights.Values.Sum();
            if (totalWeight <= 0)
            {
                _logger.LogError("[GachaService] 등급 가중치 합이 0 이하입니다. GameData 업로드 상태를 확인하세요.");
                await transaction.RollbackAsync();
                return (ErrorCode.GachaPullFailed, new(), new());
            }

            // 4. 장비 풀 로드 — GameData 미업로드 시 더미 풀로 fallback (로컬 테스트 전용)
            var equipments = _gameDataManager.GetList<GdbEquipmentData>();
            if (equipments == null || equipments.Count == 0)
            {
                equipments = BuildDummyEquipmentPool();
                _logger.LogWarning("[GachaService] EquipmentData가 비어 있어 더미 풀({Count}개)로 진행합니다. Admin/UploadGameData 이후 더미 fallback이 비활성화됩니다.", equipments.Count);
            }

            // 5. 뽑기 실행 — 배치 삽입으로 N+1 쓰기 방지
            var results = new List<PkGachaResultItem>();
            var newEquipments = new List<GameUserEquipment>();
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            for (int i = 0; i < pullCount; i++)
            {
                int grade = RollGrade(gradeWeights, totalWeight);

                var pool = equipments.Where(e => e.grade == grade).ToList();
                if (pool.Count == 0)
                {
                    pool = equipments;
                }

                var selected = pool[Random.Shared.Next(pool.Count)];

                newEquipments.Add(new GameUserEquipment
                {
                    uid = uid,
                    equipmentId = selected.id,
                    slot = selected.slot,
                    isEquipped = false,
                    equippedCharacterTag = "",
                    acquiredAt = now
                });

                results.Add(new PkGachaResultItem
                {
                    EquipmentId = selected.id,
                    Grade = selected.grade
                });
            }

            // 한 번의 SaveChanges로 배치 삽입 — EF Core가 newEquipments[i].id를 자동 채움
            await _gameDB.AddEquipmentBatchAsync(newEquipments);

            await transaction.CommitAsync();

            _logger.LogInformation("[GachaService] Uid:{Uid} PullCount:{PullCount} Cost:{Cost} Grades:[{Grades}]",
                uid, pullCount, totalCost, string.Join(",", results.Select(r => r.Grade)));

            // 신규 장비를 클라이언트가 바로 Store에 반영할 수 있도록 패킷 매핑
            var newEquipmentPackets = EquipmentService.MapToPacket(newEquipments);
            return (ErrorCode.None, results, newEquipmentPackets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GachaService] 가챠 처리 중 예외 발생. Uid:{Uid}", uid);
            await transaction.RollbackAsync();
            return (ErrorCode.GachaPullFailed, new(), new());
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
        for (int grade = 1; grade <= 4; grade++)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                pool.Add(new GdbEquipmentData
                {
                    id = id++,
                    name = $"Dummy_G{grade}_S{slot}",
                    grade = grade,
                    slot = slot,
                });
            }
        }
        return pool;
    }
}
