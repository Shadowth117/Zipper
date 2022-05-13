using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Reloaded.Memory.Streams;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using zamboni;

namespace Zipper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        OpenFileDialog fileOpen = new()
        {
            Filter = "All files (*)|*.*",
            Title = "Open file"
        };

        CommonOpenFileDialog folderOpen = new()
        {
            IsFolderPicker = true
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        public void ProcessFile(object sender, RoutedEventArgs e)
        {
            fileOpen.Multiselect = false;
            if (fileOpen.ShowDialog() == true)
            {
                var outFile = DecryptAndDeflate(fileOpen.FileName);
                if (outFile == null)
                {
                    MessageBox.Show($"File {Path.GetFileName(fileOpen.FileName)} does not need processing.");
                    return;
                }
                var ext = Path.GetExtension(fileOpen.FileName);
                switch(ext)
                {
                    case ".narc":
                        WriteNarc(fileOpen.FileName, outFile);
                        break;
                    case ".zarc":
                        WriteZarc(fileOpen.FileName, outFile);
                        break;
                    default:
                        File.WriteAllBytes(fileOpen.FileName.Replace(ext, "_out" + ext), outFile);
                        break;
                }
            }
        }

        public void DecryptAndInflateFile(object sender, RoutedEventArgs e)
        {
            fileOpen.Multiselect = false;
            if (fileOpen.ShowDialog() == true)
            {
                var outFile = DecryptAndDeflate(fileOpen.FileName);
                if(outFile == null)
                {
                    MessageBox.Show($"File {Path.GetFileName(fileOpen.FileName)} does not need processing.");
                    return;
                }
                var ext = Path.GetExtension(fileOpen.FileName);
                File.WriteAllBytes(fileOpen.FileName.Replace(ext, "_out" + ext), outFile);
            }
        }

        private byte[] DecryptAndDeflate(string fileName)
        {
            var file = File.ReadAllBytes(fileName);
            return DecryptAndDeflate(file);
        }

        private static byte[] DecryptAndDeflate(byte[] file)
        {
            if(file.Length == 0 || Encoding.UTF8.GetString(file, 0, 4) != "ZPR\0")
            {
                return null;
            }
            var newFile = new byte[file.Length - 0x10];
            var decompLength = BitConverter.ToUInt32(file, 0x8);
            Array.Copy(file, 0x10, newFile, 0, file.Length - 0x10);
            for (int i = 0; i < newFile.Length; i++)
            {
                newFile[i] ^= 0x95;
            }
            newFile = PrsCompDecomp.Decompress(newFile, decompLength);
            return newFile;
        }

        public void ProcessAllFiles(object sender, RoutedEventArgs e)
        {
            fileOpen.Multiselect = true;
            if (fileOpen.ShowDialog() == true)
            {
                textBlock.Text = "Select file(s) or a folder to decrypt, decompress, and extract them as needed. Please wait for the process to finish...";
                ProcessAllFiles(fileOpen.FileNames);
                textBlock.Text = "Select file(s) or a folder to decrypt, decompress, and extract them as needed. Actions complete!";
            }
        }

        public void ProcessAllFilesFromFolder(object sender, RoutedEventArgs e)
        {
            if (folderOpen.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var files = Directory.GetFiles(folderOpen.FileName, "*", SearchOption.AllDirectories);
                textBlock.Text = "Select file(s) or a folder to decrypt, decompress, and extract them as needed. Please wait for the process to finish...";
                ProcessAllFiles(files);
                textBlock.Text = "Select file(s) or a folder to decrypt, decompress, and extract them as needed. Actions complete!";
            }
        }

        private ParallelLoopResult ProcessAllFiles(string[] files)
        {
            return Parallel.ForEach(files, file =>
            {
                Debug.WriteLine($"File {Path.GetFileName(file)}");
                var ext = Path.GetExtension(file);
                var fileBytes = File.ReadAllBytes(file);
                var outFile = DecryptAndDeflate(fileBytes);
                if (outFile == null)
                {
                    switch(ext)
                    {
                        case ".zarc":
                            WriteZarc(file, fileBytes);
                            return;
                        case ".narc":
                            if(narcCB.IsChecked == true)
                            {
                                File.WriteAllBytes(file.Replace(ext, "_out" + ext), outFile);
                            }
                            WriteNarc(file, fileBytes);
                            return;
                    }
                    Debug.WriteLine($"File {Path.GetFileName(file)} does not need processing.");
                    return;
                }
                switch (ext)
                {
                    case ".narc":
                        if (narcCB.IsChecked == true)
                        {
                            File.WriteAllBytes(file.Replace(ext, "_out" + ext), outFile);
                        }
                        WriteNarc(file, outFile);
                        break;
                    default:
                        File.WriteAllBytes(file.Replace(ext, "_out" + ext), outFile);
                        break;
                }
            });
        }

        public static void WriteZarc(string fileName, byte[] buffer)
        {
            var path = Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName);
            Directory.CreateDirectory(path);
            var files = ExtractZarc(buffer, out List<string> names);
            for (int i = 0; i < files.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(path + "\\", names[i]), files[i]);
            }
        }

        public static List<byte[]> ExtractZarc(byte[] buffer, out List<string> names)
        {
            names = new List<string>();
            using (Stream strm = new MemoryStream(buffer))
            using (BufferedStreamReader sr = new BufferedStreamReader(strm, 8192))
            {
                sr.Seek(0xC, SeekOrigin.Begin);
                var headerSize = sr.Read<int>();
                var fileCount = sr.Read<int>();
                var fileDefSize = sr.Read<int>();
                sr.Seek(headerSize, SeekOrigin.Begin);

                List<byte[]> files = new();
                for(int i = 0; i < fileCount; i++)
                {
                    sr.Seek(headerSize + i * fileDefSize, SeekOrigin.Begin);
                    var offset = sr.Read<int>();
                    var size = sr.Read<int>();
                    sr.Seek(0x8, SeekOrigin.Current);

                    var str = Encoding.ASCII.GetString(sr.ReadBytes(sr.Position(), 0x30));
                    names.Add(str.Remove(str.IndexOf(char.MinValue)));
                    files.Add(sr.ReadBytes(offset, size));
                }

                return files;
            }
        }

        public static void WriteNarc(string fileName, byte[] buffer)
        {
            var path = Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName);
            Directory.CreateDirectory(path);
            var files = ExtractNarc(fileName, buffer, out List<string> names);
            for(int i = 0; i < files.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(path + "\\", names[i]), files[i]);
            }
        }

        public static List<byte[]> ExtractNarc(string fileName, byte[] buffer, out List<string> names)
        {
            names = new List<string>();
            using (Stream strm = new MemoryStream(buffer))
            using (BufferedStreamReader sr = new BufferedStreamReader(strm, 8192))
            {
                bool hasFileNames = true;
                sr.Seek(0xC, SeekOrigin.Begin);
                var headerLength = sr.Read<ushort>();

                //Read the FATB section
                sr.Seek(headerLength + 4, SeekOrigin.Begin);
                var fatbLength = sr.Read<int>();
                var entryCount = sr.Read<int>();

                List<(int start, int length)> startEndPairs = new();
                for(int i = 0; i < entryCount; i++)
                {
                    var start = sr.Read<int>();
                    var length = sr.Read<int>() - start; 
                    startEndPairs.Add(new (start, length));
                }

                var fntbStart = sr.Position();
                sr.Seek(4, SeekOrigin.Current);
                var fntbLength = sr.Read<int>();
                var fimgStart = fntbLength + fntbStart;
                var typeFlags = sr.Read<int>();
                sr.Seek(4, SeekOrigin.Current);

                if (fntbLength <= 16 || typeFlags == 4)
                {
                    hasFileNames = false;
                }

                if(hasFileNames)
                {
                    if(typeFlags != 0x8)
                    {
                        //NARC has this directoryo structure thing, but it's stupid and we're gonna skip it
                        var position = sr.Position();
                        var lastF0 = position;
                        while (position < fimgStart)
                        {
                            sr.Seek(1, SeekOrigin.Current);
                            position = sr.Position();
                            var bt = sr.Peek<byte>();
                            if (bt == 0xF0)
                            {
                                lastF0 = position;
                            }
                        }
                        sr.Seek(lastF0 + 1, SeekOrigin.Begin);
                        if (sr.Peek<byte>() == 0)
                        {
                            sr.Seek(1, SeekOrigin.Current);
                        }
                    }
                    var pos = sr.Position();
                    for (int i = 0; i < entryCount; i++)
                    {
                        byte len = sr.Read<byte>();
                        while(len == 0)
                        {
                            len = sr.Read<byte>();
                        }
                        names.Add(Encoding.ASCII.GetString(sr.ReadBytes(sr.Position(), len)));
                        sr.Seek(len, SeekOrigin.Current);
                    }
                }

                List<byte[]> files = new();
                for(int i = 0; i < startEndPairs.Count; i++)
                {
                    files.Add(sr.ReadBytes(fimgStart + startEndPairs[i].start + 8, startEndPairs[i].length));
                }

                return files;
            }
        }

    }
}
