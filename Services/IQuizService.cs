using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.ViewModels;

namespace VocaLearn.Services;

public interface IQuizService
{
    Task<QuizSessionViewModel?> GenerateQuizAsync(string userId, QuizMode mode, int? categoryId, int questionCount, IEnumerable<string>? lessonNames = null);
    Task<QuizResultViewModel> SubmitQuizAsync(string userId, QuizSubmitModel model);
    Task<QuizResultViewModel?> GetQuizResultAsync(int quizResultId, string userId);
    Task<List<QuizResult>> GetQuizHistoryAsync(string userId, int take = 20);
    Task<QuizSessionViewModel?> GenerateRetryQuizAsync(int quizResultId, string userId);
}

public class QuizService : IQuizService
{
    private readonly ApplicationDbContext _context;
    private readonly IProgressService _progressService;
    private static readonly Random _rng = new();

    public QuizService(ApplicationDbContext context, IProgressService progressService)
    {
        _context = context;
        _progressService = progressService;
    }

    public async Task<QuizSessionViewModel?> GenerateQuizAsync(string userId, QuizMode mode, int? categoryId, int questionCount, IEnumerable<string>? lessonNames = null)
    {
        var allUserVocabs = await _context.Vocabularies
            .Include(v => v.Category)
            .Where(v => v.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        if (allUserVocabs.Count < 2)
        {
            return null; // Not enough words
        }

        var candidatePool = allUserVocabs;
        string categoryName = "All Categories";

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            var filtered = allUserVocabs.Where(v => v.CategoryId == categoryId.Value).ToList();
            if (filtered.Count >= 2)
            {
                candidatePool = filtered;
                categoryName = filtered.First().Category?.Name ?? "Selected Category";
            }
        }

        var selectedLessons = lessonNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (selectedLessons.Count > 0)
        {
            candidatePool = candidatePool
                .Where(v => selectedLessons.Contains(v.LessonName, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidatePool.Count < 2) return null;

        var lessonLabel = selectedLessons.Count == 0 ? "All Lessons" : string.Join(", ", selectedLessons);

        // Shuffle candidate pool and take up to questionCount
        var selectedVocabs = candidatePool.OrderBy(_ => _rng.Next()).Take(questionCount).ToList();
        var questions = new List<QuizQuestionViewModel>();

        for (int i = 0; i < selectedVocabs.Count; i++)
        {
            var vocab = selectedVocabs[i];
            var q = BuildQuestion(i + 1, vocab, mode, candidatePool);
            questions.Add(q);
        }

        return new QuizSessionViewModel
        {
            Mode = mode,
            CategoryId = categoryId,
            CategoryName = categoryName,
            LessonNames = lessonLabel,
            Questions = questions
        };
    }

    public async Task<QuizSessionViewModel?> GenerateRetryQuizAsync(int quizResultId, string userId)
    {
        var result = await _context.QuizResults
            .Include(q => q.Details)
                .ThenInclude(d => d.Vocabulary)
                    .ThenInclude(v => v!.Category)
            .FirstOrDefaultAsync(q => q.Id == quizResultId && q.UserId == userId);

        if (result == null) return null;

        var incorrectDetails = result.Details.Where(d => !d.IsCorrect && d.Vocabulary != null).ToList();
        if (!incorrectDetails.Any()) return null;

        var allUserVocabs = await _context.Vocabularies
            .Where(v => v.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        var questions = new List<QuizQuestionViewModel>();
        for (int i = 0; i < incorrectDetails.Count; i++)
        {
            var detail = incorrectDetails[i];
            var vocab = detail.Vocabulary!;
            var q = BuildQuestion(i + 1, vocab, result.QuizMode, allUserVocabs);
            questions.Add(q);
        }

        return new QuizSessionViewModel
        {
            Mode = result.QuizMode,
            CategoryId = result.CategoryId,
            CategoryName = "Retry Incorrect Questions",
            Questions = questions
        };
    }

    private QuizQuestionViewModel BuildQuestion(int index, Vocabulary vocab, QuizMode mode, List<Vocabulary> allVocabs)
    {
        var question = new QuizQuestionViewModel
        {
            QuestionIndex = index,
            VocabularyId = vocab.Id,
            Mode = mode,
            Pronunciation = vocab.Pronunciation
        };

        switch (mode)
        {
            case QuizMode.MultipleChoice:
                question.Prompt = $"What is the Vietnamese meaning of \"{vocab.Word}\"?";
                question.SubPrompt = !string.IsNullOrWhiteSpace(vocab.Pronunciation) ? vocab.Pronunciation : null;
                question.CorrectAnswer = vocab.Meaning;

                // Pick 3 distractor meanings
                var distractorsMC = allVocabs
                    .Where(v => v.Id != vocab.Id && !string.Equals(v.Meaning, vocab.Meaning, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(_ => _rng.Next())
                    .Select(v => v.Meaning)
                    .Distinct()
                    .Take(3)
                    .ToList();

                var optionsMC = new List<string>(distractorsMC) { vocab.Meaning };
                question.Options = optionsMC.OrderBy(_ => _rng.Next()).ToList();
                break;

            case QuizMode.ReverseMultipleChoice:
                question.Prompt = $"Which English word translates to \"{vocab.Meaning}\"?";
                question.CorrectAnswer = vocab.Word;

                // Pick 3 distractor words
                var distractorsRMC = allVocabs
                    .Where(v => v.Id != vocab.Id && !string.Equals(v.Word, vocab.Word, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(_ => _rng.Next())
                    .Select(v => v.Word)
                    .Distinct()
                    .Take(3)
                    .ToList();

                var optionsRMC = new List<string>(distractorsRMC) { vocab.Word };
                question.Options = optionsRMC.OrderBy(_ => _rng.Next()).ToList();
                break;

            case QuizMode.FillInTheBlank:
                question.Prompt = "Fill in the blank with the correct English word:";
                question.CorrectAnswer = vocab.Word;
                question.Hint = $"Meaning: {vocab.Meaning} (Starts with '{vocab.Word.Substring(0, 1)}', {vocab.Word.Length} letters)";

                if (!string.IsNullOrWhiteSpace(vocab.Example))
                {
                    // Replace word case-insensitively
                    var pattern = $@"\b{Regex.Escape(vocab.Word)}\b";
                    var sentenceWithBlank = Regex.Replace(vocab.Example, pattern, "________", RegexOptions.IgnoreCase);
                    if (!sentenceWithBlank.Contains("________"))
                    {
                        sentenceWithBlank = $"{vocab.Example} (Target: ________)";
                    }
                    question.ExampleWithBlank = sentenceWithBlank;
                }
                else
                {
                    question.ExampleWithBlank = $"Example sentence with the word ________ (Meaning: {vocab.Meaning}).";
                }

                // Also generate 4 options for ease of use
                var distractorsFITB = allVocabs
                    .Where(v => v.Id != vocab.Id && !string.Equals(v.Word, vocab.Word, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(_ => _rng.Next())
                    .Select(v => v.Word)
                    .Distinct()
                    .Take(3)
                    .ToList();

                var optionsFITB = new List<string>(distractorsFITB) { vocab.Word };
                question.Options = optionsFITB.OrderBy(_ => _rng.Next()).ToList();
                break;

            case QuizMode.Translation:
                question.Prompt = $"Type the Vietnamese meaning for \"{vocab.Word}\":";
                question.SubPrompt = !string.IsNullOrWhiteSpace(vocab.Example) ? $"Context: \"{vocab.Example}\"" : null;
                question.CorrectAnswer = vocab.Meaning;
                question.Hint = $"Hint: starts with \"{vocab.Meaning.Substring(0, Math.Min(3, vocab.Meaning.Length))}\"...";
                break;
        }

        return question;
    }

    public async Task<QuizResultViewModel> SubmitQuizAsync(string userId, QuizSubmitModel model)
    {
        var vocabIds = model.Answers.Select(a => a.VocabularyId).Distinct().ToList();
        var vocabs = await _context.Vocabularies
            .Where(v => vocabIds.Contains(v.Id) && v.UserId == userId)
            .ToDictionaryAsync(v => v.Id);

        var details = new List<QuizDetail>();
        int correctCount = 0;

        foreach (var ans in model.Answers)
        {
            if (!vocabs.TryGetValue(ans.VocabularyId, out var vocab)) continue;

            bool isCorrect = EvaluateAnswer(ans.UserAnswer, ans.CorrectAnswer, model.Mode);
            if (isCorrect)
            {
                correctCount++;
                vocab.CorrectCount++;
                if (vocab.Status == VocabularyStatus.NotLearned)
                {
                    vocab.Status = VocabularyStatus.Learning;
                }
                else if (vocab.Status == VocabularyStatus.Learning && vocab.CorrectCount >= 3)
                {
                    vocab.Status = VocabularyStatus.Learned;
                    vocab.NeedsPractice = false;
                }
            }
            else
            {
                vocab.NeedsPractice = true;
                vocab.Status = VocabularyStatus.NeedsReview;
            }

            vocab.ReviewCount++;
            vocab.LastReviewedAt = DateTime.UtcNow;

            string explanation = isCorrect
                ? $"Excellent! \"{vocab.Word}\" means \"{vocab.Meaning}\"."
                : $"Incorrect. The correct answer for \"{vocab.Word}\" is \"{vocab.Meaning}\".";

            if (!string.IsNullOrWhiteSpace(vocab.Example))
            {
                explanation += $" Example: \"{vocab.Example}\"";
            }

            details.Add(new QuizDetail
            {
                VocabularyId = vocab.Id,
                QuestionText = ans.QuestionText,
                CorrectAnswer = ans.CorrectAnswer,
                UserAnswer = ans.UserAnswer,
                IsCorrect = isCorrect,
                Explanation = explanation
            });
        }

        var total = model.Answers.Count;
        double scorePercent = total == 0 ? 0 : Math.Round((double)correctCount / total * 100, 1);

        var result = new QuizResult
        {
            UserId = userId,
            QuizMode = model.Mode,
            CategoryId = model.CategoryId,
            TotalQuestions = total,
            CorrectAnswers = correctCount,
            ScorePercentage = scorePercent,
            CompletedAt = DateTime.UtcNow,
            Details = details
        };

        _context.QuizResults.Add(result);
        await _context.SaveChangesAsync();

        // Record daily activity
        await _progressService.RecordDailyActivityAsync(
            userId: userId,
            wordsLearnedDelta: 0,
            wordsReviewedDelta: total,
            quizzesTakenDelta: 1,
            correctDelta: correctCount,
            totalDelta: total);

        var categoryName = "All Categories";
        if (model.CategoryId.HasValue && model.CategoryId.Value > 0)
        {
            var cat = await _context.Categories.FindAsync(model.CategoryId.Value);
            if (cat != null) categoryName = cat.Name;
        }

        return new QuizResultViewModel
        {
            QuizResultId = result.Id,
            QuizMode = result.QuizMode,
            CategoryName = categoryName,
            TotalQuestions = total,
            CorrectAnswers = correctCount,
            ScorePercentage = scorePercent,
            Details = details.Select(d => new QuizDetailViewModel
            {
                VocabularyId = d.VocabularyId,
                Word = vocabs.ContainsKey(d.VocabularyId) ? vocabs[d.VocabularyId].Word : "",
                Meaning = vocabs.ContainsKey(d.VocabularyId) ? vocabs[d.VocabularyId].Meaning : "",
                QuestionText = d.QuestionText,
                CorrectAnswer = d.CorrectAnswer,
                UserAnswer = d.UserAnswer,
                IsCorrect = d.IsCorrect,
                Explanation = d.Explanation
            }).ToList()
        };
    }

    private static bool EvaluateAnswer(string? userAnswer, string correctAnswer, QuizMode mode)
    {
        if (string.IsNullOrWhiteSpace(userAnswer)) return false;

        var user = userAnswer.Trim().ToLowerInvariant();
        var correct = correctAnswer.Trim().ToLowerInvariant();

        if (mode == QuizMode.MultipleChoice || mode == QuizMode.ReverseMultipleChoice || mode == QuizMode.FillInTheBlank)
        {
            return string.Equals(user, correct, StringComparison.OrdinalIgnoreCase);
        }

        // Translation: fuzzy/token match for Vietnamese
        if (string.Equals(user, correct, StringComparison.OrdinalIgnoreCase)) return true;

        // Split multiple meaning variants separated by comma, slash or semicolon
        var parts = correct.Split(new[] { ',', ';', '/', '、' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Trim().ToLowerInvariant());

        if (parts.Any(p => p == user || user.Contains(p) || p.Contains(user)))
        {
            return true;
        }

        return false;
    }

    public async Task<QuizResultViewModel?> GetQuizResultAsync(int quizResultId, string userId)
    {
        var result = await _context.QuizResults
            .Include(q => q.Category)
            .Include(q => q.Details)
                .ThenInclude(d => d.Vocabulary)
            .FirstOrDefaultAsync(q => q.Id == quizResultId && q.UserId == userId);

        if (result == null) return null;

        return new QuizResultViewModel
        {
            QuizResultId = result.Id,
            QuizMode = result.QuizMode,
            CategoryName = result.Category?.Name ?? "All Categories",
            TotalQuestions = result.TotalQuestions,
            CorrectAnswers = result.CorrectAnswers,
            ScorePercentage = result.ScorePercentage,
            Details = result.Details.Select(d => new QuizDetailViewModel
            {
                VocabularyId = d.VocabularyId,
                Word = d.Vocabulary?.Word ?? "",
                Meaning = d.Vocabulary?.Meaning ?? "",
                QuestionText = d.QuestionText,
                CorrectAnswer = d.CorrectAnswer,
                UserAnswer = d.UserAnswer,
                IsCorrect = d.IsCorrect,
                Explanation = d.Explanation
            }).ToList()
        };
    }

    public async Task<List<QuizResult>> GetQuizHistoryAsync(string userId, int take = 20)
    {
        return await _context.QuizResults
            .Include(q => q.Category)
            .Include(q => q.Details)
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.CompletedAt)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();
    }
}
