using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Infrastructure.Data {
    public class DbContext : IdentityDbContext {
        public DbContext(DbContextOptions<DbContext> options) : base(options) {
        }

        public DbSet<AuctionHouse> AuctionHouse => Set<AuctionHouse>();
        public DbSet<Lot> Lot => Set<Lot>();

        protected override void OnModelCreating(ModelBuilder builder) {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(typeof(DbContext).Assembly);
        }
    }
}
