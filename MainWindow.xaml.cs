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
                        var typeAttr = (string)q.Attribute("type") ?? "";

                        // hole Kommentar-ID
                        string idComment = "";
                        var commentNode = q.NodesBeforeSelf().OfType<XComment>().LastOrDefault();
                        if (commentNode != null && commentNode.Value.Trim().StartsWith("question:"))
                            idComment = commentNode.Value.Trim();

                        // Kategorie und Duplikat-Filter
                        if (typeAttr == "category")
                        {
                        }
                        else if (!string.IsNullOrEmpty(idComment) && _seenQuestionIds.Contains(idComment))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(idComment) && typeAttr != "category")
                            _seenQuestionIds.Add(idComment);

                        // Unterscheide Kategorie vs. normale Frage
                        string nameText, searchText;
                        bool isCategory = false;

                        if (typeAttr == "category")
                        {
                            isCategory = true;
                            // Text aus <category><text>
                            nameText = $"Kategorie: {q.Element("category")?.Element("text")?.Value ?? "<unbekannt>"}";
                            // Beschreibung optional aus <info><text>
                            var infoText = q.Element("info")?.Element("text")?.Value ?? "";
                            searchText = (nameText + " " + infoText).ToLower();
                        }
                        else
                        {
                            // Normal-Fall: Name + Frage + Antworten
                            var nm = q.Element("name")?.Element("text")?.Value ?? "";
                            var qt = q.Element("questiontext")?.Element("text")?.Value ?? "";
                            var ans = string.Join(" ",
                                           q.Elements("answer")
                                            .Select(a => a.Element("text")?.Value ?? ""));
                            nameText = nm;
                            searchText = (nm + " " + qt + " " + ans).ToLower();
                        }

                        var question = new Question
                        {
                            QuestionElement = new XElement(q),
                            Text = nameText,
                            SearchText = searchText,
                            IdComment = idComment,
                            IsCategory = isCategory
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
                var selected = Questions.Where(q => q.IsSelected);

                // Kategorien zuerst
                foreach (var cat in selected.Where(q => q.IsCategory))
                {
                    if (!string.IsNullOrEmpty(cat.IdComment))
                        exportNodes.Add(new XComment(cat.IdComment));
                    exportNodes.Add(cat.QuestionElement);
                }

                // dann die normalen Fragen
                foreach (var qn in selected.Where(q => !q.IsCategory))
                {
                    if (!string.IsNullOrEmpty(qn.IdComment))
                        exportNodes.Add(new XComment(qn.IdComment));
                    exportNodes.Add(qn.QuestionElement);
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
            var result = MessageBox.Show(
                "Möchten Sie die Liste wirklich leeren? (Nur nicht ausgewählte Einträge werden gelöscht)",
                "Bestätigung",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var toRemove = Questions
                    .Where(q => !q.IsSelected)
                    .ToList();

                foreach (var q in toRemove)
                {
                    if (!string.IsNullOrEmpty(q.IdComment))
                    {
                        _seenQuestionIds.Remove(q.IdComment);
                    }

                    Questions.Remove(q);
                }
            }
        }
    }

    public class Question : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isVisible = true;
        private string _searchText;
        public string IdComment { get; set; }
        public bool IsCategory { get; set; }

        // Anzeigename (aus <name><text>)
        public string Text { get; set; }

        // Enthält das komplette XML-Element der Frage
        public XElement QuestionElement { get; set; }

        // Für die Filterung: Kombination aus Name, Frage und Antworten
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