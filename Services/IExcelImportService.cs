using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.ViewModels;

namespace VocaLearn.Services;

public interface IExcelImportService
{
    Task<ExcelImportPreviewViewModel> ParseAndPreviewAsync(Stream stream, string fileName, string userId);
    Task<int> SaveImportedRowsAsync(List<ExcelImportRowViewModel> rows, bool skipDuplicates, string userId);
    byte[] GenerateTemplate();
}

public class ExcelImportService : IExcelImportService
{
    private readonly ApplicationDbContext _context;

    public ExcelImportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExcelImportPreviewViewModel> ParseAndPreviewAsync(Stream stream, string fileName, string userId)
    {
        var preview = new ExcelImportPreviewViewModel
        {
            FileName = fileName
        };

        var existingWords = await _context.Vocabularies
            .Where(v => v.UserId == userId)
            .Select(v => v.Word.ToLower().Trim())
            .ToListAsync();

        var existingSet = new HashSet<string>(existingWords);
        var inBatchSet = new HashSet<string>();

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            return preview;
        }

        var rows = worksheet.RangeUsed()?.RowsUsed()?.ToList();
        if (rows == null || rows.Count <= 1)
        {
            return preview;
        }

        // Header mapping
        var headerRow = rows[0];
        int colWord = 1, colMeaning = 2, colPronunciation = 0, colExample = 3, colCategory = 4;

        for (int c = 1; c <= headerRow.CellCount(); c++)
        {
            var headerText = headerRow.Cell(c).GetString().Trim().ToLowerInvariant();
            if (headerText.Contains("english") || headerText.Contains("word") || headerText.Contains("từ"))
                colWord = c;
            else if (headerText.Contains("vietnamese") || headerText.Contains("meaning") || headerText.Contains("nghĩa"))
                colMeaning = c;
            else if (headerText.Contains("pronunciation") || headerText.Contains("pronounce") ||
                     headerText.Contains("phonetic") || headerText.Contains("transcription") ||
                     headerText.Contains("ipa") || headerText.Contains("phiên âm") || headerText.Contains("phien am"))
                colPronunciation = c;
            else if (headerText.Contains("example") || headerText.Contains("câu ví dụ") || headerText.Contains("ví dụ"))
                colExample = c;
            else if (headerText.Contains("category") || headerText.Contains("chủ đề") || headerText.Contains("loại"))
                colCategory = c;
        }

        // Iterate data rows
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var word = row.Cell(colWord).GetString().Trim();
            var meaning = row.Cell(colMeaning).GetString().Trim();
            var pronunciation = colPronunciation > 0 ? row.Cell(colPronunciation).GetString().Trim() : string.Empty;
            var example = row.Cell(colExample).GetString().Trim();
            var category = row.Cell(colCategory).GetString().Trim();

            // Skip empty rows
            if (string.IsNullOrWhiteSpace(word) && string.IsNullOrWhiteSpace(meaning))
            {
                continue;
            }

            var item = new ExcelImportRowViewModel
            {
                RowIndex = i + 1,
                Word = word,
                Meaning = meaning,
                Pronunciation = string.IsNullOrWhiteSpace(pronunciation) ? null : pronunciation,
                Example = string.IsNullOrWhiteSpace(example) ? null : example,
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category
            };

            // Validation
            if (string.IsNullOrWhiteSpace(word))
            {
                item.IsValid = false;
                item.ErrorMessage = "English word is missing.";
            }
            else if (string.IsNullOrWhiteSpace(meaning))
            {
                item.IsValid = false;
                item.ErrorMessage = "Vietnamese meaning is missing.";
            }
            else
            {
                var lowerWord = word.ToLower();
                if (existingSet.Contains(lowerWord) || inBatchSet.Contains(lowerWord))
                {
                    item.IsDuplicate = true;
                    item.IsValid = true;
                    item.ErrorMessage = "Word already exists in your vocabulary or file.";
                }
                else
                {
                    item.IsValid = true;
                    inBatchSet.Add(lowerWord);
                }
            }

