namespace Operia.Application.Settings;

public sealed record PaymentMethodsDto(
    bool CashEnabled,
    bool BankTransferEnabled,
    string? Bank,
    string? BankAccountHolder,
    string? BankAccountNumber,
    string? Iban,
    bool InstapayEnabled,
    string? InstapayId,
    string? InstapayAccountHolder,
    bool EWalletEnabled,
    string? WalletType,
    string? WalletHolderName,
    string? WalletNumber,
    bool FawryEnabled,
    string? FawryServiceCode,
    string? FawryNotes)
{
    public static PaymentMethodsDto Default => new(
        true,
        false,
        null,
        null,
        null,
        null,
        false,
        null,
        null,
        false,
        null,
        null,
        null,
        false,
        null,
        null);
}
