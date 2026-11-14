using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.ViewModels;

namespace VocaLearn.Services;

public interface IVocabularyService
{
    Task<VocabularyListViewModel> GetVocabulariesAsync(
        string userId,
        string? search,
        int? categoryId,
        VocabularyStatus? status,
        bool? needsPracticeOnly,
        string? lessonName,
        int pageIndex,
        int pageSize);

    Task<Vocabulary?> GetByIdAsync(int id, string userId);
    Task<Vocabulary> CreateAsync(Vocabulary vocabulary, string userId);
    Task<bool> UpdateAsync(Vocabulary vocabulary, string userId);
    Task<bool> DeleteAsync(int id, string userId);
    Task<int> DeleteManyAsync(IEnumerable<int> ids, string userId);
    Task<int> DeleteAllAsync(string userId);

    Task<List<Category>> GetCategoriesAsync(string userId);
    Task<Category?> GetCategoryByIdAsync(int id, string userId);
    Task<Category> CreateCategoryAsync(Category category, string userId);
    Task<bool> UpdateCategoryAsync(Category category, string userId);
    Task<bool> DeleteCategoryAsync(int id, string userId);

    Task<bool> WordExistsAsync(string word, string userId, int? excludeId = null);
}

public class VocabularyService : IVocabularyService
{
    private readonly ApplicationDbContext _context;

    public VocabularyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VocabularyListViewModel> GetVocabulariesAsync(
        string userId,
        string? search,
        int? categoryId,
        VocabularyStatus? status,
        bool? needsPracticeOnly,
        string? lessonName,
        int pageIndex,
        int pageSize)
    {
        var query = _context.Vocabularies
            .Include(v => v.Category)
            .Where(v => v.UserId == userId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v => v.Word.ToLower().Contains(term) ||
                                     v.Meaning.ToLower().Contains(term) ||
                                     (v.Example != null && v.Example.ToLower().Contains(term)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(v => v.CategoryId == categoryId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(v => v.Status == status.Value);
        }

        if (needsPracticeOnly.HasValue && needsPracticeOnly.Value)
        {
            query = query.Where(v => v.NeedsPractice);
        }

        var lessons = await _context.Vocabularies
            .Where(v => v.UserId == userId)
            .Select(v => v.LessonName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var totalItems = await query.CountAsync();
        if (!string.IsNullOrWhiteSpace(lessonName))
        {
            query = query.Where(v => v.LessonName == lessonName);
        }
        else
        {
            query = query.Where(v => false);
        }

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return new VocabularyListViewModel
        {
            Items = items,
            Categories = categories,
            Lessons = lessons,
            LessonName = lessonName,
            SearchTerm = search,
            CategoryId = categoryId,
            Status = status,
            NeedsPracticeOnly = needsPracticeOnly,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalItems = totalItems
        };
    }

    public async Task<Vocabulary?> GetByIdAsync(int id, string userId)
    {
        return await _context.Vocabularies
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);
    }

    public async Task<Vocabulary> CreateAsync(Vocabulary vocabulary, string userId)
    {
        vocabulary.UserId = userId;
        vocabulary.CreatedAt = DateTime.UtcNow;
        _context.Vocabularies.Add(vocabulary);
        await _context.SaveChangesAsync();
        return vocabulary;
    }

    public async Task<bool> UpdateAsync(Vocabulary vocabulary, string userId)
    {
        var existing = await _context.Vocabularies
            .FirstOrDefaultAsync(v => v.Id == vocabulary.Id && v.UserId == userId);

        if (existing == null) return false;

        existing.Word = vocabulary.Word;
        existing.Meaning = vocabulary.Meaning;
        existing.Example = vocabulary.Example;
        existing.Pronunciation = vocabulary.Pronunciation;
        existing.LessonName = vocabulary.LessonName;
        existing.CategoryId = vocabulary.CategoryId;
        existing.Status = vocabulary.Status;
        existing.NeedsPractice = vocabulary.NeedsPractice;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteAllAsync(string userId)
    {
        var vocabularies = await _context.Vocabularies
            .Where(v => v.UserId == userId)
            .ToListAsync();

        if (vocabularies.Count == 0) return 0;

        await RemoveQuizDetailsAsync(vocabularies.Select(v => v.Id).ToList());
        _context.Vocabularies.RemoveRange(vocabularies);
        await _context.SaveChangesAsync();
        return vocabularies.Count;
    }

    public async Task<bool> DeleteAsync(int id, string userId)
    {
        var existing = await _context.Vocabularies
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId);

        if (existing == null) return false;

        await RemoveQuizDetailsAsync(new List<int> { existing.Id });
        _context.Vocabularies.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteManyAsync(IEnumerable<int> ids, string userId)
    {
        var selectedIds = ids.Distinct().ToList();
        if (selectedIds.Count == 0) return 0;

        var vocabularies = await _context.Vocabularies
            .Where(v => selectedIds.Contains(v.Id) && v.UserId == userId)
            .ToListAsync();

        if (vocabularies.Count == 0) return 0;

        var vocabularyIds = vocabularies.Select(v => v.Id).ToList();
        await RemoveQuizDetailsAsync(vocabularyIds);
        _context.Vocabularies.RemoveRange(vocabularies);
        await _context.SaveChangesAsync();
        return vocabularies.Count;
    }

    private async Task RemoveQuizDetailsAsync(List<int> vocabularyIds)
    {
        var quizDetails = await _context.QuizDetails
            .Where(d => vocabularyIds.Contains(d.VocabularyId))
            .ToListAsync();

        if (quizDetails.Count > 0)
        {
            _context.QuizDetails.RemoveRange(quizDetails);
        }
    }

    public async Task<List<Category>> GetCategoriesAsync(string userId)
    {
        return await _context.Categories
            .Include(c => c.Vocabularies)
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(int id, string userId)
    {
        return await _context.Categories
            .Include(c => c.Vocabularies)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
    }

    public async Task<Category> CreateCategoryAsync(Category category, string userId)
    {
        category.UserId = userId;
        category.CreatedAt = DateTime.UtcNow;
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<bool> UpdateCategoryAsync(Category category, string userId)
    {
        var existing = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == category.Id && c.UserId == userId);

        if (existing == null) return false;

        existing.Name = category.Name;
        existing.Description = category.Description;
        existing.Color = category.Color;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCategoryAsync(int id, string userId)
    {
        var existing = await _context.Categories
            .Include(c => c.Vocabularies)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (existing == null) return false;

        // If category has vocabularies, reassign to a default or prevent delete
        if (existing.Vocabularies.Any())
        {
            var defaultCat = await _context.Categories
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Id != id);

            if (defaultCat == null)
            {
                defaultCat = new Category
                {
                    Name = "General",
                    Color = "#3B82F6",
                    UserId = userId
                };
                _context.Categories.Add(defaultCat);
                await _context.SaveChangesAsync();
            }

            foreach (var v in existing.Vocabularies)
            {
                v.CategoryId = defaultCat.Id;
            }
        }

        _context.Categories.Remove(existing);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> WordExistsAsync(string word, string userId, int? excludeId = null)
    {
        var query = _context.Vocabularies.Where(v => v.UserId == userId && v.Word.ToLower() == word.Trim().ToLower());
        if (excludeId.HasValue)
        {
            query = query.Where(v => v.Id != excludeId.Value);
        }
        return await query.AnyAsync();
    }
}
