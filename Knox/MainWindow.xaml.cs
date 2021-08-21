using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Knox
{
    public partial class MainWindow : Window
    {
        private static char[] folderSplitChars = new char[] { '\\', '/' };
        private static Dictionary<string, bool> folderExpandedStates = new Dictionary<string, bool>();

        private DateTime lastInteractiveTime = DateTime.Now;
        private SettingsEditor currentSettingsEditor;
        private SecretWindow currentSecretWindow;

        private TreeViewTagMetadataSecret moveFromMetadataSecret;
        private ContextMenu vaultContentMenu;
        private ContextMenu secretContextMenu;
        private MenuItem vaultMoveToHereMenu;

        public MainWindow()
        {
            InitializeComponent();
            InitVaultContextMenu();
            InitSecretContextMenu();

            var closeTimer = new DispatcherTimer();
            closeTimer.Interval = TimeSpan.FromSeconds(5);
            closeTimer.Tick += CloseTimer_Tick;
            closeTimer.Start();
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            if (KnoxSettings.Current.IdleMinutesClose != 0)
            {
                var idleMinutesSoFar = (DateTime.Now - lastInteractiveTime).TotalMinutes;
                if (idleMinutesSoFar >= KnoxSettings.Current.IdleMinutesClose)
                {
                    if (this.currentSettingsEditor != null && this.currentSettingsEditor.IsVisible)
                    {
                        this.currentSettingsEditor.Close();
                    }

                    if (this.currentSecretWindow != null && this.currentSecretWindow.IsVisible)
                    {
                        this.currentSecretWindow.Close();
                    }

                    this.Close();
                }
            }
        }

        private void InitVaultContextMenu()
        {
            vaultContentMenu = new ContextMenu();
            var vaultAddSecretMenu = new MenuItem()
            {
                Header = "Add new secret"
            };
            vaultAddSecretMenu.Click += VaultAddSecretMenu_Click;
            vaultContentMenu.Items.Add(vaultAddSecretMenu);

            var vaultShowSettingsMenu = new MenuItem()
            {
                Header = "Settings"
            };
            vaultShowSettingsMenu.Click += (a, e) => { ShowSettingsEditor(); };
            vaultContentMenu.Items.Add(vaultShowSettingsMenu);

            vaultMoveToHereMenu = new MenuItem()
            {
                Header = "Move to here"
            };
            vaultMoveToHereMenu.Click += VaultMoveToHereMenu_Click;
            vaultContentMenu.Items.Add(vaultMoveToHereMenu);
        }

        private void InitSecretContextMenu()
        {
            secretContextMenu = new ContextMenu();

            var secretMoveToNewVaultMenu = new MenuItem()
            {
                Header = "Move to different vault"
            };
            secretMoveToNewVaultMenu.Click += SecretMoveToNewVaultMenu_Click;
            secretContextMenu.Items.Add(secretMoveToNewVaultMenu);

            var secretDeleteMmenu = new MenuItem()
            {
                Header = "Delete"
            };
            secretDeleteMmenu.Click += SecretDeleteMmenu_Click;
            secretContextMenu.Items.Add(secretDeleteMmenu);
        }

        private void SecretDeleteMmenu_Click(object sender, RoutedEventArgs e)
        {
            var selectedSecret = GetTreeViewItemTagMetadata<TreeViewTagMetadataSecret>(treeSecrets.SelectedItem as TreeViewItem);
            if (selectedSecret != null)
            {
                var vaultClient = KeyVaultInteraction.VaultClients[selectedSecret.VaultName];
                var secretProperties = vaultClient.AllSecretProperties[selectedSecret.SecretName];
                var question = $"Delete secret '{selectedSecret.SecretName}'?";
                if (secretProperties.Tags.ContainsKey("DisplayName"))
                {
                    var displayName = secretProperties.Tags["DisplayName"];
                    if (!selectedSecret.SecretName.Equals(displayName))
                    {
                        question += $"\r\nDisplayName = '{displayName}'.";
                    }
                }

                var answer = MessageBox.Show(question, "Delete Secret", MessageBoxButton.YesNo);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }
                vaultClient.DeleteSecret(selectedSecret.SecretName);
                // Redraw the UI so the deleted secret is removed
                LoadSearchResultsUI();
            }
        }

        private void SecretMoveToNewVaultMenu_Click(object sender, RoutedEventArgs e)
        {
            moveFromMetadataSecret = GetTreeViewItemTagMetadata<TreeViewTagMetadataSecret>(treeSecrets.SelectedItem as TreeViewItem);
            if (moveFromMetadataSecret != null && !KnoxSettings.Current.SuppressWarnings)
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
                MessageBox.Show("Please select a vault to move to.");
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

            this.currentSecretWindow = new SecretWindow(vaultClientName);
            this.currentSecretWindow.ShowDialog();
            this.currentSecretWindow = null;
            // After the dialog closes, reload the UI again to show the new secret
            LoadSearchResultsUI();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // If the clientid/tenant id is not set then give a chance to set it before loading the clients
            if (string.IsNullOrWhiteSpace(KnoxSettings.Current.ClientId) || string.IsNullOrWhiteSpace(KnoxSettings.Current.TenantId))
            {
                ShowSettingsEditor();
            }

            KeyVaultInteraction.InitClients();
            LoadSearchResultsUI();
        }

        private void ShowSettingsEditor()
        {
            this.currentSettingsEditor = new SettingsEditor();
            this.currentSettingsEditor.ShowDialog();
        }

        private void RecordOrRestoreTreeExpandedStates(TreeViewItem treeItem, string prefixPath, bool restore)
        {
            if (treeItem == null)
            {
                foreach (var topLevelNode in treeSecrets.Items)
                {
                    var tvi = topLevelNode as TreeViewItem;
                    var metaData = GetTreeViewItemTagMetadata<TreeViewTagMetadataVault>(tvi);
                    var key = metaData.VaultName;
                    if (restore)
                    {
                        if (folderExpandedStates.ContainsKey(key))
                        {
                            tvi.IsExpanded = folderExpandedStates[key];
                        }
                    }
                    else
                    {
                        folderExpandedStates[key] = tvi.IsExpanded;
                    }
                    RecordOrRestoreTreeExpandedStates(tvi, metaData.VaultName, restore);
                }
            }
            else if(treeItem.Items != null)
            {
                foreach (var subNode in treeItem.Items)
                {
                    var tvi = subNode as TreeViewItem;
                    var metaDataBase = GetTreeViewItemTagMetadata<TreeViewTagMetadataBase>(tvi);
                    switch (metaDataBase.TagType)
                    {
                        case TreeViewTagMetadataTagType.VirtualFolder:
                            var metaDataVirtualFolder = GetTreeViewItemTagMetadata<TreeViewTagMetadataVirtualFolder>(tvi);
                            var key = $"{prefixPath}/{metaDataVirtualFolder.FolderName}";
                            if (restore)
                            {
                                if (folderExpandedStates.ContainsKey(key))
                                {
                                    tvi.IsExpanded = folderExpandedStates[key];
                                }
                            }
                            else
                            {
                                folderExpandedStates[key] = tvi.IsExpanded;
                            }
                            RecordOrRestoreTreeExpandedStates(tvi, metaDataVirtualFolder.FolderName, restore);
                            break;
                        default:
                            // Secrets don't have sub items, so there is no expansion to remember
                            break;
                    }
                }
            }
        }

        private void ExpandAllShownFolders(TreeViewItem treeItem)
        {
            if (treeItem == null)
            {
                foreach (var topLevelNode in treeSecrets.Items)
                {
                    ExpandAllShownFolders(topLevelNode as TreeViewItem);
                }
            }
            else if (treeItem.Items != null && treeItem.Items.Count > 0)
            {
                treeItem.IsExpanded = true;
                foreach (var subNode in treeItem.Items)
                {
                    ExpandAllShownFolders(subNode as TreeViewItem);
                }
            }
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

                        var secretDisplayName = secretName;
                        if (secretProperties.Tags.ContainsKey("DisplayName"))
                        {
                            secretDisplayName = secretProperties.Tags["DisplayName"];
                        }

                        var vaultSecretNode = new TreeViewItem()
                        {
                            Header = secretDisplayName,
                            Tag = JsonConvert.SerializeObject(new TreeViewTagMetadataSecret(vaultClientName, secretName))
                        };

                        vaultSecretNode.MouseDoubleClick += (s, e) =>
                        {
                            this.currentSecretWindow = new SecretWindow(vaultClientName, secretName);
                            this.currentSecretWindow.ShowDialog();
                            this.currentSecretWindow = null;
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

            // If the search is for nothing, then restore the "non-search" view of which folders are expanded
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                RecordOrRestoreTreeExpandedStates(null, null, true);
            }

            // Otherwise, this is a search, so expand everything that was found
            else
            {
                ExpandAllShownFolders(null);
            }
        }

        private void txtSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Record the expanded state of the tree as it was before any search started
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                RecordOrRestoreTreeExpandedStates(null, null, false);
            }
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

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            lastInteractiveTime = DateTime.Now;
        }
    }
}
