namespace VocaLearn.Models;

public enum VocabularyStatus
{
    NotLearned = 0,
    Learning = 1,
    Learned = 2,
    NeedsReview = 3
}

public enum QuizMode
{
    MultipleChoice = 1,
    ReverseMultipleChoice = 2,
    FillInTheBlank = 3,
    Translation = 4
}
