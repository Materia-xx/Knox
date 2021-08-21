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
            chkSuppressWarnings.IsChecked = KnoxSettings.Current.SuppressWarnings;
            txtIdleMinutesClose.Text = KnoxSettings.Current.IdleMinutesClose.ToString();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            KnoxSettings.Current.ClientId = txtClientId.Text;
            KnoxSettings.Current.TenantId = txtTenantId.Text;
            KnoxSettings.Current.SuppressWarnings = chkSuppressWarnings.IsChecked == null ? false : chkSuppressWarnings.IsChecked.Value;
            if(int.TryParse(txtIdleMinutesClose.Text, out int parsedIdleMinutes))
            {
                KnoxSettings.Current.IdleMinutesClose = parsedIdleMinutes;
            }
            else
            {
                KnoxSettings.Current.IdleMinutesClose = 0;
            }

            KnoxSettings.Save(KnoxSettings.Current);
            this.Close();
        }
    }
}
