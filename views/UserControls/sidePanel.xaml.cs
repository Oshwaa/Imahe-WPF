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
            double maxBlurValue =max_Blur.Value;
            string referencePathValue = referencePath.Text.Replace('\\', '/');
            string directoryPathValue = directoryPath.Text.Replace('\\', '/');
            

            if (string.IsNullOrWhiteSpace(referencePathValue) || string.IsNullOrWhiteSpace(directoryPathValue))
            {
                
                MessageBox.Show("Reference Path and Directory Path must not be empty.", "Input Error");
            }
            else
            {
                // Show the values for confirmation
                results.Clear();
                Status.Visibility = Visibility.Visible;
                MessageBox.Show(
                    $"Min Exposure: {minExposureValue}\n" +
                    $"Max Exposure: {maxExposureValue}\n" +
                    $"Reference Path: {referencePathValue}\n" +
                    $"Directory Path: {directoryPathValue}\n" +
                    $"Max Blur: {maxBlurValue}",
                    "Values"
                );

                RunPythonScript(minExposureValue, maxExposureValue, referencePathValue, directoryPathValue, maxBlurValue);
            }

        }


        private void RunPythonScript(double minExposure, double maxExposure, string referencePath, string directoryPath,double maxBlurValue)
        {
            // Create a new process
            ProcessStartInfo start = new ProcessStartInfo();

            // Specify the Python interpreter path
            start.FileName = "python"; // Make sure this points to your Python interpreter

            // Arguments to pass to the Python script
            string script = @"assets/script.py"; // Path to the Python script
            string args = $"--directory \"{directoryPath}\" --reference \"{referencePath}\" --exposure_min {minExposure} --exposure_max {maxExposure} --blur_max {maxBlurValue}";

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
                            Status.Visibility = Visibility.Hidden;
                           
                        }

                        if (!string.IsNullOrEmpty(errors))
                        {
                            MessageBox.Show($"Errors: {errors}");
                            Status.Visibility = Visibility.Hidden;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running script: {ex.Message}");
                Status.Visibility = Visibility.Hidden;

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

        private void min_exposure_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            minExp.Text = "Minimum Exposure: " + min_exposure.Value.ToString("F1");
        }

        private void max_exposure_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            maxExp.Text = "Maximum Exposure: " + max_exposure.Value.ToString("F1");
        }

        private void max_Blur_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            minSharp.Text = "Minimum Sharpness: " + max_Blur.Value.ToString("F1");
        }



        //end




    }
}
