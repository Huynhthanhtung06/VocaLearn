# VocaLearn – Modern English Vocabulary Learning Platform

VocaLearn is a full-featured, responsive English vocabulary learning SaaS web application built with **ASP.NET Core MVC**, **Entity Framework Core**, and **ASP.NET Core Identity**. It delivers an interactive, modern learning experience with 3D flashcards, multi-mode quizzes, Excel bulk imports via ClosedXML, and real-time learning analytics with Chart.js.

---

## 🎨 Midnight Blue Design Theme

VocaLearn uses a dark SaaS palette:
- **Main Background**: `#0B1120`
- **Sidebar Background**: `#111C30`
- **Card Background**: `#17243A`
- **Primary Blue**: `#3B82F6`
- **Accent Cyan**: `#38BDF8`
- **Main Text**: `#F8FAFC`
- **Secondary Text**: `#94A3B8`
- **Success**: `#22C55E`
- **Error / Danger**: `#EF4444`

---

## 🚀 Key Features

### 1. Multi-User Authentication & Isolation
- User registration, login, logout, profile management, and password change.
- Powered by **ASP.NET Core Identity** with cookie authentication and server-side validation.
- Complete data privacy: every user only accesses their own categories, vocabulary library, flashcard progress, and quiz history.

### 2. Interactive Dashboard
- Total vocabulary count, words mastered, words in learning, and words needing review.
- Live **Chart.js** 7-day daily activity chart (words reviewed vs quizzes taken).
- Vocabulary mastery status doughnut chart.
- Quick action shortcuts (Start Flashcards, Quick Quiz, Import Excel, Add Word).
- Recent vocabulary and recent quiz performance lists.

### 3. Vocabulary Management (CRUD)
- Search by English word, Vietnamese translation, or example sentence.
- Filter by category, learning status (`NotLearned`, `Learning`, `Learned`, `NeedsReview`), and "Need More Practice" flag.
- Add, edit, view details, and delete vocabulary items.
- Full category management with custom theme colors and word counters.
- Built-in **Web Speech API** text-to-speech (TTS) pronunciation audio on all words.

### 4. Excel Import with ClosedXML
- Bulk upload `.xlsx` spreadsheets with columns:
  | English | Vietnamese | Example | Category |
- File validation (type, size, format).
- Full data preview table before committing.
- Duplicate detection against existing user vocabulary.
- Automatic creation of new categories found in the spreadsheet.
- One-click downloadable sample Excel template (`VocaLearn_Template.xlsx`).

### 5. 3D Flashcards Learning Engine
- 3D CSS perspective card flip (`rotateY(180deg)`).
- Front: English word, phonetic pronunciation (IPA), audio speaker button, category badge.
- Back: Vietnamese meaning, example sentence in context.
- **"I Know This Word"** and **"Need More Practice"** tracking buttons.
- Words marked as needing practice are prioritized in upcoming sessions.
- Full keyboard navigation: `Spacebar` to flip, `Arrow Left/Right` to navigate, `1` for Practice, `2` for Mastered.
- Real-time progress bar (`Word X of Y`).

### 6. Four Quiz Modes
1. **Multiple Choice**: Select the correct Vietnamese meaning from 4 options.
2. **Reverse Multiple Choice**: Given a Vietnamese meaning, select the correct English word.
3. **Fill in the Blank**: Complete the target word in an authentic context sentence.
4. **Active Translation**: Type the Vietnamese translation directly with intelligent token matching.
- Selectable question count (5, 10, 15, 20 questions) and category filter.
- Instant correct/incorrect visual feedback with explanations.
- Automatic scoring, history recording, and **"Retry Incorrect Questions"** functionality.

### 7. Learning Progress & Analytics
- Vocabulary mastery rate percentage.
- 7-day learning and review trend line chart.
- Topic mastery breakdown progress bars by category.
- Comprehensive quiz history table with details review.

---

## 🛠️ Technology Stack

