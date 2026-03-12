using HashChallenge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HashChallenge.Infrastructure.Data;

public sealed class HashDbContext : DbContext
{
    public HashDbContext(DbContextOptions<HashDbContext> options)
        : base(options)
    {
    }

    public DbSet<HashEntry> Hashes => Set<HashEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HashEntry>(entity =>
        {
            entity.ToTable("hashes");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Date)
                .HasColumnName("date")
                .IsRequired();

            entity.Property(e => e.Sha1)
                .HasColumnName("sha1")
                .HasColumnType("CHAR(40)")
                .IsRequired();

            entity.HasIndex(e => e.Date)
                .HasDatabaseName("IX_hashes_Date");
        });
    }
}
