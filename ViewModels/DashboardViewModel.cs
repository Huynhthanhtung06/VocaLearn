using System.Collections.Generic;
using VocaLearn.Models;

namespace VocaLearn.ViewModels;

public class DashboardViewModel
{
    public string UserFullName { get; set; } = string.Empty;
    public int TotalVocabulary { get; set; }
    public int WordsLearned { get; set; }
    public int WordsLearning { get; set; }
    public int WordsNotLearned { get; set; }
    public int WordsNeedingReview { get; set; }
    public int TotalQuizzesCompleted { get; set; }
    public double AverageQuizScore { get; set; }
    public double MasteryPercentage { get; set; }

    public List<string> ActivityDates { get; set; } = new();
    public List<int> ActivityReviewedWords { get; set; } = new();
    public List<int> ActivityQuizzesTaken { get; set; } = new();

    public Dictionary<string, int> CategoryWordCounts { get; set; } = new();
    public List<Vocabulary> RecentVocabulary { get; set; } = new();
    public List<QuizResult> RecentQuizzes { get; set; } = new();
}
