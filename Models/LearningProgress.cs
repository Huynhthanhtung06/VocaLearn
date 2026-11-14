using System;
using System.ComponentModel.DataAnnotations;

namespace VocaLearn.Models;

public class LearningProgress
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser? User { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow.Date;

    public int WordsLearnedCount { get; set; } = 0;

    public int WordsReviewedCount { get; set; } = 0;

    public int QuizzesTakenCount { get; set; } = 0;

    public int CorrectAnswersCount { get; set; } = 0;

    public int TotalAnswersCount { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
