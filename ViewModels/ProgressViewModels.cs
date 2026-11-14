using System;
using System.Collections.Generic;
using VocaLearn.Models;

namespace VocaLearn.ViewModels;

public class ProgressAnalyticsViewModel
{
    public int TotalVocabulary { get; set; }
    public int WordsLearned { get; set; }
    public int WordsLearning { get; set; }
    public int WordsNeedingReview { get; set; }
    public double MasteryPercentage { get; set; }

    public int TotalQuizzesCompleted { get; set; }
    public double AverageQuizScore { get; set; }
    public int TotalQuestionsAnswered { get; set; }
    public int TotalCorrectAnswers { get; set; }

    public List<string> WeeklyLabels { get; set; } = new();
    public List<int> WeeklyLearnedData { get; set; } = new();
    public List<int> WeeklyReviewedData { get; set; } = new();

    public List<CategoryStatItem> CategoryStats { get; set; } = new();
    public List<QuizResult> QuizHistory { get; set; } = new();
}

public class CategoryStatItem
{
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#3B82F6";
    public int TotalWords { get; set; }
    public int LearnedWords { get; set; }
    public double MasteryPercentage => TotalWords == 0 ? 0 : Math.Round((double)LearnedWords / TotalWords * 100, 1);
}
