namespace Auktionshuset.Contracts.Dto.Admin.Auction;

public static class AuctionPrice
{
    public const decimal Maximum = 9999999999999999.99m;
    public const string ValidationMessage = "Startprisen skal være mellem 0 og 9999999999999999,99 kr. med højst to decimaler.";
    public static bool IsValid(decimal amount) => amount >= 0 && amount <= Maximum && decimal.Round(amount, 2) == amount;
}
