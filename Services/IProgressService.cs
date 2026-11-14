using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.ViewModels;

namespace VocaLearn.Services;

public interface IProgressService
{
    Task<DashboardViewModel> GetDashboardDataAsync(string userId, string userFullName);
    Task<ProgressAnalyticsViewModel> GetAnalyticsAsync(string userId);
    Task RecordDailyActivityAsync(
        string userId,
        int wordsLearnedDelta,
        int wordsReviewedDelta,
        int quizzesTakenDelta,
        int correctDelta,
        int totalDelta);
}

public class ProgressService : IProgressService
{
    private readonly ApplicationDbContext _context;

    public ProgressService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync(string userId, string userFullName)
    {
        var vocabs = await _context.Vocabularies
            .Include(v => v.Category)
            .Where(v => v.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        var totalVocab = vocabs.Count;
        var learnedCount = vocabs.Count(v => v.Status == VocabularyStatus.Learned);
        var learningCount = vocabs.Count(v => v.Status == VocabularyStatus.Learning);
        var notLearnedCount = vocabs.Count(v => v.Status == VocabularyStatus.NotLearned);
        var needsReviewCount = vocabs.Count(v => v.NeedsPractice || v.Status == VocabularyStatus.NeedsReview);

        var masteryPercentage = totalVocab == 0 ? 0 : Math.Round((double)learnedCount / totalVocab * 100, 1);

        var quizzes = await _context.QuizResults
            .Include(q => q.Category)
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CompletedAt)
            .AsNoTracking()
            .ToListAsync();

        var totalQuizzes = quizzes.Count;
        var avgQuizScore = totalQuizzes == 0 ? 0 : Math.Round(quizzes.Average(q => q.ScorePercentage), 1);

        // 7-day activity data
        var today = DateTime.UtcNow.Date;
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();

        var progressLogs = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.Date >= today.AddDays(-6))
            .ToListAsync();

        var activityDates = new List<string>();
        var activityReviewed = new List<int>();
        var activityQuizzes = new List<int>();

        foreach (var day in last7Days)
        {
            activityDates.Add(day.ToString("MMM dd"));
            var log = progressLogs.FirstOrDefault(p => p.Date.Date == day);
            activityReviewed.Add(log?.WordsReviewedCount ?? 0);
            activityQuizzes.Add(log?.QuizzesTakenCount ?? 0);
        }

        // Category breakdown
        var categoryWordCounts = vocabs
            .GroupBy(v => v.Category != null ? v.Category.Name : "General")
            .ToDictionary(g => g.Key, g => g.Count());

        var recentVocabulary = vocabs
            .OrderByDescending(v => v.LastReviewedAt ?? v.CreatedAt)
            .Take(6)
            .ToList();

        var recentQuizzes = quizzes
            .Take(5)
            .ToList();

