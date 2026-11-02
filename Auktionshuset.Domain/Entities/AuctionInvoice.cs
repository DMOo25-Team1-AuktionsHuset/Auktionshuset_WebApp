namespace Auktionshuset.Domain.Entities;

// Issued values are snapshots. Later catalogue/profile edits must not change a bill.
public sealed class AuctionInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Sequence { get; set; }
    public string Number { get; set; } = "";
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
    public string AuctionName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerAddress { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string SellerName { get; set; } = "";
    public string SellerAddress { get; set; } = "";
    public string SellerEmail { get; set; } = "";
    public int SellerCvr { get; set; }
    public string Currency { get; set; } = "DKK";
    public string PaymentTerms { get; set; } = "Kort eller kontant. Fuld betaling før afhentning.";
    public decimal PremiumPercent { get; set; }
    public decimal VatPercent { get; set; }
    public decimal HammerTotal { get; set; }
    public decimal PremiumTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal Total { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? PaidByUserId { get; set; }
    public string? PaidByName { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? PaidAmount { get; set; }
    public List<AuctionInvoiceLine> Lines { get; set; } = [];
}

public sealed class AuctionInvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public AuctionInvoice Invoice { get; set; } = null!;
    public Guid AuctionLotId { get; set; }
    public AuctionLot AuctionLot { get; set; } = null!;
    public Guid WinningBidId { get; set; }
    public Bid WinningBid { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Quantity { get; set; }
    public decimal HammerPrice { get; set; }
    public decimal Premium { get; set; }
    public decimal HammerVat { get; set; }
    public decimal PremiumVat { get; set; }
    public decimal Total { get; set; }
    public DateTime WonAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public Guid? CollectedByUserId { get; set; }
    public string? CollectedByName { get; set; }
}

public sealed class BillingSequence
{
    public int Id { get; set; } = 1;
    public long LastInvoice { get; set; }
    public long LastAuction { get; set; }
}
