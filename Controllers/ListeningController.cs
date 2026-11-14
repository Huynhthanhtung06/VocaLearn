using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.ViewModels;
using NAudio.Wave;

namespace VocaLearn.Controllers;

[Authorize]
public class ListeningController : Controller
{
    private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".ogg", ".m4a", ".wma" };
    private static readonly string[] QuestionExtensions = { ".xlsx" };
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public ListeningController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    private string GetUserId() => _userManager.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new ListeningIndexViewModel
        {
            Lessons = await GetUserLessonsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(ListeningIndexViewModel model)
    {
        var audioFile = model.Upload.AudioFile;
        var questionFile = model.Upload.QuestionFile;
        List<ListeningQuestionViewModel>? questions = null;

        ValidateFile(audioFile, AudioExtensions, 20 * 1024 * 1024, "Upload.AudioFile", "Audio must be MP3, WAV, OGG, M4A, or WMA and no larger than 20 MB.");
        if (questionFile != null)
        {
            ValidateFile(questionFile, QuestionExtensions, 8 * 1024 * 1024, "Upload.QuestionFile", "Use an XLSX question file no larger than 8 MB.");
            if (string.Equals(Path.GetExtension(questionFile.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    questions = ParseQuestions(questionFile);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException)
                {
                    ModelState.AddModelError("Upload.QuestionFile", ex.Message);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            model.Lessons = await GetUserLessonsAsync();
            return View("Index", model);
        }

        var userId = GetUserId();
        var userDirectory = Path.Combine(_environment.ContentRootPath, "App_Data", "ListeningFiles", userId);
        Directory.CreateDirectory(userDirectory);

        var audioExtension = Path.GetExtension(audioFile!.FileName).ToLowerInvariant();
        var audioStoredName = $"{Guid.NewGuid():N}{(audioExtension == ".wma" ? ".wav" : audioExtension)}";
        string? questionStoredName = null;
        var savedPaths = new System.Collections.Generic.List<string>();

        try
        {
            var audioPath = Path.Combine(userDirectory, audioStoredName);
            savedPaths.Add(audioPath);
            if (audioExtension == ".wma")
            {
                if (!OperatingSystem.IsWindows())
                {
                    ModelState.AddModelError("Upload.AudioFile", "WMA conversion is only available on Windows. Please convert this file to MP3 or WAV.");
                    model.Lessons = await GetUserLessonsAsync();
                    return View("Index", model);
                }

                var wmaPath = Path.Combine(userDirectory, $"{Guid.NewGuid():N}.wma");
                savedPaths.Add(wmaPath);
                await using (var input = new FileStream(wmaPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await audioFile.CopyToAsync(input);
                }

                using (var reader = new MediaFoundationReader(wmaPath))
                {
                    WaveFileWriter.CreateWaveFile(audioPath, reader);
                }
                System.IO.File.Delete(wmaPath);
                savedPaths.Remove(wmaPath);
            }
            else
            {
                await using var stream = new FileStream(audioPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await audioFile.CopyToAsync(stream);
            }

            if (questionFile != null)
            {
                var questionExtension = Path.GetExtension(questionFile.FileName).ToLowerInvariant();
                questionStoredName = $"{Guid.NewGuid():N}{questionExtension}";
                var questionPath = Path.Combine(userDirectory, questionStoredName);
                await using var stream = new FileStream(questionPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await questionFile.CopyToAsync(stream);
                savedPaths.Add(questionPath);
            }

            _context.ListeningLessons.Add(new ListeningLesson
            {
                Title = model.Upload.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Upload.Description) ? null : model.Upload.Description.Trim(),
                AudioStoredName = audioStoredName,
                AudioOriginalName = Path.GetFileName(audioFile.FileName),
                QuestionStoredName = questionStoredName,
                QuestionOriginalName = questionFile == null ? null : Path.GetFileName(questionFile.FileName),
                QuestionsJson = questions == null ? null : JsonSerializer.Serialize(questions),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Listening practice added successfully.";
            return RedirectToAction(nameof(Index));
        }

        catch
        {
            foreach (var path in savedPaths)
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }

            TempData["ErrorMessage"] = "Could not save this listening practice. Please try again.";
            model.Lessons = await GetUserLessonsAsync();
            return View("Index", model);
        }
    }

    [HttpGet]
    public IActionResult DownloadQuestionTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Questions");
        var headers = new[] { "Question", "A", "B", "C", "D", "Correct Answer" };
        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
        }

        worksheet.Cell(2, 1).Value = "What does 'conduct' mean?";
        worksheet.Cell(2, 2).Value = "To carry out";
        worksheet.Cell(2, 3).Value = "To cancel";
        worksheet.Cell(2, 4).Value = "To forget";
        worksheet.Cell(2, 5).Value = "To arrive";
        worksheet.Cell(2, 6).Value = "A";
        worksheet.Range("A1:F1").Style.Font.Bold = true;
        worksheet.Range("A1:F1").Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        worksheet.Range("A1:F1").Style.Font.FontColor = XLColor.White;
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Listening_Questions_Template.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> Audio(int id)
    {
        var lesson = await _context.ListeningLessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == GetUserId());
        if (lesson == null) return NotFound();

        var path = Path.Combine(_environment.ContentRootPath, "App_Data", "ListeningFiles", lesson.UserId, lesson.AudioStoredName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var contentType = Path.GetExtension(lesson.AudioStoredName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".wma" => "audio/x-ms-wma",
            _ => "application/octet-stream"
        };

        return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), contentType, enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadQuestion(int id)
    {
        var lesson = await _context.ListeningLessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id && l.UserId == GetUserId());
        if (lesson?.QuestionStoredName == null || string.IsNullOrWhiteSpace(lesson.QuestionOriginalName)) return NotFound();

        var path = Path.Combine(_environment.ContentRootPath, "App_Data", "ListeningFiles", lesson.UserId, lesson.QuestionStoredName);
        if (!System.IO.File.Exists(path)) return NotFound();

        return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", lesson.QuestionOriginalName);
    }

    private void ValidateFile(Microsoft.AspNetCore.Http.IFormFile? file, string[] allowedExtensions, long maxSize, string field, string errorMessage)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError(field, field.EndsWith("AudioFile", StringComparison.Ordinal) ? "Please select an audio file." : errorMessage);
            return;
        }

        var extension = Path.GetExtension(file.FileName);
        if (file.Length > maxSize || string.IsNullOrWhiteSpace(extension) || !Array.Exists(allowedExtensions, allowed => allowed.Equals(extension, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(field, errorMessage);
        }
    }

    private async Task<System.Collections.Generic.List<ListeningExerciseViewModel>> GetUserLessonsAsync()
    {
        var lessons = await _context.ListeningLessons
            .AsNoTracking()
            .Where(l => l.UserId == GetUserId())
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return lessons.Select(lesson => new ListeningExerciseViewModel
        {
            Lesson = lesson,
            Questions = DeserializeQuestions(lesson.QuestionsJson)
        }).ToList();
    }

    private static List<ListeningQuestionViewModel> ParseQuestions(Microsoft.AspNetCore.Http.IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        var rows = worksheet?.RangeUsed()?.RowsUsed()?.ToList();
        if (rows == null || rows.Count < 2)
            throw new InvalidDataException("The question workbook needs a header row and at least one question.");

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var header = rows[0];
        var lastColumn = header.LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var column = 1; column <= lastColumn; column++)
        {
            var value = header.Cell(column).GetString().Trim().ToLowerInvariant();
            var normalized = value.Replace("_", " ").Trim();
            if (normalized is "question" or "prompt" or "câu h?i" or "cau hoi") columns["Question"] = column;
            else if (normalized is "a" or "option a" or "choice a") columns["A"] = column;
            else if (normalized is "b" or "option b" or "choice b") columns["B"] = column;
            else if (normalized is "c" or "option c" or "choice c") columns["C"] = column;
            else if (normalized is "d" or "option d" or "choice d") columns["D"] = column;
            else if (normalized is "correct answer" or "answer" or "correct option" or "?áp án ?úng" or "dap an dung") columns["Correct"] = column;
        }

        var requiredHeaders = new[] { "Question", "A", "B", "C", "D", "Correct" };
        var missing = requiredHeaders.Where(name => !columns.ContainsKey(name)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"Missing question columns: {string.Join(", ", missing.Select(name => name == "Correct" ? "Correct Answer" : name))}.");

        var questions = new List<ListeningQuestionViewModel>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var question = new ListeningQuestionViewModel
            {
                Question = row.Cell(columns["Question"]).GetString().Trim(),
                OptionA = row.Cell(columns["A"]).GetString().Trim(),
                OptionB = row.Cell(columns["B"]).GetString().Trim(),
                OptionC = row.Cell(columns["C"]).GetString().Trim(),
                OptionD = row.Cell(columns["D"]).GetString().Trim()
            };
            var correct = row.Cell(columns["Correct"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(question.Question) && string.IsNullOrWhiteSpace(correct)) continue;

            if (string.IsNullOrWhiteSpace(question.Question) ||
                string.IsNullOrWhiteSpace(question.OptionA) || string.IsNullOrWhiteSpace(question.OptionB) ||
                string.IsNullOrWhiteSpace(question.OptionC) || string.IsNullOrWhiteSpace(question.OptionD))
                throw new InvalidDataException($"Question row {rowIndex + 1} is missing the question or one of its four options.");

            question.CorrectOption = new[] { "A", "B", "C", "D" }
                .FirstOrDefault(letter => letter.Equals(correct, StringComparison.OrdinalIgnoreCase)) ??
                (question.OptionA.Equals(correct, StringComparison.OrdinalIgnoreCase) ? "A" :
                 question.OptionB.Equals(correct, StringComparison.OrdinalIgnoreCase) ? "B" :
                 question.OptionC.Equals(correct, StringComparison.OrdinalIgnoreCase) ? "C" :
                 question.OptionD.Equals(correct, StringComparison.OrdinalIgnoreCase) ? "D" : string.Empty);

            if (string.IsNullOrEmpty(question.CorrectOption))
                throw new InvalidDataException($"Question row {rowIndex + 1}: Correct Answer must be A, B, C, D, or exactly match an option.");

            questions.Add(question);
        }

        if (questions.Count == 0) throw new InvalidDataException("No complete questions were found in the workbook.");
        return questions;
    }

    private static List<ListeningQuestionViewModel> DeserializeQuestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ListeningQuestionViewModel>();
        try
        {
            return JsonSerializer.Deserialize<List<ListeningQuestionViewModel>>(json) ?? new List<ListeningQuestionViewModel>();
        }
        catch (JsonException)
        {
            return new List<ListeningQuestionViewModel>();
        }
    }
}
