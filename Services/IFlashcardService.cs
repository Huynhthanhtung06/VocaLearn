using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.ViewModels;

namespace VocaLearn.Services;

public interface IFlashcardService
{
    Task<FlashcardSessionViewModel> GetFlashcardsAsync(string userId, int? categoryId, string filter, string? lesson = null);
    Task<bool> RecordFlashcardActionAsync(int vocabularyId, string action, string userId);
}

public class FlashcardService : IFlashcardService
{
    private readonly ApplicationDbContext _context;
    private readonly IProgressService _progressService;

    public FlashcardService(ApplicationDbContext context, IProgressService progressService)
    {
        _context = context;
        _progressService = progressService;
    }

    public async Task<FlashcardSessionViewModel> GetFlashcardsAsync(string userId, int? categoryId, string filter, string? lesson = null)
    {
        var query = _context.Vocabularies
            .Include(v => v.Category)
            .Where(v => v.UserId == userId)
            .AsNoTracking();

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(v => v.CategoryId == categoryId.Value);
        }

        if (filter == "needsPractice")
        {
            query = query.Where(v => v.NeedsPractice || v.Status == VocabularyStatus.NeedsReview);
        }
        else if (filter == "unlearned")
        {
            query = query.Where(v => v.Status != VocabularyStatus.Learned);
        }

        var lessons = await query
            .Select(v => v.LessonName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();
        if (lessons.Count == 0) lessons.Add("Lesson 1");
        lesson = string.IsNullOrWhiteSpace(lesson) || !lessons.Contains(lesson) ? lessons[0] : lesson;
        query = query.Where(v => v.LessonName == lesson);

        // Prioritize words that need practice, then least reviewed, then others.
        var list = await query
            .OrderByDescending(v => v.NeedsPractice)
            .ThenBy(v => v.ReviewCount)
            .ThenBy(v => v.LastReviewedAt)
            .ThenBy(v => v.Id)
            .Select(v => new FlashcardItemViewModel
            {
                Id = v.Id,
                Word = v.Word,
                Meaning = v.Meaning,
                Example = v.Example,
                Pronunciation = v.Pronunciation,
                CategoryName = v.Category != null ? v.Category.Name : "General",
                LessonName = v.LessonName,
                CategoryColor = v.Category != null ? v.Category.Color : "#3B82F6",
                Status = v.Status,
                NeedsPractice = v.NeedsPractice,
                ReviewCount = v.ReviewCount
            })
            .ToListAsync();

        var categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return new FlashcardSessionViewModel
        {
            Cards = list,
            Categories = categories,
            SelectedCategoryId = categoryId,
            Filter = filter ?? "all",
            SelectedLesson = lesson,
            Lessons = lessons
        };
    }

    public async Task<bool> RecordFlashcardActionAsync(int vocabularyId, string action, string userId)
    {
        var vocab = await _context.Vocabularies
            .FirstOrDefaultAsync(v => v.Id == vocabularyId && v.UserId == userId);

        if (vocab == null) return false;

        vocab.ReviewCount++;
        vocab.LastReviewedAt = DateTime.UtcNow;

        int wordsLearnedDelta = 0;

        if (action.Equals("know", StringComparison.OrdinalIgnoreCase))
        {
            vocab.CorrectCount++;
            vocab.NeedsPractice = false;
            if (vocab.Status != VocabularyStatus.Learned)
            {
                vocab.Status = VocabularyStatus.Learned;
                wordsLearnedDelta = 1;
            }
        }
        else // practice
        {
            vocab.NeedsPractice = true;
            vocab.Status = VocabularyStatus.NeedsReview;
        }

        await _context.SaveChangesAsync();

        // Record daily activity
        await _progressService.RecordDailyActivityAsync(
            userId: userId,
            wordsLearnedDelta: wordsLearnedDelta,
            wordsReviewedDelta: 1,
            quizzesTakenDelta: 0,
            correctDelta: 0,
            totalDelta: 0);

        return true;
    }
}
