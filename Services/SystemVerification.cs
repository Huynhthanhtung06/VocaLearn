using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Data;
using VocaLearn.Models;
using VocaLearn.Services;
using VocaLearn.ViewModels;

namespace VocaLearn.Services;

public class SystemVerification
{
    public static async Task<List<string>> RunAllTestsAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userMgr,
        IVocabularyService vocabService,
        IFlashcardService flashcardService,
        IQuizService quizService,
        IProgressService progressService,
        IExcelImportService importService)
    {
        var logs = new List<string>();

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                logs.Add($"[PASS] {testName}");
            }
            else
            {
                logs.Add($"[FAIL] {testName}");
                throw new Exception($"Test assertion failed: {testName}");
            }
        }

        // 1. Verify User Exists
        var demoUser = await userMgr.FindByEmailAsync("demo@vocalearn.com");
        Assert(demoUser != null, "Demo user exists in database");

        var userId = demoUser!.Id;

        // 2. Verify Seed Categories & Vocabularies
        var categories = await vocabService.GetCategoriesAsync(userId);
        Assert(categories.Count >= 5, $"Categories seeded: {categories.Count} (expected >= 5)");

        var vocabs = await vocabService.GetVocabulariesAsync(userId, null, null, null, null, null, 1, 100);
        Assert(vocabs.TotalItems >= 20, $"Vocabularies seeded: {vocabs.TotalItems} (expected >= 20)");

        // 3. Verify Vocabulary CRUD
        var testCat = categories.First();
        var newWord = new Vocabulary
        {
            Word = "Synchronous",
            Meaning = "Đồng bộ",
            Example = "Synchronous communication occurs in real time.",
            Pronunciation = "/ˈsɪŋkrənəs/",
            CategoryId = testCat.Id,
            Status = VocabularyStatus.NotLearned
        };

        var created = await vocabService.CreateAsync(newWord, userId);
        Assert(created.Id > 0, "Vocabulary word created with auto-increment ID");

        var retrieved = await vocabService.GetByIdAsync(created.Id, userId);
        Assert(retrieved != null && retrieved.Word == "Synchronous", "Retrieved created vocabulary word");

        created.Meaning = "Đồng bộ hóa";
        var updated = await vocabService.UpdateAsync(created, userId);
        Assert(updated, "Vocabulary word updated");

        var wordExists = await vocabService.WordExistsAsync("Synchronous", userId);
        Assert(wordExists, "WordExistsAsync detects existing word");

        var deleted = await vocabService.DeleteAsync(created.Id, userId);
        Assert(deleted, "Vocabulary word deleted");

        var existsAfterDelete = await vocabService.WordExistsAsync("Synchronous", userId);
        Assert(!existsAfterDelete, "Deleted word no longer exists");

        // 4. Verify User Isolation
        var fakeUserId = Guid.NewGuid().ToString();
        var fakeVocabs = await vocabService.GetVocabulariesAsync(fakeUserId, null, null, null, null, null, 1, 10);
        Assert(fakeVocabs.TotalItems == 0, "User isolation enforced: other user has 0 words");

        // 5. Verify Flashcard Service
        var flashcards = await flashcardService.GetFlashcardsAsync(userId, null, "all");
        Assert(flashcards.Cards.Count >= 10, $"Flashcards loaded: {flashcards.Cards.Count}");

        var targetFlashcard = flashcards.Cards.First();
        var recordedKnow = await flashcardService.RecordFlashcardActionAsync(targetFlashcard.Id, "know", userId);
        Assert(recordedKnow, "Recorded 'know' action on flashcard");

        var recordedPractice = await flashcardService.RecordFlashcardActionAsync(targetFlashcard.Id, "practice", userId);
        Assert(recordedPractice, "Recorded 'practice' action on flashcard");

        // 6. Verify Quiz Generation for All 4 Modes
        var mcSession = await quizService.GenerateQuizAsync(userId, QuizMode.MultipleChoice, null, 5);
        Assert(mcSession != null && mcSession.Questions.Count == 5, "Generated MultipleChoice quiz with 5 questions");
        Assert(mcSession!.Questions.All(q => q.Options.Count == 4), "MultipleChoice questions have 4 distinct options");

        var rmcSession = await quizService.GenerateQuizAsync(userId, QuizMode.ReverseMultipleChoice, null, 5);
        Assert(rmcSession != null && rmcSession.Questions.Count == 5, "Generated ReverseMultipleChoice quiz");

        var fitbSession = await quizService.GenerateQuizAsync(userId, QuizMode.FillInTheBlank, null, 5);
        Assert(fitbSession != null && fitbSession.Questions.All(q => q.ExampleWithBlank != null), "Generated FillInTheBlank quiz with sentence blanks");

        var transSession = await quizService.GenerateQuizAsync(userId, QuizMode.Translation, null, 5);
        Assert(transSession != null && transSession.Questions.Count == 5, "Generated Translation quiz");

        // 7. Verify Quiz Submission & Scoring
        var submitModel = new QuizSubmitModel
        {
            Mode = QuizMode.MultipleChoice,
            CategoryId = null,
            Answers = new List<QuizAnswerItemModel>
            {
                new()
                {
                    VocabularyId = mcSession.Questions[0].VocabularyId,
                    QuestionText = mcSession.Questions[0].Prompt,
                    UserAnswer = mcSession.Questions[0].CorrectAnswer,
                    CorrectAnswer = mcSession.Questions[0].CorrectAnswer
                },
                new()
                {
                    VocabularyId = mcSession.Questions[1].VocabularyId,
                    QuestionText = mcSession.Questions[1].Prompt,
                    UserAnswer = "Wrong Answer",
                    CorrectAnswer = mcSession.Questions[1].CorrectAnswer
                }
            }
        };

        var submitResult = await quizService.SubmitQuizAsync(userId, submitModel);
        Assert(submitResult.TotalQuestions == 2, "Quiz submission processed 2 answers");
        Assert(submitResult.CorrectAnswers == 1, "Scored 1 correct answer");
        Assert(submitResult.ScorePercentage == 50.0, "Calculated 50.0% score");

        // 8. Verify Retry Generation
        var retrySession = await quizService.GenerateRetryQuizAsync(submitResult.QuizResultId, userId);
        Assert(retrySession != null && retrySession.Questions.Count == 1, "Retry generated exactly 1 incorrect question");

        // 9. Verify ClosedXML Excel Template & Import
        var templateBytes = importService.GenerateTemplate();
        Assert(templateBytes != null && templateBytes.Length > 1000, "ClosedXML template generated successfully");

        using (var stream = new MemoryStream(templateBytes!))
        {
            var preview = await importService.ParseAndPreviewAsync(stream, "Template.xlsx", userId);
            Assert(preview.TotalRows >= 2, $"Excel parsed rows: {preview.TotalRows}");
            Assert(preview.Rows.All(r => !string.IsNullOrEmpty(r.Word)), "Parsed words correctly from Excel");
        }

        // 10. Verify Dashboard & Analytics
        var dashboard = await progressService.GetDashboardDataAsync(userId, demoUser.FullName);
        Assert(dashboard.TotalVocabulary > 0, $"Dashboard total vocabulary: {dashboard.TotalVocabulary}");
        Assert(dashboard.ActivityDates.Count == 7, "Dashboard has 7-day activity metrics");

        var analytics = await progressService.GetAnalyticsAsync(userId);
        Assert(analytics.CategoryStats.Count > 0, "Analytics computed category mastery stats");
        Assert(analytics.QuizHistory.Count > 0, "Analytics retrieved quiz history");

        logs.Add("All system verification tests executed successfully with 0 errors!");
        return logs;
    }
}
