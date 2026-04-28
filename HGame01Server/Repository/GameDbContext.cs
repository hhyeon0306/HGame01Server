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
    }
}
