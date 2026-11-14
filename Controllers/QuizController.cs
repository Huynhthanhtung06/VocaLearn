using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.Services;
using VocaLearn.ViewModels;

namespace VocaLearn.Controllers;

[Authorize]
public class QuizController : Controller
{
    private readonly IQuizService _quizService;
    private readonly IVocabularyService _vocabularyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public QuizController(
        IQuizService quizService,
        IVocabularyService vocabularyService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _quizService = quizService;
        _vocabularyService = vocabularyService;
        _userManager = userManager;
        _context = context;
    }

    private string GetUserId() => _userManager.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var categories = await _vocabularyService.GetCategoriesAsync(userId);
        var totalWords = await _context.Vocabularies.CountAsync(v => v.UserId == userId);
        var lessons = await _context.Vocabularies
            .Where(v => v.UserId == userId)
            .Select(v => v.LessonName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var model = new QuizSetupViewModel
        {
            Categories = categories,
            Lessons = lessons,
            AvailableWordCount = totalWords,
            QuestionCount = 10,
            Mode = QuizMode.MultipleChoice
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Play(QuizMode mode = QuizMode.MultipleChoice, int? categoryId = null, int count = 10, string[]? lessonNames = null)
    {
        var userId = GetUserId();
        var session = await _quizService.GenerateQuizAsync(userId, mode, categoryId, count, lessonNames);

        if (session == null || !session.Questions.Any())
        {
            TempData["ErrorMessage"] = "You need at least 2 vocabulary words to start a quiz. Please add words or import from Excel first!";
            return RedirectToAction(nameof(Index));
        }

        return View(session);
    }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] QuizSubmitModel model)
    {
        if (model == null || model.Answers == null || !model.Answers.Any())
        {
            return BadRequest(new { success = false, message = "No answers provided" });
        }

        var userId = GetUserId();
        var result = await _quizService.SubmitQuizAsync(userId, model);

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action(nameof(Result), new { id = result.QuizResultId }),
            result
        });
    }

    [HttpGet]
    public async Task<IActionResult> Result(int id)
    {
        var userId = GetUserId();
        var result = await _quizService.GetQuizResultAsync(id, userId);

        if (result == null)
        {
            return NotFound();
        }

        return View(result);
    }

    [HttpGet]
    public async Task<IActionResult> Retry(int id)
    {
        var userId = GetUserId();
        var session = await _quizService.GenerateRetryQuizAsync(id, userId);

        if (session == null || !session.Questions.Any())
        {
            TempData["SuccessMessage"] = "You got 100% correct in that quiz! No questions need retrying.";
            return RedirectToAction(nameof(Result), new { id });
        }

        return View("Play", session);
    }

    [HttpGet]
    public async Task<IActionResult> History()
    {
        var userId = GetUserId();
        var history = await _quizService.GetQuizHistoryAsync(userId, 50);
        return View(history);
    }
}
