namespace MTL.APP.Shared
{
    public class PieSegment
    {
        public string Name { get; set; } = "";
        public decimal Value { get; set; }
        public double Percent { get; set; }
        public string Color { get; set; } = "#4CAF50";
        public string ValueFormatted => Value.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + "đ";
        public int Month { get; set; }
    }
}