using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;

using Komorebi.Models;

namespace Komorebi.Views;

/// <summary>
/// SSHキー選択用の再利用可能コントロール。
/// ~/.ssh/ から秘密鍵を自動検出してドロップダウンで選択可能にする。
/// </summary>
public partial class SSHKeyPicker : UserControl
{
    /// <summary>選択されたSSHキーのファイルパス。VMのSSHKeyプロパティと双方向バインドする。</summary>
    public static readonly StyledProperty<string> SSHKeyPathProperty =
        AvaloniaProperty.Register<SSHKeyPicker, string>(nameof(SSHKeyPath), defaultValue: string.Empty,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string SSHKeyPath
    {
        get => GetValue(SSHKeyPathProperty);
        set => SetValue(SSHKeyPathProperty, value);
    }

    private List<SSHKeyInfo> _items = [];
    private bool _suppressSelectionChange;
    private int _previousSelectedIndex;

    public SSHKeyPicker()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildList();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SSHKeyPathProperty && !_suppressSelectionChange)
        {
            SyncSelectionToPath();
        }
    }

    /// <summary>ドロップダウンリストを再構築する。</summary>
    private void RebuildList()
    {
        _items = [];

        // 「指定なし（グローバル設定）」
        _items.Add(SSHKeyInfo.CreateNone());

        // ~/.ssh/ から検出されたキー
        _items.AddRange(SSHKeyInfo.ScanSSHDirectory());

        // 現在のパスが ~/.ssh/ 外のカスタムパスなら動的追加
        var currentPath = SSHKeyPath;
        if (!string.IsNullOrEmpty(currentPath) &&
            _items.Find(k => k.FilePath == currentPath) is null)
        {
            _items.Add(SSHKeyInfo.FromCustomPath(currentPath));
        }

        // 「参照...」
        _items.Add(SSHKeyInfo.CreateBrowse());

        _suppressSelectionChange = true;
        CmbSSHKey.ItemsSource = _items;
        SyncSelectionToPath();
        _suppressSelectionChange = false;
    }

    /// <summary>SSHKeyPathの値に対応するリスト項目を選択する。</summary>
    private void SyncSelectionToPath()
    {
        var path = SSHKeyPath;

        // 空文字（または旧バージョンのセンチネル値） → 「指定なし」を選択
        if (string.IsNullOrEmpty(path) || path == "__NONE__")
        {
            CmbSSHKey.SelectedIndex = 0;
            _previousSelectedIndex = 0;
            return;
        }

        // パスに一致するキーを検索
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i].Type != SSHKeyInfo.EntryType.Browse &&
                _items[i].Type != SSHKeyInfo.EntryType.None &&
                _items[i].FilePath == path)
            {
                _suppressSelectionChange = true;
                CmbSSHKey.SelectedIndex = i;
                _previousSelectedIndex = i;
                _suppressSelectionChange = false;
                return;
            }
        }

        // リストにないカスタムパスの場合、Browse の直前に挿入
        var customEntry = SSHKeyInfo.FromCustomPath(path);
        var browseIndex = _items.FindIndex(k => k.Type == SSHKeyInfo.EntryType.Browse);
        if (browseIndex >= 0)
            _items.Insert(browseIndex, customEntry);
        else
            _items.Add(customEntry);

        _suppressSelectionChange = true;
        CmbSSHKey.ItemsSource = null;
        CmbSSHKey.ItemsSource = _items;
        CmbSSHKey.SelectedItem = customEntry;
        _previousSelectedIndex = CmbSSHKey.SelectedIndex;
        _suppressSelectionChange = false;
    }

    /// <summary>ComboBox の選択変更ハンドラ。</summary>
    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChange)
            return;

        if (CmbSSHKey.SelectedItem is not SSHKeyInfo selected)
            return;

        switch (selected.Type)
        {
            case SSHKeyInfo.EntryType.None:
                // 空文字を保存 → ResolveSSHKeyAsync がグローバルキー → システムデフォルトへフォールバックする。
                SetCurrentValue(SSHKeyPathProperty, string.Empty);
                _previousSelectedIndex = CmbSSHKey.SelectedIndex;
                break;

            case SSHKeyInfo.EntryType.Key:
            case SSHKeyInfo.EntryType.CustomKey:
                SetCurrentValue(SSHKeyPathProperty, selected.FilePath);
                _previousSelectedIndex = CmbSSHKey.SelectedIndex;
                break;

            case SSHKeyInfo.EntryType.Browse:
                // ファイルピッカーを開く
                string path;
                try
                {
                    path = await ViewHelpers.SelectSSHKeyFileAsync(this);
                }
                catch
                {
                    // ファイルピッカーのエラー時は前の選択に戻す
                    _suppressSelectionChange = true;
                    CmbSSHKey.SelectedIndex = _previousSelectedIndex;
                    _suppressSelectionChange = false;
                    break;
                }
                if (path is not null)
                {
                    // 選択されたパスがリスト内にあるか確認
                    var existing = _items.Find(k =>
                        k.Type is not SSHKeyInfo.EntryType.Browse and not SSHKeyInfo.EntryType.None
                        && k.FilePath == path);

                    if (existing is not null)
                    {
                        _suppressSelectionChange = true;
                        CmbSSHKey.SelectedItem = existing;
                        _suppressSelectionChange = false;
                        SetCurrentValue(SSHKeyPathProperty, path);
                    }
                    else
                    {
                        SetCurrentValue(SSHKeyPathProperty, path);
                        // SyncSelectionToPath がカスタムエントリを追加する
                    }

                    _previousSelectedIndex = CmbSSHKey.SelectedIndex;
                }
                else
                {
                    // キャンセル: 前の選択に直接戻す（SyncSelectionToPathを呼ばない）
                    _suppressSelectionChange = true;
                    CmbSSHKey.SelectedIndex = _previousSelectedIndex;
                    _suppressSelectionChange = false;
                }
                break;
        }
    }
}
