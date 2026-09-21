using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Qylent.Kutuphane.Core.Domain;

namespace Qylent.Kutuphane.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<LibraryProfile> LibraryProfiles => Set<LibraryProfile>();
    public DbSet<AdminCredential> AdminCredentials => Set<AdminCredential>();
    public DbSet<SecurityAnswer> SecurityAnswers => Set<SecurityAnswer>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberFieldDefinition> MemberFieldDefinitions => Set<MemberFieldDefinition>();
    public DbSet<MemberFieldValue> MemberFieldValues => Set<MemberFieldValue>();
    public DbSet<BookTitle> BookTitles => Set<BookTitle>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<LoanRule> LoanRules => Set<LoanRule>();
    public DbSet<ReportPreset> ReportPresets => Set<ReportPreset>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();
    public DbSet<SchemaInfo> SchemaInfo => Set<SchemaInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite has no native DateTimeOffset type. UTC ticks preserve ordering and
        // comparisons, which are critical for queues, due dates and report periods.
        var utcTicksConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero));
        var nullableUtcTicksConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.UtcTicks : null,
            value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(utcTicksConverter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableUtcTicksConverter);
            }
        }

        modelBuilder.Entity<LibraryProfile>().HasIndex(x => x.SetupCompleted);
        modelBuilder.Entity<Operator>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Member>().HasIndex(x => x.MemberNumber).IsUnique();
        modelBuilder.Entity<Member>().HasIndex(x => x.FullName);
        modelBuilder.Entity<BookTitle>().HasIndex(x => x.Title);
        modelBuilder.Entity<BookTitle>().HasIndex(x => x.Isbn);
        modelBuilder.Entity<BookCopy>().HasIndex(x => x.Barcode).IsUnique();
        modelBuilder.Entity<Loan>().HasIndex(x => new { x.BookCopyId, x.Status });
        modelBuilder.Entity<Loan>().HasIndex(x => new { x.MemberId, x.Status });
        modelBuilder.Entity<Reservation>().HasIndex(x => new { x.BookTitleId, x.Status, x.RequestedAtUtc });
        modelBuilder.Entity<MemberFieldValue>().HasIndex(x => new { x.MemberId, x.DefinitionId }).IsUnique();
        modelBuilder.Entity<SchemaInfo>().HasKey(x => x.Id);

        modelBuilder.Entity<MemberFieldValue>()
            .HasOne(x => x.Member).WithMany(x => x.CustomFieldValues)
            .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MemberFieldValue>()
            .HasOne(x => x.Definition).WithMany(x => x.Values)
            .HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BookCopy>()
            .HasOne(x => x.BookTitle).WithMany(x => x.Copies)
            .HasForeignKey(x => x.BookTitleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Loan>()
            .HasOne(x => x.Member).WithMany(x => x.Loans)
            .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Loan>()
            .HasOne(x => x.BookCopy).WithMany(x => x.Loans)
            .HasForeignKey(x => x.BookCopyId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Reservation>()
            .HasOne(x => x.Member).WithMany(x => x.Reservations)
            .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Reservation>()
            .HasOne(x => x.BookTitle).WithMany(x => x.Reservations)
            .HasForeignKey(x => x.BookTitleId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in modelBuilder.Model.GetEntityTypes()
                     .Where(x => typeof(EntityBase).IsAssignableFrom(x.ClrType)))
        {
            modelBuilder.Entity(entity.ClrType).Property(nameof(EntityBase.CreatedAtUtc)).IsRequired();
            modelBuilder.Entity(entity.ClrType).Property(nameof(EntityBase.UpdatedAtUtc)).IsRequired();
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardAuditEntries();
        TouchUpdatedEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardAuditEntries();
        TouchUpdatedEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardAuditEntries()
    {
        if (ChangeTracker.Entries<AuditEntry>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("İşlem günlüğü kayıtları değiştirilemez veya silinemez.");
        }
    }

    private void TouchUpdatedEntities()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
