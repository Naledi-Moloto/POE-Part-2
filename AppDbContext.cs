using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class AppDbContext : DbContext
{
    // The options (including the connection string) are passed in from Program.cs
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Each DbSet becomes one table
    public DbSet<Organiser> Organisers { get; set; }
    public DbSet<Participant> Participants { get; set; }
    public DbSet<Venue> Venues { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Enrolment> Enrolments { get; set; }
    public DbSet<Result> Results { get; set; }

    // Extra rules that match the Part 1 SQL script
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Organisers ----------
        modelBuilder.Entity<Organiser>(entity =>
        {
            entity.HasIndex(o => o.Email).IsUnique();                  // UNIQUE email
            entity.Property(o => o.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // ---------- Participants ----------
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasIndex(p => p.Email).IsUnique();                  // UNIQUE email
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // ---------- Events ----------
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint(
                "CHK_Events_Type", "[EventType] IN ('Run', 'Walk', 'Cycle')"));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");

            // Organiser -> Events (1 : many), no cascade delete
            entity.HasOne(e => e.Organiser)
                  .WithMany(o => o.Events)
                  .HasForeignKey(e => e.OrganiserID)
                  .OnDelete(DeleteBehavior.Restrict);

            // Venue -> Events (1 : many), no cascade delete
            entity.HasOne(e => e.Venue)
                  .WithMany(v => v.Events)
                  .HasForeignKey(e => e.VenueID)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Categories ----------
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.EntryFee).HasDefaultValue(0m);

            // Event -> Categories (1 : many). Deleting an event deletes its categories.
            entity.HasOne(c => c.Event)
                  .WithMany(e => e.Categories)
                  .HasForeignKey(c => c.EventID)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Enrolments ----------
        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint(
                "CHK_Enrolments_Status", "[Status] IN ('Enrolled', 'Cancelled')"));

            entity.Property(e => e.EnrolmentDate).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Status).HasDefaultValue("Enrolled");

            // A participant cannot enrol in the same category twice
            entity.HasIndex(e => new { e.ParticipantID, e.CategoryID }).IsUnique();

            entity.HasOne(e => e.Participant)
                  .WithMany(p => p.Enrolments)
                  .HasForeignKey(e => e.ParticipantID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Enrolments)
                  .HasForeignKey(e => e.CategoryID)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Results ----------
        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint(
                "CHK_Results_Status", "[Status] IN ('Finished', 'DNF', 'DNS')"));

            entity.Property(r => r.CapturedAt).HasDefaultValueSql("GETDATE()");

            // Enrolment -> Result is one-to-one (0..1). This also makes EnrolmentID unique.
            entity.HasOne(r => r.Enrolment)
                  .WithOne(e => e.Result)
                  .HasForeignKey<Result>(r => r.EnrolmentID)
                  .OnDelete(DeleteBehavior.Restrict);

            // Organiser -> Results (1 : many): who captured the result
            entity.HasOne(r => r.CapturedByOrganiser)
                  .WithMany(o => o.CapturedResults)
                  .HasForeignKey(r => r.CapturedByOrganiserID)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
