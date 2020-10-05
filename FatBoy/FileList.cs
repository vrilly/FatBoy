using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Enumeration;
using System.Linq;
using System.Text;

namespace FatBoy
{
    public class FileObject
    {
        private int fileSize;
        public string filePath;
        public FileObject(string filename, int filesize, string filepath)
        {
            FileName = filename;
            fileSize = filesize;
            filePath = filepath;
        }

        public string FileName { get; set; }

        public string FileSize => fileSize.ToString();

        public Fat.FatFile FatFile => new Fat.FatFile(FileName, fileSize, filePath);
    }
    public class FileList : ObservableCollection<FileObject>
    {
        public Fat.FatFile[] GetFatFiles()
        {
            List<Fat.FatFile> fatFiles = new List<Fat.FatFile>();
            foreach (FileObject fileObject in this)
                fatFiles.Add(fileObject.FatFile);
            return fatFiles.ToArray();
        }
    }
}
