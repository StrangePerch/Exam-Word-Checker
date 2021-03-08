using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Exam_Word_Checker
{
    public class DiskChecker
    {
        public static List<string> Words = new List<string>();
        public static string Path;
        public long full_size;
        public long finished = 0;
        public double percentage;
        public static CancellationTokenSource TokenSource;
        public static bool Pause = false;
        
        public delegate void NewStat(string word, int count, string path);
        public event NewStat Notify;

        private DriveInfo drive;
        public DiskChecker(DriveInfo drive)
        {
            this.drive = drive;
        }
        public async Task StartScan()
        {
            await Task.Run((() =>
            {
                full_size = drive.TotalSize - drive.AvailableFreeSpace;
                var dir = drive.RootDirectory;
                ScanDir(dir);
                finished = full_size;
                percentage = finished * 100f / full_size;
            }));
        }

        public void ScanDir(DirectoryInfo dir)
        {
            if (TokenSource.IsCancellationRequested) return;

            foreach (var dirInfo in dir.GetDirectories())
            {
                while (Pause)
                {
                    Thread.Sleep(100);
                }
                if (TokenSource.IsCancellationRequested) return;
                try
                {
                    ScanDir(dirInfo);
                }
                catch (Exception e)
                {
                }
            }

            CheckFiles(dir.GetFiles());
        }

        public void CheckFiles(FileInfo[] files)
        {
            foreach (var file in files)
            {
                if (TokenSource.IsCancellationRequested) return;
                while (Pause)
                {
                    Thread.Sleep(100);
                }
                finished += file.Length;

                //Console.WriteLine($"{finished * 100 / full_size}% - ({finished / 1000} / {full_size / 1000})");
                percentage = finished * 100f / full_size;
                if (file.Extension == ".txt" || file.Extension == ".cs" || file.Extension == ".cpp" || file.Extension == ".h")
                {
                    CheckFile(file);
                }
            }
        }

        public void CheckFile(FileInfo file)
        {
            StreamReader reader = file.OpenText();
            string text = reader.ReadToEnd();
            int counter = 0;

            foreach (var word in Words)
            {
                int last = 0;

                while (true)
                {
                    last = text.IndexOf(word, last + 1, StringComparison.Ordinal);
                    if (last == -1) break;
                    counter++;
                }

                text = text.Replace(word, "*******");

                if (counter > 0)
                    Notify?.Invoke(word,counter, file.FullName);
            }

            if (Path != String.Empty && counter > 0)
            {
                StreamWriter writer = new StreamWriter($"{Path}\\{file.Name}");
                writer.WriteLine(text);
            }
        }
    }
}