using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Models;

namespace VocaLearn.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();
    public DbSet<LearningProgress> LearningProgresses => Set<LearningProgress>();
    public DbSet<QuizResult> QuizResults => Set<QuizResult>();
    public DbSet<QuizDetail> QuizDetails => Set<QuizDetail>();
    public DbSet<ListeningLesson> ListeningLessons => Set<ListeningLesson>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Category relations
        builder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Categories)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => new { c.UserId, c.Name });
        });

        // Vocabulary relations
        builder.Entity<Vocabulary>(entity =>
        {
            entity.HasOne(v => v.User)
                  .WithMany(u => u.Vocabularies)
                  .HasForeignKey(v => v.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.Category)
                  .WithMany(c => c.Vocabularies)
                  .HasForeignKey(v => v.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(v => new { v.UserId, v.Word });
            entity.HasIndex(v => new { v.UserId, v.Status });
        });

        // LearningProgress relations
        builder.Entity<LearningProgress>(entity =>
        {
            entity.HasOne(p => p.User)
                  .WithMany(u => u.LearningProgresses)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.UserId, p.Date });
        });

        // QuizResult relations
        builder.Entity<QuizResult>(entity =>
        {
            entity.HasOne(q => q.User)
                  .WithMany(u => u.QuizResults)
                  .HasForeignKey(q => q.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(q => q.Category)
                  .WithMany()
                  .HasForeignKey(q => q.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(q => new { q.UserId, q.CompletedAt });
        });

        // QuizDetail relations
        builder.Entity<QuizDetail>(entity =>
        {
            entity.HasOne(qd => qd.QuizResult)
                  .WithMany(qr => qr.Details)
                  .HasForeignKey(qd => qd.QuizResultId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(qd => qd.Vocabulary)
                  .WithMany()
                  .HasForeignKey(qd => qd.VocabularyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ListeningLesson>(entity =>
        {
            entity.HasIndex(l => new { l.UserId, l.CreatedAt });
        });
    }
}