            if (!item.IsValid)
            {
                preview.InvalidCount++;
            }
            else if (item.IsDuplicate)
            {
                preview.DuplicateCount++;
            }
            else
            {
                preview.ValidCount++;
            }

            preview.Rows.Add(item);
        }

        return preview;
    }

    public async Task<int> SaveImportedRowsAsync(List<ExcelImportRowViewModel> rows, bool skipDuplicates, string userId)
    {
        var existingCategories = await _context.Categories
            .Where(c => c.UserId == userId)
            .ToDictionaryAsync(c => c.Name.ToLower(), c => c);

        var existingWords = await _context.Vocabularies
            .Where(v => v.UserId == userId)
            .Select(v => v.Word.ToLower().Trim())
            .ToListAsync();
        var existingWordSet = new HashSet<string>(existingWords);

        var toAdd = new List<Vocabulary>();
        int count = 0;

        foreach (var r in rows)
        {
            if (!r.IsValid && !r.IsDuplicate) continue;

            var lowerWord = r.Word.Trim().ToLower();
            if (skipDuplicates && (existingWordSet.Contains(lowerWord) || r.IsDuplicate))
            {
                continue;
            }

            var catName = string.IsNullOrWhiteSpace(r.Category) ? "General" : r.Category.Trim();
            var lowerCat = catName.ToLower();

            if (!existingCategories.TryGetValue(lowerCat, out var category))
            {
                category = new Category
                {
                    Name = catName,
                    Color = PickCategoryColor(catName),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                existingCategories[lowerCat] = category;
            }

            var vocab = new Vocabulary
            {
                Word = r.Word.Trim(),
                Meaning = r.Meaning.Trim(),
                Example = r.Example?.Trim(),
                Pronunciation = r.Pronunciation?.Trim(),
                LessonName = string.IsNullOrWhiteSpace(r.LessonName) ? "Lesson 1" : r.LessonName.Trim(),
                CategoryId = category.Id,
                UserId = userId,
                Status = VocabularyStatus.NotLearned,
                CreatedAt = DateTime.UtcNow
            };

            toAdd.Add(vocab);
            existingWordSet.Add(lowerWord);
            count++;
        }

        if (toAdd.Any())
        {
            await _context.Vocabularies.AddRangeAsync(toAdd);
            await _context.SaveChangesAsync();
        }

        return count;
    }

    public byte[] GenerateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Vocabulary");

        // Headers
        worksheet.Cell(1, 1).Value = "English";
        worksheet.Cell(1, 2).Value = "Vietnamese";
        worksheet.Cell(1, 3).Value = "Pronunciation (IPA)";
        worksheet.Cell(1, 4).Value = "Example";
        worksheet.Cell(1, 5).Value = "Category";

        // Styling header
        var headerRange = worksheet.Range("A1:E1");
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#111C30");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Sample rows
        worksheet.Cell(2, 1).Value = "Achieve";
        worksheet.Cell(2, 2).Value = "Đạt được";
        worksheet.Cell(2, 3).Value = "/əˈtʃiːv/";
        worksheet.Cell(2, 4).Value = "I want to achieve my goal.";
        worksheet.Cell(2, 5).Value = "Basic";

        worksheet.Cell(3, 1).Value = "Environment";
        worksheet.Cell(3, 2).Value = "Môi trường";
        worksheet.Cell(3, 3).Value = "/ɪnˈvaɪrənmənt/";
        worksheet.Cell(3, 4).Value = "Protect the environment.";
        worksheet.Cell(3, 5).Value = "Nature";

        worksheet.Cell(4, 1).Value = "Collaborate";
        worksheet.Cell(4, 2).Value = "Hợp tác";
        worksheet.Cell(4, 3).Value = "/kəˈlæbəreɪt/";
        worksheet.Cell(4, 4).Value = "Teams collaborate efficiently across time zones.";
        worksheet.Cell(4, 5).Value = "Business";

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static string PickCategoryColor(string name)
    {
        var colors = new[] { "#3B82F6", "#38BDF8", "#8B5CF6", "#F59E0B", "#22C55E", "#EC4899", "#14B8A6" };
        int hash = Math.Abs(name.GetHashCode());
        return colors[hash % colors.Length];
    }
}
