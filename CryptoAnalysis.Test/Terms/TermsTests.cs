namespace Gradient.CryptoAnalysis.Test.Terms;

[TestClass]
public class TermsTests
{
    public static IEnumerable<object[]> AnnotationTypes =>
        Enum.GetValues<EnumAnnotationType>().Where(x => x != EnumAnnotationType.None).Select(x => new object[] { x });

    [DataTestMethod]
    [DynamicData(nameof(AnnotationTypes))]
    public void EveryAnnotationType_HasTerm(EnumAnnotationType type)
    {
        var term = CryptoAnalysis.Terms.Get(type);

        Assert.AreEqual(type, term.Type);
        Assert.IsFalse(string.IsNullOrWhiteSpace(term.Label));
        Assert.IsFalse(string.IsNullOrWhiteSpace(term.Name));
        Assert.IsFalse(string.IsNullOrWhiteSpace(term.Category));
        Assert.IsFalse(string.IsNullOrWhiteSpace(term.Color));
        Assert.IsFalse(string.IsNullOrWhiteSpace(term.Symbol));
        Assert.AreNotEqual(EnumPosition.None, term.Position);
    }

    [TestMethod]
    public void Labels_AreUnique()
    {
        var duplicates = CryptoAnalysis.Terms.All.GroupBy(x => x.Label).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.AreEqual(0, duplicates.Count, $"Duplicate labels: {string.Join(", ", duplicates)}");
    }

    [TestMethod]
    public void Get_None_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => CryptoAnalysis.Terms.Get(EnumAnnotationType.None));
    }

    [DataTestMethod]
    [DataRow(EnumAnnotationType.HigherHigh, "HH", EnumPosition.Above)]
    [DataRow(EnumAnnotationType.HigherLow, "HL", EnumPosition.Below)]
    [DataRow(EnumAnnotationType.LowerHigh, "LH", EnumPosition.Above)]
    [DataRow(EnumAnnotationType.LowerLow, "LL", EnumPosition.Below)]
    [DataRow(EnumAnnotationType.BullishBreakOfStructure, "BoS↑", EnumPosition.Above)]
    [DataRow(EnumAnnotationType.BearishBreakOfStructure, "BoS↓", EnumPosition.Below)]
    [DataRow(EnumAnnotationType.BullishMarketStructureBreak, "MSB↑", EnumPosition.Above)]
    [DataRow(EnumAnnotationType.BearishMarketStructureBreak, "MSB↓", EnumPosition.Below)]
    public void AnnotationCreate_DefaultsFromRegistry(EnumAnnotationType type, string expectedNote, EnumPosition expectedPosition)
    {
        var annotation = Annotation.Create(type);

        Assert.AreEqual(expectedNote, annotation.Note);
        Assert.AreEqual(expectedPosition, annotation.Position);
    }

    [TestMethod]
    public void AnnotationCreate_ExplicitValuesWin()
    {
        var annotation = Annotation.Create(EnumAnnotationType.HigherHigh, "custom", EnumPosition.Right);

        Assert.AreEqual("custom", annotation.Note);
        Assert.AreEqual(EnumPosition.Right, annotation.Position);
    }
}
