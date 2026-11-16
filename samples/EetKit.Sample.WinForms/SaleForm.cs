using System.Drawing;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Windows.Forms;

namespace EetKit.Sample.WinForms;

/// <summary>A minimal cash register window: amount, receipt number, send, show the POK.</summary>
internal sealed class SaleForm : Form
{
    private const int SampleIdJednotky = 303;
    private const string SampleIdPokl = "EetKit/WinForms";

    private readonly TextBox _celkTrzba = new() { Text = "65,00", Dock = DockStyle.Fill };
    private readonly TextBox _poradCis = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _overeni = new() { Text = "Ověřovací režim (server jen zkontroluje zprávu, POK nevydá)", AutoSize = true };
    private readonly Button _odeslat = new() { Text = "Odeslat na playground", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
    private readonly TextBox _vysledek = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly ErrorProvider _chyby = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    private readonly EetCertificate _certificate;
    private readonly HttpClient _httpClient;
    private readonly EetClient _client;

    public SaleForm()
    {
        _certificate = PlaygroundCertificate.Load();
        _httpClient = new HttpClient();
        _client = new EetClient(
            new LoadedCertificateProvider(_certificate),
            new EetClientOptions { Environment = EetEnvironment.Playground },
            _httpClient);

        Text = "EetKit: odeslání tržby na playground";
        Font = new Font("Segoe UI", 11F);
        ClientSize = new Size(640, 480);
        MinimumSize = new Size(520, 420);
        _poradCis.Text = NewReceiptNumber();
        _odeslat.Click += OnSendClick;
        AcceptButton = _odeslat;

        Controls.Add(BuildLayout());
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _client.Dispose();
        _httpClient.Dispose();
        _certificate.Dispose();
        _chyby.Dispose();
        base.OnFormClosed(e);
    }

    private TableLayoutPanel BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        layout.Controls.Add(new Label { Text = "Částka (Kč)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_celkTrzba, 1, 0);
        layout.Controls.Add(new Label { Text = "Číslo účtenky", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_poradCis, 1, 1);
        layout.Controls.Add(_overeni, 1, 2);
        layout.Controls.Add(_odeslat, 1, 3);
        layout.Controls.Add(new Label { Text = "Výsledek", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.SetColumnSpan(_vysledek, 2);
        layout.Controls.Add(_vysledek, 0, 5);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return layout;
    }

    private async void OnSendClick(object? sender, EventArgs e)
    {
        _chyby.Clear();
        if (!TryReadAmount(out var celkTrzba))
        {
            _chyby.SetError(_celkTrzba, "Zadejte částku, například 65,00.");
            return;
        }

        var trzba = new Trzba(PlaygroundCertificate.Eic, SampleIdJednotky, SampleIdPokl, _poradCis.Text, DateTimeOffset.Now, celkTrzba);
        _odeslat.Enabled = false;
        _vysledek.Text = "Odesílám...";
        try
        {
            var result = await _client.SendAsync(trzba, new SendOptions(Overeni: _overeni.Checked));
            _vysledek.Text = Describe(result);
            if (result.Status == EetStatus.Confirmed)
            {
                _poradCis.Text = NewReceiptNumber();
            }
        }
        catch (EetValidationException ex)
        {
            ShowValidationErrors(ex);
        }
        finally
        {
            _odeslat.Enabled = true;
        }
    }

    private bool TryReadAmount(out decimal amount) =>
        decimal.TryParse(_celkTrzba.Text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    // Each error is marked next to its input box (when the form has one) and listed in the result.
    private void ShowValidationErrors(EetValidationException ex)
    {
        var summary = new StringBuilder("Tržba se neodeslala, data nejsou platná:").AppendLine();
        foreach (var error in ex.Errors)
        {
            var input = error.Field switch
            {
                "celk_trzba" => _celkTrzba,
                "porad_cis" => _poradCis,
                _ => null,
            };
            if (input is not null)
            {
                _chyby.SetError(input, error.Message);
            }

            summary.AppendLine($"{error.Field}: {error.Message}");
        }

        _vysledek.Text = summary.ToString();
    }

    private static string Describe(EetResult result)
    {
        var text = new StringBuilder();
        text.AppendLine(result.Status switch
        {
            EetStatus.Confirmed => "Potvrzeno, POK patří na účtenku.",
            EetStatus.VerifiedOnly => "Ověřeno, zpráva je v pořádku. V ověřovacím režimu se POK nevydává.",
            EetStatus.TemporaryError => "Dočasná chyba. Tržbu pošlete později znovu s PrvniZaslani = false.",
            _ => "Server tržbu odmítl. Opravte data nebo certifikát.",
        });
        if (result.Pok is not null)
        {
            text.AppendLine($"POK: {result.Pok}");
        }

        if (result.ErrorCode is not null || result.ErrorText is not null)
        {
            text.AppendLine($"Chyba {result.ErrorCode}: {result.ErrorText}");
        }

        foreach (var warning in result.Warnings)
        {
            text.AppendLine($"Varování {warning.Code}: {warning.Text}");
        }

        foreach (var warning in result.LocalWarnings)
        {
            text.AppendLine($"Místní varování: {warning}");
        }

        text.AppendLine($"uuid_zpravy: {result.MessageUuid}");
        return text.ToString();
    }

    private static string NewReceiptNumber() =>
        DateTime.Now.ToString("'U/'yyyyMMdd'/'HHmmss", CultureInfo.InvariantCulture);
}
