using System;

namespace SwitchDataCollection.Function
{
    public class FileChangedEventArgs : EventArgs
    {
        public string FilePath { get; }

        public FileChangedEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }
}