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

        public SecretWindow(string vaultClientName)
        {
            this.VaultClientName = vaultClientName;

            InitializeComponent();
            InitGridSecretTagsGrid();

            var btnCreate = new Button()
            {
                Content = "Create"
            };
            btnCreate.Click += BtnCreate_Click;
            Grid.SetColumn(btnCreate, 1);
            Grid.SetColumnSpan(btnCreate, 3);
            Grid.SetRow(btnCreate, 4);
            gridSecret.Children.Add(btnCreate);

            // tags cannot be updated during a create
            lblFolder.Visibility = Visibility.Hidden;
            txtFolder.Visibility = Visibility.Hidden;
            gridTags.Visibility = Visibility.Collapsed;

            txtName.Focus();
        }

        public SecretWindow(string vaultClientName, string secretName)
        {
            this.VaultClientName = vaultClientName;
            this.SecretName = secretName;

            InitializeComponent();
            InitGridSecretTagsGrid();

            var btnUpdate = new Button()
            {
                Content = "Update"
            };
            btnUpdate.Click += BtnUpdate_Click;
            Grid.SetColumn(btnUpdate, 1);
            Grid.SetColumnSpan(btnUpdate, 3);
            Grid.SetRow(btnUpdate, 4);
            gridSecret.Children.Add(btnUpdate);

            LoadSecret();
            txtName.Focus();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            CreateNewSecret();
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            UpdateSecret();
        }

        private void CreateNewSecret()
        {
            var vaultClient = KeyVaultInteraction.VaultClients[this.VaultClientName];
            try
            {
                // Note that tags can't be set when creating a secret by the underlying client
                vaultClient.CreateSecret(txtName.Text, txtPassword.Text);

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
            SecretTag getTagWithName(List<SecretTag> sourceTags, string tagName)
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
            var newPassword = txtPassword.Text;
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
            var folderTag = getTagWithName(tags, "Folder");
            folderTag.Value = getTextBoxValueWithDefault(txtFolder, "/");
            // Keep the folder tag in the collection even if it represents the root. See notes in UpdateSecret about why.

            // TODO: add the displayname tag too

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

            // Show the password
            txtPassword.Text = secret.Value;
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
