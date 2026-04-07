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

    public GachaService(GameDbContext context, IGameDB gameDB, CurrencyService currencyService, GameDataManager gameDataManager)
    {
        _context = context;
        _gameDB = gameDB;
        _currencyService = currencyService;
        _gameDataManager = gameDataManager;
    }

    /// 뽑기 실행. pullCount만큼 장비를 뽑아서 반환.
    public async Task<(ErrorCode error, List<PkGachaResultItem> items)> PullAsync(long uid, int pullCount, bool useTicket)
    {
        if (pullCount <= 0 || (pullCount != 1 && pullCount != 10))
        {
            return (ErrorCode.GachaInvalidPullCount, new());
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. 재화 검증 및 차감
            if (useTicket)
            {
                var ticketError = await _currencyService.DeductAsync(uid, GdbConst.CurrencyType.GachaTicket, pullCount);
                if (ticketError != ErrorCode.None)
                {
                    return (ErrorCode.GachaInsufficientCurrency, new());
                }
            }
            else
            {
                int singleCost = _gameDataManager.GetConstInt(GdbConst.Gacha.Category, GdbConst.Gacha.SingleCostDiamond, 300);
                long totalCost = (long)singleCost * pullCount;

                var diamondError = await _currencyService.DeductAsync(uid, GdbConst.CurrencyType.Diamond, totalCost);
                if (diamondError != ErrorCode.None)
                {
                    return (ErrorCode.GachaInsufficientCurrency, new());
                }
            }

            // 2. 등급별 가중치 로드
            var gradeWeights = LoadGradeWeights();

            // 3. 장비 풀 로드
            var equipments = _gameDataManager.GetList<GdbEquipmentData>();
            if (equipments == null || equipments.Count == 0)
            {
                await transaction.RollbackAsync();
                return (ErrorCode.GachaPullFailed, new());
            }

            // 4. 뽑기 실행 — 배치 삽입으로 N+1 쓰기 방지
            var results = new List<PkGachaResultItem>();
            var newEquipments = new List<GameUserEquipment>();
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            for (int i = 0; i < pullCount; i++)
            {
                // 가중치 랜덤으로 등급 결정
                int grade = RollGrade(gradeWeights);

                // 등급별 장비 풀에서 랜덤 선택
                var pool = equipments.Where(e => e.grade == grade).ToList();
                if (pool.Count == 0)
                {
                    // 해당 등급에 장비가 없으면 전체 풀에서 선택
                    pool = equipments;
                }

                var selected = pool[Random.Shared.Next(pool.Count)];

                newEquipments.Add(new GameUserEquipment
                {
                    uid = uid,
                    equipmentId = selected.id,
                    slot = selected.slot,
                    isEquipped = false,
                    equippedCharacterId = 0,
                    acquiredAt = now
                });

                results.Add(new PkGachaResultItem
                {
                    EquipmentId = selected.id,
                    Grade = selected.grade
                });
            }

            // 한 번의 SaveChanges로 배치 삽입
            await _gameDB.AddEquipmentBatchAsync(newEquipments);

            await transaction.CommitAsync();
            return (ErrorCode.None, results);
        }
        catch (Exception ex)
        {
            // TODO: ILogger 주입 후 로깅 추가
            _ = ex;
            await transaction.RollbackAsync();
            return (ErrorCode.GachaPullFailed, new());
        }
    }

    /// Constants에서 등급별 가중치 로드. 기본값: 1등급 5%, 2등급 15%, 3등급 40%, 4등급 40%
    private Dictionary<int, int> LoadGradeWeights()
    {
        var weights = new Dictionary<int, int>
        {
            { 1, _gameDataManager.GetConstInt(GdbConst.Gacha.Category, GdbConst.Gacha.WeightGrade1, 5) },
            { 2, _gameDataManager.GetConstInt(GdbConst.Gacha.Category, GdbConst.Gacha.WeightGrade2, 15) },
            { 3, _gameDataManager.GetConstInt(GdbConst.Gacha.Category, GdbConst.Gacha.WeightGrade3, 40) },
            { 4, _gameDataManager.GetConstInt(GdbConst.Gacha.Category, GdbConst.Gacha.WeightGrade4, 40) },
        };
        return weights;
    }

    /// 가중치 기반 등급 결정.
    private static int RollGrade(Dictionary<int, int> weights)
    {
        int totalWeight = weights.Values.Sum();
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

        // fallback: 최저 등급
        return weights.Keys.Max();
    }
}
