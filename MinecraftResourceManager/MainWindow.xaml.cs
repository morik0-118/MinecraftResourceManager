using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MinecraftResourceManager
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<InstanceInfo> Instances { get; set; }
            = new ObservableCollection<InstanceInfo>();

        private readonly string SettingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MinecraftResourceManager");

        private readonly string SettingsFile;

        public MainWindow()
        {
            InitializeComponent();

            SettingsFile = Path.Combine(
                SettingsDirectory,
                "settings.json");

            DataContext = this;

            LoadSettings();
        }

        // ==========================================
        // Resource Packs フォルダ選択
        // ==========================================
        private void SelectResourcePackFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Resource Packs の共有フォルダを選択してください"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string folderName =
                new DirectoryInfo(dialog.FolderName).Name;

            if (folderName != "resourcepacks")
            {
                MessageBox.Show(
                    "リソースパックの共有元フォルダには\n" +
                    "「resourcepacks」という名前のフォルダを指定してください。\n\n" +
                    "※すべて小文字で入力してください。",
                    "フォルダ名が正しくありません",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ResourcePackPath.Text =
                dialog.FolderName;

            UpdatePackColumnVisibility();

            SaveSettings();
        }

        // ==========================================
        // Shader Packs フォルダ選択
        // ==========================================
        private void SelectShaderPackFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Shader Packs の共有フォルダを選択してください"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string folderName =
                new DirectoryInfo(dialog.FolderName).Name;

            if (folderName != "shaderpacks")
            {
                MessageBox.Show(
                    "シェーダーパックの共有元フォルダには\n" +
                    "「shaderpacks」という名前のフォルダを指定してください。\n\n" +
                    "※すべて小文字で入力してください。",
                    "フォルダ名が正しくありません",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            ShaderPackPath.Text =
                dialog.FolderName;

            UpdatePackColumnVisibility();

            SaveSettings();
        }

        // ==========================================
        // 共有インスタンス表示
        // ==========================================
        private void UpdatePackColumnVisibility()
        {
            bool resourcePackConfigured =
                !string.IsNullOrWhiteSpace(ResourcePackPath.Text);

            bool shaderPackConfigured =
                !string.IsNullOrWhiteSpace(ShaderPackPath.Text);

            ResourcePackShareColumn.Visibility =
                resourcePackConfigured
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ResourcePackStatusColumn.Visibility =
                resourcePackConfigured
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ShaderPackShareColumn.Visibility =
                shaderPackConfigured
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ShaderPackStatusColumn.Visibility =
                shaderPackConfigured
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // ==========================================
        // インスタンス検出
        // ==========================================

        private async void DetectInstances_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "インスタンスを検索しています...";

                string appDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

                if (string.IsNullOrWhiteSpace(appDataPath) ||
                    !Directory.Exists(appDataPath))
                {
                    MessageBox.Show(
                        "AppDataフォルダを取得できませんでした。",
                        "インスタンス検出",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;

                List<string> detectedPaths = await Task.Run(() =>
                    FindMinecraftInstances(appDataPath));

                Mouse.OverrideCursor = null;

                if (detectedPaths.Count == 0)
                {
                    StatusText.Text = "インスタンスが見つかりませんでした。";

                    MessageBox.Show(
                        "options.txt を持つMinecraftインスタンスが見つかりませんでした。",
                        "インスタンス検出",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                // すでに登録されているインスタンスは除外
                var candidates = detectedPaths
                    .Where(path => !Instances.Any(instance =>
                        string.Equals(
                            instance.Path,
                            path,
                            StringComparison.OrdinalIgnoreCase)))
                    .Select(path => new InstanceCandidate
                    {
                        IsSelected = false,
                        Name = new DirectoryInfo(path).Name,
                        Path = path
                    })
                    .OrderBy(candidate => candidate.Name)
                    .ToList();

                if (candidates.Count == 0)
                {
                    StatusText.Text = "新しく追加できるインスタンスはありません。";

                    MessageBox.Show(
                        "検出されたインスタンスは、すべて登録済みです。",
                        "インスタンス検出",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                var dialog = new InstanceDetectionWindow(candidates)
                {
                    Owner = this
                };

                bool? result = dialog.ShowDialog();

                if (result != true)
                {
                    StatusText.Text = "インスタンスの追加をキャンセルしました。";
                    return;
                }

                int addedCount = 0;

                foreach (InstanceCandidate candidate in dialog.Candidates
                             .Where(candidate => candidate.IsSelected))
                {
                    Instances.Add(new InstanceInfo
                    {
                        Name = candidate.Name,
                        Path = candidate.Path,

                        ShareResourcePacks = false,
                        ShareShaderPacks = false,

                        AppliedShareResourcePacks = false,
                        AppliedShareShaderPacks = false,

                        ResourcePackStatus = "未共有",
                        ShaderPackStatus = "未共有",
                        Status = "適用済み"
                    });

                    addedCount++;
                }

                SaveSettings();

                StatusText.Text =
                    $"{addedCount} 件のインスタンスを追加しました。";

                MessageBox.Show(
                    $"{addedCount} 件のインスタンスを追加しました。",
                    "インスタンス検出",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;

                StatusText.Text = "インスタンス検出中にエラーが発生しました。";

                MessageBox.Show(
                    $"インスタンスの検出中にエラーが発生しました。\n\n{ex.Message}",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private List<string> FindMinecraftInstances(string rootPath)
        {
            var results = new List<string>();

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.System
            };

            try
            {
                foreach (string filePath in Directory.EnumerateFiles(
                    rootPath,
                    "options.txt",
                    enumerationOptions))
                {
                    string? directory = Path.GetDirectoryName(filePath);

                    if (string.IsNullOrWhiteSpace(directory))
                        continue;

                    if (!results.Contains(
                        directory,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(directory);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // アクセスできないフォルダは無視
            }
            catch (DirectoryNotFoundException)
            {
                // 存在しなくなったフォルダは無視
            }

            return results;
        }

        // ==========================================
        // インスタンス追加
        // ==========================================
        private void AddInstance_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Minecraft インスタンスのフォルダを選択してください"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string selectedPath = dialog.FolderName;

            foreach (var instance in Instances)
            {
                if (string.Equals(
                    instance.Path,
                    selectedPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "このインスタンスはすでに登録されています。",
                        "確認",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }
            }

            string instanceName =
                new DirectoryInfo(selectedPath).Name;

            var newInstance = new InstanceInfo
            {
                Name = instanceName,
                Path = selectedPath,
                AppliedShareResourcePacks = false,
                AppliedShareShaderPacks = false,
                ShareResourcePacks = false,
                ShareShaderPacks = false,
                ResourcePackStatus = "未共有",
                ShaderPackStatus = "未共有",
                Status = "適用済み"
            };

            Instances.Add(newInstance);

            UpdateInstanceStatus(newInstance);

            StatusText.Text =
                $"「{instanceName}」を追加しました。";

            SaveSettings();
        }

        // ==========================================
        // インスタンス削除
        // ==========================================
        private void RemoveInstance_Click(object sender, RoutedEventArgs e)
        {
            if (InstanceList.SelectedItem is not InstanceInfo selected)
            {
                MessageBox.Show(
                    "削除するインスタンスを選択してください。",
                    "確認",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var result = MessageBox.Show(
                $"「{selected.Name}」を一覧から削除しますか？\n\n" +
                "共有中の場合は、共有を解除してから一覧から削除します。\n" +
                "※Minecraftフォルダ自体は削除されません。",
                "インスタンス削除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                // Resource Packs の共有を解除
                if (IsReparsePoint(
                    Path.Combine(selected.Path, "resourcepacks")))
                {
                    RemoveFolderLink(
                        selected.Path,
                        "resourcepacks");
                }

                // Shader Packs の共有を解除
                if (IsReparsePoint(
                    Path.Combine(selected.Path, "shaderpacks")))
                {
                    RemoveFolderLink(
                        selected.Path,
                        "shaderpacks");
                }

                // 共有解除後に一覧から削除
                Instances.Remove(selected);

                StatusText.Text =
                    $"「{selected.Name}」の共有を解除して一覧から削除しました。";

                SaveSettings();

                MessageBox.Show(
                    $"「{selected.Name}」の共有を解除して一覧から削除しました。",
                    "インスタンス削除",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"「{selected.Name}」の共有解除中にエラーが発生しました。\n\n" +
                    $"{ex.Message}\n\n" +
                    "インスタンスは一覧から削除していません。",
                    "インスタンス削除エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    $"「{selected.Name}」の削除に失敗しました。";
            }
        }

        private void InstanceList_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;

            while (source != null)
            {
                if (source is DataGridRow)
                {
                    return;
                }

                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }

            InstanceList.SelectedItem = null;
        }

        // ==========================================
        // 共有を適用
        // ==========================================
        private void CreateLinks_Click(object sender, RoutedEventArgs e)
        {
            if (Instances.Count == 0)
            {
                MessageBox.Show(
                    "Minecraftインスタンスを1つ以上追加してください。",
                    "確認",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (string.IsNullOrWhiteSpace(ResourcePackPath.Text) &&
                string.IsNullOrWhiteSpace(ShaderPackPath.Text))
            {
                MessageBox.Show(
                    "Resource Packs または Shader Packs の共有フォルダを設定してください。",
                    "確認",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            // ==========================================
            // 変更のあるインスタンスだけ取得
            // ==========================================
            var changedInstances =
                Instances
                    .Where(instance => instance.IsSharingChanged)
                    .ToList();

            // 変更がない場合
            if (changedInstances.Count == 0)
            {
                MessageBox.Show(
                    "変更された共有設定はありません。",
                    "共有を適用",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            // ==========================================
            // 共有フォルダを作成
            // ==========================================
            if (!string.IsNullOrWhiteSpace(ResourcePackPath.Text))
            {
                Directory.CreateDirectory(
                    ResourcePackPath.Text);
            }

            if (!string.IsNullOrWhiteSpace(ShaderPackPath.Text))
            {
                Directory.CreateDirectory(
                    ShaderPackPath.Text);
            }

            // ==========================================
            // 確認メッセージ
            // ==========================================
            string message =
                "以下のインスタンスの共有設定を変更します。\n\n";

            foreach (var instance in changedInstances)
            {
                message +=
                    $"・{instance.Name}\n";
            }

            message +=
                "\n共有設定を適用しますか？";

            var result = MessageBox.Show(
                message,
                "共有を適用",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            bool hasError = false;

            // ==========================================
            // 変更のあるインスタンスだけ適用
            // ==========================================
            foreach (var instance in changedInstances)
            {
                try
                {
                    // ==========================================
                    // Resource Packs
                    // ==========================================
                    if (!string.IsNullOrWhiteSpace(ResourcePackPath.Text))
                    {
                        if (instance.ShareResourcePacks)
                        {
                            CreateFolderLink(
                                instance.Path,
                                "resourcepacks",
                                ResourcePackPath.Text);
                        }
                        else
                        {
                            RemoveFolderLink(
                                instance.Path,
                                "resourcepacks");
                        }
                    }

                    // ==========================================
                    // Shader Packs
                    // ==========================================
                    if (!string.IsNullOrWhiteSpace(ShaderPackPath.Text))
                    {
                        if (instance.ShareShaderPacks)
                        {
                            CreateFolderLink(
                                instance.Path,
                                "shaderpacks",
                                ShaderPackPath.Text);
                        }
                        else
                        {
                            RemoveFolderLink(
                                instance.Path,
                                "shaderpacks");
                        }
                    }

                    // ==========================================
                    // 適用済み状態を更新
                    // ==========================================
                    UpdateInstanceStatus(instance);

                    instance.AppliedShareResourcePacks =
                        instance.ShareResourcePacks;

                    instance.AppliedShareShaderPacks =
                        instance.ShareShaderPacks;

                    instance.Status = "適用済み";
                }
                catch (Exception ex)
                {
                    instance.Status = "エラー";
                    hasError = true;

                    MessageBox.Show(
                        $"「{instance.Name}」でエラーが発生しました。\n\n" +
                        $"{ex.Message}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }

            // ==========================================
            // 設定を保存
            // ==========================================
            SaveSettings();

            if (hasError)
            {
                StatusText.Text =
                    "一部のインスタンスでエラーが発生しました。";
            }
            else
            {
                StatusText.Text =
                    "共有設定を適用しました。";

                MessageBox.Show(
                    "共有設定を適用しました。",
                    "完了",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ==========================================
        // インスタンスのリンク状態を確認
        // ==========================================
        private void UpdateInstanceStatus(InstanceInfo instance)
        {
            string resourcePackPath =
                Path.Combine(instance.Path, "resourcepacks");

            string shaderPackPath =
                Path.Combine(instance.Path, "shaderpacks");

            // Resource Packs
            if (IsReparsePoint(resourcePackPath))
            {
                instance.ResourcePackStatus = "共有中";
            }
            else if (Directory.Exists(resourcePackPath))
            {
                instance.ResourcePackStatus = "未共有";
            }
            else
            {
                instance.ResourcePackStatus = "未共有";
            }

            // Shader Packs
            if (IsReparsePoint(shaderPackPath))
            {
                instance.ShaderPackStatus = "共有中";
            }
            else if (Directory.Exists(shaderPackPath))
            {
                instance.ShaderPackStatus = "未共有";
            }
            else
            {
                instance.ShaderPackStatus = "未共有";
            }
        }

        // ==========================================
        // フォルダリンク作成
        // ==========================================
        private void CreateFolderLink(
            string instancePath,
            string folderName,
            string targetPath)
        {
            string linkPath =
                Path.Combine(instancePath, folderName);

            // 既存のリンクを確認
            if (IsReparsePoint(linkPath))
            {
                // 既存リンクを削除
                Directory.Delete(linkPath);
            }
            else if (Directory.Exists(linkPath))
            {
                // 通常フォルダならバックアップ
                string backupPath =
                    CreateBackupPath(
                        instancePath,
                        folderName);

                Directory.Move(
                    linkPath,
                    backupPath);
            }
            else if (File.Exists(linkPath))
            {
                throw new Exception(
                    $"{folderName} に同名のファイルが存在します。");
            }

            // リンク作成
            CreateDirectoryLink(
                linkPath,
                targetPath);
        }

        // ==========================================
        // フォルダリンク解除
        // ==========================================
        private void RemoveFolderLink(
            string instancePath,
            string folderName)
        {
            string linkPath =
                Path.Combine(instancePath, folderName);

            // リンクが存在しない場合
            if (!PathExists(linkPath))
            {
                return;
            }

            // リンクでない普通のフォルダなら何もしない
            // ※ユーザーのデータを勝手に消さない
            if (!IsReparsePoint(linkPath))
            {
                return;
            }

            // リンクそのものだけを削除
            Directory.Delete(linkPath);

            // 最新バックアップを復元
            RestoreLatestBackup(
                instancePath,
                folderName);
        }

        // ==========================================
        // 再解析ポイント判定
        // ==========================================
        private bool IsReparsePoint(string path)
        {
            try
            {
                FileAttributes attributes =
                    File.GetAttributes(path);

                return
                    (attributes & FileAttributes.ReparsePoint)
                    == FileAttributes.ReparsePoint;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        // ==========================================
        // ファイル / フォルダ / リンクの存在確認
        // ==========================================
        private bool PathExists(string path)
        {
            return
                File.Exists(path) ||
                Directory.Exists(path) ||
                IsReparsePoint(path);
        }

        // ==========================================
        // バックアップパス作成
        // ==========================================
        private string CreateBackupPath(
            string instancePath,
            string folderName)
        {
            string basePath =
                Path.Combine(
                    instancePath,
                    folderName + "_backup_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            string backupPath = basePath;

            int number = 1;

            while (Directory.Exists(backupPath))
            {
                backupPath =
                    basePath + "_" + number;

                number++;
            }

            return backupPath;
        }

        // ==========================================
        // 最新バックアップ復元
        // ==========================================
        private void RestoreLatestBackup(
            string instancePath,
            string folderName)
        {
            string searchPattern =
                folderName + "_backup_*";

            string[] backups =
                Directory.GetDirectories(
                    instancePath,
                    searchPattern);

            if (backups.Length == 0)
            {
                return;
            }

            string? latestBackup = null;

            DateTime latestTime =
                DateTime.MinValue;

            foreach (string backup in backups)
            {
                DateTime creationTime =
                    Directory.GetCreationTime(backup);

                if (creationTime > latestTime)
                {
                    latestTime = creationTime;
                    latestBackup = backup;
                }
            }

            if (string.IsNullOrEmpty(latestBackup))
            {
                return;
            }

            string originalPath =
                Path.Combine(
                    instancePath,
                    folderName);

            // すでに何か存在するなら復元しない
            if (PathExists(originalPath))
            {
                return;
            }

            Directory.Move(
                latestBackup,
                originalPath);
        }

        // ==========================================
        // mklink 実行
        // ==========================================
        private void CreateDirectoryLink(
            string linkPath,
            string targetPath)
        {
            var processInfo =
                new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments =
                        $"/c mklink /D \"{linkPath}\" \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            using var process =
                Process.Start(processInfo);

            if (process == null)
            {
                throw new Exception(
                    "mklink の実行に失敗しました。");
            }

            string output =
                process.StandardOutput.ReadToEnd();

            string error =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string detail =
                    string.IsNullOrWhiteSpace(error)
                        ? output
                        : error;

                throw new Exception(
                    $"mklink の実行に失敗しました。\n\n{detail}");
            }
        }

        // ==========================================
        // 設定保存
        // ==========================================
        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(
                    SettingsDirectory);

                var settings =
                    new AppSettings
                    {
                        ResourcePackPath =
                            ResourcePackPath.Text,

                        ShaderPackPath =
                            ShaderPackPath.Text
                    };

                foreach (var instance in Instances)
                {
                    settings.Instances.Add(
                        new InstanceSettings
                        {
                            Name = instance.Name,
                            Path = instance.Path,

                            ShareResourcePacks =
                                instance.ShareResourcePacks,

                            ShareShaderPacks =
                                instance.ShareShaderPacks,

                            AppliedShareResourcePacks =
                                instance.AppliedShareResourcePacks,

                            AppliedShareShaderPacks =
                                instance.AppliedShareShaderPacks
                        });
                }

                var options =
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                string json =
                    JsonSerializer.Serialize(
                        settings,
                        options);

                File.WriteAllText(
                    SettingsFile,
                    json);
            }
            catch
            {
                // 設定保存失敗でもアプリは動作させる
            }
        }

        // ==========================================
        // 設定読み込み
        // ==========================================
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    return;
                }

                string json =
                    File.ReadAllText(SettingsFile);

                var settings =
                    JsonSerializer.Deserialize<AppSettings>(
                        json);

                if (settings == null)
                {
                    return;
                }

                ResourcePackPath.Text =
                    settings.ResourcePackPath ?? "";

                ShaderPackPath.Text =
                    settings.ShaderPackPath ?? "";

                UpdatePackColumnVisibility();

                Instances.Clear();

                foreach (
                    var savedInstance
                    in settings.Instances)
                {
                    var instance = new InstanceInfo
                    {
                        Name =
                            savedInstance.Name ?? "",

                        Path =
                            savedInstance.Path ?? "",

                        ShareResourcePacks =
                            savedInstance.ShareResourcePacks,

                        ShareShaderPacks =
                            savedInstance.ShareShaderPacks,

                        AppliedShareResourcePacks =
                            savedInstance.AppliedShareResourcePacks,

                        AppliedShareShaderPacks =
                            savedInstance.AppliedShareShaderPacks,

                        Status = "適用済み"
                    };

                    if (instance.IsSharingChanged)
                    {
                        instance.Status = "未適用";
                    }

                    Instances.Add(instance);

                    UpdateInstanceStatus(instance);
                }

                StatusText.Text =
                    "前回の設定を読み込みました。";
            }
            catch
            {
                StatusText.Text =
                    "設定の読み込みに失敗しました。";
            }
        }

        // ==========================================
        // 終了時に保存
        // ==========================================
        protected override void OnClosing(
            CancelEventArgs e)
        {
            SaveSettings();

            base.OnClosing(e);
        }
    }

    // ==========================================
    // アプリ設定
    // ==========================================
    public class AppSettings
    {
        public string ResourcePackPath { get; set; } = "";

        public string ShaderPackPath { get; set; } = "";

        public ObservableCollection<InstanceSettings> Instances { get; set; }
            = new ObservableCollection<InstanceSettings>();
    }

    // ==========================================
    // 保存するインスタンス設定
    // ==========================================
    public class InstanceSettings
    {
        public string Name { get; set; } = "";

        public string Path { get; set; } = "";

        public bool ShareResourcePacks { get; set; }

        public bool ShareShaderPacks { get; set; }

        public bool AppliedShareResourcePacks { get; set; }

        public bool AppliedShareShaderPacks { get; set; }
    }

    public class InstanceCandidate
    {
        public bool IsSelected { get; set; }

        public string Name { get; set; } = "";

        public string Path { get; set; } = "";
    }

    // ==========================================
    // インスタンス情報
    // ==========================================
    public class InstanceInfo : INotifyPropertyChanged
    {
        private string _name = "";
        private string _path = "";

        private bool _shareResourcePacks;
        private bool _shareShaderPacks;

        private bool _appliedShareResourcePacks;
        private bool _appliedShareShaderPacks;

        private string _resourcePackStatus = "未適用";
        private string _shaderPackStatus = "未適用";
        private string _status = "未適用";


        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;

                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }


        public string Path
        {
            get => _path;
            set
            {
                if (_path == value)
                    return;

                _path = value;
                OnPropertyChanged(nameof(Path));
            }
        }


        public bool ShareResourcePacks
        {
            get => _shareResourcePacks;
            set
            {
                if (_shareResourcePacks == value)
                    return;

                _shareResourcePacks = value;

                OnPropertyChanged(nameof(ShareResourcePacks));
                OnPropertyChanged(nameof(IsSharingChanged));

                Status = IsSharingChanged
                    ? "未適用"
                    : "適用済み";
            }
        }


        public bool ShareShaderPacks
        {
            get => _shareShaderPacks;
            set
            {
                if (_shareShaderPacks == value)
                    return;

                _shareShaderPacks = value;

                OnPropertyChanged(nameof(ShareShaderPacks));
                OnPropertyChanged(nameof(IsSharingChanged));

                Status = IsSharingChanged
                    ? "未適用"
                    : "適用済み";
            }
        }


        // 最後に「共有を適用」したときの設定
        public bool AppliedShareResourcePacks
        {
            get => _appliedShareResourcePacks;
            set
            {
                if (_appliedShareResourcePacks == value)
                    return;

                _appliedShareResourcePacks = value;

                OnPropertyChanged(nameof(AppliedShareResourcePacks));
                OnPropertyChanged(nameof(IsSharingChanged));
            }
        }


        public bool AppliedShareShaderPacks
        {
            get => _appliedShareShaderPacks;
            set
            {
                if (_appliedShareShaderPacks == value)
                    return;

                _appliedShareShaderPacks = value;

                OnPropertyChanged(nameof(AppliedShareShaderPacks));
                OnPropertyChanged(nameof(IsSharingChanged));
            }
        }


        // 現在のチェック状態と、最後に適用した状態が違うか
        public bool IsSharingChanged =>
            ShareResourcePacks != AppliedShareResourcePacks ||
            ShareShaderPacks != AppliedShareShaderPacks;


        public string ResourcePackStatus
        {
            get => _resourcePackStatus;
            set
            {
                if (_resourcePackStatus == value)
                    return;

                _resourcePackStatus = value;
                OnPropertyChanged(nameof(ResourcePackStatus));
            }
        }


        public string ShaderPackStatus
        {
            get => _shaderPackStatus;
            set
            {
                if (_shaderPackStatus == value)
                    return;

                _shaderPackStatus = value;
                OnPropertyChanged(nameof(ShaderPackStatus));
            }
        }


        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                    return;

                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }


        public event PropertyChangedEventHandler? PropertyChanged;


        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}