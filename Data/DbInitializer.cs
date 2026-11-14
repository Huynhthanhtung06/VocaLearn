using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VocaLearn.Models;

namespace VocaLearn.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await context.Database.EnsureCreatedAsync();

        // Add columns introduced after the initial database was created.
        // EnsureCreated does not update an existing database schema.
        if (context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var connection = context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info('Vocabularies');";
                var hasLessonColumn = false;
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        if (string.Equals(reader.GetString(1), "LessonName", StringComparison.OrdinalIgnoreCase))
                        {
                            hasLessonColumn = true;
                            break;
                        }
                    }
                }

                if (!hasLessonColumn)
                {
                    await using var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Vocabularies ADD COLUMN LessonName TEXT NOT NULL DEFAULT 'Lesson 1';";
                    await alterCommand.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }

        if (context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS ListeningLessons (
                    Id INTEGER NOT NULL CONSTRAINT PK_ListeningLessons PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Description TEXT NULL,
                    AudioStoredName TEXT NOT NULL,
                    AudioOriginalName TEXT NOT NULL,
                    QuestionStoredName TEXT NULL,
                    QuestionOriginalName TEXT NULL,
                    QuestionsJson TEXT NULL,
                    UserId TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_ListeningLessons_UserId_CreatedAt
                    ON ListeningLessons (UserId, CreatedAt);
                """);

            var connection = context.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen) await connection.OpenAsync();
            try
            {
                await using var columnsCommand = connection.CreateCommand();
                columnsCommand.CommandText = "PRAGMA table_info('ListeningLessons');";
                var hasQuestionsColumn = false;
                await using (var reader = await columnsCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        if (string.Equals(reader.GetString(1), "QuestionsJson", StringComparison.OrdinalIgnoreCase))
                        {
                            hasQuestionsColumn = true;
                            break;
                        }
                    }
                }

                if (!hasQuestionsColumn)
                {
                    await using var alterCommand = connection.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE ListeningLessons ADD COLUMN QuestionsJson TEXT NULL;";
                    await alterCommand.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (!wasOpen) await connection.CloseAsync();
            }
        }
        else
        {
            await context.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.ListeningLessons', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.ListeningLessons (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ListeningLessons PRIMARY KEY,
                        Title nvarchar(150) NOT NULL,
                        Description nvarchar(1000) NULL,
                        AudioStoredName nvarchar(100) NOT NULL,
                        AudioOriginalName nvarchar(255) NOT NULL,
                        QuestionStoredName nvarchar(100) NULL,
                        QuestionOriginalName nvarchar(255) NULL,
                        QuestionsJson nvarchar(max) NULL,
                        UserId nvarchar(450) NOT NULL,
                        CreatedAt datetime2 NOT NULL
                    );
                    CREATE INDEX IX_ListeningLessons_UserId_CreatedAt
                        ON dbo.ListeningLessons (UserId, CreatedAt);
                END
                ELSE IF COL_LENGTH(N'dbo.ListeningLessons', N'QuestionsJson') IS NULL
                    ALTER TABLE dbo.ListeningLessons ADD QuestionsJson nvarchar(max) NULL;
                """);
        }

        // Check if demo user already exists
        const string demoEmail = "demo@vocalearn.com";
        var demoUser = await userManager.FindByEmailAsync(demoEmail);

        if (demoUser == null)
        {
            demoUser = new ApplicationUser
            {
                UserName = demoEmail,
                Email = demoEmail,
                FullName = "Alex Morgan",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            };

            var createResult = await userManager.CreateAsync(demoUser, "P@ssword123!");
            if (!createResult.Succeeded)
            {
                return;
            }
        }

        // Seed Categories for Demo User
        if (!await context.Categories.AnyAsync(c => c.UserId == demoUser.Id))
        {
            var categories = new List<Category>
            {
                new() { Name = "Basic Vocabulary", Description = "Everyday essential words for conversation", Color = "#3B82F6", UserId = demoUser.Id, CreatedAt = DateTime.UtcNow.AddDays(-20) },
                new() { Name = "Business & Career", Description = "Professional workplace and corporate terminology", Color = "#38BDF8", UserId = demoUser.Id, CreatedAt = DateTime.UtcNow.AddDays(-19) },
                new() { Name = "Technology & AI", Description = "Modern computing, software and artificial intelligence", Color = "#8B5CF6", UserId = demoUser.Id, CreatedAt = DateTime.UtcNow.AddDays(-18) },
                new() { Name = "Travel & Culture", Description = "Words for exploring international destinations and cultures", Color = "#F59E0B", UserId = demoUser.Id, CreatedAt = DateTime.UtcNow.AddDays(-17) },
                new() { Name = "Nature & Environment", Description = "Ecology, green initiatives and the natural world", Color = "#22C55E", UserId = demoUser.Id, CreatedAt = DateTime.UtcNow.AddDays(-16) }
            };

            await context.Categories.AddRangeAsync(categories);
            await context.SaveChangesAsync();

            var basicCat = categories[0];
            var bizCat = categories[1];
            var techCat = categories[2];
            var travelCat = categories[3];
            var natureCat = categories[4];

            // Seed 30+ Vocabularies
            var vocabularies = new List<Vocabulary>
            {
                // Basic
                new() { Word = "Achieve", Meaning = "Đạt được", Example = "She worked relentlessly to achieve her lifelong dreams.", Pronunciation = "/əˈtʃiːv/", CategoryId = basicCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 5, CorrectCount = 5, LastReviewedAt = DateTime.UtcNow.AddHours(-12) },
                new() { Word = "Inspire", Meaning = "Truyền cảm hứng", Example = "Great leaders inspire action through clear communication.", Pronunciation = "/ɪnˈspaɪər/", CategoryId = basicCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 4, CorrectCount = 4, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Persevere", Meaning = "Kiên trì", Example = "You must persevere even when challenges seem insurmountable.", Pronunciation = "/ˌpɜːrsəˈvɪr/", CategoryId = basicCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NeedsReview, NeedsPractice = true, ReviewCount = 3, CorrectCount = 1, LastReviewedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Word = "Fluency", Meaning = "Sự lưu loát", Example = "Consistent practice leads to spoken English fluency.", Pronunciation = "/ˈfluːənsi/", CategoryId = basicCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learning, ReviewCount = 2, CorrectCount = 2, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Resilience", Meaning = "Khả năng phục hồi, kiên cường", Example = "Emotional resilience helps you bounce back from setbacks.", Pronunciation = "/rɪˈzɪliəns/", CategoryId = basicCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NeedsReview, NeedsPractice = true, ReviewCount = 3, CorrectCount = 2, LastReviewedAt = DateTime.UtcNow.AddHours(-5) },
                new() { Word = "Dedicate", Meaning = "Cống hiến, tận tâm", Example = "He chose to dedicate his weekend to learning new skills.", Pronunciation = "/ˈdedɪkeɪt/", CategoryId = basicCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NotLearned, ReviewCount = 0, CorrectCount = 0 },

                // Business
                new() { Word = "Negotiate", Meaning = "Đàm phán", Example = "The executives met in Zurich to negotiate a partnership.", Pronunciation = "/nɪˈɡoʊʃieɪt/", CategoryId = bizCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 6, CorrectCount = 5, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Stakeholder", Meaning = "Bên liên quan", Example = "Always align project milestones with key stakeholders.", Pronunciation = "/ˈsteɪkhoʊldər/", CategoryId = bizCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learning, ReviewCount = 2, CorrectCount = 1, LastReviewedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Word = "Synergy", Meaning = "Sự cộng hưởng, hợp lực", Example = "The merger created synergy between engineering and design.", Pronunciation = "/ˈsɪnərdʒi/", CategoryId = bizCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NeedsReview, NeedsPractice = true, ReviewCount = 3, CorrectCount = 1, LastReviewedAt = DateTime.UtcNow.AddHours(-8) },
                new() { Word = "Benchmark", Meaning = "Điểm chuẩn, tiêu chuẩn đánh giá", Example = "Our product response time serves as the industry benchmark.", Pronunciation = "/ˈbentʃmɑːrk/", CategoryId = bizCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 4, CorrectCount = 4, LastReviewedAt = DateTime.UtcNow.AddDays(-3) },
                new() { Word = "Revenue", Meaning = "Doanh thu", Example = "Quarterly revenue increased by twenty percent this year.", Pronunciation = "/ˈrevənuː/", CategoryId = bizCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 5, CorrectCount = 5, LastReviewedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Word = "Entrepreneur", Meaning = "Doanh nhân khởi nghiệp", Example = "An entrepreneur takes calculated risks to build novel solutions.", Pronunciation = "/ˌɑːntrəprəˈnɜːr/", CategoryId = bizCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NotLearned, ReviewCount = 0, CorrectCount = 0 },

                // Tech
                new() { Word = "Algorithm", Meaning = "Thuật toán", Example = "The search algorithm computes relevance in milliseconds.", Pronunciation = "/ˈælɡərɪðəm/", CategoryId = techCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 7, CorrectCount = 7, LastReviewedAt = DateTime.UtcNow.AddHours(-3) },
                new() { Word = "Autonomous", Meaning = "Tự trị, tự hành", Example = "Autonomous vehicles rely on computer vision models.", Pronunciation = "/ɔːˈtɑːnəməs/", CategoryId = techCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learning, ReviewCount = 3, CorrectCount = 2, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Latency", Meaning = "Độ trễ", Example = "Edge caching significantly reduces network latency.", Pronunciation = "/ˈleɪtnsi/", CategoryId = techCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 4, CorrectCount = 4, LastReviewedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Word = "Scalability", Meaning = "Khả năng mở rộng", Example = "Cloud architecture ensures seamless scalability under high traffic.", Pronunciation = "/ˌskeɪləˈbɪləti/", CategoryId = techCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 5, CorrectCount = 5, LastReviewedAt = DateTime.UtcNow.AddHours(-18) },
                new() { Word = "Encryption", Meaning = "Mã hóa dữ liệu", Example = "End-to-end encryption preserves user privacy across devices.", Pronunciation = "/ɪnˈkrɪpʃn/", CategoryId = techCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NeedsReview, NeedsPractice = true, ReviewCount = 2, CorrectCount = 1, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Bandwidth", Meaning = "Băng thông", Example = "Streaming ultra high definition requires substantial bandwidth.", Pronunciation = "/ˈbændwɪdθ/", CategoryId = techCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NotLearned, ReviewCount = 0, CorrectCount = 0 },

                // Travel
                new() { Word = "Itinerary", Meaning = "Lịch trình chuyến đi", Example = "We drafted an exciting itinerary covering three historic cities.", Pronunciation = "/aɪˈtɪnəreri/", CategoryId = travelCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 4, CorrectCount = 4, LastReviewedAt = DateTime.UtcNow.AddDays(-3) },
                new() { Word = "Hospitality", Meaning = "Lòng hiếu khách, ngành khách sạn", Example = "The islanders are celebrated for their warm hospitality.", Pronunciation = "/ˌhɑːspɪˈtæləti/", CategoryId = travelCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learning, ReviewCount = 3, CorrectCount = 2, LastReviewedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Word = "Picturesque", Meaning = "Đẹp như tranh vẽ", Example = "The alpine village was picturesque covered in morning snow.", Pronunciation = "/ˌpɪktʃəˈresk/", CategoryId = travelCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NeedsReview, NeedsPractice = true, ReviewCount = 2, CorrectCount = 1, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Excursion", Meaning = "Chuyến tham quan, dã ngoại", Example = "The cruise passengers booked an excursion to ancient ruins.", Pronunciation = "/ɪkˈskɜːrʒn/", CategoryId = travelCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NotLearned, ReviewCount = 0, CorrectCount = 0 },
                new() { Word = "Immerse", Meaning = "Đắm chìm, hòa nhập", Example = "Travelers should immerse themselves in local customs.", Pronunciation = "/ɪˈmɜːrs/", CategoryId = travelCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learning, ReviewCount = 2, CorrectCount = 2, LastReviewedAt = DateTime.UtcNow.AddDays(-4) },

                // Nature
                new() { Word = "Environment", Meaning = "Môi trường", Example = "We must take immediate action to protect the environment.", Pronunciation = "/ɪnˈvaɪrənmənt/", CategoryId = natureCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 5, CorrectCount = 5, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Biodiversity", Meaning = "Đa dạng sinh học", Example = "Tropical rainforests support breathtaking global biodiversity.", Pronunciation = "/ˌbaɪoʊdaɪˈvɜːrsəti/", CategoryId = natureCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 3, CorrectCount = 3, LastReviewedAt = DateTime.UtcNow.AddDays(-2) },
                new() { Word = "Conservation", Meaning = "Bảo tồn thiên nhiên", Example = "Wildlife conservation preserves endangered habitats worldwide.", Pronunciation = "/ˌkɑːnsərˈveɪʃn/", CategoryId = natureCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learning, ReviewCount = 3, CorrectCount = 2, LastReviewedAt = DateTime.UtcNow.AddDays(-3) },
                new() { Word = "Sustainable", Meaning = "Bền vững", Example = "Solar and wind energy provide sustainable power sources.", Pronunciation = "/səˈsteɪnəbl/", CategoryId = natureCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NeedsReview, NeedsPractice = true, ReviewCount = 2, CorrectCount = 1, LastReviewedAt = DateTime.UtcNow.AddHours(-10) },
                new() { Word = "Ecosystem", Meaning = "Hệ sinh thái", Example = "Coral reefs form a fragile and vibrant aquatic ecosystem.", Pronunciation = "/ˈiːkoʊsɪstəm/", CategoryId = natureCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.Learned, ReviewCount = 4, CorrectCount = 4, LastReviewedAt = DateTime.UtcNow.AddDays(-1) },
                new() { Word = "Pollution", Meaning = "Ô nhiễm", Example = "Clean energy vehicles mitigate urban atmospheric pollution.", Pronunciation = "/pəˈluːʃn/", CategoryId = natureCat.Id, UserId = demoUser.Id, Status = VocabularyStatus.NotLearned, ReviewCount = 0, CorrectCount = 0 }
            };

            await context.Vocabularies.AddRangeAsync(vocabularies);
            await context.SaveChangesAsync();

            // Seed 7 Days of Learning Progress
            var now = DateTime.UtcNow.Date;
            var progressRecords = new List<LearningProgress>
            {
                new() { UserId = demoUser.Id, Date = now.AddDays(-6), WordsLearnedCount = 3, WordsReviewedCount = 8, QuizzesTakenCount = 1, CorrectAnswersCount = 9, TotalAnswersCount = 10, UpdatedAt = now.AddDays(-6) },
                new() { UserId = demoUser.Id, Date = now.AddDays(-5), WordsLearnedCount = 2, WordsReviewedCount = 12, QuizzesTakenCount = 2, CorrectAnswersCount = 18, TotalAnswersCount = 20, UpdatedAt = now.AddDays(-5) },
                new() { UserId = demoUser.Id, Date = now.AddDays(-4), WordsLearnedCount = 4, WordsReviewedCount = 15, QuizzesTakenCount = 1, CorrectAnswersCount = 10, TotalAnswersCount = 10, UpdatedAt = now.AddDays(-4) },
                new() { UserId = demoUser.Id, Date = now.AddDays(-3), WordsLearnedCount = 1, WordsReviewedCount = 7, QuizzesTakenCount = 1, CorrectAnswersCount = 8, TotalAnswersCount = 10, UpdatedAt = now.AddDays(-3) },
                new() { UserId = demoUser.Id, Date = now.AddDays(-2), WordsLearnedCount = 3, WordsReviewedCount = 14, QuizzesTakenCount = 2, CorrectAnswersCount = 17, TotalAnswersCount = 20, UpdatedAt = now.AddDays(-2) },
                new() { UserId = demoUser.Id, Date = now.AddDays(-1), WordsLearnedCount = 2, WordsReviewedCount = 11, QuizzesTakenCount = 1, CorrectAnswersCount = 9, TotalAnswersCount = 10, UpdatedAt = now.AddDays(-1) },
                new() { UserId = demoUser.Id, Date = now, WordsLearnedCount = 2, WordsReviewedCount = 6, QuizzesTakenCount = 1, CorrectAnswersCount = 5, TotalAnswersCount = 5, UpdatedAt = now }
            };

            await context.LearningProgresses.AddRangeAsync(progressRecords);

            // Seed a sample QuizResult and details
            var sampleQuiz = new QuizResult
            {
                UserId = demoUser.Id,
                QuizMode = QuizMode.MultipleChoice,
                CategoryId = techCat.Id,
                TotalQuestions = 5,
                CorrectAnswers = 4,
                ScorePercentage = 80.0,
                CompletedAt = DateTime.UtcNow.AddHours(-2)
            };

            await context.QuizResults.AddAsync(sampleQuiz);
            await context.SaveChangesAsync();

            var sampleDetails = new List<QuizDetail>
            {
                new() { QuizResultId = sampleQuiz.Id, VocabularyId = vocabularies[12].Id, QuestionText = "What is the Vietnamese meaning of 'Algorithm'?", CorrectAnswer = "Thuật toán", UserAnswer = "Thuật toán", IsCorrect = true, Explanation = "Algorithm means 'Thuật toán'." },
                new() { QuizResultId = sampleQuiz.Id, VocabularyId = vocabularies[13].Id, QuestionText = "What is the Vietnamese meaning of 'Autonomous'?", CorrectAnswer = "Tự trị, tự hành", UserAnswer = "Tự trị, tự hành", IsCorrect = true, Explanation = "Autonomous means 'Tự trị, tự hành'." },
                new() { QuizResultId = sampleQuiz.Id, VocabularyId = vocabularies[14].Id, QuestionText = "What is the Vietnamese meaning of 'Latency'?", CorrectAnswer = "Độ trễ", UserAnswer = "Độ trễ", IsCorrect = true, Explanation = "Latency means 'Độ trễ'." },
                new() { QuizResultId = sampleQuiz.Id, VocabularyId = vocabularies[15].Id, QuestionText = "What is the Vietnamese meaning of 'Scalability'?", CorrectAnswer = "Khả năng mở rộng", UserAnswer = "Khả năng mở rộng", IsCorrect = true, Explanation = "Scalability means 'Khả năng mở rộng'." },
                new() { QuizResultId = sampleQuiz.Id, VocabularyId = vocabularies[16].Id, QuestionText = "What is the Vietnamese meaning of 'Encryption'?", CorrectAnswer = "Mã hóa dữ liệu", UserAnswer = "Bảo mật mạng", IsCorrect = false, Explanation = "Correct answer is 'Mã hóa dữ liệu'." }
            };

            await context.QuizDetails.AddRangeAsync(sampleDetails);
            await context.SaveChangesAsync();
        }
    }
}
