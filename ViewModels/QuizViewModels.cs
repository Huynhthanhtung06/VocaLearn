using System.Collections.Generic;
using VocaLearn.Models;

namespace VocaLearn.ViewModels;

public class QuizSetupViewModel
{
    public List<Category> Categories { get; set; } = new();
    public List<string> Lessons { get; set; } = new();
    public int? SelectedCategoryId { get; set; }
    public QuizMode Mode { get; set; } = QuizMode.MultipleChoice;
    public int QuestionCount { get; set; } = 10;
    public int AvailableWordCount { get; set; }
}

public class QuizQuestionViewModel
{
    public int QuestionIndex { get; set; }
    public int VocabularyId { get; set; }
    public QuizMode Mode { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? SubPrompt { get; set; }
    public string? Pronunciation { get; set; }
    public List<string> Options { get; set; } = new();
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? ExampleWithBlank { get; set; }
    public string? Hint { get; set; }
}

public class QuizSessionViewModel
{
    public QuizMode Mode { get; set; }
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = "All Categories";
    public string LessonNames { get; set; } = "All Lessons";
    public List<QuizQuestionViewModel> Questions { get; set; } = new();
}

public class QuizSubmitModel
{
    public QuizMode Mode { get; set; }
    public int? CategoryId { get; set; }
    public List<QuizAnswerItemModel> Answers { get; set; } = new();
}

public class QuizAnswerItemModel
{
    public int VocabularyId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string UserAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? Explanation { get; set; }
}

public class QuizResultViewModel
{
    public int QuizResultId { get; set; }
    public QuizMode QuizMode { get; set; }
    public string CategoryName { get; set; } = "All Categories";
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public double ScorePercentage { get; set; }
    public List<QuizDetailViewModel> Details { get; set; } = new();
}

public class QuizDetailViewModel
{
    public int VocabularyId { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? UserAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public string? Explanation { get; set; }
}
