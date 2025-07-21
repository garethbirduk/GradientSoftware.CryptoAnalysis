namespace Gradient.CryptoAnalysis.Conditions.PriceConditions;

public class IsUptrendCondition : PriceCondition, IAdjustableCandles
{
    protected override bool IsMet()
    {
        return true;
    }

    public IsUptrendCondition(int successiveCandles = DefaultAdditionalCandles) : base(successiveCandles)
    {
    }

    public void SetAdditionalCandles(int additionalCandles)
    {
        AdditionalCandles = additionalCandles;
    }
}