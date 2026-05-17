using System;

namespace BeastPc.Models.Tables
{
    public class PcBuild
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string Cpu { get; set; }
        public string Gpu { get; set; }
        public string Ram { get; set; }
        public string Storage { get; set; }
        public string Cooling { get; set; }
        public string Psu { get; set; }
        public string CaseName { get; set; }
        public string ImageUrl { get; set; }
        public int Stock { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
