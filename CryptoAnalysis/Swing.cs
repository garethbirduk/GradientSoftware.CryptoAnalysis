namespace Gradient.CryptoAnalysis;

public abstract class Swing
{
    public Downleg Downleg { get; set; } = new();
    public List<Price> Prices { get; set; } = [];
    public Upleg Upleg { get; set; } = new();
}
