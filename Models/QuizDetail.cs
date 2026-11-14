using System.ComponentModel.DataAnnotations;

namespace VocaLearn.Models;

public class QuizDetail
{
    public int Id { get; set; }

    [Required]
    public int QuizResultId { get; set; }
    public virtual QuizResult? QuizResult { get; set; }

    [Required]
    public int VocabularyId { get; set; }
    public virtual Vocabulary? Vocabulary { get; set; }

    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string CorrectAnswer { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? UserAnswer { get; set; }

    public bool IsCorrect { get; set; }

    [MaxLength(500)]
    public string? Explanation { get; set; }
}
