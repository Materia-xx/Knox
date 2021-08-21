using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Knox
{
    /// <summary>
    /// Interaction logic for SecretWindow.xaml
    /// </summary>
    public partial class SecretWindow : Window
    {
        private string VaultClientName { get; }
        private string SecretName { get; }

        // Keeps track of what the secret is, so if it's changed we know to update it
        private string savedPassword = string.Empty;

        private string mode = string.Empty;

        public SecretWindow(string vaultClientName)
        {
            InitializeComponent();

            this.VaultClientName = vaultClientName;
            this.mode = "Create";
            btnAction.Content = this.mode;
            SetPasswordShowMode(true); // In create mode we do initially show what the user is typing to them

            // take away the ability to show/hide/copy when adding.
            // We don't have a fancy box that actually shows stars and lets you type at the same time. Maybe eventually.
            btnShowHidePassword.Visibility = Visibility.Hidden;
            btnCopyPassword.Visibility = Visibility.Hidden;
            Grid.SetColumnSpan(txtPasswordReal, 5);

            InitGridSecretTagsGrid();

            // tags cannot be updated during a create
            lblFolder.Visibility = Visibility.Hidden;
            txtFolder.Visibility = Visibility.Hidden;
            gridTags.Visibility = Visibility.Collapsed;

            txtName.Focus();
        }

        public SecretWindow(string vaultClientName, string secretName)
        {
            InitializeComponent();

            this.VaultClientName = vaultClientName;
            this.SecretName = secretName;
            this.mode = "Update";
            btnAction.Content = this.mode;
            SetPasswordShowMode(false);
            InitGridSecretTagsGrid();

            LoadSecret();
            txtName.Focus();
        }

        private void btnAction_Click(object sender, RoutedEventArgs e)
        {
            switch (this.mode)
            {
                case "Create":
                    CreateNewSecret();
                    break;
                case "Update":
                    UpdateSecret();
                    break;
                default:
                    break;
            }
        }

        private void btnShowHidePassword_Click(object sender, RoutedEventArgs e)
        {
            if (btnShowHidePassword.Content.ToString() == "Show")
            {
                SetPasswordShowMode(true);
                btnShowHidePassword.Content = "Hide";
            }
            else
            {
                SetPasswordShowMode(false);
                btnShowHidePassword.Content = "Show";
            }
        }

        private void btnCopyPassword_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtPasswordReal.Text);
        }

        private void SetPasswordShowMode(bool show)
        {
            if (show)
            {
                txtPasswordReal.Visibility = Visibility.Visible;
                txtPasswordStars.Visibility = Visibility.Hidden;
            }
            else
            {
                txtPasswordReal.Visibility = Visibility.Hidden;
                txtPasswordStars.Visibility = Visibility.Visible;
            }
        }

        private void CreateNewSecret()
        {
            var vaultClient = KeyVaultInteraction.VaultClients[this.VaultClientName];
            try
            {
                // Note that tags can't be set when creating a secret by the underlying client
                vaultClient.CreateSecret(txtName.Text, txtPasswordReal.Text);

                // The form will have to be re-opened in edit mode to do editing.
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void UpdateSecret()
        {
            SecretTag getOrCreateTagWithName(List<SecretTag> sourceTags, string tagName)
            {
                var foundTag = sourceTags.FirstOrDefault(t => t.Name.Equals(tagName));
                if (foundTag == null)
                {
                    foundTag = new SecretTag();
                    foundTag.Name = tagName;
                    sourceTags.Add(foundTag);
                }
                return foundTag;
            }

            void removeTagWithName(List<SecretTag> sourceTags, string tagName)
            {
                var tagsToRemove = sourceTags.Where(tag => tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                foreach (var tag in tagsToRemove)
                {
                    sourceTags.Remove(tag);
                }
            }

            string getTextBoxValueWithDefault(TextBox box, string defaultValue)
            {
                var value = box.Text.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = defaultValue;
                }
                return value;
            }

            // Validate password
            var newPassword = txtPasswordReal.Text;
            if (string.IsNullOrEmpty(newPassword))
            {
                // Password values cannot be an empty string
                MessageBox.Show("Password cannot be empty");
                return;
            }

            // Only do the save if the data has changed
            bool updatePassword = newPassword != savedPassword;

            // Figure out the list of tags
            var tags = ((ObservableCollection<SecretTag>)gridTags.ItemsSource).ToList();
            // Make sure we have the folder tag added with the right value
            var folderTag = getOrCreateTagWithName(tags, "Folder");
            folderTag.Value = getTextBoxValueWithDefault(txtFolder, "/");
            // Keep the folder tag in the collection even if it represents the root. See notes in UpdateSecret about why.

            if (!txtName.Text.Equals(this.SecretName))
            {
                var displayNameTag = getOrCreateTagWithName(tags, "DisplayName");
                displayNameTag.Value = txtName.Text;
            }
            else
            {
                removeTagWithName(tags, "DisplayName");
            }

            // Do the update
            var vaultClient = KeyVaultInteraction.VaultClients[this.VaultClientName];
            vaultClient.UpdateSecret(this.SecretName, updatePassword, newPassword, tags);

            // Assumes all editing is done when the update button is pressed
            this.Close();
        }

        private void LoadSecret()
        {
            var vaultClient = KeyVaultInteraction.VaultClients[this.VaultClientName];
            var secret = vaultClient.GetSecret(this.SecretName);

            // Load data from tags
            var gridTagsData = new ObservableCollection<SecretTag>();
            foreach (var tagName in secret.Properties.Tags.Keys)
            {
                var tagValue = secret.Properties.Tags[tagName];

                // Some tags are not shown in the tag editor itself
                switch (tagName)
                {
                    case "Folder":
                        txtFolder.Text = tagValue;
                        break;
                    case "DisplayName":
                        txtName.Text = tagValue;
                        break;
                    default:
                        gridTagsData.Add(new SecretTag(tagName, tagValue));
                        break;
                }
            }
            gridTags.ItemsSource = gridTagsData;

            // If the DisplayName tag isn't present, then show the actual name
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                txtName.Text = secret.Name;
            }

            txtPasswordReal.Text = secret.Value;
            txtPasswordStars.Text = new string('*', secret.Value.Length);
            savedPassword = secret.Value;
        }

        private void InitGridSecretTagsGrid()
        {
            var nameBinding = new Binding("Name")
            {
                Mode = BindingMode.Default,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            var valueBinding = new Binding("Value")
            {
                Mode = BindingMode.Default,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            gridTags.Columns.Add(new DataGridTextColumn()
            {
                Header = "Name",
                Binding = nameBinding
            });
            // TODO: if a tag value starts with http then instead make it a hyperlink column that opens the link in a browser
            gridTags.Columns.Add(new DataGridTextColumn()
            {
                Header = "Value",
                Binding = valueBinding
            });
        }
    }
}
