using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;

namespace QuizFilterApp
{
    public partial class QuestionDetailWindow : Window
    {
        public QuestionDetailWindow(Question question)
        {
            InitializeComponent();

            var viewModel = new QuestionDetailViewModel
            {
                Name = StripHtml(question.QuestionElement.Element("name")?.Element("text")?.Value ?? string.Empty),
                QuestionText = StripHtml(question.QuestionElement.Element("questiontext")?.Element("text")?.Value ?? string.Empty)
            };

            var subQuestions = question.QuestionElement.Descendants("subquestion");
            if (subQuestions.Any())
            {
                foreach (var sub in subQuestions)
                {
                    string subText = StripHtml(sub.Element("text")?.Value ?? string.Empty);
                    string answerText = StripHtml(sub.Element("answer")?.Element("text")?.Value ?? string.Empty);
                    viewModel.SubQuestions.Add(new SubQuestion { Text = subText, Answer = answerText });
                }
            }
            else
            {
                var answers = question.QuestionElement.Elements("answer");
                foreach (var answer in answers)
                {
                    string answerText = StripHtml(answer.Element("text")?.Value ?? string.Empty);
                    viewModel.SubQuestions.Add(new SubQuestion { Text = answerText, Answer = string.Empty });
                }
            }

            DataContext = viewModel;
        }

        private string StripHtml(string input)
        {
            return string.IsNullOrEmpty(input)
                ? input
                : Regex.Replace(input, "<.*?>", string.Empty);
        }
    }

    public class QuestionDetailViewModel
    {
        public string Name { get; set; }
        public string QuestionText { get; set; }
        public ObservableCollection<SubQuestion> SubQuestions { get; set; } = new ObservableCollection<SubQuestion>();
    }

    public class SubQuestion
    {
        // Bei Matching-Fragen ist 'Text' die Unterfrage und 'Answer' die zugehörige Antwort.
        // Bei Multiple-Choice/TrueFalse werden hier nur die Antworttexte in 'Text' abgelegt.
        public string Text { get; set; }

        public string Answer { get; set; }
    }
}