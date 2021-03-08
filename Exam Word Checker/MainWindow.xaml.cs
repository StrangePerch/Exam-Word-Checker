using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Exam_Word_Checker.Annotations;
using Microsoft.Win32;
using Application = System.Windows.Forms.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Exam_Word_Checker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private List<Task> tasks = new List<Task>();
        private CancellationTokenSource TokenSource;

        private static Mutex Mutex = null;

        private ObservableCollection<Data> _top;
        public ObservableCollection<Data> Top
        {
            get => _top;
            set
            {
                _top = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();

            
            Mutex = new Mutex(true, "WordCheckerMutex", out var created);

            // This thread owns the mutex only if it both requested 
            // initial ownership and created the named mutex.
            if (!created)
            {
                MessageBox.Show("Only one instance of program is allowed");
                Close();
            }

            Top = new ObservableCollection<Data>();
            DataGridTop.Items.IsLiveSorting = true;
            Resources["Top"] = Top;

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(DataGridTop.ItemsSource);
            view.SortDescriptions.Add(new SortDescription("Count", ListSortDirection.Descending));
        }

        public async Task DriveStart(DriveInfo drive, CustomProgressBar bar)
        {
            //await Task.Run(() => new DiskChecker(drive));
            await Task.Run((() =>
            {
                DiskChecker checker = new DiskChecker(drive);
                checker.Notify += Add;
                Task task = checker.StartScan();
                while (this.Dispatcher.Invoke(() => bar.ProgressBar.Value) < 100)
                {
                    this.Dispatcher.Invoke(() => bar.ProgressBar.Value = checker.percentage);
                    Thread.Sleep(500);
                    if (TokenSource.IsCancellationRequested)
                        return;
                    
                }
            }),TokenSource.Token);
        }

        public void Add(string word, int count, string path)
        {
            this.Dispatcher.Invoke(() =>
            {
                var temp = new Data(word, count);
                if (Top.Contains(temp))
                {
                    Top[Top.IndexOf(temp)].Count += count;
                    OnPropertyChanged(nameof(Top));
                }
                else
                {
                    Top.Add(temp);
                }

                ListView.Items.Add(new Data(word, count, path));
            });
        }

        private void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            AddButton.IsEnabled = false;
            DelButton.IsEnabled = false;
            PickFileButton.IsEnabled = false;
            PickFolderButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            PauseButton.IsEnabled = true;

            Panel.Children.Clear();
            ListView.Items.Clear();

            if (DiskChecker.Pause)
            {
                DiskChecker.Words.Clear();
                foreach (var item in ListBox.Items)
                {
                    DiskChecker.Words.Add(item.ToString());
                }
                DiskChecker.Pause = false;
                return;
            }

            TokenSource = new CancellationTokenSource();
            DiskChecker.TokenSource = TokenSource;


            foreach (var item in ListBox.Items)
            {
                DiskChecker.Words.Add(item.ToString());
            }
            

            foreach (var drive in DriveInfo.GetDrives())
            {
                //if (drive.Name == "F:\\")
                {
                    CustomProgressBar bar = new CustomProgressBar { TextBlock = { Text = drive.Name } };

                    Panel.Children.Add(bar);
                    tasks.Add(DriveStart(drive, bar));
                }
            }
        }

        private void PickFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.ShowDialog();
            try
            {
                if (dialog.FileName != String.Empty)
                {
                    StreamReader reader = new StreamReader(dialog.OpenFile());
                    string text = reader.ReadToEnd();
                    foreach (var word in text.Split(' '))
                    {
                        ListBox.Items.Add(word);
                    }
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.ToString());
            }
            
        }

        private void PickFolderButton_OnClick(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            DiskChecker.Path = dialog.SelectedPath;
        }

        private void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ListBox.Items.Contains(TextBox.Text))
            {
                ListBox.Items.Add(TextBox.Text);
                TextBox.Text = String.Empty;
            }
        }

        private void DelButton_OnClick(object sender, RoutedEventArgs e)
        {
            ListBox.Items.Remove(ListBox.SelectedItem);
        }

        private void StopButton_OnClick(object sender, RoutedEventArgs e)
        {
            TokenSource.Cancel();
            StopButton.IsEnabled = false;
            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            PickFileButton.IsEnabled = true;
            PickFolderButton.IsEnabled = true;
            AddButton.IsEnabled = true;
            DelButton.IsEnabled = true;
        }

        private void PauseButton_OnClick(object sender, RoutedEventArgs e)
        {
            DiskChecker.Pause = true;
            StopButton.IsEnabled = false;
            StartButton.IsEnabled = true;
            PauseButton.IsEnabled = false;
            PickFileButton.IsEnabled = true;
            AddButton.IsEnabled = true;
            DelButton.IsEnabled = true;
        }
    }

    public class Data : IEquatable<Data>, INotifyPropertyChanged
    {
        public string Word { get; set; }

        private int _count;
        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                OnPropertyChanged();
            }
        }
        public string Path { get; set; }


        public Data(string w, int c, string p = "")
        {
            Word = w;
            Count = c;
            Path = p;
        }

        public bool Equals(Data other)
        {
            return Word == other.Word;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
