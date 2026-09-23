using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Database
{
    public class AHDBContext : IdentityDbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AHDBContext"/> with the supplied options.
        /// </summary>
        /// <param name="options">The options to be used by this <see cref="AHDBContext"/>.</param>
        public AHDBContext(DbContextOptions<AHDBContext> options) : base(options)
        {
        }

        public DbSet<AuctionHouse> AuctionHouse => Set<AuctionHouse>();
        public DbSet<Lot> Lot => Set<Lot>();
        public DbSet<Auction> Auction => Set<Auction>();
        public DbSet<AuctionLot> AuctionLot => Set<AuctionLot>();
        public DbSet<Employee> Employee => Set<Employee>();

        /// <summary>
        /// Applies every <c>IEntityTypeConfiguration</c> declared in the infrastructure assembly.
        /// </summary>
        /// <param name="builder">The builder used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyConfigurationsFromAssembly(typeof(AHDBContext).Assembly);
        }
    }
}
