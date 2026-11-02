namespace Auktionshuset.Contracts.Dto.Billing;

public sealed record InvoiceResponse(Guid Id, string Number, Guid AuctionId, string AuctionName,
    DateTime IssuedAt, string CustomerName, string CustomerAddress, string CustomerEmail,
    string SellerName, string SellerAddress, string SellerEmail, int SellerCvr, string Currency,
    string PaymentTerms, decimal PremiumPercent, decimal VatPercent, decimal HammerTotal,
    decimal PremiumTotal, decimal VatTotal, decimal Total, string Status,
    DateTime? PaidAt, string? PaymentMethod, decimal? PaidAmount, Guid? PaidByUserId, string? PaidByName,
    List<InvoiceLineResponse> Lines);

public sealed record InvoiceLineResponse(Guid Id, Guid AuctionLotId, string Name, string Description, int Quantity,
    decimal HammerPrice, decimal Premium, decimal HammerVat, decimal PremiumVat, decimal Total,
    DateTime WonAt, string Status, DateTime? CollectedAt, Guid? CollectedByUserId, string? CollectedByName);

public sealed record RegisterInvoicePayment(string Method, decimal Amount);
