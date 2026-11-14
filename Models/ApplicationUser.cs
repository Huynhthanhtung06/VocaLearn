using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace VocaLearn.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<Vocabulary> Vocabularies { get; set; } = new List<Vocabulary>();
    public virtual ICollection<LearningProgress> LearningProgresses { get; set; } = new List<LearningProgress>();
    public virtual ICollection<QuizResult> QuizResults { get; set; } = new List<QuizResult>();
}
