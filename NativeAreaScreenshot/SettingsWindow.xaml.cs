using System.Windows;

namespace NativeAreaScreenshot
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            TxtSaveFolder.Text = SettingsManager.Instance.SaveFolder;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "スクリーンショットの保存先を選択してください";
                dialog.SelectedPath = TxtSaveFolder.Text;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtSaveFolder.Text = dialog.SelectedPath;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SaveFolder = TxtSaveFolder.Text;
            SettingsManager.Instance.Save();
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
