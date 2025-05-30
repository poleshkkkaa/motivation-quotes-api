using Microsoft.EntityFrameworkCore;
using MotivationQuotesAPI.Models;

namespace MotivationQuotesAPI.Models
{
    public class QuotesDbContext : DbContext
    {
        public QuotesDbContext(DbContextOptions<QuotesDbContext> options) : base(options) { }

        public DbSet<Quote> Quotes { get; set; } = null!;
        public DbSet<Favorite> Favorites { get; set; } = null!;
        public DbSet<SearchHistory> SearchHistories { get; set; } = null!;
        public DbSet<QuoteReaction> QuoteReactions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Налаштування зв'язку між Favorite та Quotes
            modelBuilder.Entity<Favorite>().HasOne(f => f.Quote).WithMany().HasForeignKey(f => f.QuoteId).OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
