# EetKit: EET 2.0 for .NET and C#

[![CI](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml)
[![Verified against playground](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml)

EetKit is an open-source C# and .NET library for sending sales to the Czech electronic sales registration EET 2.0 (elektronická evidence tržeb, data interface v4.1, starting 1 January 2027). It validates the sale, builds and signs the data message (SOAP, WS-Security), sends it to the Financial Administration of the Czech Republic, verifies the signature of the response and returns the result with the POK code for the receipt. It is meant for developers of point-of-sale systems, e-shops and other applications that register sales.

[Česká verze](https://github.com/Kazalax/eetkit/blob/main/README.md)

> Not an official product of the Financial Administration of the Czech Republic. The taxpayer is responsible for meeting the registration obligation.

## Install

```bash
dotnet add package EetKit
```

`EetKit.Extensions.DependencyInjection` registers the client in ASP.NET Core and other DI hosts.

| Platform | Status |
| --- | --- |
| .NET 8 and .NET 10 | tested on Linux and Windows |
| .NET Framework 4.7.2 and later | tested on .NET Framework 4.8 (Windows), uses the `netstandard2.0` target |
| other .NET versions (6, 7, 9, .NET Core 3.1) | should run via the `netstandard2.0` or `net8.0` target, not tested |

The automated tests run on every change on all tested platforms. Sending to the real playground of the Financial Administration runs every night on .NET 10 (Linux) and .NET Framework 4.8 (Windows).

## Example

```csharp
using var client = EetClient.FromPkcs12("certificate.p12", password, EetEnvironment.Playground);

var trzba = new Trzba("CZ00000019", idJednotky: 303, idPokl: "POKLADNA/1", poradCis: "2027/0001",
    datTrzby: DateTimeOffset.Now, celkTrzba: 65.00m);

var result = await client.SendAsync(trzba);
if (result.Status == EetStatus.Confirmed)
    Console.WriteLine($"POK for the receipt: {result.Pok}");
else if (result.IsRetryable)
    Console.WriteLine("Send again later with new SendOptions(PrvniZaslani: false)");
```

Sale fields keep the Czech names from the official specification (`celk_trzba` is `CelkTrzba`, `porad_cis` is `PoradCis`), so they map one to one to the documentation of the Financial Administration.

## Send result

| `Status` | Meaning | Next step |
| --- | --- | --- |
| `Confirmed` | The server accepted the sale | Print the POK from `Pok` on the receipt |
| `VerifiedOnly` | Verification mode (`Overeni = true`), code 0 | The message is valid, no POK is issued |
| `TemporaryError` | Timeout, network error, server error (codes -1 and 8), any HTTP error, response without a valid signature | Send again with `PrvniZaslani = false`. The server detects duplicates itself |
| `Rejected` | The server rejected the message (codes 2 to 7 and unknown positive codes) | Fix the data or certificate, retrying will not help |

Invalid data is never sent: `SendAsync` throws `EetValidationException` listing the fields. Local warnings (for example an unusual `id_jednotky`, an expired certificate or an EIČ that does not match the certificate) are in `LocalWarnings`, server warnings in `Warnings`.

## Playground

The test environment of the Financial Administration works like production, but nothing is registered. Test certificates for three EIČ values (`CZ00000019`, `CZ683555118`, `CZ8551015704`) and their password are in [CAEET_Playground_2026_v1.zip](https://github.com/Kazalax/eetkit/blob/main/docs/spec/CAEET_Playground_2026_v1.zip). A playground POK ends with `-ff` and `IsTest` is `true`.

The sample application sends a sale to the playground with one button:

```bash
dotnet run --project samples/EetKit.Sample.AspNetCore
```

Then open http://localhost:5080.

## ASP.NET Core

```csharp
builder.Services.AddEet(o =>
{
    o.Environment = EetEnvironment.Production;
    o.CertificatePath = builder.Configuration["Eet:CertificatePath"];
    o.CertificatePassword = builder.Configuration["Eet:CertificatePassword"];
});
```

`EetClient` is a single shared instance; take it in your constructor. At startup the settings are validated and the certificate is opened, so a wrong path or password stops the application. Register your own certificate source (a key vault, for example) as `IEetCertificateProvider`.

## What the library takes care of

- The signature follows the sample messages of the Financial Administration byte for byte (checked by a test).
- The legacy `.p12` format issued by CA EET loads on Windows and Linux without touching OpenSSL.
- A confirmation with POK is accepted only with a valid signature of the Generální finanční ředitelství certificate issued by I.CA, from the right environment and for the message that was sent. Error responses are not signed by the server.
- Messages over 12,288 bytes are not sent (limit verified on the playground).
- Passwords, keys and certificate contents are never logged.

## FAQ

### How do I send a sale to EET 2.0 from C#?

Install the `EetKit` package, load the cash register certificate (`.p12`) and call `EetClient.SendAsync` (see the [example](#example)). When the result is `Confirmed`, print the code from `Pok` on the receipt.

### What happens when EET does not answer or the network is down?

`SendAsync` waits at most 5 seconds (configurable, minimum 2 s) and returns `TemporaryError` with `IsRetryable = true`. It does not throw. Send the sale again later with `SendOptions(PrvniZaslani: false)`. Every attempt gets a new `uuid_zpravy`. The free library has no queue that would resend sales on its own.

### Where do I get the certificate?

The cash register certificate is issued by the CA EET certification authority. It is requested through the DIS+ tax portal (MOJE daně). The library loads the `.p12` file exactly as CA EET issues it, on Windows and Linux.

### How can I try it without a production certificate?

On the playground, the test environment of the Financial Administration. Test certificates are in this repository (see [Playground](#playground)) and `EetEnvironment.Playground` is the default.

### What does error 4 or another code mean?

Positive codes 2 to 7 mean that the server rejected the message. Retrying will not help; fix the data or the certificate. The error text is in `ErrorText`, the code in `ErrorCode`. Codes -1 and 8 are temporary (`TemporaryError`). See the [send result](#send-result) table.

### Does the library support the old EET (interface v3)?

No, only EET 2.0 with interface v4.1. The old interface is not compatible with EET 2.0.

### Does it work on .NET Framework?

Yes, from version 4.7.2. The automated tests and sending to the playground run on .NET Framework 4.8 (see [Install](#install)).

## Turnkey integration

Need EET in your own point-of-sale system, a sales queue for network outages or automatic certificate renewal? Write to bedrichmaly79@gmail.com.

## License

[MIT](https://github.com/Kazalax/eetkit/blob/main/LICENSE)
