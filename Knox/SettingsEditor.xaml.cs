using System.Windows;

namespace Knox
{
    /// <summary>
    /// Interaction logic for SettingsEditor.xaml
    /// </summary>
    public partial class SettingsEditor : Window
    {
        public SettingsEditor()
        {
            InitializeComponent();
            txtClientId.Text = KnoxSettings.Current.ClientId;
            txtTenantId.Text = KnoxSettings.Current.TenantId;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            KnoxSettings.Current.ClientId = txtClientId.Text;
            KnoxSettings.Current.TenantId = txtTenantId.Text;
            KnoxSettings.Save(KnoxSettings.Current);
            this.Close();
        }
    }
}
