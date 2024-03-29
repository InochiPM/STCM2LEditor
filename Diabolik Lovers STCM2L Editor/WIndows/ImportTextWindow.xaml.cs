﻿using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace STCM2LEditor
{
    /// <summary>
    /// Логика взаимодействия для ImportTextWindow.xaml
    /// </summary>
    public partial class ImportTextWindow : Window
    {
        public ImportTextWindow()
        {
            InitializeComponent();
        }

        private void OpenFileCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt"
            };
            if ((bool)!ofd.ShowDialog())
            {
                return;
            }
            LoadTextDocument(ofd.FileName);
        }
        public IEnumerable<string> Texts
        {
            get => new TextRange(RichTextBox1.Document.ContentStart, RichTextBox1.Document.ContentEnd)
.Text.Split(new char[] { '\n' }).Select(x => x.Trim()).Where(x => x.Length > 0)
.Select(x => x.Substring(x.IndexOf(':') + 1)).Select(x => x.Trim());
            set
            {
                var range = new TextRange(RichTextBox1.Document.ContentStart, RichTextBox1.Document.ContentEnd);
                range.Text = string.Join("\n", value);
            }
        }
        private void LoadTextDocument(string fileName)
        {
            TextRange range;
            System.IO.FileStream fStream;
            if (System.IO.File.Exists(fileName))
            {
                range = new TextRange(RichTextBox1.Document.ContentStart, RichTextBox1.Document.ContentEnd);
                fStream = new System.IO.FileStream(fileName, System.IO.FileMode.OpenOrCreate);
                range.Load(fStream, System.Windows.DataFormats.Text);
                fStream.Close();
            }
        }
    }
}
