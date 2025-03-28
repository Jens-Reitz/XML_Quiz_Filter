using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

namespace QuizFilterApp
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Question> Questions { get; set; } = new ObservableCollection<Question>();

        public MainWindow()
        {
            InitializeComponent();
            QuestionsListBox.ItemsSource = Questions;
        }

        private void ImportXml_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    var doc = XDocument.Load(file);
                    var questions = doc.Descendants("question").Select(q =>
                    {
                        var commentNode = q.NodesBeforeSelf().OfType<XComment>().LastOrDefault();
                        string idComment = string.Empty;
                        if (commentNode != null && commentNode.Value.Trim().StartsWith("question:"))
                        {
                            idComment = commentNode.Value.Trim();
                        }

                        string nameText = StripHtml(q.Element("name")?.Element("text")?.Value ?? string.Empty);
                        string questionText = StripHtml(q.Element("questiontext")?.Element("text")?.Value ?? string.Empty);
                        string answersText = string.Empty;

                        if (q.Descendants("subquestion").Any())
                        {
                            answersText = string.Join(" ", q.Descendants("subquestion").Select(sub =>
                                (StripHtml(sub.Element("text")?.Value ?? string.Empty) + " " +
                                 StripHtml(sub.Element("answer")?.Element("text")?.Value ?? string.Empty))
                            ));
                        }
                        else // Andernfalls: direkt alle <answer>-Elemente (Multiple Choice, True/False)
                        {
                            answersText = string.Join(" ", q.Elements("answer").Select(a =>
                                StripHtml(a.Element("text")?.Value ?? string.Empty)
                            ));
                        }

                        string searchText = (nameText + " " + questionText + " " + answersText).ToLower();

                        return new Question
                        {
                            QuestionElement = new XElement(q),
                            Text = nameText,
                            SearchText = searchText,
                            IdComment = idComment
                        };
                    }).ToList();

                    questions.ForEach(q => Questions.Add(q));
                }
            }
        }

        private void ExportQuestions_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = "ExportedQuestions.xml"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var exportNodes = new List<XNode>();
                foreach (var question in Questions.Where(q => q.IsSelected))
                {
                    if (!string.IsNullOrEmpty(question.IdComment))
                    {
                        exportNodes.Add(new XComment(question.IdComment));
                    }
                    exportNodes.Add(question.QuestionElement);
                }

                var exportDoc = new XElement("quiz", exportNodes);
                exportDoc.Save(saveFileDialog.FileName);
                MessageBox.Show("Export abgeschlossen!");
            }
        }

        private void QuestionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QuestionsListBox.SelectedItem is Question selectedQuestion)
            {
                var detailWindow = new QuestionDetailWindow(selectedQuestion);
                detailWindow.ShowDialog();
            }
        }

        private void ApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            string filter = FilterTextBox.Text.ToLower();
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(QuestionsListBox.ItemsSource);
            view.Filter = item =>
            {
                if (item is Question q)
                {
                    return string.IsNullOrEmpty(filter) || q.SearchText.Contains(filter);
                }
                return false;
            };
            view.Refresh();
        }

        private string StripHtml(string input)
        {
            return string.IsNullOrEmpty(input) ? input : Regex.Replace(input, "<.*?>", string.Empty);
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Möchten Sie die Liste wirklich leeren?", "Bestätigung", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Questions.Clear();
            }
        }
    }

    public class Question : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isVisible = true;
        private string _searchText;

        // Anzeigename (aus <name><text>)
        public string Text { get; set; }

        // Enthält das komplette XML-Element der Frage
        public XElement QuestionElement { get; set; }

        // Für die Filterung: Kombination aus Name, Frage und Antworten (alles in Kleinbuchstaben)
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged("SearchText"); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged("IsSelected"); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged("IsVisible"); }
        }

        public string IdComment { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}