- **Platform**: ASP.NET Core MVC with C#
- **Target Framework**: .NET 9 (`net9.0`)
- **ORM**: Entity Framework Core (`Microsoft.EntityFrameworkCore.SqlServer` & `Microsoft.EntityFrameworkCore.Sqlite`)
- **Authentication**: ASP.NET Core Identity
- **Excel Processing**: ClosedXML (`0.105.1`)
- **Charts**: Chart.js
- **Icons**: Lucide Icons
- **Typography**: Inter (Google Fonts)

---

## 📂 Project Architecture

```
VocaLearn/
├── Controllers/
│   ├── HomeController.cs        # Dashboard & Overview
│   ├── AccountController.cs     # Auth, Register, Login, Profile, Password
│   ├── VocabularyController.cs  # Vocabulary CRUD, search, filter
│   ├── CategoryController.cs    # Categories management
│   ├── FlashcardsController.cs  # 3D Flashcard study session
│   ├── QuizController.cs        # Quiz generator, 4 quiz modes, scoring, retry
│   ├── ProgressController.cs    # Learning analytics & charts
│   └── ImportController.cs      # ClosedXML upload, preview, template download
├── Models/
│   ├── ApplicationUser.cs       # Identity user extension
│   ├── Category.cs              # Vocabulary topic categories
│   ├── Vocabulary.cs            # Core vocabulary word entity
│   ├── LearningProgress.cs      # Daily activity metrics
│   ├── QuizResult.cs            # Quiz session result header
│   ├── QuizDetail.cs            # Question-by-question breakdown
│   └── Enums.cs                 # VocabularyStatus, QuizMode
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core DbContext with Fluent API mappings
│   └── DbInitializer.cs         # Database creation & rich initial seed data
├── Services/
│   ├── IVocabularyService.cs    # Vocabulary & Category business logic
│   ├── IFlashcardService.cs     # Flashcard state & review priority
│   ├── IQuizService.cs          # Multi-mode question generation & grading
│   ├── IProgressService.cs      # Analytics computation & daily tracking
│   ├── IExcelImportService.cs   # ClosedXML parsing, preview, and template
│   └── SystemVerification.cs    # Automated end-to-end verification suite
├── ViewModels/                  # Clean view models for each controller
├── Views/                       # Dark theme Razor views with Lucide & Chart.js
└── wwwroot/
    ├── css/site.css             # Midnight Blue SaaS design system & 3D flip CSS
    └── js/site.js               # Text-to-speech audio & interactive helpers
```

---

## ⚡ Quick Start & Run

### 1. Run via CLI
Open a terminal in `VocaLearn`:
```bash
dotnet run
```
Navigate to:
```
http://localhost:5058
```

### 2. Open in Visual Studio
1. Open Visual Studio.
2. Select **Open a project or solution**.
3. Choose `VocaLearn.csproj`.
4. Press `F5` or `Ctrl + F5` to run.

---

## 🔑 Pre-seeded Demo Credentials

A demo account is automatically initialized with 29 vocabulary words across 5 categories, 7 days of learning activity, and sample quiz history:
- **Email**: `demo@vocalearn.com`
- **Password**: `P@ssword123!`

*(You can also register a brand new account at `/Account/Register`)*

---

## 🗄️ Database Configuration

By default, the application runs on **SQLite** (`Data Source=vocalearn.db`) for immediate zero-config execution. 

To use **Microsoft SQL Server / LocalDB**, update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=vocalearn.db",
    "SqlServerConnection": "Server=(localdb)\\mssqllocaldb;Database=VocaLearnDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "DatabaseProvider": "SqlServer"
}
```
Entity Framework Core will automatically ensure the database and tables are created.

---

## 🧪 Run Automated Verification Suite

To verify all services, CRUD operations, ClosedXML parsing, and quiz modes:
```bash
dotnet run -- --verify
```
All assertions will execute and output passing test results.
