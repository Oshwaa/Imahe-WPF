using System;
using System.ComponentModel;

namespace Imahe.models
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string? _referencePath;
        private string? _directoryPath;

        public string? ReferencePath
        {
            get => _referencePath;
            set
            {
                if (_referencePath != value)
                {
                    _referencePath = value;
                    OnPropertyChanged(nameof(ReferencePath));
                }
            }
        }

        public string? DirectoryPath
        {
            get => _directoryPath;
            set
            {
                if (_directoryPath != value)
                {
                    _directoryPath = value;
                    OnPropertyChanged(nameof(DirectoryPath));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
