using System;
using System.ComponentModel.DataAnnotations;

namespace VocaLearn.Models;

public class Vocabulary
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Word { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Meaning { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Example { get; set; }

    [MaxLength(100)]
    public string? Pronunciation { get; set; }

    [Required]
    [MaxLength(100)]
    public string LessonName { get; set; } = "Lesson 1";

    [Required]
    public int CategoryId { get; set; }
    public virtual Category? Category { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser? User { get; set; }

    public VocabularyStatus Status { get; set; } = VocabularyStatus.NotLearned;

    public bool NeedsPractice { get; set; } = false;

    public int ReviewCount { get; set; } = 0;

    public int CorrectCount { get; set; } = 0;

    public DateTime? LastReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
