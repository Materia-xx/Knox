using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Knox
{
    // TODO: make it so the program can auto-discover all keyvaults in the specified subscription, even better if it can auto-discover all subscriptions you have access to and we can do away with the config file

    // TODO: make the UI remember which folder names are expanded so when it redraws the UI the same ones are opened.

    public partial class MainWindow : Window
    {
        private static char[] folderSplitChars = new char[] { '\\', '/' };

        private TreeViewTagMetadataSecret moveFromMetadataSecret;
        private ContextMenu vaultContentMenu;
        private ContextMenu secretContextMenu;
        private MenuItem vaultMoveToHereMenu;

        public MainWindow()
        {
            InitializeComponent();
            InitContextMenus();
        }

        private void InitContextMenus()
        {
            vaultContentMenu = new ContextMenu();
            var vaultAddSecretMenu = new MenuItem()
            {
                Header = "Add new secret"
            };
            vaultAddSecretMenu.Click += VaultAddSecretMenu_Click;
            vaultContentMenu.Items.Add(vaultAddSecretMenu);

            vaultMoveToHereMenu = new MenuItem()
            {
                Header = "Move to here"
            };
            vaultMoveToHereMenu.Click += VaultMoveToHereMenu_Click;
            vaultContentMenu.Items.Add(vaultMoveToHereMenu);

            secretContextMenu = new ContextMenu();

            var secretMoveToNewVaultMenu = new MenuItem()
            {
                Header = "Move to vault..."
            };
            secretMoveToNewVaultMenu.Click += SecretMoveToNewVaultMenu_Click;
            secretContextMenu.Items.Add(secretMoveToNewVaultMenu);
        }

        private void SecretMoveToNewVaultMenu_Click(object sender, RoutedEventArgs e)
        {
            moveFromMetadataSecret = GetTreeViewItemTagMetadata<TreeViewTagMetadataSecret>(treeSecrets.SelectedItem as TreeViewItem);
            if (moveFromMetadataSecret != null)
            {
                MessageBox.Show("Warning: Secret history is not copied during a move.\r\nTo complete the move select the vault to move to and choose\r\n 'Move to here'.", "Move Secret");
            }
        }

        private void VaultMoveToHereMenu_Click(object sender, RoutedEventArgs e)
        {
            if (moveFromMetadataSecret == null)
            {
                return;
            }

            var moveToMetadataVault = GetTreeViewItemTagMetadata<TreeViewTagMetadataVault>(treeSecrets.SelectedItem as TreeViewItem);
            if (moveToMetadataVault == null)
            {
                MessageBox.Show("Please select a vault to move to");
                return;
            }

            if (moveToMetadataVault.VaultName.Equals(moveFromMetadataSecret.VaultName))
            {
                MessageBox.Show("The secret is already in this vault.");
                return;
            }

            var fromVaultClient = KeyVaultInteraction.VaultClients[moveFromMetadataSecret.VaultName];
            var toVaultClient = KeyVaultInteraction.VaultClients[moveToMetadataVault.VaultName];

            if (toVaultClient.AllSecretProperties.ContainsKey(moveFromMetadataSecret.SecretName))
            {
                MessageBox.Show($"The selected destination vault already contains a secret named {moveFromMetadataSecret.SecretName}");
                return;
            }

            try
            {
                var fromSecret = fromVaultClient.GetSecret(moveFromMetadataSecret.SecretName);
                var toSecret = toVaultClient.CloneTo(fromSecret, moveFromMetadataSecret.SecretName);
                fromVaultClient.DeleteSecret(moveFromMetadataSecret.SecretName);
                moveFromMetadataSecret = null;

                // Redraw the UI to show the moved secret
                LoadSearchResultsUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void VaultAddSecretMenu_Click(object sender, RoutedEventArgs e)
        {
            if (treeSecrets.SelectedItem == null)
            {
                return;
            }

            var selectedTreeNode = treeSecrets.SelectedItem as TreeViewItem;
            var vaultClientName = selectedTreeNode.Header.ToString();

            var secretWindow = new SecretWindow(vaultClientName);
            secretWindow.ShowDialog();
            // After the dialog closes, reload the UI again to show the new secret
            LoadSearchResultsUI();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            KeyVaultInteraction.InitClients();
            LoadSearchResultsUI();
        }

        private void LoadSearchResultsUI()
        {
            TreeViewItem findFolderNode(ItemCollection items, string header)
            {
                foreach (var item in items)
                {
                    if (item is TreeViewItem)
                    {
                        var tvi = item as TreeViewItem;
                        if (tvi.Header.Equals(header))
                        {
                            return tvi;
                        }
                    }
                }
                return null;
            }

            treeSecrets.Items.Clear();
            var searchTerm = txtSearch.Text;

            foreach (var vaultClientName in KeyVaultInteraction.VaultClients.Keys)
            {
                var vaultClient = KeyVaultInteraction.VaultClients[vaultClientName];

                var vaultTreeNode = new TreeViewItem()
                {
                    Header = vaultClientName,
                    Tag = JsonConvert.SerializeObject(new TreeViewTagMetadataVault(vaultClientName))
                };
                treeSecrets.Items.Add(vaultTreeNode);

                bool vaultHasMatchingSecrets = false;
                foreach (var secretName in vaultClient.AllSecretProperties.Keys)
                {
                    var secretProperties = vaultClient.AllSecretProperties[secretName];

                    bool matchesSearch = string.IsNullOrWhiteSpace(searchTerm) ? true : secretName.CaseInsensitiveContains(searchTerm);
                    matchesSearch = matchesSearch ? matchesSearch : secretName.CaseInsensitiveContains(searchTerm);

                    if (!matchesSearch)
                    {
                        foreach (var secretTag in secretProperties.Tags)
                        {
                            matchesSearch = matchesSearch ? matchesSearch : secretTag.Value.CaseInsensitiveContains(searchTerm);
                        }
                    }

                    if (matchesSearch)
                    {
                        vaultHasMatchingSecrets = true;
                        var vaultSecretNode = new TreeViewItem()
                        {
                            Header = secretName, // TODO: show DisplayName here, but make sure not to mess up the name we pass to the editor, which needs to be the real name
                            Tag = JsonConvert.SerializeObject(new TreeViewTagMetadataSecret(vaultClientName, secretName))
                        };

                        vaultSecretNode.MouseDoubleClick += (s, e) =>
                        {
                            var secretWindow = new SecretWindow(vaultClientName, secretName);
                            secretWindow.ShowDialog();
                            // After the dialog closes, reload the UI again because the user may have changed the folder or name
                            LoadSearchResultsUI();
                        };

                        // If the secret should be displayed under a specific folder
                        var secretParentItemCollection = vaultTreeNode.Items;
                        if (secretProperties.Tags.ContainsKey("Folder"))
                        {
                            var folders = secretProperties.Tags["Folder"].Split(folderSplitChars, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var folderName in folders)
                            {
                                // See if the parent already has a node with this name first before making a new one
                                var subFolderNode = findFolderNode(secretParentItemCollection, folderName);

                                // If a folder doesn't exist yet, then create it
                                if (subFolderNode == null)
                                {
                                    subFolderNode = new TreeViewItem()
                                    {
                                        Header = folderName,
                                        Tag = JsonConvert.SerializeObject(new TreeViewTagMetadataVirtualFolder(vaultClientName, folderName))
                                    };
                                    subFolderNode.ExpandSubtree();
                                    secretParentItemCollection.Add(subFolderNode);
                                }
                                secretParentItemCollection = subFolderNode.Items;
                            }
                        }
                        secretParentItemCollection.Add(vaultSecretNode);
                    }
                }

                // By default when the program first loads it won't expand all the vaults, this could be a lot of secrets and IMO shows too many things
                // Instead it will only expand the folders when a search is being done.
                if (vaultHasMatchingSecrets && !string.IsNullOrWhiteSpace(searchTerm))
                {
                    vaultTreeNode.ExpandSubtree();
                }
            }

            // Make sure the cursor is in the search box
            txtSearch.Focus();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadSearchResultsUI();
        }

        /// <summary>
        /// Gets the currently selected treeview item if it represents a secret
        /// Or null if it doesn't
        /// </summary>
        /// <returns></returns>
        private T GetTreeViewItemTagMetadata<T>(TreeViewItem tvi) where T : TreeViewTagMetadataBase
        {
            if (tvi == null || tvi.Tag == null || string.IsNullOrWhiteSpace(tvi.Tag.ToString()))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(tvi.Tag.ToString());
            }
            catch
            {
                return null;
            }
        }

        private void TreeSecrets_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Figure out the tree node that is under the mouse
            var source = e.OriginalSource;
            while (source != null && !(source is TreeViewItem))
            {
                source = VisualTreeHelper.GetParent(source as DependencyObject);
            }
            if (source == null)
            {
                treeSecrets.ContextMenu = null;
                return;
            }

            var selectedNode = (source as TreeViewItem);
            selectedNode.Focus();

            var baseTagMetadata = GetTreeViewItemTagMetadata<TreeViewTagMetadataBase>(selectedNode);
            if (baseTagMetadata == null)
            {
                treeSecrets.ContextMenu = null;
                return;
            }

            switch (baseTagMetadata.TagType)
            {
                case TreeViewTagMetadataTagType.Vault:
                    if (this.moveFromMetadataSecret == null)
                    {
                        vaultMoveToHereMenu.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        vaultMoveToHereMenu.Visibility = Visibility.Visible;
                        vaultMoveToHereMenu.Header = $"Move '{this.moveFromMetadataSecret.SecretName}' to here.";
                    }

                    vaultMoveToHereMenu.Visibility = this.moveFromMetadataSecret != null ? Visibility.Visible : Visibility.Collapsed;
                    treeSecrets.ContextMenu = vaultContentMenu;
                    break;
                case TreeViewTagMetadataTagType.Secret:
                    treeSecrets.ContextMenu = secretContextMenu;
                    break;
                default:
                    treeSecrets.ContextMenu = null;
                    break;
            }
        }
    }
}
