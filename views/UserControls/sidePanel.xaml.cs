using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using Imahe.models;
using Imahe.helpers;
using System.Diagnostics;
using System.IO;



namespace Imahe.views.UserControls
{
    /// <summary>
    /// Interaction logic for sidePanel.xaml
    /// </summary>
    public partial class sidePanel : UserControl
    {
        private displayPanel _displayPanel;
        public sidePanel()
        {
            InitializeComponent();
            DataContext = ViewModelLocator.MainViewModel;
        }

        private void reference_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            bool? success = fileDialog.ShowDialog();
            if (success == true)
            {
                ref_placeholder.Text = string.Empty;
                
                referencePath.Text = fileDialog.FileName;
                string path = fileDialog.FileName;
                ViewModelLocator.MainViewModel.ReferencePath = path;
                MessageBox.Show($"ReferencePath: {ViewModelLocator.MainViewModel.ReferencePath}");
            }
        }

        private void directory_button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                CheckFileExists = false,
                ValidateNames = false,
                FileName = "Select Folder" // This is necessary to use OpenFileDialog to select folders
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                string folderPath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);

                if (!string.IsNullOrEmpty(folderPath))
                {
                    dir_placeholder.Text = string.Empty;
                    directoryPath.Text = folderPath;
                    ViewModelLocator.MainViewModel.DirectoryPath = folderPath;
                    MessageBox.Show($"DirectoryPath: {ViewModelLocator.MainViewModel.DirectoryPath}");
                    
                    _displayPanel?.load_images();
                }
                else
                {
                    MessageBox.Show("No Folder Selected");
                }
            }
        }
        private void sort_button_Click(object sender, RoutedEventArgs e)
        {
            double minExposureValue = min_exposure.Value;
            double maxExposureValue = max_exposure.Value;
            string referencePathValue = referencePath.Text.Replace('\\', '/');
            string directoryPathValue = directoryPath.Text.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(referencePathValue) || string.IsNullOrWhiteSpace(directoryPathValue))
            {
                MessageBox.Show("Reference Path and Directory Path must not be empty.", "Input Error");
            }
            else
            {
                // Show the values for confirmation
                MessageBox.Show($"Min Exposure: {minExposureValue}\nMax Exposure: {maxExposureValue}\nReference Path: {referencePathValue}\nDirectory Path: {directoryPathValue}", "Values");

                
                RunPythonScript(minExposureValue, maxExposureValue, referencePathValue, directoryPathValue);
            }

        }


        private void RunPythonScript(double minExposure, double maxExposure, string referencePath, string directoryPath)
        {
            // Create a new process
            ProcessStartInfo start = new ProcessStartInfo();

            // Specify the Python interpreter path
            start.FileName = "python"; // Make sure this points to your Python interpreter

            // Arguments to pass to the Python script
            string script = @"script.py"; // Path to the Python script
            string args = $"--directory \"{directoryPath}\" --reference \"{referencePath}\" --exposure_min {minExposure} --exposure_max {maxExposure}";

            // Set up process start info
            start.Arguments = $"{script} {args}";
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;

            try
            {
                using (Process process = Process.Start(start))
                {
                    using (StreamReader outputReader = process.StandardOutput)
                    using (StreamReader errorReader = process.StandardError)
                    {
                        string result = outputReader.ReadToEnd();
                        string errors = errorReader.ReadToEnd();

                        if (!string.IsNullOrEmpty(result))
                        {
                            results.Text = result;
                             
                        }

                        if (!string.IsNullOrEmpty(errors))
                        {
                            MessageBox.Show($"Errors: {errors}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running script: {ex.Message}");
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(results.Text))
            {
                Clipboard.SetText(results.Text);
                MessageBox.Show("Text copied to clipboard!");
            }
            else
            {
                MessageBox.Show("No text to copy.");
            }
        }



        //end




    }
}
