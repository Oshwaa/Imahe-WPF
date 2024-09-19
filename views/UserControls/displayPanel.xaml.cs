using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Imahe.helpers;
using Imahe.models;

namespace Imahe.views.UserControls
{
    /// <summary>
    /// Interaction logic for displayPanel.xaml
    /// </summary>
    public partial class displayPanel : UserControl
    {
        public displayPanel()
        {
            InitializeComponent();
            DataContext = ViewModelLocator.MainViewModel;
            ViewModelLocator.MainViewModel.PropertyChanged += _viewModelOnPropertyChanged;
        }

        private void _viewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModelLocator.MainViewModel.ReferencePath))
            {
                load_images();
            }
        }

        public void load_images()
        {
            string referencePath = ViewModelLocator.MainViewModel.ReferencePath;

            if (string.IsNullOrEmpty(referencePath))
            {
                MessageBox.Show("Reference path is not set.");
                return;
            }

            if (!File.Exists(referencePath))
            {
                MessageBox.Show("Reference file does not exist.");
                return;
            }

            // Clear existing images (if any)
            ImagePanel.Children.Clear();

            // Load and display the reference image
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(referencePath, UriKind.Absolute);
            bitmap.EndInit();

            Image imageControl = new Image
            {
                Source = bitmap,
                Stretch = System.Windows.Media.Stretch.Uniform // Adjust as needed
            };

            // Add the image to the panel
            ImagePanel.Children.Add(imageControl);
        }
    }
}
