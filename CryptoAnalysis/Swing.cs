namespace Gradient.CryptoAnalysis;

public abstract class Swing
{
    public abstract Price? BreakOfStructure { get; }
    public bool Broken => BreakOfStructure != null;

    public Price InitialPrice
    {
        get
        {
            return Prices.First();
        }
    }

    public abstract Price? MarketStructureBreak { get; }
    public Price? NextPrice { get; set; }
    public List<Price> Prices { get; set; } = [];
}