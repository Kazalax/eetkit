# EetKit: EET 2.0 for .NET

[![CI](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/ci.yml)
[![Verified against playground](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml/badge.svg)](https://github.com/Kazalax/eetkit/actions/workflows/playground.yml)

A library for sending sales to the Czech electronic sales registration EET 2.0 (interface v4.1), which starts on 1 January 2027. It validates the data, builds and signs the message, sends it to the Financial Administration, verifies the signature of the response and returns the result with the POK code.

> Not an official product of the Financial Administration of the Czech Republic. The taxpayer is responsible for meeting the registration obligation.

## Install

```bash
dotnet add package EetKit
```

Tested on .NET 8 and .NET 10 (Linux and Windows). The `netstandard2.0` target is meant for .NET Framework, which is not tested yet. `EetKit.Extensions.DependencyInjection` registers the client in ASP.NET Core and other DI hosts.

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

| `Status` | Meaning | Next step |
| --- | --- | --- |
| `Confirmed` | Accepted | Print `Pok` on the receipt |
| `VerifiedOnly` | Verification mode, code 0 | No POK is issued |
| `TemporaryError` | Timeout, network or server error | Send again with `PrvniZaslani = false` |
| `Rejected` | Rejected (codes 2 to 7) | Fix the data or certificate |

Invalid data is never sent: `SendAsync` throws `EetValidationException` listing the fields.

## Playground

Test certificates for three EIČ values and their password are in [CAEET_Playground_2026_v1.zip](https://github.com/Kazalax/eetkit/blob/main/docs/spec/CAEET_Playground_2026_v1.zip). Run the sample with `dotnet run --project samples/EetKit.Sample.AspNetCore` and open http://localhost:5080.

## License

[MIT](https://github.com/Kazalax/eetkit/blob/main/LICENSE)