        return new DashboardViewModel
        {
            UserFullName = userFullName,
            TotalVocabulary = totalVocab,
            WordsLearned = learnedCount,
            WordsLearning = learningCount,
            WordsNotLearned = notLearnedCount,
            WordsNeedingReview = needsReviewCount,
            TotalQuizzesCompleted = totalQuizzes,
            AverageQuizScore = avgQuizScore,
            MasteryPercentage = masteryPercentage,
            ActivityDates = activityDates,
            ActivityReviewedWords = activityReviewed,
            ActivityQuizzesTaken = activityQuizzes,
            CategoryWordCounts = categoryWordCounts,
            RecentVocabulary = recentVocabulary,
            RecentQuizzes = recentQuizzes
        };
    }

    public async Task<ProgressAnalyticsViewModel> GetAnalyticsAsync(string userId)
    {
        var vocabs = await _context.Vocabularies
            .Include(v => v.Category)
            .Where(v => v.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        var totalVocab = vocabs.Count;
        var learnedCount = vocabs.Count(v => v.Status == VocabularyStatus.Learned);
        var learningCount = vocabs.Count(v => v.Status == VocabularyStatus.Learning);
        var needsReviewCount = vocabs.Count(v => v.NeedsPractice || v.Status == VocabularyStatus.NeedsReview);
        var masteryPercentage = totalVocab == 0 ? 0 : Math.Round((double)learnedCount / totalVocab * 100, 1);

        var quizzes = await _context.QuizResults
            .Include(q => q.Category)
            .Include(q => q.Details)
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CompletedAt)
            .AsNoTracking()
            .ToListAsync();

        var totalQuizzes = quizzes.Count;
        var avgScore = totalQuizzes == 0 ? 0 : Math.Round(quizzes.Average(q => q.ScorePercentage), 1);
        var totalQuestionsAnswered = quizzes.Sum(q => q.TotalQuestions);
        var totalCorrect = quizzes.Sum(q => q.CorrectAnswers);

        // 7 days progress
        var today = DateTime.UtcNow.Date;
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();

        var progressLogs = await _context.LearningProgresses
            .Where(p => p.UserId == userId && p.Date >= today.AddDays(-6))
            .ToListAsync();

        var weeklyLabels = new List<string>();
        var weeklyLearned = new List<int>();
        var weeklyReviewed = new List<int>();

        foreach (var day in last7Days)
        {
            weeklyLabels.Add(day.ToString("ddd, MMM dd"));
            var log = progressLogs.FirstOrDefault(p => p.Date.Date == day);
            weeklyLearned.Add(log?.WordsLearnedCount ?? 0);
            weeklyReviewed.Add(log?.WordsReviewedCount ?? 0);
        }

        // Category stats
        var categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var categoryStats = categories.Select(c =>
        {
            var catVocabs = vocabs.Where(v => v.CategoryId == c.Id).ToList();
            return new CategoryStatItem
            {
                CategoryName = c.Name,
                CategoryColor = c.Color,
                TotalWords = catVocabs.Count,
                LearnedWords = catVocabs.Count(v => v.Status == VocabularyStatus.Learned)
            };
        }).ToList();

        return new ProgressAnalyticsViewModel
        {
            TotalVocabulary = totalVocab,
            WordsLearned = learnedCount,
            WordsLearning = learningCount,
            WordsNeedingReview = needsReviewCount,
            MasteryPercentage = masteryPercentage,
            TotalQuizzesCompleted = totalQuizzes,
            AverageQuizScore = avgScore,
            TotalQuestionsAnswered = totalQuestionsAnswered,
            TotalCorrectAnswers = totalCorrect,
            WeeklyLabels = weeklyLabels,
            WeeklyLearnedData = weeklyLearned,
            WeeklyReviewedData = weeklyReviewed,
            CategoryStats = categoryStats,
            QuizHistory = quizzes
        };
    }

    public async Task RecordDailyActivityAsync(
        string userId,
        int wordsLearnedDelta,
        int wordsReviewedDelta,
        int quizzesTakenDelta,
        int correctDelta,
        int totalDelta)
    {
        var today = DateTime.UtcNow.Date;
        var progress = await _context.LearningProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == today);

        if (progress == null)
        {
            progress = new LearningProgress
            {
                UserId = userId,
                Date = today,
                WordsLearnedCount = wordsLearnedDelta,
                WordsReviewedCount = wordsReviewedDelta,
                QuizzesTakenCount = quizzesTakenDelta,
                CorrectAnswersCount = correctDelta,
                TotalAnswersCount = totalDelta,
                UpdatedAt = DateTime.UtcNow
            };
            _context.LearningProgresses.Add(progress);
        }
        else
        {
            progress.WordsLearnedCount += wordsLearnedDelta;
            progress.WordsReviewedCount += wordsReviewedDelta;
            progress.QuizzesTakenCount += quizzesTakenDelta;
            progress.CorrectAnswersCount += correctDelta;
            progress.TotalAnswersCount += totalDelta;
            progress.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
