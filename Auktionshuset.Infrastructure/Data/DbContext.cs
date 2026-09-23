using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Infrastructure.Data {
    public class DbContext : IdentityDbContext {
        /// <summary>
        /// Initializes a new instance of the <see cref="DbContext"/> with the supplied options.
        /// </summary>
        /// <param name="options">The options to be used by this <see cref="DbContext"/>.</param>
        public DbContext(DbContextOptions<DbContext> options) : base(options) {
        }

        public DbSet<AuctionHouse> AuctionHouse => Set<AuctionHouse>();
        public DbSet<Lot> Lot => Set<Lot>();
        public DbSet<Auction> Auction => Set<Auction>();
        public DbSet<AuctionLot> AuctionLot => Set<AuctionLot>();
        public DbSet<Employee> Employee => Set<Employee>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        /// <summary>
        /// Applies every <c>IEntityTypeConfiguration</c> declared in the infrastructure assembly.
        /// </summary>
        /// <param name="builder">The builder used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder builder) 
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(typeof(DbContext).Assembly);
        }
    }
}
