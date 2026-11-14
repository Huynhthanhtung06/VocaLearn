using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using VocaLearn.Models;

namespace VocaLearn.ViewModels;

public class ListeningUploadViewModel
{
    [Required, MaxLength(150)]
    [Display(Name = "Listening title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose an audio file.")]
    public IFormFile? AudioFile { get; set; }

    public IFormFile? QuestionFile { get; set; }
}

public class ListeningIndexViewModel
{
    public ListeningUploadViewModel Upload { get; set; } = new();
    public List<ListeningExerciseViewModel> Lessons { get; set; } = new();
}

public class ListeningExerciseViewModel
{
    public ListeningLesson Lesson { get; set; } = new();
    public List<ListeningQuestionViewModel> Questions { get; set; } = new();
}

public class ListeningQuestionViewModel
{
    public string Question { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = string.Empty;
}
