using Microsoft.EntityFrameworkCore;
using HGame01Server.Models;

namespace HGame01Server.Repository;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<GameUser> Users { get; set; }
    public DbSet<GameUserCharacter> UserCharacters { get; set; }
    public DbSet<GameUserCurrency> UserCurrencies { get; set; }
    public DbSet<GameUserEquipment> UserEquipments { get; set; }
    public DbSet<GameUserShopPurchase> UserShopPurchases { get; set; }
    public DbSet<GameUserMail> UserMails { get; set; }
    public DbSet<GameUserQuestInstance> UserQuestInstances { get; set; }
    public DbSet<GameUserQuestEventApplied> UserQuestEventsApplied { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // user_currencies: (uid, currencyType) 복합 인덱스
        modelBuilder.Entity<GameUserCurrency>()
            .HasIndex(c => new { c.uid, c.currencyType })
            .IsUnique()
            .HasDatabaseName("IX_user_currencies_uid_currencyType");

        // user_equipments: (uid) 인덱스
        modelBuilder.Entity<GameUserEquipment>()
            .HasIndex(e => e.uid)
            .HasDatabaseName("IX_user_equipments_uid");

        // user_shop_purchases: (uid, shopItemId) 복합 인덱스
        modelBuilder.Entity<GameUserShopPurchase>()
            .HasIndex(p => new { p.uid, p.shopItemId })
            .HasDatabaseName("IX_user_shop_purchases_uid_shopItemId");

        // user_mails: (uid, claimedAt) 복합 인덱스 — 미수령 조회 + claim 빠른 검색
        modelBuilder.Entity<GameUserMail>()
            .HasIndex(m => new { m.uid, m.claimedAt })
            .HasDatabaseName("IX_user_mails_uid_claimedAt");

        // user_mails: expireAt 단일 인덱스 — lazy 만료 청소 쿼리용
        modelBuilder.Entity<GameUserMail>()
            .HasIndex(m => m.expireAt)
            .HasDatabaseName("IX_user_mails_expireAt");

        // user_quest_instances: (uid, containerStableId, status) 복합 인덱스 — Active 필터 + Daily 회전 검색 가속.
        modelBuilder.Entity<GameUserQuestInstance>()
            .HasIndex(q => new { q.uid, q.containerStableId, q.status })
            .HasDatabaseName("IX_user_quest_instances_uid_container_status");

        // user_quest_instances: (uid, questDataId, status) — 같은 quest 활성 row 중복 검색 가속.
        modelBuilder.Entity<GameUserQuestInstance>()
            .HasIndex(q => new { q.uid, q.questDataId, q.status })
            .HasDatabaseName("IX_user_quest_instances_uid_questData_status");

        // user_quest_events_applied: (uid, questInstanceId, eventClientId) unique — 설계 의도 정합 멱등 dedup.
        modelBuilder.Entity<GameUserQuestEventApplied>()
            .HasIndex(e => new { e.uid, e.questInstanceId, e.eventClientId })
            .IsUnique()
            .HasDatabaseName("IX_user_quest_events_applied_uid_instance_clientId");

        // user_quest_events_applied: appliedAtUtc 인덱스 — 7일 GC 쿼리용
        modelBuilder.Entity<GameUserQuestEventApplied>()
            .HasIndex(e => e.appliedAtUtc)
            .HasDatabaseName("IX_user_quest_events_applied_appliedAtUtc");
    }
}
