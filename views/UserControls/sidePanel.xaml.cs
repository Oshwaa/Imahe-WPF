using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using Imahe.models;
using Imahe.helpers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Imahe.views.UserControls
{
    public partial class sidePanel : UserControl
    {
        private displayPanel _displayPanel;
        private CancellationTokenSource _cancellationTokenSource;
        private Process _pythonProcess; // Track the process so we can kill it on cancellation

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
                FileName = "Select Folder"
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

        private async void sort_button_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null) // Cancel if already running
            {
                _cancellationTokenSource.Cancel();
                if (_pythonProcess != null && !_pythonProcess.HasExited)
                {
                    _pythonProcess.Kill(); // Terminate Python process
                }
                sort_button.Content = "Sort";
                Status.Visibility = Visibility.Hidden;
                MessageBox.Show("Processing canceled.");
                return;
            }

            // Otherwise, start sorting
            double minExposureValue = min_exposure.Value;
            double maxExposureValue = max_exposure.Value;
            double maxBlurValue = max_Blur.Value;
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
                sort_button.Content = "Cancel"; // Change to "Cancel" while processing
                _cancellationTokenSource = new CancellationTokenSource(); // Initialize cancellation token source

                try
                {
                    await RunPythonScriptAsync(minExposureValue, maxExposureValue, referencePathValue, directoryPathValue, maxBlurValue, _cancellationTokenSource.Token);
                }
                finally
                {
                    _cancellationTokenSource = null; 
                    sort_button.Content = "Sort"; 
                }
            }
        }

        private async Task RunPythonScriptAsync(double minExposure, double maxExposure, string referencePath, string directoryPath, double maxBlurValue, CancellationToken cancellationToken)
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "python", // Ensure correct Python path
                Arguments = $"assets/script.py --directory \"{directoryPath}\" --reference \"{referencePath}\" --exposure_min {minExposure} --exposure_max {maxExposure} --blur_max {maxBlurValue}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (_pythonProcess = new Process { StartInfo = start })
                {
                    _pythonProcess.Start();

                    Task<string> outputTask = _pythonProcess.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = _pythonProcess.StandardError.ReadToEndAsync();

                    // Poll the cancellation token for cancellation request
                    while (!_pythonProcess.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _pythonProcess.Kill();
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        await Task.Delay(100); // Poll every 100 ms
                    }

                    string result = await outputTask;
                    string errors = await errorTask;

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
            catch (OperationCanceledException)
            {
                results.Text = "Processing was canceled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running script: {ex.Message}");
            }
            finally
            {
                Status.Visibility = Visibility.Hidden;
                _pythonProcess = null; // Reset the process variable
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
    }
}
