namespace BeastPc.Models.Tables
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int PcBuildId { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
