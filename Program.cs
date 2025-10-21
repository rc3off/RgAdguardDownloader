// RG-Adguard Microsoft Store Downloader
// Single-file WinForms app targeting .NET 8
// Build:  dotnet new winforms -n RgAdguardDownloader -f net8.0-windows --use-program-main
//         Replace Program.cs with this file's content (or create a new .cs file and set it as Program.cs)
//         dotnet build -c Release
// Run:    bin/Release/net8.0-windows/RgAdguardDownloader.exe
// Notes:  This app calls https://store.rg-adguard.net/api/GetFiles with form-encoded parameters to fetch
//         the generated download links (which are time-limited). Be gentle and respect their service.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    private readonly TextBox txtInput = new TextBox { PlaceholderText = "Paste Store URL / ProductId / PackageFamilyName / CategoryId" };
    private readonly ComboBox cmbType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox cmbRing = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button btnFetch = new Button { Text = "Fetch Links" };
    private readonly Button btnOpenSite = new Button { Text = "Open rg-adguard" };
    private readonly DataGridView grid = new DataGridView { ReadOnly = false, AutoGenerateColumns = false, AllowUserToAddRows = false };
    private readonly CheckBox chkFilterPackages = new CheckBox { Text = "Show only packages (.appx/.msix)" };
    private readonly Button btnDownloadSelected = new Button { Text = "Download Selected" };
    private readonly ProgressBar progress = new ProgressBar { Style = ProgressBarStyle.Blocks, Minimum = 0, Maximum = 100 };
    private readonly Label lblStatus = new Label { AutoSize = true, Text = "Ready" };

    private readonly BindingSource binding = new BindingSource();

    public MainForm()
    {
        Text = "RG-Adguard Microsoft Store Downloader";
        Width = 1100;
        Height = 720;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 5,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); // Type label
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); // Type combo
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Input
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // Ring
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); // Fetch
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // Open site

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        var lblType = new Label { Text = "Lookup type:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
        cmbType.Items.AddRange(new[] { "url", "ProductId", "PackageFamilyName", "CategoryId" });
        cmbType.SelectedIndex = 0;

        cmbRing.Items.AddRange(new[] { "Retail", "RP", "WIF", "WIS" });
        cmbRing.SelectedIndex = 0;

        txtInput.Dock = DockStyle.Fill;
        cmbType.Dock = DockStyle.Fill;
        cmbRing.Dock = DockStyle.Fill;
        btnFetch.Dock = DockStyle.Fill;
        btnOpenSite.Dock = DockStyle.Fill;

        table.Controls.Add(lblType, 0, 0);
        table.Controls.Add(cmbType, 1, 0);
        table.Controls.Add(txtInput, 2, 0);
        table.Controls.Add(cmbRing, 3, 0);
        table.Controls.Add(btnFetch, 4, 0);
        table.Controls.Add(btnOpenSite, 5, 0);

        chkFilterPackages.Checked = true;
        table.SetColumnSpan(chkFilterPackages, 2);
        table.Controls.Add(chkFilterPackages, 0, 1);

        grid.Dock = DockStyle.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = true;

        var colSel = new DataGridViewCheckBoxColumn { HeaderText = "Select", DataPropertyName = nameof(DownloadItem.Selected), Width = 60 };
        var colName = new DataGridViewTextBoxColumn { HeaderText = "Package", DataPropertyName = nameof(DownloadItem.Name), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
        var colUrl = new DataGridViewTextBoxColumn { HeaderText = "URL", DataPropertyName = nameof(DownloadItem.Url), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
        var colExt = new DataGridViewTextBoxColumn { HeaderText = "Ext", DataPropertyName = nameof(DownloadItem.Extension), Width = 60 };

        grid.Columns.AddRange(colSel, colName, colExt, colUrl);
        grid.DataSource = binding;

        table.SetColumnSpan(grid, 6);
        table.Controls.Add(grid, 0, 2);

        btnDownloadSelected.Dock = DockStyle.Left;
        table.Controls.Add(btnDownloadSelected, 0, 3);
        table.SetColumnSpan(btnDownloadSelected, 2);

        progress.Dock = DockStyle.Fill;
        table.Controls.Add(progress, 2, 3);
        table.SetColumnSpan(progress, 3);

        lblStatus.Dock = DockStyle.Fill;
        table.Controls.Add(lblStatus, 5, 3);

        var disclaimer = new LinkLabel
        {
            Text = "Links expire; use responsibly. Double-click a row to open the URL.",
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        disclaimer.LinkClicked += (s, e) => MessageBox.Show(
            "This app uses store.rg-adguard.net to generate time-limited CDN links to Microsoft Store packages. " +
            "Respect their rate limits and terms. Download only what you are allowed to.",
            "Disclaimer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        table.SetColumnSpan(disclaimer, 6);
        table.Controls.Add(disclaimer, 0, 4);

        Controls.Add(table);

        btnFetch.Click += async (s, e) => await FetchAsync();
        btnOpenSite.Click += (s, e) => Process.Start(new ProcessStartInfo("https://store.rg-adguard.net/") { UseShellExecute = true });
        btnDownloadSelected.Click += async (s, e) => await DownloadSelectedAsync();
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                if (binding.Current is DownloadItem item)
                {
                    try { Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true }); }
                    catch { }
                }
            }
        };
        chkFilterPackages.CheckedChanged += (s, e) => ApplyFilter();
    }

    private List<DownloadItem> allItems = new();

    private async Task FetchAsync()
    {
        var value = txtInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show("Please enter a value (Store URL, ProductId, PackageFamilyName, or CategoryId).", "Input required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ToggleUi(false);
        lblStatus.Text = "Fetching...";
        progress.Style = ProgressBarStyle.Marquee;

        try
        {
            var (html, error) = await QueryRgAdguardAsync(
                type: cmbType.SelectedItem!.ToString()!,
                value: value,
                ring: cmbRing.SelectedItem!.ToString()!,
                lang: "en-US"
            );

            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, "Service error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            allItems = ParseLinks(html);
            ApplyFilter();

            lblStatus.Text = $"Found {allItems.Count} links.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to fetch links.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 0;
            ToggleUi(true);
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<DownloadItem> items = allItems;
        if (chkFilterPackages.Checked)
        {
            string[] keep = new[] { ".appx", ".appxbundle", ".msix", ".msixbundle", ".eappx", ".eappxbundle" };
            items = items.Where(i => keep.Contains(i.Extension, StringComparer.OrdinalIgnoreCase));
        }
        binding.DataSource = items.ToList();
    }

    private async Task DownloadSelectedAsync()
    {
        var selected = ((IEnumerable<DownloadItem>)binding.List).Where(i => i.Selected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select one or more rows (toggle the checkbox) to download.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var fbd = new FolderBrowserDialog { Description = "Choose a folder to save the files" };
        if (fbd.ShowDialog(this) != DialogResult.OK) return;
        var folder = fbd.SelectedPath;

        ToggleUi(false);
        progress.Style = ProgressBarStyle.Blocks;
        progress.Value = 0;
        lblStatus.Text = "Downloading...";

        try
        {
            int completed = 0;
            foreach (var item in selected)
            {
                var target = Path.Combine(folder, SafeFileName(item.Name));
                await DownloadFileAsync(item.Url, target, pct => progress.Value = pct);
                completed++;
                lblStatus.Text = $"Downloaded {completed}/{selected.Count}: {item.Name}";
            }
            MessageBox.Show($"Downloaded {selected.Count} file(s) to:\n{folder}", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            progress.Value = 0;
            lblStatus.Text = "Ready";
            ToggleUi(true);
        }
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private void ToggleUi(bool enabled)
    {
        txtInput.Enabled = enabled;
        cmbType.Enabled = enabled;
        cmbRing.Enabled = enabled;
        btnFetch.Enabled = enabled;
        btnOpenSite.Enabled = enabled;
        btnDownloadSelected.Enabled = enabled;
        grid.Enabled = enabled;
    }

    private static async Task<(string html, string? error)> QueryRgAdguardAsync(string type, string value, string ring, string lang)
    {
        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        using var client = new HttpClient(handler);

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Referrer = new Uri("https://store.rg-adguard.net/");

        var pairs = new List<KeyValuePair<string?, string?>>
        {
            new("type", type),
            new(type.Equals("url", StringComparison.OrdinalIgnoreCase) ? "url" : type, value),
            new("ring", ring),
            new("lang", lang)
        };

        using var content = new FormUrlEncodedContent(pairs!);

        using var resp = await client.PostAsync("https://store.rg-adguard.net/api/GetFiles", content);
        var html = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            return (html, $"Service returned {(int)resp.StatusCode} {resp.ReasonPhrase}.");
        }

        if (html.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase))
            return (html, "Service returned: Internal Server Error");
        if (html.Contains("The server returned an empty list", StringComparison.OrdinalIgnoreCase))
            return (html, "The server returned an empty list (no files). Try a different ring or identifier.");

        return (html, null);
    }

    private static List<DownloadItem> ParseLinks(string html)
    {
        var items = new List<DownloadItem>();

        // The response is HTML containing a table of <tr> rows with <a href="...">PackageName</a>
        var re = new Regex("<tr[^>]*>.*?<a\\s+href=\"(?<url>[^\"]+)\"[^>]*>(?<text>.*?)</a>.*?</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = re.Matches(html);
        foreach (Match m in matches)
        {
            var url = WebUtility.HtmlDecode(m.Groups["url"].Value);
            var text = WebUtility.HtmlDecode(StripTags(m.Groups["text"].Value)).Trim();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(text))
                continue;

            var ext = Path.GetExtension(new Uri(url).AbsolutePath);
            items.Add(new DownloadItem
            {
                Selected = false,
                Name = text,
                Url = url,
                Extension = string.IsNullOrEmpty(ext) ? "" : ext
            });
        }
        return items;
    }

    private static string StripTags(string input)
    {
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    private static async Task DownloadFileAsync(string url, string path, Action<int> reportPct)
    {
        using var client = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

        using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;

        await using var stream = await resp.Content.ReadAsStreamAsync();
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fs.WriteAsync(buffer, 0, n);
            read += n;
            if (total > 0)
            {
                var pct = (int)(read * 100 / total);
                reportPct(Math.Clamp(pct, 0, 100));
            }
        }
        reportPct(100);
    }
}

public class DownloadItem
{
    public bool Selected { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
}
