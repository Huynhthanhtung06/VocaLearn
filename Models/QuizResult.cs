using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VocaLearn.Models;

public class QuizResult
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser? User { get; set; }

    public QuizMode QuizMode { get; set; } = QuizMode.MultipleChoice;

    public int? CategoryId { get; set; }
    public virtual Category? Category { get; set; }

    public int TotalQuestions { get; set; }

    public int CorrectAnswers { get; set; }

    public double ScorePercentage { get; set; }

    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<QuizDetail> Details { get; set; } = new List<QuizDetail>();
}
