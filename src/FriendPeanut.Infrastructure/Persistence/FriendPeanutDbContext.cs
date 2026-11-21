using Microsoft.EntityFrameworkCore;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class FriendPeanutDbContext : DbContext
{
    public FriendPeanutDbContext(
        DbContextOptions<FriendPeanutDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("friend_peanut");

        modelBuilder.Entity<DocumentRecord>(entity =>
        {
            entity.ToTable("documents");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(40);

            entity.Property(x => x.Version)
                .HasColumnName("version");
        });

        base.OnModelCreating(modelBuilder);
    }
}
