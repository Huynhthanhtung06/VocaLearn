using System;
using System.ComponentModel.DataAnnotations;

namespace VocaLearn.Models;

public class ListeningLesson
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string AudioStoredName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string AudioOriginalName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? QuestionStoredName { get; set; }

    [MaxLength(255)]
    public string? QuestionOriginalName { get; set; }

    public string? QuestionsJson { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
