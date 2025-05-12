using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using System.Collections.Generic;

namespace QuizFilterApp
{
    public partial class MainWindow : Window
    {
        // HashSet zum Tracken bereits geladener Kommentar-IDs
        private readonly HashSet<string> _seenQuestionIds = new HashSet<string>();

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

                    var questionElements = doc.Descendants("question");

                    foreach (var q in questionElements)
                    {
                        // Hole Kommentar direkt über der Frage
                        string idComment = "";
                        var commentNode = q.NodesBeforeSelf().OfType<XComment>().LastOrDefault();
                        if (commentNode != null && commentNode.Value.Trim().StartsWith("question:"))
                        {
                            idComment = commentNode.Value.Trim();
                        }

                        // Prüfe auf Duplikat
                        if (!string.IsNullOrEmpty(idComment) && _seenQuestionIds.Contains(idComment))
                        {
                            continue;
                        }

                        // Wenn neue ID → hinzufügen zur Liste
                        if (!string.IsNullOrEmpty(idComment))
                        {
                            _seenQuestionIds.Add(idComment);
                        }

                        // Beispiel: Extrahiere Daten aus Frage
                        string nameText = q.Element("name")?.Element("text")?.Value ?? "";
                        string questionText = q.Element("questiontext")?.Element("text")?.Value ?? "";
                        string answersText = string.Join(", ",
                            q.Elements("answer").Select(a => a.Element("text")?.Value ?? ""));

                        string searchText = (nameText + " " + questionText + " " + answersText).ToLower();

                        var question = new Question
                        {
                            QuestionElement = new XElement(q),
                            Text = nameText,
                            SearchText = searchText,
                            IdComment = idComment
                        };

                        Questions.Add(question);
                    }
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
                if (!(item is Question q))
                    return false;

                // kein Filter-Text → alle anzeigen
                if (string.IsNullOrEmpty(filter))
                    return true;

                // Suche in SearchText case-insensitive
                return q.SearchText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            view.Refresh();
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
        public string IdComment { get; set; }

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

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }
}